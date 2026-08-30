# 아키텍처 명세 허브 (`specs/`)

`docs/specs/`는 **구현된 코드와 실제로 일치하는** as-built 문서만 둔다. 구현 전 계획·범위·TBD는
[`docs/plans/`](../plans/)에, 게임 내용(무엇을·왜)은 [`docs/design/`](../design/)에,
프로젝트 전체 결정은 [`docs/guide/`](../guide/Train-Survival-개발-가이드.md)에 있다.

> **specs에 "예정"이나 "TBD 예정"이 들어가면 그 문서는 이미 틀렸다.** §11 리스크 절의
> "현재 이런 한계가 있다"는 기록이지, "앞으로 이렇게 할 것이다"는 계획이 아니다.

도메인 폴더는 코드의 `Assets/_Project/Scripts/` 구조를 미러링한다.

## 도메인 인덱스 (20편)

### Gameplay

| 도메인 | 문서 | 한 줄 요약 |
|---|---|---|
| `world/` | [scroll-and-streaming.md](world/scroll-and-streaming.md) | **열차 고정 + 월드 스크롤 좌표계**, 지형 타일 스트리밍, 지상 자원, 안착 축 공용화 |
| `world/` | [fuel-loop.md](world/fuel-loop.md) | 엔진 투입→충전→소모→감속. 소모율 = 기본 + 칸 수 + 건축물, 자원별 발열량 차등 |
| `world/` | [train-art-layout.md](world/train-art-layout.md) | 열차 아트 배치·궤도 타일·접지 높이 참조표 (QA용) |
| `world/` | [distant-scenery.md](world/distant-scenery.md) | **원경 4층 시차 레이어 + 지역 × 국면 안개**. 대자연은 폴리곤이 아니라 각속도로 만든다 |
| `world/` | [asset-import-pipeline.md](world/asset-import-pipeline.md) | 배치 에셋의 **스케일(100배)·피벗·Mesh LOD** 규약과 지역별 폴리곤 실측. LOD는 `LODGroup`이 아니다 |
| `train/` | [train-state-model.md](train/train-state-model.md) | **호스트 소유 단일 상태 모델** — 편성·파괴·연쇄 이탈·수리·재결합. 재접속 복원의 원천 |
| `train/` | [construction.md](train/construction.md) | 갑판 셀 그리드·건축물·판자 증축. 갑판 폭이 데이터에서 파생된다 |
| `harpoon/` | [grapple-pipeline.md](harpoon/grapple-pipeline.md) | 집게 발사→판정→견인. 로컬 선반영 + 호스트 권위 분리, 등급·몬스터 그랩 |
| `player/` | [network-movement.md](player/network-movement.md) | 소유자 권위 이동, 호스트 개입 상태 머신, 1인칭 통합 시점·파지·애니메이션 |
| `player/` | [interaction-arbitration.md](player/interaction-arbitration.md) | E키 상호작용 중재 — 겹쳐 뜨던 안내를 겨눈 것 하나로 좁힌다. 로컬 표시·입력 전용 |
| `cycle/` | [day-night-cycle.md](cycle/day-night-cycle.md) | 낮→밤→Day+1. 호스트 누적 시간 하나에서 전 피어가 순수 파생 + 시각 연출 |
| `monsters/` | [wave-and-steering.md](monsters/wave-and-steering.md) | 밤 웨이브, NavMesh 불사용 호스트 조향, 변종·보스·스탬피드·그랩 |
| `combat/` | [weapon-combat.md](combat/weapon-combat.md) | 총기 공통 사격·근접·탄약. **새 무기 = 에셋 1개**, 산탄은 시드만 중계 |
| `inventory/` | [hotbar.md](inventory/hotbar.md) | 통합 핫바 5칸 + 가방 15칸, 자원 종류 분화, 장비·창고·보따리 |
| `crafting/` | [crafting-pipeline.md](crafting/crafting-pipeline.md) | 레시피 인덱스 = RPC 식별자, 차감·지급 원자성 |
| `region/` | [region-timeline.md](region/region-timeline.md) | Day 번호 → 지역·일차·마지막 밤 순수 파생. **지역 추가 = 에셋 경로** |
| `region/` | [weather-events.md](region/weather-events.md) | 호스트 권위 무작위 날씨, 환경 배율 감속 + 로컬 안개 |
| `session/` | [lifecycle.md](session/lifecycle.md) | 전멸 판정(끊긴 플레이어 제외)·부활 대기·게임오버·재접속 스냅샷 |

### Systems · UI

| 도메인 | 문서 | 한 줄 요약 |
|---|---|---|
| `networking/` | [transport-and-lobby.md](networking/transport-and-lobby.md) | 세션 서비스, 트랜스포트 전환(인자로만), Steam 로비, 풀링↔NGO 통합 |
| `meta/` | [progress-and-achievements.md](meta/progress-and-achievements.md) | 로컬 JSON 진행 저장, 업적 플래그, Steam 미러 |
| `ui/` | [hud-architecture.md](ui/hud-architecture.md) | HUD 4계층, 이벤트 구독 표준 패턴, 디자인 토큰 |

## 대표 명세 추천 순서 (심사자용)

이 프로젝트를 **처음 읽는 사람이 가장 빠르게 구조를 파악하는 순서**다.

1. **[world/scroll-and-streaming.md](world/scroll-and-streaming.md)** — 전체의 기준 좌표계를 먼저
   이해해야 나머지가 읽힌다. "열차는 멈춰 있고 세계가 흘러온다."
2. **[train/train-state-model.md](train/train-state-model.md)** — 최대 도메인이자 방어 대상.
   호스트 단일 상태 모델과 변이 4단계 파이프라인(스냅샷 → 순수 판정 → 일괄 write-back → 이벤트).
3. **[harpoon/grapple-pipeline.md](harpoon/grapple-pipeline.md)** — 로컬 선반영과 호스트 권위를
   분리한 파이프라인. 프로젝트 최고 난이도였고 순수 로직 분리가 가장 잘 드러난다.
4. **[player/network-movement.md](player/network-movement.md)** — 위 셋을 소비하는 입장에서
   서비스 경계(`ServiceLocator`)가 어떻게 쓰이는지 확인.

여러 문서의 §4에 Mermaid `classDiagram`이 있어 **도메인 간 경계(어떤 인터페이스로만 서로를
참조하는지)**를 명시한다 — [아키텍처 규칙](../conventions/architecture-rules.md)의 단방향 의존이
실제로 지켜졌는지 대조하는 용도로도 쓸 수 있다.

## 문서 공통 구조

전 문서가 같은 13절을 따른다.

| 절 | 내용 |
|---|---|
| 1~2 | 개요·목적 / 범위(포함·미포함) |
| 3 | **요구사항 → 설계 해석** — 어떤 요구가 어떤 설계 결정이 됐는가 |
| 4~5 | 시스템 구조(다이어그램) / 데이터 구조 |
| 6 | 상세 로직·상태 — 수식·시퀀스·엣지 케이스 |
| 7~9 | 인터페이스 경계 / SOLID 적용 / Unity 특화 |
| 10~13 | 테스트 케이스 / 리스크·TBD / 확장 여지 / 파일 위치 |

## 커버리지

| 코드 영역 | 명세 |
|---|---|
| `Gameplay/` 12개 도메인 | ✅ 전부 (Debugging 제외 — 에디터 도구) |
| `Systems/` (Networking·Steam·Meta) | ✅ 2편 |
| `UI/` | ✅ 1편 |
| `Core/` (EventBus·ServiceLocator·PoolManager) | 미작성 — [아키텍처 규칙](../conventions/architecture-rules.md)이 사용 원칙을 정의 |
