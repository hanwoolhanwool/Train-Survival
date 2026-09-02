'use strict';

/**
 * 기준선 대비 회귀 판정 — 성능 프로파일링 자동화 계획 2.1.
 *
 *   node tools/perf/compare.js Perf/runs/*.json [--baseline Perf/baseline/forest-day-60s.json]
 *
 * 실행을 여러 개 주면 지표별 중앙값으로 비교한다(3회 권장 · §2 결정 ⑤).
 * 기준선을 생략하면 실행의 시나리오 이름으로 `Perf/baseline/<시나리오>.json`을 찾는다.
 *
 * 종료 코드 — CI·에디터 메뉴가 이 값 하나로 판정한다:
 *   0  통과 (경고는 있을 수 있다)
 *   1  회귀 — 게이트를 넘었다
 *   2  비교 불가 (파일 없음 · 깨진 JSON · 인자 오류)
 */

const fs = require('fs');
const path = require('path');
const gates = require('./gates');

const EXIT_OK = 0;
const EXIT_REGRESSED = 1;
const EXIT_UNUSABLE = 2;

const MARK = {
  [gates.VERDICT.PASS]: '[OK]',
  [gates.VERDICT.WARN]: '[!!]',
  [gates.VERDICT.FAIL]: '[XX]',
  [gates.VERDICT.INFO]: '[--]',
  [gates.VERDICT.UNUSABLE]: '[??]',
};

function parseArgs(argv) {
  const runPaths = [];
  let baselinePath = null;
  let json = false;

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--baseline' || arg === '-b') {
      baselinePath = argv[++i];
    } else if (arg === '--json') {
      json = true;
    } else if (arg.startsWith('-')) {
      throw new Error(`모르는 옵션: ${arg}`);
    } else {
      runPaths.push(arg);
    }
  }

  if (runPaths.length === 0) {
    throw new Error('실행 결과 JSON 을 하나 이상 지정해야 한다.');
  }

  return { runPaths, baselinePath, json };
}

function formatNumber(value, unit) {
  if (value === null) {
    return '-';
  }

  if (unit === 'ms') {
    return value.toFixed(3);
  }

  if (unit === 'B') {
    return Math.round(value).toLocaleString('en-US');
  }

  return Number.isInteger(value) ? value.toLocaleString('en-US') : value.toFixed(2);
}

function formatDelta(row) {
  if (row.deltaPercent === null) {
    return '-';
  }

  if (!Number.isFinite(row.deltaPercent)) {
    return '신규';
  }

  const sign = row.deltaPercent >= 0 ? '+' : '';
  return `${sign}${row.deltaPercent.toFixed(1)} %`;
}

/**
 * 콘솔에서 차지하는 칸 수. 한글·기호는 두 칸을 먹으므로 `String.length`로 맞추면 표가 어긋난다.
 * 이 표의 지표 이름이 한글이라 필요한 계산이다.
 */
function displayWidth(text) {
  let width = 0;
  for (const char of String(text)) {
    const code = char.codePointAt(0);
    const wide =
      (code >= 0x1100 && code <= 0x115f) ||
      (code >= 0x2e80 && code <= 0xa4cf) ||
      (code >= 0xac00 && code <= 0xd7a3) ||
      (code >= 0xf900 && code <= 0xfaff) ||
      (code >= 0xfe30 && code <= 0xfe6f) ||
      (code >= 0xff00 && code <= 0xff60) ||
      (code >= 0xffe0 && code <= 0xffe6);
    width += wide ? 2 : 1;
  }

  return width;
}

function pad(text, width) {
  const str = String(text);
  const gap = width - displayWidth(str);
  return gap > 0 ? str + ' '.repeat(gap) : str;
}

function padLeft(text, width) {
  const str = String(text);
  const gap = width - displayWidth(str);
  return gap > 0 ? ' '.repeat(gap) + str : str;
}

function printReport(result) {
  const { rows, comparability, bottleneck, baselineBottleneck, runs, baseline } = result;

  console.log('');
  console.log(`시나리오: ${baseline.scenario} · 실행 ${runs.length}회` +
    (runs.length > 1 ? ' (지표별 중앙값으로 비교)' : ''));
  console.log(`기준선  : ${path.relative(process.cwd(), baseline.__path)}`);
  console.log('');

  if (comparability.length > 0) {
    console.log('!! 비교 조건이 다르다 — 아래 판정은 신뢰할 수 없다:');
    for (const problem of comparability) {
      console.log(`   - ${problem}`);
    }
    console.log('');
  }

  console.log(`${pad('지표', 20)}${padLeft('기준선', 14)}${padLeft('이번', 14)}${padLeft('변화', 10)}  판정`);
  console.log('-'.repeat(66));

  for (const row of rows) {
    const line =
      pad(row.label, 20) +
      padLeft(formatNumber(row.baseline, row.unit), 14) +
      padLeft(formatNumber(row.current, row.unit), 14) +
      padLeft(formatDelta(row), 10) +
      `  ${MARK[row.verdict]}` +
      (row.reason ? ` ${row.reason}` : '');
    console.log(line);
  }

  console.log('');

  // 이 계획이 만드는 값의 핵심 — 리포트는 병목을 항상 한 줄로 말한다 (§4.5).
  const b = bottleneck;
  console.log(
    `→ 병목: ${b.name} ` +
      `(gpu ${b.gpu.toFixed(3)} · cpuMain ${b.cpuMain.toFixed(3)} · cpuRender ${b.cpuRender.toFixed(3)} ms p50)`);

  if (baselineBottleneck.name !== b.name) {
    console.log(`   기준선의 병목은 ${baselineBottleneck.name} 이었다 — 병목이 옮겨갔다.`);
  }

  if (b.name === 'Unknown') {
    console.log('   세 값이 전부 0 이다 — Frame Timing Stats 가 꺼진 빌드다. 판정을 믿으면 안 된다.');
  }

  const spreadRows = rows.filter((r) => r.spreadPercent !== null && r.gate.kind === 'ratio');
  if (spreadRows.length > 0) {
    const parts = spreadRows.map((r) => `${r.label} ${r.spreadPercent.toFixed(1)} %`);
    console.log(`   반복 편차: ${parts.join(' · ')}`);
  }

  console.log('');

  if (result.unreliable) {
    console.log(`? 판정 불가 — 반복 편차가 게이트 임계보다 크다 (${result.unusable.map((r) => r.label).join(', ')})`);
    console.log('  이 실행에서는 통과든 회귀든 우연이다. 측정 환경이 조용하지 않았을 가능성이 높다:');
    console.log('  다른 앱(IDE·브라우저·에디터)을 닫고 다시 재야 한다.');
    console.log('  렌더 카운터(드로우콜·삼각형·셰도우 캐스터)는 머신 부하와 무관하니 그쪽만 참고할 수 있다.');
  } else if (result.regressed) {
    console.log(`X 회귀 ${result.failed.length}건 — ${result.failed.map((r) => r.label).join(', ')}`);
  } else if (result.warned.length > 0) {
    console.log(`! 통과 (경고 ${result.warned.length}건 — ${result.warned.map((r) => r.label).join(', ')})`);
  } else {
    console.log('O 통과 — 회귀 없음.');
  }

  console.log('');
}

function main() {
  let options;
  try {
    options = parseArgs(process.argv.slice(2));
  } catch (error) {
    console.error(`오류: ${error.message}`);
    console.error('사용: node tools/perf/compare.js <run.json...> [--baseline <baseline.json>] [--json]');
    return EXIT_UNUSABLE;
  }

  let runs;
  try {
    runs = options.runPaths.map((p) => gates.readRun(p));
  } catch (error) {
    console.error(`실행 결과를 읽지 못했다 — ${error.message}`);
    return EXIT_UNUSABLE;
  }

  const baselinePath = options.baselinePath || gates.defaultBaselinePath(runs[0], process.cwd());
  if (!fs.existsSync(baselinePath)) {
    console.error(`기준선이 없다 — ${baselinePath}`);
    console.error('첫 측정이라면 실행 결과를 그 경로로 복사해 기준선으로 삼는다.');
    return EXIT_UNUSABLE;
  }

  let baseline;
  try {
    baseline = gates.readRun(baselinePath);
  } catch (error) {
    console.error(`기준선을 읽지 못했다 — ${error.message}`);
    return EXIT_UNUSABLE;
  }

  const result = gates.compare(runs, baseline);

  if (options.json) {
    console.log(JSON.stringify({
      scenario: baseline.scenario,
      regressed: result.regressed,
      bottleneck: result.bottleneck.name,
      failed: result.failed.map((r) => r.label),
      warned: result.warned.map((r) => r.label),
      comparability: result.comparability,
      rows: result.rows.map((r) => ({
        label: r.label,
        baseline: r.baseline,
        current: r.current,
        deltaPercent: r.deltaPercent,
        verdict: r.verdict,
      })),
    }, null, 2));
  } else {
    printReport(result);
  }

  // 판정 불가는 "통과"도 "회귀"도 아니다 — 비교 불가와 같은 코드로 알린다.
  if (result.unreliable) {
    return EXIT_UNUSABLE;
  }

  return result.regressed ? EXIT_REGRESSED : EXIT_OK;
}

process.exitCode = main();
