from pathlib import Path
from xml.sax.saxutils import escape

from PIL import Image as PILImage
from reportlab.lib import colors
from reportlab.lib.enums import TA_CENTER, TA_LEFT, TA_RIGHT
from reportlab.lib.pagesizes import A4, landscape
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.units import mm
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.pdfgen import canvas
from reportlab.lib.utils import ImageReader
from reportlab.platypus import Paragraph


ROOT = Path(__file__).resolve().parents[1]
OUTPUT = ROOT / "output" / "pdf" / "이한울_2026_넥토리얼_게임프로그래머_포트폴리오.pdf"
ASSETS = Path(r"C:/Users/iam12/ts-ghpages/assets")

PAGE_W, PAGE_H = landscape(A4)
NAVY = colors.HexColor("#101B2D")
NAVY_2 = colors.HexColor("#172740")
INK = colors.HexColor("#172238")
MUTED = colors.HexColor("#637083")
PALE = colors.HexColor("#F3F6F8")
LINE = colors.HexColor("#D8E0E6")
TEAL = colors.HexColor("#16A6A0")
TEAL_DARK = colors.HexColor("#087A77")
TEAL_PALE = colors.HexColor("#E7F6F4")
ORANGE = colors.HexColor("#ED8B42")
ORANGE_PALE = colors.HexColor("#FFF1E6")
WHITE = colors.white
RED = colors.HexColor("#C94B50")
GREEN = colors.HexColor("#1C8E6A")


def register_fonts():
    pdfmetrics.registerFont(TTFont("Malgun", r"C:/Windows/Fonts/malgun.ttf"))
    pdfmetrics.registerFont(TTFont("Malgun-Bold", r"C:/Windows/Fonts/malgunbd.ttf"))
    pdfmetrics.registerFontFamily(
        "Malgun", normal="Malgun", bold="Malgun-Bold", italic="Malgun", boldItalic="Malgun-Bold"
    )


register_fonts()


def style(size=10, leading=None, color=INK, bold=False, align=TA_LEFT, space_after=0):
    return ParagraphStyle(
        name=f"s-{size}-{bold}-{align}-{color}",
        fontName="Malgun-Bold" if bold else "Malgun",
        fontSize=size,
        leading=leading or size * 1.48,
        textColor=color,
        alignment=align,
        spaceAfter=space_after,
        allowWidows=0,
        allowOrphans=0,
    )


S = {
    "eyebrow": style(8.5, 11, TEAL_DARK, True),
    "title": style(23, 30, INK, True),
    "subtitle": style(11, 17, MUTED),
    "h2": style(14, 19, INK, True),
    "h3": style(10.5, 15, INK, True),
    "body": style(9, 14, INK),
    "body_muted": style(8.3, 13, MUTED),
    "small": style(7.3, 10.5, MUTED),
    "white": style(9, 14, WHITE),
    "white_muted": style(8.2, 12.5, colors.HexColor("#C3CFDD")),
    "metric": style(17, 20, INK, True),
    "metric_white": style(18, 21, WHITE, True),
}


def p(c, html, x, y_top, width, ps="body", height=None):
    st = S[ps] if isinstance(ps, str) else ps
    para = Paragraph(html, st)
    _, h = para.wrap(width, height or PAGE_H)
    para.drawOn(c, x, y_top - h)
    return h


def rule(c, x1, y, x2, color=LINE, width=0.7):
    c.setStrokeColor(color)
    c.setLineWidth(width)
    c.line(x1, y, x2, y)


def round_box(c, x, y, w, h, fill=WHITE, stroke=LINE, radius=4 * mm, line_width=0.7):
    c.setFillColor(fill)
    c.setStrokeColor(stroke)
    c.setLineWidth(line_width)
    c.roundRect(x, y, w, h, radius, fill=1, stroke=1)


def pill(c, text, x, y, fill=TEAL_PALE, color=TEAL_DARK, width=None):
    ps = style(7.2, 9, color, True, TA_CENTER)
    width = width or (pdfmetrics.stringWidth(text, "Malgun-Bold", 7.2) + 7 * mm)
    h = 6.8 * mm
    c.setFillColor(fill)
    c.roundRect(x, y, width, h, h / 2, fill=1, stroke=0)
    p(c, escape(text), x + 2 * mm, y + h - 1.5 * mm, width - 4 * mm, ps)
    return width


def draw_crop(c, path, x, y, w, h, anchor="center"):
    path = Path(path)
    im = PILImage.open(path)
    iw, ih = im.size
    scale = max(w / iw, h / ih)
    sw, sh = w / scale, h / scale
    left = max(0, (iw - sw) / 2)
    top = max(0, (ih - sh) / 2)
    if anchor == "top":
        top = 0
    elif anchor == "bottom":
        top = ih - sh
    cropped = im.crop((int(left), int(top), int(left + sw), int(top + sh)))
    c.drawImage(ImageReader(cropped), x, y, w, h, mask="auto")


def link(text, url, color="#087A77"):
    return f'<link href="{url}" color="{color}"><u>{escape(text)}</u></link>'


def page_chrome(c, number, section, dark=False):
    if dark:
        return
    c.setFillColor(TEAL)
    c.rect(0, 0, 5 * mm, PAGE_H, fill=1, stroke=0)
    p(c, f"2026 NEXON NEXTORIAL  /  {escape(section)}", 16 * mm, PAGE_H - 9 * mm, 120 * mm, "eyebrow")
    p(c, f"{number:02d}", PAGE_W - 25 * mm, PAGE_H - 8 * mm, 12 * mm, style(8, 10, MUTED, True, TA_RIGHT))
    rule(c, 16 * mm, 11 * mm, PAGE_W - 14 * mm)
    p(c, "이한울 · Train Survival · 2026-09-07", 16 * mm, 9 * mm, 90 * mm, "small")
    p(c, "hanwoolhanwool.github.io/Train-Survival", PAGE_W - 92 * mm, 9 * mm, 78 * mm, style(7.3, 10, MUTED, False, TA_RIGHT))


def page_title(c, eyebrow, title, subtitle=None):
    p(c, escape(eyebrow.upper()), 16 * mm, PAGE_H - 18 * mm, 245 * mm, "eyebrow")
    y = PAGE_H - 26 * mm
    h = p(c, title, 16 * mm, y, 250 * mm, "title")
    if subtitle:
        p(c, subtitle, 16 * mm, y - h - 2 * mm, 250 * mm, "subtitle")


def metric(c, x, y, w, value, label, dark=False, accent=TEAL):
    ps_val = "metric_white" if dark else "metric"
    ps_lbl = "white_muted" if dark else "body_muted"
    p(c, escape(value), x, y, w, ps_val)
    c.setFillColor(accent)
    c.rect(x, y - 23, 10 * mm, 1.3 * mm, fill=1, stroke=0)
    p(c, escape(label), x, y - 9 * mm, w, ps_lbl)


def dot_item(c, title, body, x, y_top, w, accent=TEAL, dark=False):
    c.setFillColor(accent)
    c.circle(x + 1.6 * mm, y_top - 2.8 * mm, 1.3 * mm, fill=1, stroke=0)
    title_style = style(9.2, 13.5, WHITE if dark else INK, True)
    body_style = "white_muted" if dark else "body_muted"
    h1 = p(c, title, x + 6 * mm, y_top, w - 6 * mm, title_style)
    h2 = p(c, body, x + 6 * mm, y_top - h1 - 1 * mm, w - 6 * mm, body_style)
    return h1 + h2 + 4 * mm


def draw_arrow(c, x1, y1, x2, y2, color=TEAL, width=1.4):
    c.setStrokeColor(color)
    c.setFillColor(color)
    c.setLineWidth(width)
    c.line(x1, y1, x2, y2)
    import math
    angle = math.atan2(y2 - y1, x2 - x1)
    size = 3.5 * mm
    a1 = angle + 2.65
    a2 = angle - 2.65
    path = c.beginPath()
    path.moveTo(x2, y2)
    path.lineTo(x2 + size * math.cos(a1), y2 + size * math.sin(a1))
    path.lineTo(x2 + size * math.cos(a2), y2 + size * math.sin(a2))
    path.close()
    c.drawPath(path, fill=1, stroke=0)


def page_1(c):
    c.setFillColor(NAVY)
    c.rect(0, 0, PAGE_W, PAGE_H, fill=1, stroke=0)
    draw_crop(c, ASSETS / "ts-hero.png", 132 * mm, 0, PAGE_W - 132 * mm, PAGE_H, "center")
    c.saveState()
    c.setFillAlpha(0.88)
    c.setFillColor(NAVY)
    c.rect(122 * mm, 0, 72 * mm, PAGE_H, fill=1, stroke=0)
    c.setFillAlpha(1)
    c.restoreState()
    c.setFillColor(NAVY)
    c.rect(0, 0, 136 * mm, PAGE_H, fill=1, stroke=0)
    c.setFillColor(TEAL)
    c.rect(0, 0, 5 * mm, PAGE_H, fill=1, stroke=0)

    p(c, "2026 NEXON NEXTORIAL", 17 * mm, PAGE_H - 19 * mm, 100 * mm,
      style(9, 12, colors.HexColor("#5EE0D7"), True))
    p(c, "게임을 만들고<br/>기준까지 만든<br/>클라이언트 개발자", 17 * mm, PAGE_H - 38 * mm, 108 * mm,
      style(27, 35, WHITE, True))
    p(c, "C# · Unity · Networked Gameplay · Tooling", 17 * mm, PAGE_H - 114 * mm, 102 * mm,
      style(10.5, 15, colors.HexColor("#C7D5E2")))
    c.setFillColor(ORANGE)
    c.rect(17 * mm, PAGE_H - 126 * mm, 22 * mm, 1.8 * mm, fill=1, stroke=0)
    p(c, "이한울", 17 * mm, PAGE_H - 137 * mm, 58 * mm, style(15, 20, WHITE, True))
    p(c, "Train Survival · 1인 개발<br/>2026.07.17 - 진행 중", 17 * mm, PAGE_H - 148 * mm, 88 * mm, "white_muted")

    # Bottom metric strip
    c.saveState()
    c.setFillAlpha(0.94)
    c.setFillColor(colors.HexColor("#0B1423"))
    c.rect(0, 0, PAGE_W, 32 * mm, fill=1, stroke=0)
    c.setFillAlpha(1)
    c.restoreState()
    metrics = [
        ("57,115", "C# lines / 446 files"),
        ("1,433", "test declarations"),
        ("902", "Git commits"),
        ("14", "automation and tools"),
    ]
    for i, (value, label) in enumerate(metrics):
        metric(c, 17 * mm + i * 63 * mm, 26 * mm, 52 * mm, value, label, True, ORANGE if i == 3 else TEAL)
    p(c, "Snapshot 2026-09-07 · repository source count", PAGE_W - 90 * mm, 4 * mm, 76 * mm,
      style(6.5, 8, colors.HexColor("#7F90A5"), False, TA_RIGHT))


def page_2(c):
    page_chrome(c, 2, "JOB FIT")
    page_title(c, "01 / JOB FIT", "공고가 찾는 역량을<br/>프로젝트의 증거로 답합니다",
               "기술 키워드보다, 실제로 내린 결정과 검증 결과가 먼저 보이도록 구성했습니다.")
    x0, y_top = 16 * mm, PAGE_H - 62 * mm
    widths = [55 * mm, 133 * mm, 68 * mm]
    row_h = 19.4 * mm
    headers = ["공고의 평가 축", "Train Survival에서 확인할 증거", "읽을 위치"]
    c.setFillColor(NAVY)
    c.roundRect(x0, y_top - 10 * mm, sum(widths), 10 * mm, 3 * mm, fill=1, stroke=0)
    cur = x0
    for i, hd in enumerate(headers):
        p(c, hd, cur + 4 * mm, y_top - 2.4 * mm, widths[i] - 8 * mm, style(8, 10, WHITE, True))
        cur += widths[i]

    rows = [
        ("C# · 게임 시스템", "Unity 6에서 집게·열차·건축·전투·지역 사이클을 구현. 판정 로직을 순수 C#으로 분리해 테스트.", "p.3-5"),
        ("자료구조 · 알고리즘", "Day 하나에서 지역 상태를 파생하고, 상태 변이는 스냅샷-판정-일괄 반영으로 원자화. 전체 순회를 공간 단위로 축소.", "p.3-5"),
        ("디버깅 · 최적화", "성능 계측기 오류 5건을 찾아 안전장치로 전환. 문서상 1순위 최적화를 기각하고 GPU p95 6.3% 개선안을 채택.", "p.6"),
        ("툴 제작 · 자동화", "성능 회귀 게이트, Unity CI, AssetPostprocessor, ViewLab 등 14건을 구축해 판단과 반복 작업을 자동화.", "p.7"),
        ("네트워크 경험", "NGO + Steam P2P. 상태별 권위를 분리하고 월드 위치의 상시 재전송을 제거. 60 ms RTT 프로파일로 집게 반응성 검증.", "p.4-5"),
        ("생성형 AI 활용", "AI 작업 규칙을 저장소에 두고, 결과를 테스트·플레이 계측·대조군·아트 예산으로 검증. 실패 결과도 근거와 함께 기각.", "p.8"),
    ]
    y = y_top - 10 * mm
    for idx, row in enumerate(rows):
        fill = WHITE if idx % 2 == 0 else PALE
        c.setFillColor(fill)
        c.setStrokeColor(LINE)
        c.rect(x0, y - row_h, sum(widths), row_h, fill=1, stroke=0)
        cur = x0
        p(c, row[0], cur + 4 * mm, y - 4 * mm, widths[0] - 8 * mm, "h3")
        cur += widths[0]
        p(c, row[1], cur + 4 * mm, y - 4 * mm, widths[1] - 8 * mm, "body_muted")
        cur += widths[1]
        pill(c, row[2], cur + 5 * mm, y - row_h + 6 * mm, TEAL_PALE, TEAL_DARK, 21 * mm)
        y -= row_h
    round_box(c, 16 * mm, 20 * mm, 256 * mm, 20 * mm, TEAL_PALE, TEAL_PALE)
    p(c, "<b>핵심 역량</b>  Unity와 C#으로 게임 시스템과 콘텐츠를 직접 구현하고, 디버깅·최적화까지 책임집니다. 반복 작업과 판정 기준은 자동화 도구로 옮겨 정확성과 효율을 높이며, 개발 과정의 비효율을 구조적으로 개선합니다.",
      22 * mm, 35 * mm, 244 * mm, style(9.4, 13.5, TEAL_DARK))
    p(c, "공고 근거: " + link("2026 넥토리얼 For Game Programmer", "https://www.nexon-tutorial.com/"),
      170 * mm, 16 * mm, 102 * mm, style(7.2, 10, MUTED, False, TA_RIGHT))


def page_3(c):
    page_chrome(c, 3, "PROJECT")
    page_title(c, "02 / PROJECT", "멈추면 죽는 열차에서<br/>낮에 모으고, 밤에 버팁니다",
               "1-4인 협동 생존 크래프팅 · PC / Steam · Unity 6 · Netcode for GameObjects")
    # Images
    draw_crop(c, ASSETS / "ts-day.png", 16 * mm, 65 * mm, 82 * mm, 55 * mm)
    draw_crop(c, ASSETS / "ts-night.png", 100 * mm, 65 * mm, 82 * mm, 55 * mm)
    pill(c, "DAY / 채집", 20 * mm, 69 * mm, TEAL, WHITE, 31 * mm)
    pill(c, "NIGHT / 방어", 104 * mm, 69 * mm, ORANGE, WHITE, 35 * mm)
    p(c, "집게로 지상의 자원을 낚아채 제작·건설·열차 증설에 사용합니다.", 18 * mm, 61 * mm, 78 * mm, "body_muted")
    p(c, "몬스터가 칸과 연결부를 공격합니다. 뒤 편성이 이탈하면 사람이 끌어와 재결합합니다.", 102 * mm, 61 * mm, 78 * mm, "body_muted")

    # Right column ownership
    round_box(c, 190 * mm, 37 * mm, 82 * mm, 83 * mm, NAVY, NAVY)
    p(c, "1인 개발 범위", 198 * mm, 112 * mm, 65 * mm, style(13, 18, WHITE, True))
    yy = 99 * mm
    for title, body in [
        ("DESIGN", "코어 루프·경제·지역·엔드게임"),
        ("CLIENT", "게임플레이·UI·물리·렌더링"),
        ("NETWORK", "권위·복제·Steam 로비/릴레이"),
        ("QUALITY", "테스트·CI·프로파일링·QA 도구"),
    ]:
        yy -= dot_item(c, title, body, 198 * mm, yy, 65 * mm, TEAL, True)
    p(c, "진행 상태: 핵심 루프와 네트워크 수직 슬라이스 구현. 월드·레벨 확장 진행 중.",
      198 * mm, 48 * mm, 65 * mm, style(7.5, 11, colors.HexColor("#C3CFDD")))

    # Architecture strip
    p(c, "코드 구조", 16 * mm, 31 * mm, 40 * mm, "h2")
    layers = [("Utilities", 16), ("Core", 59), ("Systems", 102), ("Gameplay", 145), ("UI", 188)]
    for i, (name, xpos) in enumerate(layers):
        fill = TEAL_PALE if i < 3 else ORANGE_PALE
        stroke = TEAL if i < 3 else ORANGE
        round_box(c, xpos * mm, 16 * mm, 37 * mm, 10 * mm, fill, stroke, 2.5 * mm)
        p(c, name, xpos * mm, 23.6 * mm, 37 * mm, style(8.3, 10, INK, True, TA_CENTER))
        if i < len(layers) - 1:
            draw_arrow(c, (xpos + 38) * mm, 21 * mm, (xpos + 41) * mm, 21 * mm, MUTED, 0.8)
    p(c, "asmdef가 의존 방향을 한쪽으로만 열어 역참조를 컴파일 에러로 차단합니다.",
      16 * mm, 13.5 * mm, 220 * mm, "small")


def page_4(c):
    page_chrome(c, 4, "ARCHITECTURE")
    page_title(c, "03 / ARCHITECTURE", "열차를 움직이지 않았더니<br/>네트워크 문제가 줄었습니다",
               "좌표계를 먼저 바꾸고, 상태별 권위를 나눠, 필요한 사실만 복제했습니다.")
    # Left: world scroll decision
    round_box(c, 16 * mm, 24 * mm, 80 * mm, 102 * mm, PALE, LINE)
    pill(c, "DECISION 01", 23 * mm, 114 * mm, TEAL, WHITE, 35 * mm)
    p(c, "열차 고정 + 월드 스크롤", 23 * mm, 108 * mm, 66 * mm, "h2")
    p(c, "실제 열차 이동은 장거리 주행에서 부동소수점 정밀도와 이동 플랫폼 동기화 문제를 동시에 만듭니다. 열차를 원점에 고정하고 지형·자원·몬스터가 뒤로 흐르게 했습니다.",
      23 * mm, 94 * mm, 66 * mm, "body")
    # mini diagram
    c.setFillColor(NAVY_2)
    c.roundRect(24 * mm, 51 * mm, 64 * mm, 23 * mm, 3 * mm, fill=1, stroke=0)
    c.setFillColor(ORANGE)
    c.roundRect(48 * mm, 59 * mm, 18 * mm, 8 * mm, 2 * mm, fill=1, stroke=0)
    p(c, "TRAIN", 48 * mm, 65 * mm, 18 * mm, style(6.5, 8, WHITE, True, TA_CENTER))
    draw_arrow(c, 42 * mm, 63 * mm, 29 * mm, 63 * mm, colors.HexColor("#5EE0D7"), 1.2)
    draw_arrow(c, 84 * mm, 63 * mm, 71 * mm, 63 * mm, colors.HexColor("#5EE0D7"), 1.2)
    p(c, "world moves", 30 * mm, 55 * mm, 52 * mm, style(6.7, 8, colors.HexColor("#C3CFDD"), False, TA_CENTER))
    p(c, "<b>결과</b> 월드 오브젝트 상시 위치 재전송 0건<br/>주행 거리와 무관하게 원점 부근 정밀도 유지", 23 * mm, 43 * mm, 66 * mm, style(8.5, 13, TEAL_DARK))

    # Middle authority matrix
    round_box(c, 102 * mm, 24 * mm, 82 * mm, 102 * mm, WHITE, LINE)
    pill(c, "DECISION 02", 109 * mm, 114 * mm, ORANGE, WHITE, 35 * mm)
    p(c, "상태마다 권위를 분리", 109 * mm, 108 * mm, 68 * mm, "h2")
    authority = [
        ("이동 · 시점 · 발사 연출", "소유자", "입력 지연 0"),
        ("명중 보고", "소유자", "로컬 레이캐스트"),
        ("데미지 · 사망", "호스트", "최종 확정"),
        ("월드 · 인벤토리 · 열차", "호스트", "복제·원자성"),
    ]
    y = 91 * mm
    for idx, (state, who, why) in enumerate(authority):
        c.setFillColor(PALE if idx % 2 else WHITE)
        c.rect(109 * mm, y - 12 * mm, 68 * mm, 12 * mm, fill=1, stroke=0)
        p(c, state, 112 * mm, y - 3 * mm, 31 * mm, style(7.4, 10, INK, True))
        p(c, who, 145 * mm, y - 3 * mm, 13 * mm, style(7.2, 10, TEAL_DARK if who == "소유자" else ORANGE, True))
        p(c, why, 158 * mm, y - 3 * mm, 17 * mm, style(6.8, 9, MUTED))
        y -= 12 * mm
    p(c, "협동 PvE의 신뢰 모델에 맞춰 치팅 리스크를 수용하고,<br/>예측·랙 보상 복잡도를 줄였습니다.",
      109 * mm, 39 * mm, 68 * mm, style(7.2, 10, MUTED))

    # Right: deterministic data
    round_box(c, 190 * mm, 24 * mm, 82 * mm, 102 * mm, TEAL_PALE, TEAL_PALE)
    pill(c, "DECISION 03", 197 * mm, 114 * mm, NAVY, WHITE, 35 * mm)
    p(c, "복제보다 파생", 197 * mm, 108 * mm, 67 * mm, "h2")
    items = [
        ("월드 위치", "spawnZ - (currentDistance - spawnDistance)"),
        ("지역 상태", "Day 번호 1개에서 인덱스·마지막 날·다음 지역 파생"),
        ("산탄 궤적", "시드 1개를 공유하고 모든 피어가 동일 궤적 재계산"),
        ("대기실 UI", "접속 순서 목록 1개에서 이름·호스트·자리 상태 파생"),
    ]
    yy = 93 * mm
    for title, body in items:
        c.setFillColor(TEAL_DARK)
        c.circle(199 * mm, yy - 2.8 * mm, 1.2 * mm, fill=1, stroke=0)
        p(c, title, 203 * mm, yy, 59 * mm, style(8.1, 10.5, INK, True))
        p(c, body, 203 * mm, yy - 5.3 * mm, 59 * mm, style(6.7, 9.2, MUTED))
        yy -= 15 * mm
    p(c, "원천 상태를 줄이면 갱신 순서가 어긋날 가능성도 함께 줄어듭니다.", 197 * mm, 31 * mm, 67 * mm,
      style(7.6, 10.5, TEAL_DARK, True))


def page_5(c):
    page_chrome(c, 5, "GAMEPLAY CASE")
    page_title(c, "04 / GAMEPLAY CASE", "집게의 손맛을<br/>지연 0과 호스트 확정 사이에 놓았습니다",
               "로컬 반응과 권위 판정을 분리하고, 둘이 어긋났을 때 반드시 한 상태로 수렴하게 했습니다.")
    draw_crop(c, ASSETS / "ts-day.png", 16 * mm, 22 * mm, 104 * mm, 93 * mm, "center")
    c.saveState()
    c.setFillAlpha(0.84)
    c.setFillColor(NAVY)
    c.roundRect(21 * mm, 29 * mm, 92 * mm, 33 * mm, 3 * mm, fill=1, stroke=0)
    c.setFillAlpha(1)
    c.restoreState()
    p(c, "발사 즉시 로컬 훅 생성", 27 * mm, 55 * mm, 78 * mm, style(13, 18, WHITE, True))
    p(c, "호스트 응답을 기다리는 동안에도 탄성 연출을 유지하고, 승인·거부가 오면 Attach 또는 Retract로 수렴합니다.",
      27 * mm, 45 * mm, 78 * mm, "white_muted")

    # pipeline
    x = 130 * mm
    steps = [
        ("1", "FIRE", "로컬 선반영", TEAL),
        ("2", "HIT", "SphereCast", TEAL),
        ("3", "VALIDATE", "호스트 판정", ORANGE),
        ("4", "CONVERGE", "Attach / Retract", NAVY),
    ]
    yy = 111 * mm
    for i, (n, title, body, col) in enumerate(steps):
        c.setFillColor(col)
        c.circle(x + 4 * mm, yy - 4 * mm, 4 * mm, fill=1, stroke=0)
        p(c, n, x, yy - 1.3 * mm, 8 * mm, style(7.5, 9, WHITE, True, TA_CENTER))
        p(c, title, x + 12 * mm, yy, 43 * mm, style(9, 12, INK, True))
        p(c, body, x + 56 * mm, yy, 34 * mm, style(8, 11, MUTED))
        if i < len(steps) - 1:
            c.setStrokeColor(LINE)
            c.setLineWidth(1)
            c.line(x + 4 * mm, yy - 9 * mm, x + 4 * mm, yy - 17 * mm)
        yy -= 18 * mm

    # metrics and implementation
    round_box(c, 225 * mm, 73 * mm, 47 * mm, 42 * mm, NAVY, NAVY)
    metric(c, 232 * mm, 108 * mm, 34 * mm, "0 frame", "local fire response", True, TEAL)
    metric(c, 232 * mm, 88 * mm, 34 * mm, "66-72 ms", "approval avg @ 60 ms RTT", True, ORANGE)

    round_box(c, 130 * mm, 20 * mm, 142 * mm, 30 * mm, PALE, LINE)
    p(c, "구현과 검증", 138 * mm, 44 * mm, 55 * mm, "h3")
    col_w = 59 * mm
    p(c, "<b>상태 기계 2개</b>  입력 게이트와 시각 단계를 분리해 취소·실패·타임아웃을 독립 제어.<br/><b>IGrabbable</b>  자원·몬스터·손잡이를 같은 계약에 연결.",
      138 * mm, 37 * mm, col_w, style(6.7, 9.2, MUTED))
    p(c, "<b>자동 검증 40건</b>  상태 전이 9 · 승인 5 · 훅 모션 12 · 견인 10 · 조준 4.<br/><b>100회+ 계측</b>  워프/역행 0 · 승인 거부 0 · 불일치율 0%.",
      205 * mm, 37 * mm, 59 * mm, style(6.7, 9.2, MUTED))


def page_6(c):
    page_chrome(c, 6, "PERFORMANCE")
    page_title(c, "05 / PERFORMANCE", "폴리곤은 네 번 쟀지만<br/>밀리초는 한 번도 재지 않았습니다",
               "측정 체계를 만든 뒤, 문서상 1순위를 기각하고 최하위 후보를 채택했습니다.")
    # Narrative cards
    cards = [
        ("01", "문제", "47일째까지 프레임 시간 측정 0회. 대리 지표만으로 최적화 우선순위 7개가 정해져 있었습니다.", PALE, INK),
        ("02", "측정", "빌드가 스스로 인게임에 진입해 300프레임 워밍업 뒤 60초씩 3회 주행하도록 자동화했습니다.", TEAL_PALE, TEAL_DARK),
        ("03", "판정", "같은 머신·같은 세션의 델타만 비교하고, 반복 편차가 임계를 넘으면 비교 불가로 처리했습니다.", ORANGE_PALE, colors.HexColor("#A55720")),
    ]
    y = 90 * mm
    for num, title, body, fill, col in cards:
        round_box(c, 16 * mm, y - 27 * mm, 75 * mm, 24 * mm, fill, fill)
        p(c, num, 22 * mm, y - 8 * mm, 10 * mm, style(10, 13, col, True))
        p(c, title, 34 * mm, y - 7 * mm, 45 * mm, style(10.5, 14, INK, True))
        p(c, body, 22 * mm, y - 15 * mm, 62 * mm, style(7.6, 11, MUTED))
        y -= 29 * mm

    # measured baseline
    round_box(c, 99 * mm, 66 * mm, 73 * mm, 49 * mm, NAVY, NAVY)
    p(c, "FIRST RELIABLE BASELINE", 106 * mm, 107 * mm, 58 * mm, style(7.2, 9, colors.HexColor("#5EE0D7"), True))
    p(c, "1.24 ms", 106 * mm, 98 * mm, 58 * mm, style(26, 30, WHITE, True))
    p(c, "GPU 1.02  >  CPU Main 0.86  >  CPU Render 0.40 ms", 106 * mm, 78 * mm, 59 * mm, "white_muted")
    p(c, "RTX 4060 Ti · Ryzen 5 7500F · 1920×1080 · Windows 11", 106 * mm, 70 * mm, 59 * mm, style(6.8, 9, colors.HexColor("#8FA0B4")))

    # ranking chart
    p(c, "문서의 우선순위와 실측 결과", 181 * mm, 114 * mm, 90 * mm, "h2")
    chart_x, chart_y = 185 * mm, 77 * mm
    bar_max = 67 * mm
    chart = [
        ("1순위  GPU Resident Drawer", 16.7, RED, "+16.7% CPU 회귀 / 기각"),
        ("3순위  Point Light Shadow", 0.0, MUTED, "이미 충족"),
        ("7순위  Shadow Cascade 4→2", -6.3, GREEN, "-6.3% GPU / 채택"),
    ]
    for i, (label, val, col, verdict) in enumerate(chart):
        yy = chart_y - i * 20 * mm
        p(c, label, chart_x, yy + 11 * mm, 78 * mm, style(8, 11, INK, True))
        c.setFillColor(colors.HexColor("#E8EDF1"))
        c.roundRect(chart_x, yy + 3 * mm, bar_max, 4.5 * mm, 2.25 * mm, fill=1, stroke=0)
        length = min(bar_max, max(4 * mm, abs(val) / 17 * bar_max))
        c.setFillColor(col)
        c.roundRect(chart_x, yy + 3 * mm, length, 4.5 * mm, 2.25 * mm, fill=1, stroke=0)
        p(c, verdict, chart_x, yy, 78 * mm, style(7.1, 9, col, True, TA_RIGHT))

    # tool lies strip
    round_box(c, 99 * mm, 22 * mm, 172 * mm, 35 * mm, PALE, LINE)
    p(c, "측정 도구가 거짓말한 다섯 번", 106 * mm, 50 * mm, 76 * mm, "h2")
    lies = [
        "없는 카운터가 0을 반환",
        "빈 화면을 GPU 70.8% 개선으로 오인",
        "해상도 인자가 무시됨",
        "노이즈 위에서 회귀 2건 판정",
        "설정 변경 뒤 재빌드 누락",
    ]
    yy = 41 * mm
    for i, item in enumerate(lies):
        x = 106 * mm + (i % 3) * 53 * mm
        y0 = yy - (i // 3) * 10 * mm
        p(c, f"<b>{i+1}</b>  {escape(item)}", x, y0, 49 * mm, style(7.3, 10.5, MUTED))
    p(c, "결론: 이상하게 좋은 수치는 성과가 아니라 검증 신호였습니다. 같은 코드의 대조군을 다시 세워 -25.6%를 -6.3%로 정정했습니다.  ·  " +
      link("성능·검증 사례집", "https://hanwoolhanwool.github.io/Train-Survival/performance.html"),
      106 * mm, 27 * mm, 158 * mm, style(8, 12, TEAL_DARK, True))


def page_7(c):
    page_chrome(c, 7, "TOOLING")
    page_title(c, "06 / TOOLING", "사람이 기억하던 판단을<br/>게이트와 도구로 옮겼습니다",
               "이미 겪은 사고를 다시 겪지 않도록, 더 이른 시점에서 자동으로 막았습니다.")
    # metrics
    metric(c, 16 * mm, 107 * mm, 48 * mm, "14", "automation and tools")
    metric(c, 70 * mm, 107 * mm, 48 * mm, "9:54", "CI wall time / from 17:38")
    metric(c, 124 * mm, 107 * mm, 48 * mm, "-41.0%", "visible triangles")
    metric(c, 178 * mm, 107 * mm, 48 * mm, "5%", "performance gate")
    metric(c, 232 * mm, 107 * mm, 40 * mm, "987", "ViewLab lines")

    # three cards
    card_y, card_h, card_w = 23 * mm, 66 * mm, 80 * mm
    xs = [16 * mm, 104 * mm, 192 * mm]
    data = [
        ("01 / CI", "꺼진 게이트를 먼저 살렸습니다", "55회 실행 55회 실패 상태를 복구한 뒤, PlayMode 세그폴트를 이등분 4회로 [MenuItem] 등록 수 임계 7까지 좁혔습니다.", "17:38 → 9:54", NAVY),
        ("02 / ASSET PIPELINE", "반복 보정을 임포트 시점으로", "LOD 승격과 피벗 보정을 AssetPostprocessor로 옮겼습니다. 원본 FBX·GUID·머티리얼을 유지하면서 보이는 삼각형을 줄였습니다.", "1,183k → 699k", TEAL_DARK),
        ("03 / VIEWLAB", "플레이 중 자세를 직접 저장", "씬 뷰 핸들로 무기 피벗을 조정하고 프리팹에 바로 저장합니다. 코드 수정-실행-종료의 반복을 드래그 1회로 줄였습니다.", "3단계 → 1회", colors.HexColor("#A55720")),
    ]
    for i, (tag, title, body, result, col) in enumerate(data):
        round_box(c, xs[i], card_y, card_w, card_h, WHITE, LINE)
        c.setFillColor(col)
        c.roundRect(xs[i], card_y + card_h - 12 * mm, card_w, 12 * mm, 4 * mm, fill=1, stroke=0)
        c.rect(xs[i], card_y + card_h - 12 * mm, card_w, 4 * mm, fill=1, stroke=0)
        p(c, tag, xs[i] + 6 * mm, card_y + card_h - 4 * mm, card_w - 12 * mm, style(7.5, 9, WHITE, True))
        p(c, title, xs[i] + 6 * mm, card_y + card_h - 19 * mm, card_w - 12 * mm, "h3")
        p(c, body, xs[i] + 6 * mm, card_y + card_h - 32 * mm, card_w - 12 * mm, "body_muted")
        p(c, result, xs[i] + 6 * mm, card_y + 14 * mm, card_w - 12 * mm, style(15, 18, col, True))
    # ViewLab thumbnail inside third card
    draw_crop(c, ASSETS / "ts-viewlab.png", 238 * mm, 28 * mm, 28 * mm, 15 * mm, "center")
    p(c, "원칙: 새 자동화는 '있으면 좋아서' 만들지 않았습니다. 실제 사고가 있었고, 반복될 비용이 수동 판정보다 클 때만 만들었습니다.  ·  " +
      link("자동화 설비 상세", "https://hanwoolhanwool.github.io/Train-Survival/automation.html"),
      16 * mm, 18 * mm, 256 * mm, style(7.6, 10.5, TEAL_DARK, True, TA_CENTER))


def page_8(c):
    page_chrome(c, 8, "AI & GROWTH")
    page_title(c, "07 / AI-NATIVE WORK", "AI의 답을 받는 것보다<br/>검증 가능한 질문을 만드는 데 집중했습니다",
               "컨텍스트를 고정하고, 결과를 코드·테스트·실측으로 확인하며, 판단 근거를 저장소에 남깁니다.")

    # Loop
    loop_y = 104 * mm
    steps = [
        ("01", "CONTEXT", "CLAUDE.md와 설계 규약"),
        ("02", "PROPOSE", "문제 분해·가설·diff"),
        ("03", "VERIFY", "테스트·플레이·대조군"),
        ("04", "RECORD", "결정과 기각 근거 문서화"),
    ]
    for i, (num, title, body) in enumerate(steps):
        x = 16 * mm + i * 65 * mm
        round_box(c, x, 78 * mm, 56 * mm, 27 * mm, TEAL_PALE if i != 2 else ORANGE_PALE,
                  TEAL_PALE if i != 2 else ORANGE_PALE)
        p(c, num, x + 5 * mm, loop_y - 5 * mm, 9 * mm, style(8, 10, TEAL_DARK if i != 2 else ORANGE, True))
        p(c, title, x + 15 * mm, loop_y - 4 * mm, 34 * mm, style(9, 11, INK, True))
        p(c, body, x + 5 * mm, loop_y - 14 * mm, 46 * mm, style(7.5, 10.5, MUTED, False, TA_CENTER))
        if i < 3:
            draw_arrow(c, x + 57 * mm, 91 * mm, x + 63 * mm, 91 * mm, MUTED, 0.8)

    # Evidence columns
    round_box(c, 16 * mm, 27 * mm, 122 * mm, 43 * mm, NAVY, NAVY)
    p(c, "코드 작업에서의 역할", 24 * mm, 62 * mm, 106 * mm, style(12, 16, WHITE, True))
    yy = 51 * mm
    for title, body in [
        ("제약을 먼저 제공", "Unity C# 9, 의존 방향, 풀링·이벤트 규칙을 작업 컨텍스트로 고정"),
        ("완료 조건을 수치화", "신규 RPC 0건, TODO 0건, 회귀 임계 5%처럼 검사 가능한 문장으로 정의"),
        ("결과를 직접 판정", "컴파일·테스트 통과 뒤에도 플레이 계측과 대조군으로 채택 여부 결정"),
    ]:
        yy -= dot_item(c, title, body, 24 * mm, yy, 106 * mm, TEAL, True)

    round_box(c, 146 * mm, 27 * mm, 126 * mm, 43 * mm, PALE, LINE)

    p(c, "생성형 에셋도 같은 방식으로", 154 * mm, 62 * mm, 108 * mm, "h2")
    p(c, "<b>ChatGPT 이미지 → Tripo·Meshy → Blender → Unity</b><br/>실루엣을 3D 생성 전에 판정하고, 반입 뒤에는 치수·피벗·tris·LOD를 다시 검사했습니다. 자동 LOD가 형상에 따라 26-43% 실패한다는 사실을 검수 씬에서 확인해, 도구의 출력보다 육안·예산 판정을 우선했습니다.",
      154 * mm, 51 * mm, 108 * mm, "body_muted")
    p(c, "AI가 속도를 만들고, 검증 기준이 품질을 결정합니다.", 154 * mm, 33 * mm, 108 * mm,
      style(9.5, 13, TEAL_DARK, True))

    c.setFillColor(TEAL)
    c.rect(0, 0, PAGE_W, 17 * mm, fill=1, stroke=0)
    p(c, "코드를 읽고, 재고, 고치며, 다음 사람이 확인할 수 있는 기준으로 남기겠습니다.",
      16 * mm, 12 * mm, 190 * mm, style(10.5, 14, WHITE, True))
    p(c, link("Portfolio", "https://hanwoolhanwool.github.io/Train-Survival/", "#FFFFFF") + "  ·  " +
      link("GitHub", "https://github.com/hanwoolhanwool/Train-Survival", "#FFFFFF"),
      208 * mm, 12 * mm, 64 * mm, style(8.5, 12, WHITE, True, TA_RIGHT))


def build():
    OUTPUT.parent.mkdir(parents=True, exist_ok=True)
    c = canvas.Canvas(str(OUTPUT), pagesize=(PAGE_W, PAGE_H), pageCompression=1)
    c.setTitle("이한울 - 2026 넥토리얼 게임 프로그래머 포트폴리오")
    c.setAuthor("이한울")
    c.setSubject("Train Survival 게임 클라이언트 개발 포트폴리오")
    for draw_page in [page_1, page_2, page_3, page_4, page_5, page_6, page_7, page_8]:
        draw_page(c)
        c.showPage()
    c.save()
    print(OUTPUT)


if __name__ == "__main__":
    build()
