# 이미지 생성 브리프 — Train Survival

작성일: 2026-08-20 · 버전: v1.0 · 용도: **외부 이미지 생성 AI(ChatGPT 등) 입력용 발췌본**

> 출처: [`세계관 컨셉`](Train-Survival-세계관-컨셉.md) · [`비주얼·UI/UX 가이드`](Train-Survival-비주얼-UIUX-가이드.md) · [`기획서`](Train-Survival-기획서.md)
> 이 문서는 **원본이 아니라 발췌본**이다. 원본과 충돌하면 원본이 이긴다.
>
> **사용법**: ① §A~§F(코어 브리프)를 대화 첫 메시지로 통째 붙여넣는다 → ② 이후 §G의 샷 모듈 하나를 골라 짧게 요청한다.
> 매번 전체를 다시 붙일 필요 없다. 참조 이미지는 `docs/Game design/InGame/*`, `docs/Game design/UI/*`를 첨부.

---

## A. 한 줄 컨셉

> **다정했던 세계가 별에 홀렸다. 우리는 멈출 수 없는 기차를 타고, 그 다정함을 지나쳐 간다.**

- 장르: 1~4인 협동 생존 크래프팅 / **1인칭** / PC·Steam
- 무대: 남 → 북으로 한 방향으로만 달리는 기차. 봄(숲) → 여름(사막) → 가을(대초원) → 겨울(북극) → 우주
- 세계는 **망한 것이 아니라 조용해진 것**이다. 폐허의 참혹함이 아니라 자연이 부드럽게 되덮은 상태
- **멈추면 죽는다** — 별씨는 정지한 것에만 뿌리내린다. 기차의 불이 꺼지면 기차 자체가 골렘이 된다

## B. 아트 톤 (한 문장 요약)

**스타일라이즈드 세미 카툰 / 로우폴리 + 디젤펑크 열차 디자인 언어.**
사실적 PBR·포토리얼 금지. 큰 형태, 강한 실루엣, 손으로 칠한 표면.

**핵심 공식**
> 단순한 형태 + 강한 실루엣 + 제한된 지역 팔레트(5색 이내) + 손으로 만든 표면 + 귀여운 동물 + 무쇠 기차 + 절제된 HUD + 발광하는 이상(異常)

## C. 절대 지켜야 할 세 겹의 대비

| 대비 | 앞 | 뒤 | 효과 |
|---|---|---|---|
| **귀여움 ↔ 적의** | 둥근 실루엣, 폭신한 털 | 붉게 타는 눈, 낮은 자세 | 죄책감 섞인 전투감 |
| **근경 ↔ 원경** | 진흙·바퀴 자국·리벳의 생활감 | 도달할 수 없는 거대한 절경 | 여행감 |
| **기차 ↔ 세계** | 쇠·기름·소음의 인공물 | 부드럽고 유기적인 자연 | 기차가 "우리 편"으로 읽힘 |

한 장의 이미지 안에 **최소 2개**가 보이게 구성한다.

---

## D. 컬러 — 그대로 쓸 것

### D.1 발광 4색 *(이 게임 고유색 · 가장 중요)*

| 이름 | HEX | 대상 | 규칙 |
|---|---|---|---|
| **Starstruck Red** | `#FF4A3D` | 별들린 동물의 **눈** | HDR emissive. 20 m 밖에서 "붉은 점 2개"로 읽히는 게 1차 임무 |
| **Starseed Cyan** | `#67E0D2` | 몸에 박힌 **별씨 결정** | 약점 표시. 눈과 색상 대립 |
| **Starseed Violet** | `#8E7BE8` | 순수 별씨, **오로라**, 최종장 근원 | 숙주 없는 별씨 = 차가운 빛 |
| **Firebox Ember** | `#F2762E` | 화실·등불·계기판 | **열차만 쓰는 따뜻한 발광** |

> **색 서사 규칙**: 순수한 별씨는 차가운 빛(보라·청록), **살아 있는 것에 뿌리내리면 붉어진다.**
> 화면에서 **따뜻한 광원은 열차뿐**이어야 "여기가 안전하다"가 성립한다.

### D.2 지역 팔레트 (각 5색 이내)

| 지역 | 대표색 | 하늘·정서 |
|---|---|---|
| **숲 (봄)** | `#5E8C46` 수관 · `#33523A` 그늘 · `#6E5136` 나무껍질 · `#F3E2C7` 꽃가루 · `#A9CFE0` 연한 하늘 | 연한 하늘, 꽃가루가 날린다 / **설렘** |
| **사막 (여름)** | `#DCA85C` 모래 · `#A9613A` 녹슨 사구 · `#E8DCC0` 백열 하늘 · `#2B3A63` 밤 남색 · `#6FA69B` 유리 | 백열하는 낮, 별이 쏟아지는 밤 / **경외** |
| **대초원 (가을)** | `#D9A441` 황금 곡물 · `#B87A2C` 호박 · `#EBD9A6` 마른 풀 · `#C9705B` 석양 · `#5A5F6B` 폭풍 슬레이트 | 길고 낮은 석양, 채도 최대 / **그리움** |
| **북극 (겨울)** | `#E6EEF2` 빙하 · `#9EC2D6` 얼음 · `#3E5A72` 깊은 서리 · `#141C2B` 극야 · `#5FD6C0` 오로라 | 낮이 거의 없다 / **불안** |
| **최종장 (우주)** | `#0B0D14` 공허 · `#F5F7FF` 별빛 · `#8E7BE8` 별씨 | 자연색 소거 / **이해** |

**열차 (지역 무관 고정)** — `#3A3A3C` 무쇠 · `#232526` 기름 · `#C89B4A` 황동 · `#F2762E` 화실 불빛
> 지역이 네 번 바뀌는 동안 **변하지 않는 유일한 것**이 열차여야 한다.

### D.3 UI 토큰

`#1F1B1A` 패널 그을음 · `#4A423C` 경계선 · `#F2EAE0` 본문 텍스트 · `#9A9089` 보조 · `#C89B4A` 포커스(황동) · `#6B6660` 비활성
**상태 4단계 (순서 고정)** — 안전 `#7FA653` → 주의 `#E3B23C` → 경고 `#DD7A2E` → 위험 `#B23A2E` (어두운 배경 텍스트는 밝은 변형 `#9FC46E` / `#F0705F`)

---

## E. 형태 언어

| 영역 | 권장 | 피할 것 |
|---|---|---|
| 지형 | 큰 덩어리, 둥근 암석, 굽은 나무, 단순 랜드마크 | 미세 지오메트리, 사실적 파손 디테일 |
| 선로 | 지형에 순응하는 절개면·교량·곡선, 눌린 자갈, 기름 얼룩 | 평지에 그냥 놓인 직선 선로 |
| 열차 | 리벳·파이프·판금의 **큰 반복 단위**, 과장된 연결부·손잡이, **낮고 긴 실루엣** | 실제 기관차 같은 부품 밀도 |
| 캐릭터 | 큰 머리(신장의 30~40%), 짧은 다리, 굵은 팔다리, 큰 부츠·장갑 | 현실 비율, 작은 실루엣 차이 |
| 몬스터(동물) | 실존 동물의 **둥근 해석**, 폭신한 덩어리 | 뿔·가시·촉수 덧붙이기, 기형화 |
| 골렘 | 주변 지형과 같은 재질의 덩어리, **얼굴 없이 눈만** | 인간형 근육 구조, 정교한 관절 |
| 소품·무기 | 한눈에 읽히는 기능 형태, 과장된 손잡이 | 실제 제품 같은 복잡한 파츠 |
| 표면 | 큰 얼룩, 브러시 스트로크, 페인트 벗겨짐, 높은 roughness | 고주파 사진 텍스처, 강한 금속 반사 |

**실루엣 테스트**: 텍스처를 지우고 검은 실루엣만 남겨도 종류와 역할이 구분되어야 한다.

**열차 크루 어휘** — 기름때 작업복, 두꺼운 장갑, 고글, 공구 벨트 / 계기판, 밸브, 황동 명판, 운행표 / 화실, 파이프, 석탄고, 연결부 / 방한 코트, 수통, 배낭, 보존식 통 / 지도, 신호기, 이정표, 폐역 표지

## F. 라이팅 · 카메라 · 금지 사항

- 방향광 1개(해=달 겸함). **Ambient를 충분히 유지** — 그림자에서 로컬 컬러가 사라지면 안 된다
- 낮 `#FFF4D6` / 황혼 `#FFA65C` / 밤 달빛 `#8C9ED9` (차가운 청색 = 별의 시간)
- 밤을 정직하게 어둡게 하지 않는다. 갑판 위 전투가 보여야 한다
- 전경 진하게 / 원경은 Fog + 낮은 채도로 깊이 분리
- 포스트: 컬러 그레이딩 · Fog · **약한** Bloom · 통제된 노출까지
- 카메라: 1인칭 게임이므로 시점 스크린샷은 **눈높이·수평·광각 살짝**, 컨셉아트는 자유

**금지(negative)**
> photorealistic, PBR photo textures, gore/blood, neon signs, holograms, glassmorphism, sci-fi scan lines, chromatic aberration, film grain, heavy motion blur, lens flare gimmicks, horns/spikes/tentacles on animals, dark desaturated "corrupted" monsters, text/logo/watermark

---

## G. 샷 모듈 — 필요한 것만 골라 쓴다

각 항목은 위 브리프를 이미 붙여넣은 상태에서 **추가로 던지는 한 문단**이다.

### G1. 키비주얼 / 표지 컷
> 달리는 열차를 측후방에서 본 와이드 샷. 갑판 위에 크루 3~4명, 한 명은 집게(하푼)를 쏘고 있다. 근경엔 눌린 자갈과 기름 얼룩, 중경엔 뒤로 흘러가는 폐선로와 홀리지 않은 사슴 무리, 원경엔 **구름을 뚫고 선 거목과 폭포**. 열차 화실만 따뜻한 주황으로 빛나고, 어둑한 숲 그늘 안에 **붉은 점 2개**가 몇 쌍 떠 있다.

### G2. 지역 환경 (지역명·시간대만 바꿔 재사용)
> 지역: **{숲/사막/대초원/북극}**, 시간대: **{낮/황혼/밤}**. §D.2의 해당 5색만 사용. 원경 랜드마크는 **{거목과 폭포 / 모래에 반쯤 묻힌 거대 고대 구조물과 신기루 / 지평선까지 이어진 황금 물결과 거대 풍차 군락 / 빙벽과 오로라}**. 하늘이 화면의 절반을 차지한다. 선로는 지형에 순응해 휘어지고, 절개면·낮은 교량이 보인다. 인간의 흔적(폐침목, 녹슨 신호기, 이정표)은 **1~2개만**.

### G3. 열차 (외관 / 칸 구성)
> 디젤펑크 화물열차. 무쇠 `#3A3A3C` · 기름 `#232526` · 황동 `#C89B4A`, 화실 불빛 `#F2762E`. 리벳과 파이프는 큰 단위로 과장, 연결부와 손잡이는 크게. **낮고 긴 실루엣**, 위로 솟는 건 굴뚝·물탱크뿐. 낡았지만 관리되고 있다 — 벗겨진 페인트, 덧댄 철판. 갑판 위에 플레이어가 지은 조잡한 건축물(방어벽, 온실칸, 물탱크)이 얹혀 있어 **더 조잡하고 더 사랑스럽다**.

### G4. 캐릭터 (TP 전신 / 코스메틱 시트)
> 큰 머리(신장의 30~40%), 짧은 다리, 큰 부츠와 두꺼운 장갑. 열차 크루 복장 — 기름때 작업복, 고글, 공구 벨트, 방한 코트, 배낭. **기본 체형은 공통**이고 Hat / Hair / Goggles / Coat / Pack / Patch 슬롯만 다르다. 캐릭터 구분은 **코트 색 + 머리 실루엣처럼 축 2개 이상**으로 — 작은 배지로 구분하지 않는다(식별 거리 15~35 m). 얼굴은 눈 크기·눈썹 각도·주근깨 같은 소수 요소로 크게 읽히게.

### G5. 별들린 동물 (몬스터)
> **원래 모습을 잃지 않는다.** 실루엣·비율·털결을 변형하지 않고 몸은 지역 자연색 그대로 — 어둡게 오염시키지 않는다. 변하는 것은 딱 3가지: ① **눈** — 홍채가 `#FF4A3D`로 발광, 동공은 점처럼 축소, 눈꺼풀 각도만으로 분노 ② **별씨 결정** — 이마·등·목덜미 중 한 곳에 작은 `#67E0D2` 발광 결정 ③ **자세** — 털이 곤두서고 무게중심이 낮아진다. 걸음은 여전히 동물답게 귀엽다. 대상: 토끼, 여우, 다람쥐, 멧돼지, 늑대, 순록.
>
> *거리 컷 변주*: 20 m — 어둠 속 붉은 점 2개만 / 15 m — 실루엣+발광, "귀여운 동물이다"가 읽힘 / 2 m — 눈 모양과 결정 디테일 / **처치 순간** — 결정이 부서지고 빛이 꺼지며 잠들듯 옆으로 쓰러진다(유혈·파괴 없음)

### G6. 골렘 · 보스
> **골렘**: 얼굴 없이 눈만 있다. 주변 지형과 **같은 재질**로 만들어져 일어서기 전까지 지형의 일부로 보인다. 핵심에 큰 `#67E0D2` 별씨 결정 — 약점이자 집게로 뜯어낼 대상.
> **보스(지역별)**: 숲 = 겨울잠에서 별들린 채 깨어난 **곰**(몸집만 클 뿐 여전히 곰) / 사막 = 모래와 유리가 뭉친 골렘(무너지고 다시 뭉친다) / 대초원 = 스탬피드를 이끄는 거대 **들소·큰뿔사슴** / 북극 = 빙하가 일어선 골렘, **몸 안에 별들린 동물들이 얼어붙어 있다**.

### G7. 소품 · 무기 (아이콘 시트에도 사용)
> 집게(하푼) 3단계, 리볼버, 샷건, 볼트액션 라이플, 화염방사기, 마체테·도끼, 작살, 거치 기관총, 수리 망치. 전부 **디젤펑크 어휘**(리벳·파이프·황동·그을음)로 통일. 한눈에 읽히는 기능 형태 + 과장된 손잡이. 기능이 다르면 색이 아니라 **주기능부의 형태**를 바꾼다.

### G8. UI 목업 (HUD / 로비 / 설정)
> §D.3 토큰만 사용. **World > Character > Interaction > HUD** 순서 엄수.
> **HUD**: 정상 상태에서 화면에 보이는 요소 **3개 이하**. 좌하단에 체력+시간대만 낮게, 체력은 안전 구간에서 투명도 40%. 화면 중앙 하단은 손·무기 자리이므로 비운다. 퀵슬롯 5칸은 아이템을 크게, 슬롯 배경은 최소. 배경이 흐르므로 패널은 **불투명 85% 이상**이거나 아예 외곽선만 — 중간값이 가장 나쁘다.
> **로비/메인 메뉴**: 소프트웨어 화면이 아니라 **세계 안의 장면**. 정차한 기관차 앞, 화실 불빛, 흐르는 연기. 버튼 = 황동 명판 / 운행표 행 / 밸브 레버. 패치노트 = 벽에 붙은 공고문 종이.
> **설정 화면**: 세계관보다 스캔 속도 우선. Label 45~55% / Control 30~35% 고정 폭. 배경은 월드 위 어두운 오버레이 65~80%. 토글은 색만으로 상태를 구분하지 않는다.

---

## H. 영문 프롬프트 코어 (복붙용)

이미지 모델에는 영문이 더 안정적이다. 아래를 앞에 붙이고 §G의 한 모듈을 영어로 이어 쓴다.

```
STYLE: stylized semi-cartoon low-poly game art, diesel-punk train design language,
hand-painted large color blocks, strong readable silhouettes, high roughness,
low metallic, soft normals, large brush-stroke stains and chipped paint.
NOT photorealistic, no PBR photo textures.

WORLD: a cozy world that did not collapse — it simply went quiet. Nature has softly
reclaimed abandoned rails, stations and half-buried structures. One diesel-punk train
runs one-way, south to north, through spring forest → summer desert → autumn golden
steppe → winter arctic. The train can never stop.

ANOMALY (Starseed / Starstruck): glowing crystal shards fell from the sky. Animals
possessed by them KEEP their original cute round shape — only three things change:
(1) iris glows #FF4A3D with a pinpoint pupil, (2) a small #67E0D2 glowing crystal
embedded in forehead / back / nape, (3) fur bristled, lowered stance.
Never add horns, spikes or tentacles. Never darken or desaturate their bodies. No gore.

LIGHT: single directional light, generous ambient so local colors survive in shadow.
The TRAIN is the ONLY warm light source in frame (#F2762E firebox glow). All other
emissive light in the world is cold (#67E0D2 cyan, #8E7BE8 violet aurora).

PALETTE (max 5 colors per region):
  forest  #5E8C46 #33523A #6E5136 #F3E2C7 #A9CFE0
  desert  #DCA85C #A9613A #E8DCC0 #2B3A63 #6FA69B
  steppe  #D9A441 #B87A2C #EBD9A6 #C9705B #5A5F6B
  arctic  #E6EEF2 #9EC2D6 #3E5A72 #141C2B #5FD6C0
  train (fixed, region-independent)  #3A3A3C #232526 #C89B4A #F2762E

COMPOSITION: keep at least two of these contrasts visible —
  cute vs hostile / gritty foreground vs unreachable majestic distance / iron train vs organic nature.
Sky occupies about half the frame. Rails bend and conform to terrain, never straight on flat ground.

NEGATIVE: photorealism, gore, blood, neon, holograms, glassmorphism, sci-fi scan lines,
chromatic aberration, film grain, heavy motion blur, text, logo, watermark.
```

---

## I. 검수 체크리스트 — 생성된 이미지 판정

- ☐ 열차가 화면에서 **유일하게 따뜻한 광원**인가
- ☐ 별들린 동물이 **여전히 귀여운 동물**로 읽히는가 (오염·기형화 없음)
- ☐ 눈(`#FF4A3D`)과 별씨 결정(`#67E0D2`)이 **다른 색**으로 분리되는가
- ☐ 지역 대표색이 5개 이내로 유지되는가
- ☐ 실루엣만 남겨도 종류와 역할이 구분되는가
- ☐ 선로가 지형에 순응하는가 (평지 위 직선 배치 아님)
- ☐ 세 대비 중 최소 2개가 한 화면에 있는가
- ☐ 하늘이 지역 정체성을 근경만큼 강하게 말하는가
