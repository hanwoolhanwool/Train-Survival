'use strict';

/**
 * 회귀 판정 규칙과 실행 파일 읽기 — compare.js 와 report.js 가 공유한다.
 *
 * 임계를 두 파일에 나눠 적으면 한쪽만 고쳐진 채로 "통과"가 나오고, 그 통과를 믿게 된다.
 * 그래서 게이트의 출처는 이 파일 하나다 (성능 프로파일링 자동화 계획 §4.6).
 */

const fs = require('fs');
const path = require('path');

/**
 * 게이트 임계 — 2026-09-02 첫 3회 실측으로 조정했다.
 *
 * 프레임 시간의 반복 편차가 gpu p95 0.8 % · cpuMain p95 0.7 % 로 작아 착수값 +10 % 를 절반으로
 * 조였다. 노이즈의 6배면 거짓 실패는 나지 않으면서 5 % 회귀는 잡힌다.
 *
 * GC 만 반대로 느슨하다. 계획의 목표는 "프레임당 할당 0" 이지만 기준선이 이미 7,218 B 이고,
 * 그 값을 줄이는 것은 최적화라 이 계획의 범위 밖이다 — 지금 상태를 봉인해 악화만 막는다.
 */
const GATES = [
  {
    key: 'gpuMs',
    stat: 'p95',
    label: 'gpu p95',
    unit: 'ms',
    kind: 'ratio',
    failOverPercent: 5,
    note: '실측 노이즈 0.8 % 의 6배',
  },
  {
    key: 'cpuMainMs',
    stat: 'p95',
    label: 'cpuMain p95',
    unit: 'ms',
    kind: 'ratio',
    failOverPercent: 5,
    note: '실측 노이즈 0.7 % 의 7배',
  },
  {
    key: 'cpuRenderMs',
    stat: 'p95',
    label: 'cpuRender p95',
    unit: 'ms',
    kind: 'ratio',
    warnOverPercent: 5,
    note: '주 병목이 아니라 경고까지만',
  },
  {
    key: 'gcAllocPerFrameBytes',
    stat: 'p50',
    label: 'GC 할당/프레임',
    unit: 'B',
    kind: 'absolute',
    failOverAbsolute: 8192,
    warnOnIncrease: true,
    note: '8 KB 초과는 실패 · 기준선보다 늘면 경고',
  },
  {
    key: 'framesOver33ms',
    stat: null,
    label: '33 ms 초과 프레임',
    unit: '개',
    kind: 'absolute',
    warnOnIncrease: true,
    note: '하드웨어 의존이 커 실패로는 안 삼는다',
  },
  {
    key: 'drawCalls',
    stat: 'p50',
    label: '드로우콜 p50',
    unit: '',
    kind: 'info',
    note: '예산 §7 우선순위 1 의 진행을 보는 값',
  },
  {
    key: 'setPassCalls',
    stat: 'p50',
    label: 'SetPass p50',
    unit: '',
    kind: 'info',
  },
  {
    key: 'triangles',
    stat: 'p50',
    label: '삼각형 p50',
    unit: '',
    kind: 'info',
  },
  {
    key: 'shadowCasters',
    stat: 'p50',
    label: '셰도우 캐스터 p50',
    unit: '',
    kind: 'info',
  },
];

const VERDICT = { PASS: 'pass', WARN: 'warn', FAIL: 'fail', INFO: 'info' };

/** BOM 이 붙은 JSON 도 읽는다 — 런타임이 UTF-8 BOM 으로 쓴다. */
function readRun(filePath) {
  const raw = fs.readFileSync(filePath, 'utf8').replace(/^﻿/, '');
  const parsed = JSON.parse(raw);
  parsed.__path = filePath;
  return parsed;
}

/** `median.<key>.<stat>` 또는 `median.<key>` 를 꺼낸다. 없으면 null — 0 과 구분해야 한다. */
function metricValue(run, gate) {
  const node = run && run.median ? run.median[gate.key] : undefined;
  if (node === undefined || node === null) {
    return null;
  }

  if (gate.stat === null) {
    return typeof node === 'number' ? node : null;
  }

  const value = node[gate.stat];
  return typeof value === 'number' ? value : null;
}

/**
 * 여러 실행의 중앙값을 고른다 — 평균을 내면 한 번 튄 실행이 결과를 끌고 간다.
 * 짝수 개면 아래쪽 중앙값을 쓴다(값을 만들어 내지 않기 위함이다).
 */
function median(values) {
  if (!values.length) {
    return null;
  }

  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.floor((sorted.length - 1) / 2)];
}

/** 반복 실행의 편차(%) — 게이트 임계가 노이즈보다 위인지 확인하는 값이다. */
function spreadPercent(values) {
  if (values.length < 2) {
    return null;
  }

  const min = Math.min(...values);
  const max = Math.max(...values);
  if (min <= 0) {
    return null;
  }

  return ((max - min) / min) * 100;
}

/** 여러 실행 → 지표별 중앙값 하나. 실행 1개면 그 값이 그대로 나온다. */
function reduceRuns(runs) {
  const reduced = { values: {}, spread: {}, samples: {} };

  for (const gate of GATES) {
    const values = runs.map((run) => metricValue(run, gate)).filter((v) => v !== null);
    reduced.values[gate.key] = median(values);
    reduced.spread[gate.key] = spreadPercent(values);
    reduced.samples[gate.key] = values;
  }

  return reduced;
}

/**
 * 병목 판정 — 세 프레임 시간의 p50 중 가장 큰 쪽.
 * 셋 다 0이면 판정하지 않는다. "GPU 가 0 ms 라 CPU 바운드"는 Frame Timing Stats 가
 * 꺼진 빌드에서 나오는 거짓말이다 (§1.1).
 */
function determineBottleneck(run) {
  const m = run && run.median ? run.median : {};
  const cpuMain = m.cpuMainMs ? m.cpuMainMs.p50 : 0;
  const cpuRender = m.cpuRenderMs ? m.cpuRenderMs.p50 : 0;
  const gpu = m.gpuMs ? m.gpuMs.p50 : 0;

  if (!cpuMain && !cpuRender && !gpu) {
    return { name: 'Unknown', cpuMain, cpuRender, gpu };
  }

  if (gpu >= cpuMain && gpu >= cpuRender) {
    return { name: 'GPU', cpuMain, cpuRender, gpu };
  }

  return {
    name: cpuMain >= cpuRender ? 'CPU (main thread)' : 'CPU (render thread)',
    cpuMain,
    cpuRender,
    gpu,
  };
}

/**
 * 측정 머신·빌드가 같은가. 다르면 델타 비교가 무의미하므로 판정 자체를 신뢰할 수 없다 (§4.4).
 * "예전에 12 ms 였는데 지금 14 ms" 는 하드웨어가 바뀌었으면 아무 말도 아니다.
 */
function checkComparability(run, baseline) {
  const problems = [];
  const runMachine = run.machine || {};
  const baseMachine = baseline.machine || {};

  if (runMachine.gpu !== baseMachine.gpu) {
    problems.push(`GPU 가 다르다 — 기준선 "${baseMachine.gpu}" vs 이번 "${runMachine.gpu}"`);
  }

  if (runMachine.cpu !== baseMachine.cpu) {
    problems.push(`CPU 가 다르다 — 기준선 "${baseMachine.cpu}" vs 이번 "${runMachine.cpu}"`);
  }

  if (runMachine.driver !== baseMachine.driver) {
    problems.push(`그래픽 드라이버가 다르다 — 기준선 "${baseMachine.driver}" vs 이번 "${runMachine.driver}"`);
  }

  const runBuild = run.build || {};
  const baseBuild = baseline.build || {};
  if (runBuild.development !== baseBuild.development) {
    problems.push(
      `빌드 종류가 다르다 — 기준선 development=${baseBuild.development} vs 이번 ${runBuild.development}` +
        ' (배포 빌드는 개발 빌드보다 빠르다)');
  }

  if (runBuild.unityVersion !== baseBuild.unityVersion) {
    problems.push(`Unity 버전이 다르다 — 기준선 ${baseBuild.unityVersion} vs 이번 ${runBuild.unityVersion}`);
  }

  const runConfig = run.config || {};
  const baseConfig = baseline.config || {};
  if (runConfig.resolution !== baseConfig.resolution) {
    problems.push(
      `해상도가 다르다 — 기준선 ${baseConfig.resolution} vs 이번 ${runConfig.resolution}` +
        ' (창 크기가 GPU 시간을 지배한다)');
  }

  if (run.scenario !== baseline.scenario) {
    problems.push(`시나리오가 다르다 — 기준선 "${baseline.scenario}" vs 이번 "${run.scenario}"`);
  }

  return problems;
}

/** 게이트 하나를 판정한다. */
function evaluateGate(gate, current, base) {
  const row = {
    gate,
    label: gate.label,
    unit: gate.unit,
    current,
    baseline: base,
    deltaPercent: null,
    verdict: VERDICT.INFO,
    reason: '',
  };

  if (current === null || base === null) {
    row.verdict = VERDICT.INFO;
    row.reason = '값 없음';
    return row;
  }

  if (base > 0) {
    row.deltaPercent = ((current - base) / base) * 100;
  } else if (current > 0) {
    row.deltaPercent = Infinity;
  } else {
    row.deltaPercent = 0;
  }

  if (gate.kind === 'info') {
    return row;
  }

  if (gate.failOverAbsolute !== undefined && current > gate.failOverAbsolute) {
    row.verdict = VERDICT.FAIL;
    row.reason = `절대 임계 ${gate.failOverAbsolute}${gate.unit} 초과`;
    return row;
  }

  if (gate.failOverPercent !== undefined && row.deltaPercent > gate.failOverPercent) {
    row.verdict = VERDICT.FAIL;
    row.reason = `기준선 +${gate.failOverPercent} % 초과`;
    return row;
  }

  if (gate.warnOverPercent !== undefined && row.deltaPercent > gate.warnOverPercent) {
    row.verdict = VERDICT.WARN;
    row.reason = `기준선 +${gate.warnOverPercent} % 초과`;
    return row;
  }

  if (gate.warnOnIncrease && current > base) {
    row.verdict = VERDICT.WARN;
    row.reason = '기준선보다 늘었다';
    return row;
  }

  row.verdict = VERDICT.PASS;
  return row;
}

/**
 * 실행들 ↔ 기준선 비교. 반환값이 compare.js(종료 코드)와 report.js(마크다운)의 공통 입력이다.
 */
function compare(runs, baseline) {
  const reduced = reduceRuns(runs);
  const baseReduced = reduceRuns([baseline]);

  const rows = GATES.map((gate) =>
    evaluateGate(gate, reduced.values[gate.key], baseReduced.values[gate.key]));

  for (const row of rows) {
    row.spreadPercent = reduced.spread[row.gate.key];
    row.samples = reduced.samples[row.gate.key];
  }

  const comparability = checkComparability(runs[0], baseline);
  const failed = rows.filter((r) => r.verdict === VERDICT.FAIL);
  const warned = rows.filter((r) => r.verdict === VERDICT.WARN);

  return {
    runs,
    baseline,
    rows,
    comparability,
    failed,
    warned,
    bottleneck: determineBottleneck(runs.length === 1 ? runs[0] : medianRun(runs)),
    baselineBottleneck: determineBottleneck(baseline),
    regressed: failed.length > 0,
  };
}

/** 프레임 시간 기준 중앙값 실행 — 병목 판정은 실행 하나의 세 값을 함께 봐야 한다. */
function medianRun(runs) {
  const scored = runs
    .map((run) => ({ run, score: (run.median && run.median.gpuMs ? run.median.gpuMs.p95 : 0) }))
    .sort((a, b) => a.score - b.score);

  return scored[Math.floor((scored.length - 1) / 2)].run;
}

/** 기준선 경로를 시나리오 이름으로 유추한다 — 인자를 하나 덜 쓰기 위한 편의다. */
function defaultBaselinePath(run, projectRoot) {
  return path.join(projectRoot, 'Perf', 'baseline', `${run.scenario}.json`);
}

module.exports = {
  GATES,
  VERDICT,
  readRun,
  compare,
  reduceRuns,
  medianRun,
  median,
  spreadPercent,
  metricValue,
  determineBottleneck,
  checkComparability,
  defaultBaselinePath,
};
