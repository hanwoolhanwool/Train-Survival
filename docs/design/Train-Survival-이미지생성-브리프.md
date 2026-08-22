# 이미지 생성 브리프 — Train Survival

작성일: 2026-08-20 · 버전: **v1.1** · 용도: **외부 이미지 생성 AI(ChatGPT 등) 입력용 발췌본**

> **v1.1 변경 요약 (2026-08-22)** — **3D 생성 경로 신설.** 기존 §G1~G8은 전부 *보여주기 위한 장면*이고,
> image-to-3D(Tripo·Meshy)에 먹일 *입력*은 규칙이 정반대라 별도 모듈이 필요했다.
> - **§G9** 3D 생성용 단일 오브젝트 시트 — **§F(라이팅·카메라)를 대체**한다. §F를 그대로 쓰면
>   생성기가 그림자를 BaseColor 텍스처에 구워 넣어 Unity에서 조명이 이중으로 겹친다
> - **§J** 자원 노드 12종 생성 지시 시트 (`ResourceCatalog.asset` 실측 색·요구 집게 등급)
> - **§K** 배경 에셋 생성 지시 시트 — 지형·식생·바위·흔적·중원경 + **타일당 예산표**

> 출처: [`세계관 컨셉`](Train-Survival-세계관-컨셉.md) · [`비주얼·UI/UX 가이드`](Train-Survival-비주얼-UIUX-가이드.md) · [`기획서`](Train-Survival-기획서.md)
> 이 문서는 **원본이 아니라 발췌본**이다. 원본과 충돌하면 원본이 이긴다.
>
> **사용법 — 용도에 따라 입구가 갈린다.**
>
> | 목적 | 첫 메시지 | 이후 |
> |---|---|---|
> | **장면·컨셉아트** (키비주얼·환경·UI 목업) | §A~**§F** 전부 | §G1~G8 중 하나 |
> | **3D 생성 입력** (Tripo·Meshy에 먹일 것) | §A~**§E** + **§G9** (§F는 **쓰지 않는다**) | §J(자원) · §K(배경)의 한 줄 |
>
> 매번 전체를 다시 붙일 필요 없다. 참조 이미지는 `docs/Game design/InGame/*`, `docs/Game design/UI/*`를 첨부.
> 3D 입력용은 참조 이미지에 **기존 반입 에셋의 텍스처**(`Assets/_Project/Art/Textures/T_Structure_*.png`)를
> 함께 붙이면 톤이 맞는다 — 이미 게임에 들어가 있는 실물이기 때문이다.

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

### G9. 3D 생성용 단일 오브젝트 시트 *(image-to-3D 입력 전용 — 다른 모듈과 규칙이 다르다)*

**용도가 다르다.** G1~G8은 보여주기 위한 **장면**이고, 이 모듈은 Tripo·Meshy 같은 image-to-3D에
먹일 **입력**이다. 따라서 **§F(라이팅·카메라)를 적용하지 않는다** — 그 절의 강한 명암과 방향광은
생성기가 그림자를 **BaseColor 텍스처에 그대로 구워** 넣게 만들고, 그러면 Unity에서 조명을 받을 때
그림자가 이중으로 겹친다.

**그대로 유지하는 것**: §B 아트 톤 · §C 세 대비(형태 판단 기준으로만) · §D 컬러 · §E 형태 언어
**대체하는 것**: §F 라이팅·카메라 → 아래 규격

```
IMPORTANT — these images are INPUT FOR image-to-3D conversion (Tripo / Meshy),
not concept art. Ignore any scene/lighting/composition rules from the brief above.

FRAMING (critical)
- ONE isolated object only, centered, filling ~80% of a SQUARE canvas
- Plain flat neutral gray background #808080 — nothing else in frame
- 3/4 front view, camera slightly above the object
- Minimal perspective distortion (long-lens look)
- Entire object visible, nothing cropped, object sitting flat (no floating, no tilt)

LIGHTING (critical — do NOT bake light into the texture)
- Even neutral white studio light, soft ambient fill
- NO cast shadow, NO ground shadow, NO contact shadow, NO rim light
- NO dramatic lighting, NO color grading, NO bloom
- Flat hand-painted base color only

STYLE
Stylized semi-cartoon LOW-POLY game asset. Large simple forms, chunky rounded
shapes, strong readable silhouette, high roughness, no metallic highlights,
no PBR photo texture.

NEGATIVE
shadows, background scenery, ground plane, multiple objects, photorealism,
PBR photo textures, glossy metal, neon, glow, text, watermark, logo,
motion blur, depth of field, cropped edges, floating object
```

**생성 후 검수 (§I와 별개 — 3D 입력용)**

- ☐ 배경이 **완전한 무지 회색**인가 (풀·바닥·소품이 끼어들지 않았는가)
- ☐ **그림자가 없는가** — 접지 그림자 하나만 있어도 텍스처에 굽힌다
- ☐ 오브젝트가 **잘리지 않았는가**
- ☐ 물리적으로 성립하는 형태인가 (관통·비대칭 오류는 3D에서 그대로 망가진다)
- ☐ 지역 팔레트(§D.2) 안에 있는가
- ☐ **실루엣만 남겨도 종류가 구분되는가** — 3D를 뽑기 전에 여기서 거른다

> **왜 이미지를 거쳐야 하는가**: text-to-3D에 `#5E8C46`을 줘도 생성기는 무시한다. 이미지로 주면
> 그 색이 텍스처에 들어간다. 또 실루엣 판정을 **3D 생성 전에** 끝낼 수 있어 버리는 비용이 훨씬 싸다.

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

---

## J. 자원 노드 12종 — 생성 지시 시트 *(§G9와 함께 쓴다)*

색은 `ResourceCatalog.asset` 실측값이다. **한 대화에서 한 종씩** 뽑는다 —
여러 개를 한 이미지에 담으면 image-to-3D가 분리하지 못한다.

| # | 종류 | 표시명 | 색 | 요구 집게 | 영문 지시 (§G9 뒤에 이어 붙인다) |
|---:|---|---|---|:---:|---|
| 1 | `Wood` | 목재 | `#805426` | 1 | a bundle of 3-4 short cut logs tied with rope, bark brown #805426, knee-height |
| 2 | `Stone` | 돌 | `#949499` | 1 | a pile of 4-5 angular gray rocks, #949499, knee-height |
| 3 | `Scrap` | 고철 | `#7A4233` | 1 | a heap of rusted iron plates and bent pipes, rust brown #7A4233 |
| 4 | `Niter` | 화약 원료 | `#E6CC40` | 1 | a chunk of white mineral with yellow crystals embedded, #E6CC40 |
| 5 | `RawFood` | 식재료 | `#73B34D` | 1 | a small pile of red berries and mushrooms with green leaves, #73B34D |
| 6 | `Timber` | 원목 | `#523314` | **2** | ONE large log with bark still on, dark brown #523314, **TWICE the size** of the Wood bundle |
| 7 | `OreVein` | 광맥 | `#596B80` | **3** | a rock chunk with blue-gray ore veins running through it, #596B80 |
| 8 | `Rice` | 벼 | `#F29E1F` | 1 | a tied bundle of golden rice stalks, #F29E1F |
| 9 | `Salt` | 소금 | `#ADE6F7` | 1 | a slab of crusted white salt crystals, pale blue-white #ADE6F7 |
| 10 | `Ice` | 얼음 | `#59B8E0` | 1 | a chunk of blue translucent ice, #59B8E0 |
| 11 | `RareMetal` | 희귀 금속 | `#C7B8F2` | 1 | a silver-white metal ore chunk with faint violet tint, #C7B8F2 |
| 12 | `RelicPart` | 유적 부품 | `#33D9B3` | 1 | an ancient machine part, teal #33D9B3, **with a soft cyan glow** |

### 반드시 지킬 것 2가지

- **⑥ 원목은 ① 목재의 2배 크기.** 요구 집게가 2단계라 *"아직 못 잡는 것"* 이 **형태로** 읽혀야 한다.
  같은 논리로 ⑦ 광맥(3단계)은 가장 크고 무거워 보여야 한다 — 색만 다르면 플레이어가 학습하지 못한다
  ([비주얼 가이드 §3.2](Train-Survival-비주얼-UIUX-가이드.md) 실루엣 테스트).
- **⑫ 유적 부품만 발광한다.** 나머지 11종은 §G9의 NEGATIVE에 `glow`가 들어 있으므로 자동으로 막힌다.
  유적 부품의 청록은 우연이 아니라 **북극 유적의 고대 동력원 = 별씨와 같은 물질**([세계관 §6.1](Train-Survival-세계관-컨셉.md))
  이라는 설정이 데이터에 반영된 것이다 — `Starseed Cyan #67E0D2` 계열을 유지한다.

### 크기 기준

지상 노드의 시각 크기는 `ResourceNode.prefab`의 `Visual` 스케일 **(0.8, 0.6, 0.8)** 근처다
(사람 무릎 ~ 허리 높이). **집게로 조준하는 대상**이므로 20 m 밖에서 실루엣이 읽혀야 하며,
그보다 작게 만들면 채집 대상으로 보이지 않는다.

---

## K. 배경 에셋 — 생성 지시 시트 *(§G9와 함께 쓴다)*

배치 규격·예산의 근거는 [`레벨 디자인 가이드`](Train-Survival-레벨디자인-가이드.md) §4·§8이다.
이 절은 그 규격을 **생성 프롬프트**로 옮긴 것이다.

### K.0 배경 전용 추가 규격 *(자원 12종과 다른 점 — §G9에 이어 붙인다)*

```
- FLAT BOTTOM: the object must have a flat base so it can sit on flat ground
- NO THIN FLAT SHEETS: keep every part volumetric — thin planes break 3D reconstruction
- Solid chunky masses, not scattered small details
```

**생성기 출력 설정 (Tripo·Meshy)**

| 항목 | 값 | 근거 |
|---|---|---|
| 텍스처 해상도 | **2K (2048²)** — 4K를 고르지 않는다 | 프로젝트 임포트 규약이 `Max Size 2048`이라 4K는 어차피 다운샘플된다([M8 에셋 수용 판정 §5.7](../plans/M8/M8-에셋-수용-판정.md)). 게다가 1.5 m 바위에 2K를 쓰면 1,365 px/m로 **게임 표준(256~512)의 3~5배**이며, 스타일라이즈드 로우폴리는 4K가 담는 고주파 디테일을 애초에 금지한다. 74종 × 4K = 1 GB, 2K = 266 MB |
| PBR 맵 | **생성하지 않거나 반입 시 버린다** | Simple Lit — BaseColor만 쓴다 (M8 결정 ⑦) |

> 반입 후 Unity `Max Size`를 512~1024로 낮춰 확인해 볼 것. 배경 프롭은 대개 **512에서도 티가 나지 않는다** —
> 지역별 아틀라스로 통합하면 각 프롭이 차지할 영역이 어차피 그 정도다(레벨 가이드 §4.1).

**모듈 조각**(절벽·경사·교량처럼 옆으로 이어붙이는 것)에는 한 줄을 더 붙인다:

```
- MODULAR: flat vertical BACK face and flat LEFT/RIGHT side faces,
  so identical copies can be tiled side by side seamlessly
```

> **왜 모듈인가**: AI 생성기는 한 방향으로 긴 지형을 잘 만들지 못한다. 대신 **덩어리 하나를 잘 만들고
> 반복 배치**하면 절개면 전체가 성립한다 — 40 m 규격을 지키는 것은 메시가 아니라 **타일 프리팹**이다
> (레벨 가이드 §11 결정 ⑦ 파생).

### K.1 지형 조각 — 숲 기준 · 6종

`Env_` · 폴더 `Art/Models/Environment/Forest/`

| 에셋 | 실치수 | tris | 영문 지시 | 모듈 |
|---|---|---:|---|:---:|
| `Env_Cliff_Face_A` | 6×4×3 m | 2,500 | a modular weathered rock cliff chunk, rounded stone, moss on top, bark brown #6E5136 with shadow green #33523A moss | ● |
| `Env_Cliff_Face_B` | 6×6×3 m | 2,500 | same cliff family but taller and more cracked, with a small ledge | ● |
| `Env_Slope_Chunk_A` | 8×2.5×5 m | 2,000 | a modular grassy embankment slope chunk, gentle rounded incline, grass #5E8C46 on top, exposed soil #6E5136 on the face | ● |
| `Env_BridgePier_A` | 2×4×2 m | 800 | a stone-and-timber bridge pier, chunky riveted braces, weathered wood and gray stone | |
| `Env_BridgeRail_A` | 8×1.2×0.4 m | 500 | a modular wooden bridge railing section, thick chunky posts and beams (volumetric, not thin planks) | ● |
| `Env_RiverBank_A` | 8×1×4 m | 1,500 | a modular riverbank edge chunk, rounded pebbles and wet soil, a few reeds, water-worn | ● |

### K.2 식생 — 숲 · 9종

| 에셋 | 실치수 | tris | 영문 지시 |
|---|---|---:|---|
| `Env_Tree_Conifer_L` | 높이 12 m | 1,200 | a stylized conifer tree, chunky canopy built from 2-3 large rounded cone masses, canopy #5E8C46 with shadow #33523A, trunk #6E5136, slightly curved trunk |
| `Env_Tree_Conifer_M` | 8 m | 900 | same conifer family, medium size, 2 canopy masses |
| `Env_Tree_Conifer_S` | 5 m | 600 | same conifer family, small, single canopy mass |
| `Env_Tree_Broadleaf_L` | 10 m | 1,400 | a stylized broadleaf tree, one big rounded blobby canopy, thick curved trunk, canopy #5E8C46, trunk #6E5136 |
| `Env_Tree_Broadleaf_M` | 7 m | 1,000 | same broadleaf family, medium, leaning slightly |
| `Env_Bush_A` | 1.5 m | 300 | a round chunky bush, one solid blobby mass of leaves, #5E8C46 |
| `Env_Stump_A` | 0.8 m | 250 | a cut tree stump with visible rings on top, roots spreading at the base, #6E5136 |
| `Env_LogFallen_A` | 길이 6 m | 500 | a fallen mossy log lying on its side, bark texture, moss patches, #6E5136 with #33523A moss |
| `Env_GrassClump_A` ⚠ | 0.5 m | 120 | a small clump of tall grass blades, volumetric tuft not flat planes, #5E8C46 |

> ⚠ **`Env_GrassClump_A`는 AI 취약 항목이다.** 풀은 얇은 판이라 3D 재구성이 뭉개지고, 120 tris까지
> 감축하면 실루엣이 남지 않는다. **먼저 생성해 보고 감축 판정에서 떨어지면 Blender에서 직접**
> 만든다(교차 평면 2~3장, 5분 작업).

### K.3 바위·지형 프롭 — 4지역 공용 · 5종  ✅ **반입 완료 (2026-08-23)**

**지역 전용이 아니다** — 머티리얼 색만 갈아 사막·대초원·북극에 재사용한다(가이드 결정 ⑤).
따라서 프롬프트에서 색을 **중립 회색조**로 요청한다.

> **반입 결과** — `Art/Models/Environment/Common/`에 6종(여분 `Env_Rock_Cluster` 포함).
> 생성물 평균색은 `#BFBCBE`(밝기 74 %)로 목표 `#949499`(58 %)보다 밝지만,
> **중립 회색조라는 핵심 조건은 지켜졌다**(R/G/B 편차 2~4) — 공용 재사용의 조건은 색조이지 밝기가
> 아니고, 밝기는 머티리얼 틴트로 내릴 수 있다. 실제 tris는 아래 표보다 낮다(K.7 참조).

| 에셋 | 실치수 | tris | 영문 지시 |
|---|---|---:|---|
| `Env_Rock_L` | 3 m | 800 | a large rounded boulder, weathered smooth stone, neutral gray #949499, chunky asymmetric mass |
| `Env_Rock_M` | 1.5 m | 400 | same rock family, medium |
| `Env_Rock_S` | 0.6 m | 200 | same rock family, small, slightly angular |
| `Env_RockOutcrop` | 5 m | 1,000 | an exposed bedrock outcrop breaking out of the ground, layered horizontal strata, neutral gray |
| `Env_Gravel_Pile` | 2 m | 300 | a low pile of crushed gravel and small stones, flat spreading mound |

### K.4 인간의 흔적 — 4종 *(타일당 1~2개, 없는 타일이 있어도 좋다)*  ✅ **반입 완료 (2026-08-23)**

| 에셋 | 실치수 | tris | 영문 지시 | AI 적합 |
|---|---|---:|---|:---:|
| `Env_Signal_Rusty` | 3.5 m | 600 | a rusted old railway signal post, diesel-punk, chunky lamp housing on top, peeling paint, rust #7A4233 and iron #3A3A3C | ● |
| `Env_Fence_Broken` | 3 m | 400 | a broken wooden fence section, thick chunky posts, some planks missing and tilted | ● |
| `Env_Sleeper_Old` ⚠ | 2.5 m | 200 | a stack of old discarded railway sleepers, weathered dark timber | 직접 |
| `Env_Milepost` ⚠ | 1.2 m | 150 | a small weathered milepost marker, chunky wooden post with a metal plate | 직접 |

> ⚠ 침목·이정표는 **단순 박스·막대**다. AI로 뽑아 감축하는 것보다 Blender에서 직접 만드는 것이
> 빠르고 예산도 정확히 맞는다.
>
> **이 ⚠는 실측으로 옳았음이 확인됐다 (2026-08-23).** 이정표는 직접 만들어 **88 tris**로
> 목표(150)의 절반에 들어왔고, AI로 뽑은 침목은 200에서 판자 경계가 무너져 **600까지 올려야** 했다.
> 신호기·울타리는 AI 생성물을 채택했지만 울타리도 400 → **800**으로 올렸다.

### K.5 중경·원경 — 3종  ⏳ **2/3 반입 (2026-08-23)** — 거목은 계획 3차로 미룸

| 에셋 | 실치수 | tris | 영문 지시 |
|---|---|---:|---|
| `Env_Deer_Idle` | 1.6 m | 800 | a cute stylized deer standing calmly, rounded soft forms, **NORMAL EYES — no glow, no red, no crystal** (this animal is NOT possessed), warm brown fur |
| `Env_BirdFlock` | — | 300 | a small flock of simple stylized birds in flight, chunky simplified wing shapes, dark silhouette |
| `Env_Landmark_GreatTree` | 60 m+ | 4,000 | a colossal ancient tree piercing the clouds with a waterfall falling beside it, seen as a distant silhouette landmark, simplified massive forms |

> **`Env_Deer_Idle`의 "눈이 빛나지 않는다"는 규칙이 아니라 임무다.** 중경에 멀쩡한 사슴이 있어야
> 플레이어가 텍스트 없이 *"빛나는 눈 = 씌인 것"* 을 학습하고, 별들림이 비극으로 읽힌다
> ([세계관 §4.2](Train-Survival-세계관-컨셉.md)). §G5(별들린 동물)와 **반대로** 주문해야 한다.
>
> 반입본은 이 조건을 지켰다 — 눈이 정상이고 털색 `#C9A477`로 따뜻하다. 사슴만 숲 서식이라
> `Environment/Forest/`에 두고, 새 무리는 4지역 공용이라 `Common/`에 뒀다(사막은 도마뱀 — 가이드 §7.3).

### K.6 나머지 지역 전용분 *(숲 파일럿 이후)*

공용(K.3 바위 5종 · K.4 흔적 4종)은 **머티리얼 색만 교체**해 재사용한다. 아래는 전용 신규분이다.

| 지역 | 전용 에셋 | 랜드마크 (§G9 + 원경) |
|---|---|---|
| **사막** | 선인장 2 · 마른 관목 1 · 마른 나무 1 · 모래언덕 조각 2 · **난파 열차 잔해 1**(3,000 tris) · 메사 조각 1 | 모래에 반쯤 묻힌 거대 고대 구조물 + 신기루 |
| **대초원** | 벼밭 클럼프 2 · 억새 1 · 홀로 선 나무 1 · 수로 조각 1 · 곡물 저장고 폐허 1 | 지평선까지 이어진 황금 물결 + 거대 풍차 군락 |
| **북극** | 눈 덮인 침엽수 2 · 얼어붙은 관목 1 · 빙벽 조각 2 · 얼어붙은 강 조각 1 · **유적 노두 1** · 얼어붙은 열차 잔해 1 | 빙벽 + 오로라 (`#5FD6C0` — 별씨의 궤적이라는 복선) |

각 지역의 색은 **§D.2 지역 팔레트 5색**으로 교체한다. 형태 언어와 §G9 규격은 동일하다.

### K.7 타일당 예산 — 이것이 진짜 병목이다

AI 생성물 원본은 30,000~50,000 tris다. **감축 목표를 못 지키면 타일 하나가 예산을 통째로 먹는다.**

| 종류 | 타일당 개수 | 개당 tris | 소계 |
|---|---:|---:|---:|
| 대형 지형 조각 (절벽·경사) | 2~4 | 2,500 | 10,000 |
| 나무 | 6~10 | 1,000 | 10,000 |
| 바위·관목 | 8~12 | 400 | 4,000 |
| 풀·소품 | 8~12 | 150 | 1,800 |
| 인간 흔적 | 1~2 | 400 | 800 |
| 자원 노드 | 3~4 | 600 | 2,400 |
| **합계** | **약 30** | | **29,000** ✓ (목표 30,000) |

활성 타일 9장 × 29 k = **약 261 k tris** — [아트 예산 §6](Train-Survival-아트-렌더링-예산.md)의 배경 270 k와 맞는다.

**감축 판정 순서**: 생성 → Decimate 0.1~0.2 → **실루엣이 남는가**(§3.2 테스트) → 남으면 채택,
뭉개지면 ① 개당 tris를 올리고 타일당 개수를 줄이거나 ② Blender 직접 제작으로 돌린다.

#### K.7.1 감축 실측 (2026-08-23) — **인공물이 자연물보다 훨씬 약하다**

K.3~K.5 12종을 이 순서대로 돌린 결과. 원본 33,484 → **6,184 tris (82 % 감축)**.

| 계열 | 0.1~0.2 배율에서 | 최종 |
|---|---|---|
| **자연물** — 바위 3종·노두·사슴·새 | 그대로 통과 | 400 / 600 / 800 / 300 |
| **인공물** — 자갈·침목·울타리 | **무너짐** — 평평한 면과 직각 모서리가 먼저 죽는다 | 800 / 600 / 800 |
| **직접 제작** — 이정표 | 해당 없음 | **88** |

> **왜 인공물이 먼저 무너지는가.** Decimate(Collapse)는 곡률이 낮은 면을 먼저 없앤다 —
> 바위의 울퉁불퉁한 표면은 곡률이 살아 있어 정점을 잃어도 형태가 남지만, 판자·각재의 **평평한 면과
> 직각 모서리**는 곧바로 뒤틀린다. 목재가 뒤틀리면 "낡은 나무"가 아니라 **썩은 뭉치**로 보인다.
>
> 그래서 **직각이 뜻을 가지는 것(침목·이정표·상자·판자)은 처음부터 Blender에서 만든다.**
> 이정표 88 tris는 AI 생성물 감축본(600~800)의 1/8이면서 형태는 더 정확하다.

---

## L. 열차 부품 — 생성 지시 시트 *(§G9와 함께 쓴다)*

**밤 방어전의 핵심 두 부품이 아직 프리미티브다** (2026-08-23 실측). 연결부는 큐브, 손잡이는 구체이며
둘 다 `M_Default`를 쓴다. 배경 프롭과 달리 이 둘은 **게임플레이가 직접 겨누는 대상**이라
"무엇인지 즉시 읽히는 것"이 실루엣보다 우선한다.

| 부품 | 게임 역할 | 근거 |
|---|---|---|
| **연결부** | 몬스터가 **공격해 파괴하는 목표**. 뚫리면 뒤 칸이 이탈한다 | 기획서 §9 · [손잡이-이탈저항 스펙](Train-Survival-손잡이-이탈저항-스펙.md) |
| **손잡이** | 이탈 중인 칸을 **집게로 붙잡는 표적**. 여러 명이 함께 잡아 끌어당긴다 | 〃 §3 |

### L.1 규격 — 현행 배치 실측  ✅ **반입 완료 (2026-08-23) · 아래 표는 착수 전 값이라 3곳이 틀렸다**

| 에셋 | 실치수 | 배치 | tris | 개수 |
|---|---|---|---:|---:|
| `Train_Coupler` | **1.4 × 0.2 × 1.6 m** | 칸 사이 z 간격 1.5 m, ~~갑판 높이 y 3.47~~ → **범퍼 높이 y 2.87** | 800 | 편성당 3 |
| `Train_Handrail` | **0.8 m** (구체 자리) | 칸 끝 좌우 x ±2.0, y 3.57 | 400 | ~~칸당 2 (4칸 = 8)~~ → **6** |

> 실치수는 **콜라이더가 이미 쓰고 있는 값**이라 바꾸면 조준·타격 판정이 함께 움직인다.
> 모델은 이 부피 안에 들어와야 하고, 넘칠 것 같으면 스펙 쪽을 먼저 고친다.

**반입하며 드러난 정정 3건** — 확정 수치와 as-built는 [열차 아트 배치 §8](../specs/world/train-art-layout.md).

1. **연결 높이는 갑판이 아니라 범퍼다.** 칸의 범퍼 돌출부는 y **2.794~2.942**(중심 2.868)로,
   표의 3.47에 맞추면 연결부가 0.6 m 떠서 범퍼와 안 맞물린다.
2. **갑판 상면은 3.47이 아니라 3.44**다 (칸 끝 x ±2 지점 실측 3.435 · 중앙 3.451).
   3.58은 갑판이 아니라 측면 림(rim) 높이다.
3. **손잡이는 8기가 아니라 6기.** 기관차 칸에는 없다 (Car2·3·4 × 좌우).

넘칠 것 같으면 스펙을 고치라는 위 원칙대로, 연결부는 길이를 지킬 수 없어
**콜라이더 높이를 0.2 → 0.6 m로 키웠다**(폭·길이 유지 = 표적이 좁아지는 방향은 없음).
손잡이는 반대로 **실물을 그랩 구체 안에 맞춰 넣었다**.

### L.2 생성 지시

| 에셋 | 영문 지시 |
|---|---|
| `Train_Coupler` | a chunky diesel-punk railway coupler unit, thick iron knuckle joint with oversized rivets, a heavy slack chain looping below, brass reinforcement plate, worn dark iron #3A3A3C with rust streaks #7A4233, **built to look breakable — visible bolts and a weak seam** |
| `Train_Handrail` | a sturdy grab handle for a train car end, thick vertical iron pipe bent into a broad U grip, oversized chunky proportions, **polished brass #C89B4A on the grip where hands wear it** against dark iron #3A3A3C, mounted on a riveted base plate |

### L.3 이 둘에만 걸리는 추가 조건

1. **연결부는 "부서질 것"으로 보여야 한다.** 몬스터가 왜 저기를 때리는지 텍스트 없이 읽혀야 한다 —
   드러난 볼트·이음매·헐거운 체인처럼 **약점이 보이는 형태**로 주문한다.
2. **손잡이는 멀리서도 집게 표적으로 읽혀야 한다.** 이탈한 칸은 후방 수십 m로 밀려나므로,
   그 거리에서 "잡을 수 있는 것"이 구분돼야 한다 — **손이 닿는 부분만 황동으로 밝게** 빼서
   무쇠 편성 위에서 눈에 띄게 한다. 이것은 장식이 아니라 **어포던스**다.
3. **좌우 대칭으로 만든다.** 손잡이는 칸 좌우(x ±2.0)에 같은 모델을 쓰므로, 한쪽으로 기운 형태면
   반대쪽이 어색해진다 ([레벨 가이드 §4.1.1](Train-Survival-레벨디자인-가이드.md)의 좌우 규칙과 같은 이유).
4. **감축은 인공물 규칙을 따른다** — §K.7.1. 리벳·볼트 같은 직각 디테일은 Decimate에서 먼저 무너지므로,
   0.1~0.2 배율에서 뭉개지면 배율을 올리거나 Blender에서 직접 만든다.

### L.4 함께 볼 것 — 아직 프리미티브인 나머지

| 오브젝트 | 현재 | 비고 |
|---|---|---|
| `BoardingRamp` | 큐브 2.0 × 0.2 × 8.34 m | 승·하차 동선. 이번 시트 밖이지만 같은 이유로 교체 대상 |
| `Connector_*`의 `M_Default` | 회색 기본 머티리얼 | 모델을 넣으면 `M_Train_Coupler`로 함께 교체한다 |
