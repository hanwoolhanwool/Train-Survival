// QA 디버그 키 마인드맵 SVG 생성기
// 출력: docs/guide/assets/qa-keyboard-map.svg
const fs = require('fs');
const path = require('path');

const OUT = process.argv[2];

// ── 그룹 정의 ────────────────────────────────────────────────────────────
const G = {
  build: { n: 1, name: '편성·건축', color: '#DD7C2F' },
  res:   { n: 2, name: '자원·피해', color: '#2E9E6B' },
  mon:   { n: 3, name: '몬스터·보스', color: '#D94F55' },
  cycle: { n: 4, name: '사이클·시간', color: '#3E7BC4' },
  view:  { n: 5, name: '연출·플레이어', color: '#8B5CC7' },
};

// ── 캔버스 ───────────────────────────────────────────────────────────────
const W = 1240, H = 740;

// ── 숫자패드 격자 ────────────────────────────────────────────────────────
const NX = 472, NY = 190, KS = 68, GAP = 8, STEP = KS + GAP;
const cx = j => NX + j * STEP;
const cy = i => NY + i * STEP;

// 배치 규칙: 숫자패드의 한 "행"이 곧 한 기능 그룹이다 (윗줄 = 몬스터·보스, 789 = 편성·건축,
// 456 = 자원·피해, 123 = 사이클). 넘치는 0·. 만 아래 행으로 내려가 각자 자기 그룹 열을 잇는다.
// [열, 행, 폭칸, 높이칸, 기호, 라벨, 그룹]
const numpad = [
  [0, 0, 1, 1, 'Num', null, null],
  [1, 0, 1, 1, '/', '보스 소환', 'mon'],
  [2, 0, 1, 1, '*', '몬스터 1기', 'mon'],
  [3, 0, 1, 1, '−', '웨이브 토글', 'mon'],
  [0, 1, 1, 1, '7', '연결부 파괴', 'build'],
  [1, 1, 1, 1, '8', '칸 건설', 'build'],
  [2, 1, 1, 1, '9', '부위 피해', 'build'],
  [3, 1, 1, 2, '+', '재시작', 'build'],
  [0, 2, 1, 1, '4', '자원 지급', 'res'],
  [1, 2, 1, 1, '5', '피해 실측', 'res'],
  [2, 2, 1, 1, '6', '창고 경합', 'res'],
  [0, 3, 1, 1, '1', '낮으로', 'cycle'],
  [1, 3, 1, 1, '2', '밤으로', 'cycle'],
  [2, 3, 1, 1, '3', '다음 Day', 'cycle'],
  [3, 3, 1, 2, '↵', null, null],
  [0, 4, 2, 1, '0', '동시 그랩', 'res'],
  [2, 4, 1, 1, '.', '보스 처치', 'mon'],
];

// ── F 열 ─────────────────────────────────────────────────────────────────
const FX = 322, FY = 66, FW = 44, FH = 48, FGAP = 4;
const fx = i => FX + i * (FW + FGAP) + (i >= 4 ? 12 : 0) + (i >= 8 ? 12 : 0);

// [인덱스, 기호, 라벨, 그룹, 로컬전용]
const frow = [
  [0, 'F1', null, null, false],
  [1, 'F2', '열차 높이', 'build', false],
  [2, 'F3', '시간 배속', 'cycle', false],
  [3, 'F4', null, null, false],
  [4, 'F5', null, null, false],
  [5, 'F6', null, null, false],
  [6, 'F7', null, null, false],
  [7, 'F8', '연출 모드', 'view', true],
  [8, 'F9', '구속', 'view', false],
  [9, 'F10', '시점', 'view', true],
  [10, 'F11', null, null, false],
  [11, 'F12', null, null, false],
];

// ── 카드 ─────────────────────────────────────────────────────────────────
const CW = 375;
const cards = [
  {
    g: 'build', x: 30, y: 150, side: 'right',
    rows: [
      ['7', '후미 연결부 1개 즉사 → 연쇄 이탈'],
      ['8', '칸 1칸 무료 건설, 빈 슬롯 우선'],
      ['9', '샘플 피해 30, 망치 조준 부위 우선'],
      ['+', '현재 인게임 씬 재로드 = 전체 초기화'],
      ['F2', '열차·궤도 높이 순환 ⟨ArtTest 전용⟩'],
    ],
  },
  {
    g: 'res', x: 30, y: 350, side: 'right',
    rows: [
      ['4', '자원 9종 일괄 지급 (건설·요리·방한)'],
      ['5', '자기 피해 20 — 장비 감산 실측용'],
      ['6', '창고 동시 경합 → 총량 보존 콘솔'],
      ['0', '동시 그랩 경합 → 승인·거부 콘솔'],
    ],
  },
  {
    g: 'view', x: 835, y: 150, side: 'left',
    rows: [
      ['F8', '낮/밤 연출 모드 Off→A→B ⟨ArtTest⟩'],
      ['F9', '구속(Grabbed) 상태 토글 ⟨상시 켜짐⟩'],
      ['F10', '시점 전환: 1인칭 통합 ↔ 분리'],
    ],
  },
  {
    g: 'mon', x: 835, y: 320, side: 'left',
    rows: [
      ['/', '지역 보스 즉시 소환, 새벽 보류 없음'],
      ['*', '요청자 전방 10 m에 몬스터 1마리'],
      ['−', '웨이브 스폰 토글, 끄면 진행분 회수'],
      ['.', '지역 보스 즉시 처치 → 드랍·배너'],
    ],
  },
  {
    g: 'cycle', x: 835, y: 500, side: 'left',
    rows: [
      ['1', '현재 Day의 낮 시작으로 점프'],
      ['2', '현재 Day의 밤 시작으로 점프'],
      ['3', '다음 Day 아침으로 (지역 전환용)'],
      ['F3', '시간 배속 ×1 → ×4 → ×16 가속'],
    ],
  },
];

const HEAD = 34, ROW = 25, PAD = 10;
const cardH = c => HEAD + c.rows.length * ROW + PAD;

// ── 연결선: [시작 x, y, 끝 x, y, 그룹] ───────────────────────────────────
const links = [
  [NX - 14, cy(1) + KS / 2, 30 + CW, 150 + cardH(cards[0]) / 2, 'build'],
  [NX - 14, cy(2) + KS / 2, 30 + CW, 350 + cardH(cards[1]) / 2, 'res'],
  [fx(8) + FW / 2, FY + FH, 835, 150 + cardH(cards[2]) / 2, 'view'],
  [NX + 4 * STEP - GAP + 14, cy(0) + KS / 2, 835, 320 + cardH(cards[3]) / 2, 'mon'],
  [NX + 4 * STEP - GAP + 14, cy(3) + KS / 2, 835, 500 + cardH(cards[4]) / 2, 'cycle'],
];

// ── 렌더 ─────────────────────────────────────────────────────────────────
const esc = s => String(s).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
const out = [];
const p = s => out.push(s);

p(`<?xml version="1.0" encoding="UTF-8"?>`);
p(`<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${W} ${H}" width="${W}" height="${H}" font-family="'Malgun Gothic','Apple SD Gothic Neo','Noto Sans KR','Segoe UI',sans-serif">`);
p(`<style>
    :root{--bg:#ffffff;--fg:#1f2328;--muted:#6b7280;--card:#f6f8fa;--line:#d0d7de;--key:#ffffff;--off:#eceff2;--offfg:#98a1ac;}
    @media (prefers-color-scheme: dark){:root{--bg:#0d1117;--fg:#e6edf3;--muted:#9aa5b1;--card:#161b22;--line:#30363d;--key:#161b22;--off:#1c2128;--offfg:#6e7781;}}
    .t{fill:var(--fg)} .m{fill:var(--muted)} .o{fill:var(--offfg)}
  </style>`);
p(`<rect width="${W}" height="${H}" fill="var(--bg)"/>`);

// 제목
p(`<text x="30" y="34" class="t" font-size="19" font-weight="700">QA 디버그 키 지도</text>`);
p(`<text x="30" y="56" class="m" font-size="12.5">한 행 = 한 기능 그룹 · 색 = 그룹 · 회색 = QA 미사용</text>`);

// 연결선
for (const [x1, y1, x2, y2, g] of links) {
  const dx = Math.max(40, Math.abs(x2 - x1) * 0.55);
  const s = x2 > x1 ? 1 : -1;
  p(`<path d="M ${x1} ${y1} C ${x1 + s * dx} ${y1}, ${x2 - s * dx} ${y2}, ${x2} ${y2}" fill="none" stroke="${G[g].color}" stroke-width="2.4" stroke-opacity="0.75" stroke-linecap="round"/>`);
  p(`<circle cx="${x1}" cy="${y1}" r="6.5" fill="${G[g].color}"/>`);
}

// 숫자패드 배경판
p(`<rect x="${NX - 14}" y="${NY - 14}" width="${4 * STEP - GAP + 28}" height="${5 * STEP - GAP + 28}" rx="12" fill="var(--card)" stroke="var(--line)"/>`);
p(`<text x="${NX + (4 * STEP - GAP) / 2}" y="${NY + 5 * STEP - GAP + 34}" class="m" font-size="12" text-anchor="middle">숫자패드 — NumLock·Enter를 빼면 15키가 전부 QA다</text>`);

function key(x, y, w, h, sym, label, g, dashed) {
  const c = g ? G[g].color : null;
  const fill = c ? `${c}" fill-opacity="0.16` : 'var(--off)';
  const stroke = c || 'var(--line)';
  const dash = dashed ? ` stroke-dasharray="5 3"` : '';
  p(`<rect x="${x}" y="${y}" width="${w}" height="${h}" rx="8" fill="${fill}" stroke="${stroke}" stroke-width="${c ? 2 : 1.2}"${dash}/>`);
  return { c, stroke };
}

// 숫자패드 키
for (const [j, i, wc, hc, sym, label, g] of numpad) {
  const x = cx(j), y = cy(i);
  const w = wc * KS + (wc - 1) * GAP, h = hc * KS + (hc - 1) * GAP;
  key(x, y, w, h, sym, label, g, false);
  const mx = x + w / 2, my = y + h / 2;
  if (label) {
    p(`<text x="${mx}" y="${my - 4}" class="t" font-size="21" font-weight="700" text-anchor="middle">${esc(sym)}</text>`);
    p(`<text x="${mx}" y="${my + 15}" class="m" font-size="10.5" text-anchor="middle">${esc(label)}</text>`);
  } else {
    p(`<text x="${mx}" y="${my + 5}" class="o" font-size="14" text-anchor="middle">${esc(sym)}</text>`);
  }
  if (g) {
    p(`<circle cx="${x + w - 11}" cy="${y + 11}" r="6" fill="${G[g].color}"/>`);
    p(`<text x="${x + w - 11}" y="${y + 14.5}" fill="#ffffff" font-size="8.5" font-weight="700" text-anchor="middle">${G[g].n}</text>`);
  }
}

// F 열
p(`<rect x="${FX - 12}" y="${FY - 12}" width="${fx(11) + FW - FX + 24}" height="${FH + 24}" rx="10" fill="var(--card)" stroke="var(--line)"/>`);
for (const [i, sym, label, g, local] of frow) {
  const x = fx(i);
  key(x, FY, FW, FH, sym, label, g, local);
  const mx = x + FW / 2;
  if (label) {
    p(`<text x="${mx}" y="${FY + 20}" class="t" font-size="13" font-weight="700" text-anchor="middle">${esc(sym)}</text>`);
    p(`<text x="${mx}" y="${FY + 36}" class="m" font-size="8.5" text-anchor="middle">${esc(label)}</text>`);
  } else {
    p(`<text x="${mx}" y="${FY + 29}" class="o" font-size="11" text-anchor="middle">${esc(sym)}</text>`);
  }
  if (g) {
    p(`<circle cx="${x + FW - 9}" cy="${FY + 9}" r="5.2" fill="${G[g].color}"/>`);
    p(`<text x="${x + FW - 9}" y="${FY + 12}" fill="#ffffff" font-size="7.5" font-weight="700" text-anchor="middle">${G[g].n}</text>`);
  }
}
// F3~F6 빈자리 표시
const bx = fx(2), bw = fx(5) + FW - bx;
p(`<path d="M ${bx} ${FY + FH + 9} L ${bx} ${FY + FH + 14} L ${bx + bw} ${FY + FH + 14} L ${bx + bw} ${FY + FH + 9}" fill="none" stroke="var(--line)" stroke-width="1.2"/>`);
p(`<text x="${bx + bw / 2}" y="${FY + FH + 27}" class="m" font-size="10.5" text-anchor="middle">비어 있다 — 새 QA 키는 여기로</text>`);

// 카드
for (const c of cards) {
  const h = cardH(c), col = G[c.g].color;
  p(`<rect x="${c.x}" y="${c.y}" width="${CW}" height="${h}" rx="10" fill="var(--card)" stroke="${col}" stroke-width="1.6" stroke-opacity="0.55"/>`);
  p(`<circle cx="${c.x + 24}" cy="${c.y + 22}" r="9" fill="${col}"/>`);
  p(`<text x="${c.x + 24}" y="${c.y + 25.5}" fill="#ffffff" font-size="11" font-weight="700" text-anchor="middle">${G[c.g].n}</text>`);
  p(`<text x="${c.x + 41}" y="${c.y + 27}" class="t" font-size="15" font-weight="700">${esc(G[c.g].name)}</text>`);
  c.rows.forEach(([chip, text], k) => {
    const ry = c.y + HEAD + k * ROW;
    const cwid = chip.length >= 3 ? 34 : chip.length === 2 ? 28 : 22;
    p(`<rect x="${c.x + 14}" y="${ry + 2}" width="${cwid}" height="18" rx="5" fill="${col}" fill-opacity="0.18" stroke="${col}" stroke-width="1.2"/>`);
    p(`<text x="${c.x + 14 + cwid / 2}" y="${ry + 15.5}" class="t" font-size="11" font-weight="700" text-anchor="middle">${esc(chip)}</text>`);
    p(`<text x="${c.x + 20 + cwid + 10}" y="${ry + 15.5}" class="t" font-size="12.5">${esc(text)}</text>`);
  });
}

// 범례
const LY = 660;
p(`<line x1="30" y1="${LY - 22}" x2="${W - 30}" y2="${LY - 22}" stroke="var(--line)"/>`);
const legend = [
  '점선 테두리 = 로컬 전용(F7·F10). 나머지 키는 클라이언트에서 눌러도 호스트가 확정해 전 피어에 반영된다.',
  '⟨ArtTest⟩ = Game_ArtTest 씬에만 배선 — Game.unity에서는 눌러도 무효 로그만 남는다.',
  'F9만 끄는 스위치가 없다. 나머지는 인스펙터 bool 또는 PlayerViewSettings로 끈다 — 릴리스 전 확인.',
];
legend.forEach((t, i) => p(`<text x="30" y="${LY + i * 19}" class="m" font-size="12.5">${esc(t)}</text>`));

p(`</svg>`);

fs.mkdirSync(path.dirname(OUT), { recursive: true });
fs.writeFileSync(OUT, out.join('\n'), 'utf8');
console.log('wrote', OUT, out.join('\n').length, 'bytes');
