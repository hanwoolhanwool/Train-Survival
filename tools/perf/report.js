'use strict';

/**
 * 마크다운 리포트 — 성능 프로파일링 자동화 계획 2.2.
 *
 *   node tools/perf/report.js Perf/runs/*.json [--baseline <경로>] [--out <경로>]
 *
 * `compare.js`와 같은 판정(`gates.js`)을 쓰되, 출력이 사람이 읽고 붙여 넣을 문서다.
 * 기본 출력 경로는 `Perf/reports/<시각>-vs-baseline.md` 이고, `--out -` 이면 표준 출력으로 낸다.
 *
 * 종료 코드는 compare.js 와 같다 — 리포트를 쓰면서도 회귀면 1이다.
 */

const fs = require('fs');
const path = require('path');
const { execFileSync } = require('child_process');
const gates = require('./gates');

const EXIT_OK = 0;
const EXIT_REGRESSED = 1;
const EXIT_UNUSABLE = 2;

const MARK = {
  [gates.VERDICT.PASS]: '✅',
  [gates.VERDICT.WARN]: '⚠️',
  [gates.VERDICT.FAIL]: '❌',
  [gates.VERDICT.INFO]: '·',
};

function parseArgs(argv) {
  const runPaths = [];
  let baselinePath = null;
  let outPath = null;

  for (let i = 0; i < argv.length; i++) {
    const arg = argv[i];
    if (arg === '--baseline' || arg === '-b') {
      baselinePath = argv[++i];
    } else if (arg === '--out' || arg === '-o') {
      outPath = argv[++i];
    } else if (arg.startsWith('-') && arg !== '-') {
      throw new Error(`모르는 옵션: ${arg}`);
    } else {
      runPaths.push(arg);
    }
  }

  if (runPaths.length === 0) {
    throw new Error('실행 결과 JSON 을 하나 이상 지정해야 한다.');
  }

  return { runPaths, baselinePath, outPath };
}

/**
 * 실행 JSON 의 git 칸은 런타임이 채울 수 없어 비어 있다 — 리포트를 만드는 지금 채운다.
 * 저장소 밖에서 돌리면 조용히 비운다(리포트가 못 나오는 것보다 낫다).
 */
function readGitContext() {
  const run = (args) => execFileSync('git', args, { encoding: 'utf8' }).trim();

  try {
    return {
      sha: run(['rev-parse', '--short', 'HEAD']),
      branch: run(['rev-parse', '--abbrev-ref', 'HEAD']),
      dirty: run(['status', '--porcelain']).length > 0,
      subject: run(['log', '-1', '--pretty=%s']),
    };
  } catch (error) {
    return null;
  }
}

/**
 * 로컬 시각 — 리포트를 읽는 사람과 파일명이 같은 시계를 봐야 한다.
 * UTC 로 찍으면 새벽에 돌린 실행이 전날 파일로 남아 순서가 뒤집힌 것처럼 보인다.
 */
function localStamp(date) {
  const two = (n) => String(n).padStart(2, '0');
  const ymd = `${date.getFullYear()}-${two(date.getMonth() + 1)}-${two(date.getDate())}`;
  const hms = `${two(date.getHours())}:${two(date.getMinutes())}:${two(date.getSeconds())}`;

  return {
    text: `${ymd} ${hms}`,
    file: `${ymd.replace(/-/g, '')}-${two(date.getHours())}${two(date.getMinutes())}`,
  };
}

function formatNumber(value, unit) {
  if (value === null) {
    return '—';
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
    return '—';
  }

  if (!Number.isFinite(row.deltaPercent)) {
    return '신규';
  }

  const sign = row.deltaPercent >= 0 ? '+' : '';
  return `${sign}${row.deltaPercent.toFixed(1)} %`;
}

function buildMarkdown(result, git) {
  const { baseline, runs, rows, comparability, bottleneck, baselineBottleneck } = result;
  const now = localStamp(new Date());
  const lines = [];

  const headline = result.regressed
    ? `회귀 ${result.failed.length}건`
    : (result.warned.length > 0 ? `통과 (경고 ${result.warned.length}건)` : '통과');

  lines.push(`# 성능 리포트 — ${baseline.scenario} · ${headline}`);
  lines.push('');
  lines.push(`측정 ${now.text} · 실행 ${runs.length}회` +
    (runs.length > 1 ? ' (지표별 중앙값)' : '') +
    ` · 기준선 \`${path.relative(process.cwd(), baseline.__path).replace(/\\/g, '/')}\``);

  if (git) {
    lines.push('');
    lines.push(`커밋 \`${git.sha}\` (\`${git.branch}\`)${git.dirty ? ' · **작업 트리에 미커밋 변경이 있다**' : ''} — ${git.subject}`);
  }

  lines.push('');

  // 병목 한 줄 — 이 계획이 만드는 값의 핵심이다 (§4.5).
  lines.push(`> **병목: ${bottleneck.name}** — gpu ${bottleneck.gpu.toFixed(3)} · ` +
    `cpuMain ${bottleneck.cpuMain.toFixed(3)} · cpuRender ${bottleneck.cpuRender.toFixed(3)} ms (p50)`);

  if (baselineBottleneck.name !== bottleneck.name) {
    lines.push('>');
    lines.push(`> 기준선의 병목은 **${baselineBottleneck.name}** 이었다 — 병목이 옮겨갔다.`);
  }

  if (bottleneck.name === 'Unknown') {
    lines.push('>');
    lines.push('> ⚠️ 세 값이 전부 0이다 — Frame Timing Stats 가 꺼진 빌드다. **판정을 믿으면 안 된다.**');
  }

  lines.push('');

  if (comparability.length > 0) {
    lines.push('## ⚠️ 비교 조건이 다르다');
    lines.push('');
    lines.push('절대값이 아니라 **같은 머신에서 연속으로 잰 델타로만 판정**하는 것이 이 벤치의 전제다.');
    lines.push('아래가 어긋난 상태의 판정은 신뢰할 수 없다:');
    lines.push('');
    for (const problem of comparability) {
      lines.push(`- ${problem}`);
    }
    lines.push('');
  }

  lines.push('## 판정');
  lines.push('');
  lines.push('| 지표 | 기준선 | 이번 | 변화 | 판정 | 비고 |');
  lines.push('|---|---:|---:|---:|:---:|---|');

  for (const row of rows) {
    const note = row.reason || (row.gate.kind === 'info' ? '정보' : (row.gate.note || ''));
    lines.push(`| ${row.label} | ${formatNumber(row.baseline, row.unit)} | ` +
      `${formatNumber(row.current, row.unit)} | ${formatDelta(row)} | ${MARK[row.verdict]} | ${note} |`);
  }

  lines.push('');

  const spreadRows = rows.filter((r) => r.spreadPercent !== null && r.gate.kind === 'ratio');
  if (spreadRows.length > 0 && runs.length > 1) {
    lines.push(`**반복 ${runs.length}회 편차** — 게이트 임계가 노이즈보다 위인지 보는 값이다.`);
    lines.push('');
    for (const row of spreadRows) {
      const samples = row.samples.map((v) => formatNumber(v, row.unit)).join(' · ');
      lines.push(`- ${row.label}: **${row.spreadPercent.toFixed(1)} %** (${samples})`);
    }
    lines.push('');
  }

  lines.push('## 측정 환경');
  lines.push('');
  const machine = runs[0].machine || {};
  const build = runs[0].build || {};
  const config = runs[0].config || {};
  lines.push('| 항목 | 값 |');
  lines.push('|---|---|');
  lines.push(`| GPU | ${machine.gpu || '—'} (${machine.driver || '—'}) |`);
  lines.push(`| CPU | ${machine.cpu || '—'} |`);
  lines.push(`| OS | ${machine.os || '—'} |`);
  lines.push(`| Unity | ${build.unityVersion || '—'} |`);
  lines.push(`| 빌드 | ${build.development ? '개발 빌드 — **배포판은 이 값보다 빠르다**' : '배포 빌드'} |`);
  lines.push(`| 해상도 | ${config.resolution || '—'} |`);
  lines.push(`| 측정 | 워밍업 ${config.warmupFrames}프레임 폐기 후 ${config.durationSeconds}초 |`);
  lines.push(`| 강제 조건 | \`${runs[0].forcedConditions || '—'}\` |`);
  lines.push(`| 프레임 수 | ${runs.map((r) => (r.frames || 0).toLocaleString('en-US')).join(' · ')} |`);
  lines.push('');

  const spikeRun = runs.length === 1 ? runs[0] : gates.medianRun(runs);
  const spikes = spikeRun.spikes || [];
  lines.push('## 스파이크');
  lines.push('');

  if (spikes.length === 0) {
    lines.push('중앙값 실행에서 스파이크(중앙값의 3배 초과) 없음.');
  } else {
    const intervals = [];
    for (let i = 1; i < spikes.length; i++) {
      intervals.push(spikes[i].timeSeconds - spikes[i - 1].timeSeconds);
    }

    const worst = spikes.reduce((a, b) => (a.ms >= b.ms ? a : b));
    lines.push(`중앙값 실행에서 **${spikes.length}건** · 최대 **${worst.ms.toFixed(2)} ms** (${worst.timeSeconds.toFixed(2)}초 지점).`);
    lines.push('');

    // 타일 교체 주기(40 m ÷ 6 m/s = 6.67초)와 맞는 간격이 반복되면 범인이 그 자리에서 드러난다 (§4.3).
    const tilePeriod = 6.67;
    const nearPeriod = intervals.filter((gap) => Math.abs(gap - tilePeriod) < 0.7).length;
    if (intervals.length >= 3 && nearPeriod / intervals.length > 0.5) {
      lines.push(`> ⚠️ 스파이크 간격의 ${Math.round((nearPeriod / intervals.length) * 100)} %가 ` +
        `**타일 교체 주기 6.67초**와 맞는다 — 타일 교체가 범인일 가능성이 높다.`);
    } else {
      lines.push(`> 간격에 타일 교체 주기(6.67초)와 맞는 규칙성은 없다 — 교체 스파이크로 보이지 않는다.`);
    }
  }

  lines.push('');
  lines.push('---');
  lines.push('');
  lines.push('생성: `node tools/perf/report.js` · 판정 규칙: ' +
    '[성능 프로파일링 자동화 계획 §4.6](../../docs/plans/features/성능-프로파일링-자동화-계획.md)');
  lines.push('');

  return lines.join('\n');
}

function main() {
  let options;
  try {
    options = parseArgs(process.argv.slice(2));
  } catch (error) {
    console.error(`오류: ${error.message}`);
    console.error('사용: node tools/perf/report.js <run.json...> [--baseline <경로>] [--out <경로|->]');
    return EXIT_UNUSABLE;
  }

  let runs;
  let baseline;
  try {
    runs = options.runPaths.map((p) => gates.readRun(p));
    const baselinePath = options.baselinePath || gates.defaultBaselinePath(runs[0], process.cwd());
    if (!fs.existsSync(baselinePath)) {
      console.error(`기준선이 없다 — ${baselinePath}`);
      return EXIT_UNUSABLE;
    }

    baseline = gates.readRun(baselinePath);
  } catch (error) {
    console.error(`읽지 못했다 — ${error.message}`);
    return EXIT_UNUSABLE;
  }

  const result = gates.compare(runs, baseline);
  const markdown = buildMarkdown(result, readGitContext());

  if (options.outPath === '-') {
    console.log(markdown);
    return result.regressed ? EXIT_REGRESSED : EXIT_OK;
  }

  const stamp = localStamp(new Date()).file;
  const outPath = options.outPath ||
    path.join(process.cwd(), 'Perf', 'reports', `${stamp}-${baseline.scenario}-vs-baseline.md`);

  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  fs.writeFileSync(outPath, markdown, 'utf8');

  console.log(`리포트: ${path.relative(process.cwd(), outPath).replace(/\\/g, '/')}`);
  console.log(`병목: ${result.bottleneck.name} · ` +
    (result.regressed ? `회귀 ${result.failed.length}건` : '회귀 없음'));

  return result.regressed ? EXIT_REGRESSED : EXIT_OK;
}

process.exitCode = main();
