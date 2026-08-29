# 이미지 생성 브리프 — Train Survival

작성일: 2026-08-20 · 버전: **v1.3** · 용도: **외부 이미지 생성 AI(ChatGPT 등) 입력용 발췌본**

> **v1.3 변경 요약 (2026-08-26)** — **보스 경로 신설.**
> - **§N** 지역 보스 4종 생성 지시 시트 — 보스는 §G1~G8(장면)도 §J~§L(무발광 3D 입력)도 아니다.
>   **눈과 별씨 결정이 발광해야** 하는데 §G9의 `NEGATIVE`가 `glow`를 막고 있어, §N.3이 그 부분 해제
>   규칙을 정의한다. 같은 규칙으로 §J ⑫ 유적 부품의 모순도 함께 풀린다
> - **실측으로 드러난 것**: 보스 4종만 아직 프리미티브이고 **사막과 북극은 완전히 동일한 복제**다.
>   §N의 1차 임무는 넷을 실루엣으로 갈라놓는 것이다
> - **§G6**은 컨셉 문단으로 남기고, 실제 생성 규격·고유명·크기는 §N이 갖는다
> - **§N.5.1 처치 트리거 확정** — **결정을 집게로 뜯는 것이 사망 조건**이다(체력 0이 아니다).
>   근거는 실측 하나다: 지금 숲 보스는 **밤 150초 중 9초면 죽는다**. 모델에는 **소켓**과
>   **머티리얼 발광 토글**이 요구사항으로 걸린다
> - **사막 보스 재정의** — 모래 폭풍 그 자체(샌드맨형)이고 **트릭으로만 처치**된다.
>   **폭풍은 VFX, 3D로 뽑는 것은 본체 하나뿐**이다 — 폭풍을 그림에 넣으면 본체가 파묻힌다
> - **사막만 마지막 날 아침에 나온다** (2026-08-27, Day 9의 낮 시작). 넷 중 유일하게 밤이 아니다 —
>   **낮에는 발광이 안 읽히므로** 사막 본체만 실루엣·명도로 판독되게 규격이 갈린다

> **v1.2 변경 요약 (2026-08-23)** — **지면 텍스처 경로 신설.**
> - **§M** 지면 텍스처 생성 지시 시트 — 3D를 거치지 않고 **그대로 반복해 까는 평면**이라
>   §G9(3D 입력)와 규칙이 정반대다. 무지 회색 배경·3/4 시점을 적용하면 못 쓰는 그림이 나온다.
>   **심리스가 절대 조건**이고 반복 크기가 배선(타일링 6×4 = 10 m)에 묶여 있다
> - **§L** 열차 부품 시트에 반입 결과 반영 — 실측 정정 3건

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
> | **3D 생성 입력** (Tripo·Meshy에 먹일 것) | §A~**§E** + **§G9** (§F는 **쓰지 않는다**) | §J(자원) · §K(배경) · §L(열차 부품)의 한 줄 |
> | **보스 3D 입력** (발광이 필요한 것) | §A~**§E** + **§G9** + **§N.3** (§F는 **쓰지 않는다**) | §N.4의 한 줄 |
> | **지면 텍스처** (3D를 안 거치고 그대로 까는 것) | §A~**§E** + **§M** (§F·§G9 **둘 다 쓰지 않는다**) | §M.4의 지역 한 줄 |
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
| **바다 (늦여름)** | `#2D7387` 바닷물 · `#C7E5EF` 잔물결 · `#3E7AB1` 하늘 상단 · `#BDDBE5` 수평선 · `#1E272B` 해저 어둠 | 사방이 물, 수평선만 남는다 / **갈증** |
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
>
> **3D 모델을 뽑을 거라면 여기가 아니라 [§N](#n-지역-보스-4종--생성-지시-시트-g9와-함께-쓴다)이다.** 이 문단은 장면·컨셉아트용이고, 실제 생성 규격 — 고유명(거수·모래 포식자·무리 우두머리·설원의 파수꾼) · 크기 · 결정 개수 · **발광 해제 규칙** — 은 §N이 갖는다.

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
| **바다** | **§O 전용 시트** — 식생이 없고 지형이 교량이라 이 표의 구성과 어긋난다 (11종) | 수평선 · 등대 · 좌초 난파선 |
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

---

## M. 지면 텍스처 — 생성 지시 시트 *(§G9를 쓰지 않는다 — 규칙이 다르다)*

**§G9·§K·§L과 용도가 다르다.** 그쪽은 image-to-3D에 먹일 **오브젝트 한 개**의 입력이고,
이것은 3D를 거치지 않고 **그대로 반복해 까는 평면 텍스처**다. 무지 회색 배경·3/4 시점·
단일 오브젝트 같은 §G9 규격을 여기에 적용하면 못 쓰는 그림이 나온다.

### M.1 현행 배선 — 이 수치를 바꾸면 안 된다

| 항목 | 값 | 근거 |
|---|---|---|
| 지면 판 | **60(X) × 40(Z) m** 큐브 상면, UV 0..1 | `TerrainTile` 계열의 `Ground` |
| 머티리얼 | `M_Ground`(숲·기본) · `M_GroundDesert` · `M_GroundGrassland` · `M_GroundArctic` | 지역마다 1장 |
| **타일링** | **(6, 4) = 축마다 10 m 반복** | 10 m가 60·40 **둘 다의 정수배**라 타일이 40 m씩 재배치돼도 이음매에서 무늬가 안 끊긴다 |
| 텍스처 | `T_Env_Ground_<지역>_BaseColor.png` · **1024²** | 10 m에 1024 px = **102 px/m**. 2 cm보다 잔 디테일은 어차피 안 보인다 |
| 셰이더 | URP **Simple Lit** · `_BaseColor` 흰색 | BaseColor만 쓴다(M8 결정 ⑦). 텍스처가 색을 갖고 온다 |

> **타일링 (6, 4)를 깨면 스크롤 중 타일 경계가 줄무늬로 드러난다.** 숫자를 바꾸고 싶으면
> 60과 40을 동시에 나누는 값(10 · 20 · 5 …)에서 고른다.

### M.2 이 시트의 절대 조건 — **심리스**

생성한 그림은 **좌우가 이어지고 위아래가 이어져야 한다.** 한 장이 곧 10 m 패치이고
그것을 24번(6×4) 깔기 때문에, 가장자리가 안 맞으면 **바둑판 격자가 화면에 그대로 뜬다.**

**ChatGPT·DALL·E는 심리스를 신뢰할 수 없다.** "seamless tileable"이라고 적어도 대개 안 맞는다.
그래서 아래 셋 중 하나로 간다 — **①을 먼저 시도하고, 실패하면 ②로 내린다.**

| # | 방법 | 언제 |
|---|---|---|
| **①** | 생성 → **이음매 검사(M.5)** 통과하면 그대로 반입 | 운이 좋으면 여기서 끝난다 |
| **②** | 생성물을 **룩 레퍼런스**로만 쓰고, 색·얼룩 크기·대비를 절차 생성 쪽에 옮긴다 | 현행 숲 지면이 이 경로다(계획 §10.11). **가장 확실하다** |
| ③ | 이미지 편집기에서 50% 오프셋 → 나타난 십자 이음매를 힐링으로 지운다 | 도구가 있을 때만. 손이 많이 간다 |

> **②가 후퇴가 아니다.** 절차 생성은 심리스가 공짜이고 타일링·색을 나중에 조절할 수 있다.
> AI에게서 얻고 싶은 것은 **"어떤 얼룩이 어떤 크기로 있어야 하는가"** 라는 판단이지 픽셀이 아니다.

### M.3 생성 지시 — 공통 *(§H 전체를 붙이지 않는다)*

**§H 코어를 통째로 앞에 붙이면 안 된다.** 그 안의 `WORLD`·`ANOMALY`·`LIGHT`·`COMPOSITION`은
**장면**을 위한 규칙이라("하늘이 화면의 절반", "열차가 유일한 따뜻한 광원") 탑다운 지면 텍스처에
그대로 해가 된다. §H에서는 **`STYLE` 과 `PALETTE` 두 덩이만** 가져오고, 나머지는 아래로 대체한다.
아래 블록은 그 대체분을 이미 포함한 **단독 완성본**이라 이것만 붙여도 된다.

```
IMPORTANT — this is a TILING GROUND TEXTURE, not concept art and not a 3D input.
Ignore any scene, camera, character or lighting-direction rules from the brief above.

FRAMING (critical)
- Perfectly TOP-DOWN orthographic view of flat ground, square canvas
- The image represents a 10m x 10m patch of terrain
- SEAMLESS TILEABLE: left edge continues into right edge, top edge into bottom edge
- Fill the whole frame with ground only — no horizon, no sky, no vignette, no border

CONTENT
- Ground surface material only: NO trees, NO rocks, NO props, NO rails, NO path,
  NO footprints, NO man-made marks, NO creatures
- Large soft patches at roughly 2-4m scale, plus medium 0.5m breakup
- Irregular torn patch edges — no circular blobs, no repeating motifs

LIGHTING (critical — do NOT bake light into the texture)
- Flat even ambient light, NO sun direction, NO cast shadows, NO highlights
- Soft contact AO in crevices only
- No color grading, no bloom, no vignette

STYLE
Stylized semi-cartoon hand-painted game texture. Large simple value shapes,
gentle gradients, high roughness. NOT a photo scan, NOT PBR, no fine grain,
no tiny speckle noise, no visible brush strokes at pixel scale.

NEGATIVE
seams, grid lines, tiling artifacts, horizon, sky, perspective, shadows,
sun direction, vignette, border, frame, trees, rocks, props, path, footprints,
photorealism, photo scan, PBR, fine noise, text, watermark, logo
```

### M.4 지역별 내용 — 4종

색은 **§D.2 지역 팔레트 안에서만** 고른다. 지면은 화면 아래 절반을 차지하므로
여기서 팔레트를 벗어나면 지역 정체성이 통째로 흔들린다.

| 지역 | 파일명 | 영문 지시 (§M.3 뒤에 이어 쓴다) |
|---|---|---|
| **숲 (봄)** | `T_Env_Ground_Forest_BaseColor` | mossy forest floor, deep shade green `#33523A` mixed with sunlit grass `#5E8C46`, torn patches of bare earth `#6E5136`, scattered dry leaves in warm tan, damp and soft |
| **사막 (여름)** | `T_Env_Ground_Desert_BaseColor` | dry desert hardpan, pale sand `#DCA85C` with rust-stained gravel `#A9613A`, wide cracked clay plates, wind ripples in the sand, bleached and dusty |
| **대초원 (가을)** | `T_Env_Ground_Grassland_BaseColor` | dry autumn prairie, golden grain `#D9A441` laid flat in drifts, amber patches `#B87A2C`, straw-colored dead grass `#EBD9A6`, worn earth showing through |
| **북극 (겨울)** | `T_Env_Ground_Arctic_BaseColor` | wind-packed snow field, glacier white `#E6EEF2` with blue shadow hollows `#9EC2D6`, deep frost crevices `#3E5A72`, hard crust and sastrugi ridges, no sparkle |

### M.5 검수 — **이음매 검사가 먼저다**

- ☐ **이음매**: 이미지를 가로·세로 **50% 오프셋**했을 때 십자 이음매가 보이는가 →
  보이면 M.2 ②·③으로 간다. **이것을 통과 못 하면 나머지는 볼 필요가 없다**
- ☐ **반복 인지**: 2×2로 붙여 놓고 봤을 때 눈에 띄는 특징 하나가 격자로 반복되지 않는가
  (밝은 반점·큰 균열 하나가 범인이다)
- ☐ **그림자 없음**: 한쪽에서 온 빛의 방향이 읽히는가 → 읽히면 Unity 조명과 이중으로 겹친다
- ☐ **소품 없음**: 나무·바위·발자국·길이 들어가지 않았는가 (그것들은 3D 프롭이 따로 담당한다)
- ☐ **미세 노이즈 없음**: 사진 스캔처럼 잘게 지글거리지 않는가 (가이드 §8.2 비권장)
- ☐ **팔레트**: §D.2 안에 있는가
- ☐ **밝기**: 열차(무쇠 `#3A3A3C`)가 지면 위에서 실루엣으로 읽히는가 — 지면이 너무 어두우면
  편성이 배경에 묻힌다 (§C 세 겹의 대비)

### M.6 반입

1. `Assets/_Project/Art/Textures/T_Env_Ground_<지역>_BaseColor.png` (1024²)
2. 해당 지역 머티리얼(`M_Ground*`)의 `_BaseMap`·`_MainTex`에 물리고 **타일링 (6, 4)**
3. `_BaseColor`는 **흰색** — 텍스처가 색을 갖고 오므로 단색을 곱하면 어두워진다
4. `TerrainTile` 계열 프리팹은 건드리지 않는다 (머티리얼만 갈면 10종이 전부 따라온다)

> **현행 숲 지면은 절차 생성본이다**(계획 §10.11). ChatGPT본이 이음매 검사를 통과하면
> 같은 파일명으로 덮어써 guid 보존으로 무수정 교체가 된다.

---

## N. 지역 보스 4종 — 생성 지시 시트 *(§G9와 함께 쓴다)*

근거는 [세계관 §5.3](Train-Survival-세계관-컨셉.md)의 교차 배치와 `BossDefinition_*.asset`·`Boss_*.prefab` 실측값이다.
§J·K·L과 결정적으로 다른 점이 하나 있다 — **이 시트에는 발광이 필수**다. §G9의 `NEGATIVE`에는 `glow`가
들어 있어 그대로 쓰면 보스의 눈과 별씨 결정이 막힌다. **N.3이 그 부분 해제 규칙**이며, 같은 규칙으로
§J ⑫ 유적 부품(`with a soft cyan glow`)의 모순도 함께 풀린다.

### N.0 현행 실상 — 보스만 아직 프리미티브다 *(2026-08-26 실측)*

일반 몬스터는 `Monster_Guardian.fbx` 실물을 쓰는데 **보스 4종만 큐브·캡슐·구체 조합**이고,
4종이 일반 몬스터와 **같은 붉은 머티리얼 `MonsterBody.mat` 한 장을 공유**한다.
눈도 별씨 결정도 없다 — §D.1의 발광 4색이 보스에는 한 톨도 안 들어가 있다.

| 지역 | 표시명 | 현행 Visual | 콜라이더 | 페이즈 | 고유 패턴 |
|---|---|---|---|:---:|---|
| 숲 | **거수** | 캡슐 (2.4, 2.2, 2.4) + 구체 머리 | r1.5 · h**3.6** | 2 | 부하 소환 |
| 사막 | **모래 포식자** | 큐브 (2.2, 1.8, 4.4) + 큐브 머리 | r1.4 · h2.8 | 3 | 투사체 |
| 대초원 | **무리 우두머리** | 큐브 (2.6, 2.2, 3.8) + 실린더 뿔 | r1.5 · h3.4 | 3 | 무리 호출 |
| 북극 | **설원의 파수꾼** | **사막과 완전히 동일한 복제** | r1.4 · h2.8 | 3 | 투사체 |

> **사막과 북극은 Visual 서브트리가 한 글자도 다르지 않다.** 지금 화면에서 두 보스는 *같은 것*이다.
> 이 시트의 1차 임무는 예쁘게 만드는 게 아니라 **넷을 서로 갈라놓는 것**이다.

### N.1 크기를 잴 자 — 이미 화면에 있는 것

| 자 | 실측 | 쓰는 법 |
|---|---|---|
| 열차 칸 하나 | **3.4 m** 높이 · 13.6 m 길이 · 4.6 m 폭 | 보스가 칸 옆에 서면 몇 칸을 가리는가 |
| 갑판 상면 | y **3.44 m** | 이 높이가 플레이어 발밑이다 |
| 편성 전체 | **77.7 m** (4량) | |
| 일반 몬스터 | 높이 **1.8 m** | 보스가 그 몇 배인가 |
| 보스 등장 위치 | 측면 **±16 m** · 전방 20 m → 첫 인지 약 **25 m** | 실루엣이 읽혀야 하는 거리 |
| 집게 사거리 | 1단계 **20 m** / 2단계 26 / 3단계 32 | 결정을 뜯으려면 이 안에 들어와야 한다 |
| tris 예산 | 몬스터 60마리 600 k(그림자 포함) = **개당 5,000** | 보스는 동시 1기다 |

### N.2 이 시트가 고정한 것 — 계열 · 크기 · 이름

**① 계열은 [세계관 §5.3](Train-Survival-세계관-컨셉.md)의 교차 배치를 유지한다** — 대형 동물 ↔ 원소 골렘.
연민(숲) → 공포(사막) → 압도(대초원) → 절망(북극)으로 정서가 진동하고, 북극이 **두 계열의 합류점**이라
최종장으로 넘어간다. 이 배치를 깨면 서사가 함께 끊긴다.

**①-사막 — 사막 보스는 "모래 폭풍 그 자체"로 구체화한다 (2026-08-26 확정).**
스파이더맨의 샌드맨이 거대 모래로 변한 상태에 가깝다. 폭풍이 몸이고, 그 안에 **응축된 본체(core)** 가 있다.
세계관 §5.3의 *"모래와 유리가 뭉친 골렘 — 무너지고 다시 뭉친다"* 를 부정하는 게 아니라 **끝까지 밀어붙인 것**이다.
따라서 **직사 화기가 통하지 않고, 트릭으로만 처치된다** — 예: 폭풍에 폭발물을 던져 본체에 타격을 준다.
이미 있는 **집게 던지기(데미지 60 · 사거리 8 m · 속도 20)** 가 그 전달 수단으로 그대로 쓰인다.

> **3D 생성 관점에서 이것이 이 시트의 가장 큰 제약이다.** 모래 폭풍은 **메시로 못 만든다** —
> 얇고 흩어진 것은 image-to-3D가 뭉갠다(§K.2 `Env_GrassClump_A` ⚠와 같은 함정이고 훨씬 심하다).
> 그래서 **폭풍은 VFX, 3D로 뽑는 것은 본체 하나뿐**이다. 폭풍을 그림에 넣어 달라고 하면
> 생성기가 그것까지 메시로 만들어 본체가 모래 뭉치에 파묻힌다.

**①-사막-2 — 사막 보스만 밤이 아니라 "마지막 날 아침"에 나온다 (2026-08-27 확정).**
사막은 4일이므로 **Day 9의 낮 시작**이다(숲 5 + 사막 4). 넷 중 유일하게 밤에 안 나온다.

| 왜 아침인가 | 실측 |
|---|---|
| **트릭에는 시간이 필요하다** | 낮 **240초** = 밤 150초의 **1.6배** |
| **웨이브와 겹치면 트릭을 풀 여유가 없다** | 사막 웨이브 배율 **1.6**으로 4지역 중 가장 높다. 아침이면 그 밤과 겹치지 않는다 |
| **폭풍은 밝은 배경에서만 보인다** | 모래 기둥이 하늘을 가리는 그림은 백열 하늘 `#E8DCC0`이 있어야 성립한다. 밤에는 검은 덩어리다 |
| **규칙 위반이 공포다** | "밤이 위험하다"를 한 번 깨는 변주. 세계관 §5.3의 사막 정서 *"부숴도 다시 일어난다"* 와 맞물린다 |

**이것이 아트에 그대로 걸린다 — 사막만 발광 판독이 안 통한다.**
§D.1의 *"20 m 밖에서 붉은 점 2개로 읽힌다"* 는 **밤 규칙**이다. 백열 하늘 아래서는 눈의 발광이 죽는다.
그래서 사막 보스의 1차 인지 신호는 발광이 아니라 **폭풍의 실루엣**이고, 결정은 **색 대비**로 읽어야 한다 —
청록 `#67E0D2`가 모래 `#DCA85C`의 보색 쪽이라 낮에도 살아남는다. 보라 눈 `#8E7BE8`은 기대하지 않는다.

> **처치 흐름도 여기서 갈린다.** 숲이 *총이 열고 집게가 뜯는다*라면, 사막은
> **폭발이 열고 → 총이 깎고 → 집게가 뜯는다**의 3단이다. 폭발이 폭풍을 흩어 본체를 드러내고,
> 노출된 동안만 직사 화기가 통하며, 이내 다시 뭉친다 — 세계관 §5.3의 *"무너지고 다시 뭉친다"* 가
> 연출이 아니라 **전투 루프 그 자체**가 된다. 실효 체력은 `1,100 × 2.132 = 약 2,350`이라
> 던지기 데미지 60만으로는 39회가 필요하다 — **폭발만으로 깎는 설계는 성립하지 않는다**는 뜻이고,
> 위 3단이 그 답이다.

**② 크기는 칸 높이 3.4 m를 자로 삼아 다시 정했다.** 현행 콜라이더는 이 곡선을 따르지 않는다 —
아래 값은 **모델이 지켜야 할 값이자 콜라이더가 따라와야 할 값**이다(§L.1의 "실치수는 콜라이더가 이미
쓰는 값" 원칙과 방향이 반대인 유일한 시트다. 보스 기획 자체가 재설정 대상이기 때문이다).

| 에셋 | 표시명 | 실치수 (W × H × D) | 칸 대비 | tris | 콜라이더 갱신값 | 왜 이 크기인가 |
|---|---|---|:---:|---:|---|---|
| `Boss_Forest` | 거수 | **3.0 × 3.6 × 3.4 m** | ×1.06 | 8,000 | r1.5 · h3.6 *(유지)* | 갑판 상면 3.44 m와 눈이 맞는다. **첫 보스는 연민이라 압도하면 안 된다** |
| `Boss_Desert_Core` | 모래 포식자 | **3.6 × 4.5 × 3.6 m** | ×1.32 | 6,000 | r1.8 · h4.5 | **폭풍 속 본체만 모델이다**(N.2 ①-사막). 폭풍이 늘 절반을 가리므로 디테일은 낭비다 |
| *(같은 보스)* | 폭풍 | 직경 **12 m** · 높이 10 m | — | **VFX** | 별도 | **3D 생성 대상이 아니다.** 직경 12 m는 칸 길이 13.6 m에 가깝다 — 폭풍 하나가 칸 하나를 덮는다 |
| `Boss_Grassland` | 무리 우두머리 | **2.8 × 4.2 × 5.0 m** | ×1.24 | 8,000 | r1.5 · h4.2 | 혼자 압도하지 않는다 — **수가 압도한다**(무리 호출) |
| `Boss_Arctic` | 설원의 파수꾼 | **5.0 × 7.0 × 5.0 m** | ×2.06 | 10,000 | r2.5 · h7.0 | 몸 안에 얼어붙은 동물이 보여야 해서 부피가 필요하고, **합류점이라 가장 크다** |

> tris 8~10 k는 그림자를 포함해도 16~20 k로 **몬스터 3~4마리 몫**이다. 동시 1기이므로 예산은 넉넉하다
> ([아트 예산 §6](Train-Survival-아트-렌더링-예산.md)). 이 값을 넘길 이유가 없고, 넘기면 부하 소환·무리
> 호출이 함께 도는 밤에 먼저 터진다.

**③ 고유명은 현행 데이터 이름을 유지한다** — 거수 / 모래 포식자 / 무리 우두머리 / 설원의 파수꾼.
`BossDefinition_*.asset`·HUD·로그가 이미 쓰고 있어, 이름을 건드리지 않으면 **변경 범위가 이미지로 닫힌다.**
(다만 "모래 포식자"가 골렘 이름으로 적절한지는 [세계관 §7 TBD](Train-Survival-세계관-컨셉.md)에 남긴다.)

### N.3 §G9에 이어 붙이는 보스 전용 블록 — **발광 부분 해제**

```
BOSS ADDENDUM — this overrides two rules of the block above.

EMISSIVE (exception to the NEGATIVE list — glow IS allowed here)
- Glow appears on TWO features only:
    (1) the EYES
    (2) the embedded starseed crystals
- Everything else stays matte, hand-painted base color
- Paint the glow as FLAT BRIGHT COLOR FILL, not as light
- NO bloom, NO halo, NO light spilling onto fur / rock / ice around it
  (the engine adds bloom later — baked glow doubles it)

CRYSTALS (gameplay-critical, not decoration)
- Exactly N crystals, clearly SEPARATED on the body, each PROTRUDING outward
- Graspable chunky shapes — a claw will latch onto them and tear them off
- Do NOT sink them flush into the body, do NOT scatter tiny fragments

POSE (image-to-3D bakes the pose into the mesh)
- Neutral idle stance, weight even on all limbs, head level
- NO roaring, NO rearing, NO attacking, NO running, NO motion

SILHOUETTE
- Must read at 25 m from the SIDE against a dark night background
- DESERT ONLY: read it against a BRIGHT bleached daytime sky #E8DCC0 instead —
  that boss appears in the morning, so glow does not carry. Value contrast must.
- One dominant mass + ONE clear secondary feature (head / horns / crystal crown)

STILL FORBIDDEN
cast shadow, ground shadow, contact shadow, rim light, bloom, halo, light spill,
background scenery, gore, wounds, horns or spikes added to animals
```

### N.4 4종 생성 지시 *(§G9 + N.3 뒤에 이어 붙인다 · 한 대화에 한 종)*

| # | 에셋 | 영문 지시 |
|:---:|---|---|
| 1 | `Boss_Forest` | a colossal stylized bear on all fours, still unmistakably a bear — rounded soft forms, thick shaggy fur bark brown `#6E5136` with shadow-green `#33523A` moss caught in the coat, big blunt muzzle, small rounded ears, heavy paws. TWO glowing `#FF4A3D` eyes with pinpoint pupils. **TWO** `#67E0D2` crystals protruding from the shoulders. Heavy sleepy proportions — it was woken from hibernation, not built for war |
| 2 | `Boss_Desert_Core` | the compacted CORE of a sandstorm creature — a hunched torso-and-head mass of packed damp sand, **no legs**: the lower body tapers into a broad torn-off stump that sits flat (the storm carries it). **No face — only eyes.** Sand `#DCA85C` in coarse layered strata with rust-stained sediment `#A9613A`, chips of translucent sea-glass `#6FA69B` fused into the surface. FOUR small cold `#8E7BE8` glowing eyes clustered where a head would be. **THREE** `#67E0D2` crystals protruding from chest and both shoulders. The surface must read as **loosely bound grains about to fly apart**, not as carved stone. **NO storm, NO dust cloud, NO swirling debris in the image** — only the solid core |
| 3 | `Boss_Grassland` | a massive stylized bison-elk, long low body, heavy shoulder hump, broad sweeping horns. Coarse golden-amber coat `#B87A2C` with dry straw `#EBD9A6` on the flanks, horns and hooves in weathered bone. TWO glowing `#FF4A3D` eyes with pinpoint pupils, head lowered in a herd-leader stance. **THREE** `#67E0D2` crystals protruding along the spine ridge. Still a grazing animal — no predator features |
| 4 | `Boss_Arctic` | a towering golem of glacier ice and packed snow, **no face — only eyes**. Blocky asymmetric mass of glacier white `#E6EEF2` and blue ice `#9EC2D6`, deep frost `#3E5A72` in the crevices. Small stylized ANIMAL SHAPES frozen in the ice, shown as **raised relief on the surface** — rounded fox and deer forms, calm and sleeping, no gore, no detail. FIVE small cold `#8E7BE8` glowing eyes. **THREE** `#67E0D2` crystals protruding from chest, shoulder and knee. **Wide heavy base narrowing upward — a tower** |

### N.5 이 넷에만 걸리는 추가 조건

1. **결정 수 = 페이즈 수다.** 숲 2개 · 나머지 3개(`_phaseHealthThresholds` 실측). 페이즈가 넘어갈 때마다
   하나씩 깨지면 **텍스트 없이 진행이 읽힌다.** 개수를 틀리면 그 연출을 나중에 못 붙인다.
2. **결정은 반드시 몸에서 돌출한다.** 3단계 집게로 뜯어낼 대상이기 때문이다(세계관 §5.3 · 기획서 §8.3).
   파묻힌 결정은 표적이 안 된다 — §L.3의 손잡이 어포던스와 **같은 논리이고, 장식이 아니다.**
   보스는 측면 16 m에 등장하므로 **1단계 집게(20 m) 사거리 안**에서 이미 겨눠진다.
3. **골렘의 눈은 차갑다 (`#8E7BE8`), 동물의 눈만 붉다 (`#FF4A3D`).**
   §D.1의 색 서사 규칙 — *"순수한 별씨는 차가운 빛, 살아 있는 것에 뿌리내리면 붉어진다"* — 을 그대로 적용한
   결과다. 골렘은 숙주가 아니라 **물질을 끌어모은 몸**이라 붉어질 이유가 없다.
   덕분에 20 m 판독이 **"붉은 점 2개 = 동물 / 차가운 점 여러 개 = 골렘"** 으로 갈린다.
   > §D.1은 골렘 눈 색을 정하지 않았다. **이 한 줄은 이 시트가 채운 자리**이므로, 채택하면 §D.1과
   > 세계관 §5.2에 함께 반영해야 한다(현재 미결).
4. **자세는 중립이어야 한다.** image-to-3D는 포즈를 메시에 그대로 굽는다. 포효·돌진 포즈로 뽑으면
   애니메이션을 얹을 수 없다 — §G9의 "no tilt"가 생물에서는 **"no action pose"** 로 확장된다.
5. **사막과 북극은 실루엣부터 갈라놓는다.** 둘 다 골렘이라 재질만 바꾸면 지금 프리팹처럼 같은 것이 된다.
   **사막은 옆으로 퍼진 덩어리, 북극은 위로 솟은 탑** — 검은 실루엣만 남겨도 구분돼야 한다(§B 실루엣 테스트).
6. **감축은 자연물 규칙(§K.7.1)을 따른다.** 골렘은 바위 계열이라 Decimate 0.1~0.2를 통과할 가능성이 높지만,
   **뿔·돌출 결정·북극의 부조는 곡률이 낮아 먼저 죽는다.** 뭉개지면 배율을 올린다 —
   결정이 뭉개지는 것은 장식이 아니라 **표적이 사라지는 것**이라 감축 실패로 판정한다.
7. **사막은 본체만 그린다.** 폭풍을 그림에 넣으면 생성기가 그것까지 메시로 만들어 본체가 파묻힌다(N.2 ①-사막).
   본체의 실루엣은 **폭풍과 반대여야 한다** — 폭풍은 흐릿한 원뿔, 본체는 **각지고 단단한 덩어리**다.
   폭발로 폭풍이 흩어졌을 때 드러나는 것이 본체이므로, 그 순간에 "저것이 알맹이다"가 즉시 읽혀야 한다.

### N.5.1 처치 트리거 — **결정을 집게로 뜯는다** *(2026-08-26 확정)*

숲 보스로 확정했고, 나머지 셋도 같은 문법을 따른다(사막은 폭풍을 걷어낸 뒤라는 조건이 하나 더 붙는다).
세계관 §5.3·기획서 §8.3이 이미 약속한 동작이고, `IGrabbable`·`MonsterGrabTarget` 규약이 이미 있어
신규 구현이 가장 얕은 길이기도 하다.

**왜 트리거가 필요한가 — 실측 하나로 끝난다.** 숲 보스의 실효 체력은 `1,400 × Day 배율 1.32 = 약 1,850`이고
4인이 라이플만 들어도 `51 dps × 4 = 204 dps`다. **약 9초면 죽는다** — 밤 150초의 6 %다.
"지역 졸업 시험"(기획서 §5)인데 시험이 성립하지 않는다.

**흐름** — 총이 결정을 *열고*, 집게가 결정을 *뜯는다*. 둘 다 있어야 진행된다.

| 단계 | 조건 | 일어나는 일 |
|---|---|---|
| 1 | 체력 **50 %** (`_phaseHealthThresholds` 실측) | 1번 결정의 **발광이 켜진다** = 열렸다 |
| 2 | 집게로 잡고 릴 | 뜯긴다. 자리에 **빈 소켓**이 남는다 |
| 3 | 체력이 더 깎임 | 2번 결정이 열린다 |
| 4 | 2번을 뜯음 | **처치.** 체력 0이 아니라 **결정 0이 사망 조건**이다 |

**모델에 걸리는 요구사항은 셋뿐이고, 셋 다 모델 하나로 닫힌다.**

1. **결정은 처음부터 메시에 있다.** 열림/닫힘은 **머티리얼 `_EmissionColor` 토글**로 처리한다 —
   숨겼다 꺼내는 방식이면 모델이 두 벌 필요해진다. §N.7-3이 발광을 텍스처가 아니라 머티리얼에
   넣으라고 한 이유가 여기서 회수된다
2. **결정 밑에 어두운 소켓을 판다.** 뜯으면 결정만 사라지고 **구멍이 남아야** 한다.
   소켓이 없으면 뜯긴 자리가 매끈한 털·바위로 남아 "뜯었다"가 안 읽힌다
3. **결정은 집게가 물 수 있는 덩어리 형태여야 한다** — N.3의 `graspable chunky shapes`가
   장식 규칙이 아니라 **처치 조건**으로 승격된다

> **파지 등급은 3단계(무게 3)를 기본으로 본다.** §J ⑥ 원목(2단계 = 연료 해결) · ⑦ 광맥(3단계 = 탄약 해결)과
> 같은 학습 구조로 **"3단계를 만들면 보스가 풀린다"** 가 이어지기 때문이다. 다만 3단계가 없으면 진행이
> 막히므로 **2단계도 가능하되 릴 시간 2배**로 완화한다.
>
> **아직 없는 것**: 보스 프리팹에 `MonsterGrabTarget`이 붙어 있지 않고, 결정용 자식 콜라이더도 없다.
> 체력·릴 시간·소켓 판정 같은 **게임플레이 수치와 네트워크 권위는 이 시트의 범위 밖**이라
> 별도 스펙에서 확정한다 — 여기서는 **모델이 갖춰야 할 것**까지만 고정한다.

### N.6 검수 *(§G9의 3D 입력 검수에 이어서 본다)*

- ☐ **25 m 실루엣에서 4종이 서로 구분되는가** — 특히 **사막 ↔ 북극**. 여기서 떨어지면 나머지는 볼 필요 없다
- ☐ 결정이 몸에서 **돌출**했는가 (파묻히면 집게 표적이 안 된다 = **처치가 불가능해진다**, N.5.1)
- ☐ 결정 **밑에 어두운 소켓**이 있는가 — 뜯긴 자리가 남아야 한다 (N.5.1)
- ☐ **사막에 폭풍이 안 들어갔는가** — 본체만 있어야 한다. 모래 구름이 한 자락이라도 있으면 다시 뽑는다
- ☐ **사막 본체가 밝은 배경에서도 읽히는가** — 발광을 지우고 봤을 때 형태와 명도만으로 구분되는가.
  아침에 나오므로 발광에 기댈 수 없다 (N.2 ①-사막-2)
- ☐ 결정 개수가 페이즈 수와 맞는가 — **숲 2 · 나머지 3**
- ☐ 발광이 **눈과 결정 두 곳에만** 있는가
- ☐ 발광이 주변 표면으로 **번지지 않았는가** (번지면 Unity Bloom과 이중으로 겹친다)
- ☐ 동물 보스가 **여전히 동물로 읽히는가** — 오염·기형화·뿔 덧붙이기 없음 (§G5·§I와 같은 기준)
- ☐ 골렘에 **얼굴이 없는가** — 눈만 있어야 한다 (세계관 §5.2)
- ☐ 자세가 **중립**인가 (포효·돌진·질주 포즈가 아닌가)
- ☐ 지역 팔레트(§D.2) **5색 안**인가
- ☐ 북극의 얼어붙은 동물이 **부조**로 처리됐는가 (내부 투명 표현은 3D 재구성이 못 만든다) · **유혈 없음**

### N.7 반입

1. `Assets/_Project/Art/Models/Boss_<지역>.fbx` · `Art/Materials/M_Boss_<지역>.mat` ·
   `Art/Textures/T_Boss_<지역>_BaseColor.png` (**2K** — §K.0과 같은 근거).
   **사막만 `Boss_Desert_Core`** 로 들어간다 — 프리팹은 `Boss_Desert.prefab` 그대로이고, 그 안에
   본체 모델 + 폭풍 VFX 두 자식이 함께 산다
2. `Boss_<지역>.prefab`의 **`Visual` 서브트리를 통째로 교체**하고, `CapsuleCollider`를 **N.2의 갱신값**으로
   올린다. 콜라이더를 안 올리면 조준·타격 판정이 모델과 어긋난다
3. **발광은 텍스처가 아니라 머티리얼 `_EmissionColor`로 넣는다.** 현행 `MonsterBody.mat`은 Emission이 0이라
   그대로 두면 눈·결정이 죽는다. 텍스처에 밝게 칠하는 것으로 대신하지 않는다 — HDR 발광이라야 밤에 읽힌다
4. 프리팹을 편집했으면 **`GlobalObjectIdHash` 정규화**를 잊지 않는다 — 어긋나면 클라 접속이 거부된다
5. 스폰은 `PoolManager.Spawn` 경유이고 NetworkPrefabs 목록에 **중복 등록하지 않는다**(풀링 규약)
6. 콜라이더를 키웠으면 `_chargeHitRadius` 3 m · `_attackRange` 4 m와의 관계를 **한 번은 눈으로 확인**한다.
   보스가 커진 만큼 "닿았는데 안 맞는" 구간이 생길 수 있다

### N.8 함께 볼 것 — 아직 프리미티브인 나머지

| 오브젝트 | 현재 | 비고 |
|---|---|---|
| **사막 폭풍** | 없음 | 이 보스의 몸 자체다(N.2 ①-사막). **VFX로 만든다** — 직경 12 m · 높이 10 m. 3D 생성 대상이 아니라 이 시트 밖이지만, **본체보다 먼저 필요하다** |
| **던질 폭발물** | 없음 | 사막 트릭의 탄약. 집게 던지기(데미지 60)가 전달 수단은 되지만 **던질 폭발물 아이템 자체가 없다** — 화약 원료(`Niter`)는 있으므로 제작 경로를 세우면 된다. 게임플레이 스펙 쪽 |
| **아침 등장 배선** | 불가능 | `BossSpawner`는 **밤 전환에 스폰하고 낮 전환에 무조건 회수**한다(`ServerRetreatBoss("낮 전환")`). 사막 보스를 그대로 두면 **뜨자마자 회수된다.** `BossDefinition`에 등장 시간대 필드가 필요하고, `INightHoldGate`의 대칭인 **낮 보류**가 없으면 240초 뒤 밤이 와서 또 회수된다. 게임플레이 스펙 쪽 |
| `BossProjectile` | 구체 0.7 + 실린더 낙점 링(y 0.02) | 사막·북극 공용. **별씨 덩어리**로 보여야 하고 낙점 링은 UI에 가깝다 — 이번 시트 밖 |
| **보스 핵** (`BossCore`) | 아이콘·모델 없음 | 처치 보상이며 **채집으로 못 얻는 유일한 자연 대역 자원**이다. §J 12종에서 빠져 있다. 카탈로그 색은 `#9E298C`로 §D.1의 별씨 보라 `#8E7BE8`와 **다르다** — 어느 쪽에 맞출지 미결 |
| `MonsterBody.mat` | 보스 4종 + 일반 몬스터 공유 | 모델을 넣으면 지역별 `M_Boss_*`로 갈라진다. 일반 몬스터 쪽은 그대로 둔다 |
| **최종 보스 "근원"** | 미구현 | 최종장(우주)은 콘텐츠 자체가 없다. 세계관 §5.3·기획서 §8.3 기준으로 **해당 차수에 별도 시트**를 만든다 |

---

## O. 바다 전용 에셋 — 생성 지시 시트 *(§G9 + §K.0과 함께 쓴다)*

> **왜 별도 시트인가.** 바다는 §K.6 표의 구성(식생 + 지형 조각)이 통째로 어긋난다 —
> **식생이 0종**이고 지형이 땅이 아니라 **교량**이며, 화면의 절반이 **물속**이다.
> 규격 근거는 [`바다 지역 구현 계획`](../plans/features/바다-지역-구현-계획.md) §3·§5.3이다.

### O.0 현황 실측 (2026-08-29) — **바다에는 전용 모델이 하나도 없다**

바다 타일 10종(`TerrainTile_Sea_A~J`)을 훑은 결과다.

| 항목 | 수 |
|---|---:|
| 커스텀 메시 참조 | **0** |
| 내장 프리미티브(큐브 등) 참조 | **468** |
| 재사용 중인 모델 | 6종 — 전부 **숲·공용** (`Env_BridgePier_A`(숲) · `Env_Sleeper_Old` · `Env_Rock_L` · `Env_RockOutcrop` · `Env_LogFallen_A`(숲) · `Env_Fence_Broken`) |

> 계획서 §5.3이 세운 팔레트 10종의 변주 축(**교각 · 원경 · 물속**)이 **전부 큐브로 서 있다.**
> 검증에서 *"등대가 어디 있지?"*(G3)가 나온 것도 이것과 무관하지 않다 — 큐브는 등대로 읽히지 않는다.

### O.1 규격 — 바다 실측값

| 항목 | 값 | 쓰임 |
|---|---|---|
| 상판 폭 · 두께 | **8 m (±4)** · 0.8 m | 모듈 폭의 상한 |
| 타일 길이 | 40 m | 상판 모듈 4장 = 1타일 |
| 물면 · 해저 | **y −4** · y −12 (수심 **8 m**) | 교각 길이·물속 배치의 기준 |
| 교각 | 1.6 × **11.2** × 1.6 m · \|x\| = 2.8 · 40 m마다 1쌍 | 상판 밑에 들어간다 |
| 자원 대역 | \|x\| 4~16 m | 부표가 뜨는 자리 |
| 원경 대역 | \|x\| 24~30 m | 난파선·등대 |
| 복귀 사다리 | 상판 0 → 물속 **−7.2** (높이 8 m) · 타일당 4개 | 물에서 올라오는 유일한 경로 |

### O.2 교량 — 모듈 3종

`Env_` · 폴더 `Art/Models/Environment/Sea/` · **§K.0의 MODULAR 문구를 반드시 붙인다**

| 에셋 | 실치수 | tris | 영문 지시 | 모듈 |
|---|---|---:|---|:---:|
| `Env_SeaDeck_A` | 8×0.8×10 m | 400 | a modular railway bridge deck section over open sea, thick riveted steel girders under a plain top surface, weathered gray steel #3A3A3C with rust #A9613A streaks, no railings | ● |
| `Env_SeaDeck_Open_B` | 8×0.8×10 m | 600 | same bridge deck family but the deck is thinner and the sleepers are exposed with gaps between them, you can see through to the water below, same weathered steel and rusted bolts | ● |
| `Env_SeaLadder_A` | 0.8×8×0.3 m | 300 | a vertical steel access ladder running down the side of a sea bridge pier, chunky volumetric rungs (not thin rods), two side rails, salt-corroded steel #3A3A3C with barnacle crust near the bottom | |

> **`Env_SeaLadder_A`는 AI 취약 항목이다** (§K.2 `Env_GrassClump_A`와 같은 이유) — 가로대가 얇은
> 봉이라 재구성이 뭉개진다. **먼저 생성해 보고 감축 판정에서 떨어지면 Blender로 직접** 만든다
> (박스 9개, 5분). 지금도 큐브 조합이라 형태만 다듬으면 된다.

### O.3 교각 3종 — **변주의 주력**

상판이 ±4 m로 좁아 위에서는 볼 것이 적은 대신, **물속·물 위 시점에서 교각이 화면을 채운다**
(계획 §5.3). 세 종이 세그먼트 C · D · I를 각각 맡는다.

| 에셋 | 실치수 | tris | 영문 지시 |
|---|---|---:|---|
| `Env_SeaPier_Truss_A` | 1.6×11.2×1.6 m | 1,200 | a tall steel truss bridge pier standing in the sea, X-shaped cross braces stacked in three tiers, riveted joints, weathered gray steel #3A3A3C, dark algae #1E272B on the lower half where it meets the water |
| `Env_SeaPier_Arch_A` | 1.6×11.2×1.6 m | 1,400 | a tall stone masonry bridge pier with a rounded arch opening at its base, large weathered blocks, pale gray stone, dark waterline stain and barnacles at the bottom |
| `Env_SeaPier_Broken_A` | 1.6×8×1.6 m | 1,000 | the same stone bridge pier but snapped in half, jagged broken top, exposed rebar bent outward, rubble collar at the waterline |

> **I(무너진 교각)는 상판이 기울어 있는 세그먼트다** — 교각만 부러뜨리면 되고 상판은
> `Env_SeaDeck_A`를 회전시켜 쓴다. 모델을 따로 만들지 않는다.

### O.4 랜드마크 4종 — 원경과 물속을 채운다

**미결정 ⑪이 여기 걸려 있다**: *"화면의 대부분이 물과 하늘"*인데 원경을 지탱할 난파선·등대의
가중 합이 **0.166**뿐이다. 모델이 서면 빈도를 다시 정한다(검증 G3와 함께).

| 에셋 | 실치수 | tris | 영문 지시 |
|---|---|---:|---|
| `Env_Lighthouse_A` | 4×14×4 m | 2,000 | a lone stylized lighthouse on a small rock outcrop, tapered cylindrical tower with a horizontal band, glass lantern room at the top, chunky simple silhouette readable from far away, weathered white and rust red, no thin railings |
| `Env_Shipwreck_A` | 12×6×5 m | 3,000 | a rusted cargo ship wrecked and half sunken, hull broken and tilted, exposed ribs, thick volumetric masses only, heavy rust #A9613A over dark steel, no thin masts or wires |
| `Env_SunkenRuin_A` | 8×5×8 m | 2,000 | a submerged stone ruin resting on the seabed, a partly collapsed columned structure covered in algae and sediment, chunky worn blocks, muted teal-gray #2D7387 tint |
| `Env_Reef_A` | 3×2×3 m | 400 | a chunky rock reef breaking the surface, rounded wave-worn stone, dark waterline band, a little algae on top, neutral gray |

> **등대는 밤에 회전광이 돈다** (계획 §5.3 H) — 광원과 회전은 **머티리얼·컴포넌트**가 하고,
> 모델은 **램프실을 별도 오브젝트로 분리할 수 있게** 만들어야 한다. 생성 지시에
> `glass lantern room at the top`을 넣은 이유다.

### O.5 소품 2종

| 에셋 | 실치수 | tris | 영문 지시 |
|---|---|---:|---|
| `Env_Buoy_A` | 0.8×1.4×0.8 m | 200 | a floating channel buoy, chunky cylindrical float with a short mast and a small lamp cage on top, bright warning orange #F2762E with a dark band, weathered |
| `Prop_FishingRod_A` | 0.1×1.8×0.1 m | 400 | a simple handmade fishing rod, a slightly curved tapered wooden pole with a cord-wrapped grip and a small metal reel, volumetric not thin, weathered wood #6E5136 with brass #C89B4A fittings |

> **부표는 자원 표지이지 자원이 아니다** (2회차 C2 판정) — 자원을 뜯어 가도 **남는다**.
> 다음 자원이 같은 자리에 뜨므로 플레이어가 어디를 볼지 학습한다. 그래서 **밤에도 식별돼야 한다**
> (emissive 램프).
>
> **낚싯대는 손에 드는 물건이다** — 축 규약은 §L의 무기와 같다(**총구 방향 +Y · 위 +Z**).
> 지금은 모델이 없어 *"뭘 들었는지 안 보인다"*(미결정 ⑰).

### O.6 몬스터 — 별들린 물고기 *(4차 신규)*

바다 4차가 세운 **도약 전용 변종**이다. 물에서만 살고, 물 밖 표적에게 **튀어올라** 상판 위를 친다.

| 에셋 | 실치수 | tris | 영문 지시 |
|---|---|---:|---|
| `Monster_SeaLeaper` | 길이 2 m | 800 | a stylized corrupted leaping fish, thick torpedo body with a large crescent tail and stubby fins, jagged jaw, glowing crystal shards growing along its spine, deep teal #2D7387 body with pale #C7E5EF underside, emissive violet #8E7BE8 in the eyes and shards |

> **왜 지금 필요한가.** 변종은 프리팹이 아니라 **`MonsterSettings`로만** 구분하는 규약이라
> (`MonsterVariantCatalog` 주석), 모델이 없으면 **여우형 `Monster_Guardian`이 청록색으로 튀어오른다.**
> 지역 고유 위협인데 지역 고유의 모습이 없다.
>
> 발광은 **텍스처가 아니라 `_EmissionColor`**로 넣는다 (§N.7과 같은 규약) — 밤 바다는 물이 하늘빛을
> 반사해 밝게 남고 그 위 모든 것이 실루엣이 되므로(0차 실측), **발광만이 형태를 읽게 한다.**

### O.7 타일당 예산 — 바다는 식생이 없다

| 종류 | 타일당 개수 | 개당 tris | 소계 |
|---|---:|---:|---:|
| 상판 모듈 | 4 | 400~600 | 2,000 |
| 교각 | 2 (1쌍) | 1,200~1,400 | 2,600 |
| 복귀 사다리 | 4 | 300 | 1,200 |
| 부표 | 3~15 | 200 | 600~3,000 |
| 암초 | 0~4 | 400 | 1,600 |
| 랜드마크 (특징·이벤트 세그먼트만) | 0~1 | 2,000~3,000 | 3,000 |
| **합계** | | | **약 10,400~13,400** |

> **목표 30,000의 절반 이하다** (§K.7). 식생 20,000이 통째로 빠진 자리다 —
> **교각과 랜드마크에 더 써도 된다.** 물속 시점에서 교각이 화면을 채우는 지역이므로
> 트러스를 1,200에서 2,000까지 올려도 예산이 남는다.

### O.8 이 시트 밖 — 아직 없는 것

| 대상 | 현재 | 비고 |
|---|---|---|
| **물보라 · 숨 기포** | 없음 (미결정 ⑬) | **VFX**다. 물에 들고 나는 **순간**의 피드백이 없어 경계가 밋밋하다 — 3D 생성 대상이 아니지만 물고기 모델보다 체감이 클 수 있다 |
| **수영 애니메이션** | 없음 (미결정 ⑭) | 남이 헤엄치는 모습이 **걷는 자세**다. 애니메이션 축 |
| **몬스터 수면 이동 표현** | 없음 (검증 H4) | 몬스터가 물면 높이에서 **걸어서** 온다. 애니메이션 + VFX |
| **낚싯줄** | 없음 (검증 K11) | 던진 줄이 보이지 않는다. LineRenderer 축 |
| **물 드로우콜** | 타일 9장 × 물 평면 (미결정 ④) | 알파 오버드로가 화면 전체를 덮는다 — 아트 예산 재확인 대상 |
