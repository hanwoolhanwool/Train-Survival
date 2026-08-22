# 열차·궤도 높이 규약과 QA 높이 토글

열차가 너무 높아 보인다는 QA 판단에서 출발한 문서다. **높이를 어디가 정하는지**, **바꿀 때 무엇이
함께 움직여야 하는지**, 그리고 **플레이 중 눈으로 비교할 3단계 토글**을 한곳에 모은다.

대상 씬: `Assets/_Project/Scenes/Game_ArtTest.unity` / 최초 작성 2026-08-22.
아트 배치 자체(모델·회전·GUID)는 [열차 아트 배치 참조](train-art-layout.md)가, 접지 기준선의 유래는
그 문서 §7이 다룬다. 여기서는 **그 기준선을 통째로 올리고 내리는 축** 하나만 다룬다.

---

## 1. 한 줄 요약

높이를 바꾸는 방법은 **오프셋 하나를 표현과 규칙 양쪽에 똑같이 흘리는 것**뿐이다.

- **표현**(씬 오브젝트·궤도 타일)은 `TrainElevationFollower`가 자기 기준 y에 오프셋을 더해 따라온다.
- **규칙**(건설 배치·조준 평면·몬스터 착지·플레이어 스폰·체온·즉사 존)은 전부 한 값
  `TrainLayoutSettings.DeckHeight`를 읽으므로, 거기에 같은 오프셋을 얹으면 함께 따라온다.

둘이 **같은 오프셋**을 쓰기 때문에 어느 단계에서도 바퀴는 레일에 얹혀 있고 갑판까지의 거리도 일정하다 —
이것이 높이를 바꿔도 열차 위 건설과 열차·레일 콜라이더가 어긋나지 않는 근거다.

---

## 2. 기준 배치 (단계 0 = 씬·에셋에 굳어 있는 값)

| 항목 | 값 | 정하는 곳 |
|---|---|---|
| 궤도 루트 `RailTrack` | localPos y **−0.5** | `TerrainTile_Rail.prefab` (배리언트 `TerrainTile_Forest_A`가 상속) |
| 레일 상면 | y **0.976** | −0.5 + 레일 층 콜라이더 로컬 상면 1.476 |
| 도상(자갈) 상면 | y **0.55** | −0.5 + 도상 층 로컬 상면 1.05 → 지면(y 0) 위로 0.55 노출 |
| 편성 루트 `Train` | y **0.916** | 씬 루트 (바퀴 접지 로컬 0.06 → 월드 0.976 = 레일 상면) |
| `Train_Handrails` | y **0.916** | 씬 루트 |
| `EngineFuelPort` / `CraftingStation` | y **3.566** | 씬 루트 (편성 자식이 아니다) |
| **갑판 상면 `_deckHeight`** | **3.566** | `Assets/_Project/Data/TrainLayoutSettings.asset` |
| 열차 하부 즉사 존 `_wheelKillHeight` | 1.2 | 같은 에셋 |

> `_deckHeight`가 이 체계의 급소다. **20곳 가까이가 이 한 값을 읽는다** — 건축물 설치 좌표,
> 망치 조준 평면, 판자 스폰, 몬스터 점프·착지, 플레이어 초기 스폰·부활, 체온의 실내 판정,
> 창고 배출 위치. 열차만 내리고 이 값을 안 맞추면 건설 고스트가 공중에 뜨고 몬스터가 갑판을 못 밟는다.

---

## 3. QA 높이 토글 — F2

인게임에서 **F2**를 누르면 단계가 순환한다: 현재 → 아래 → 더 아래 → 다시 현재.

| 단계 | 오프셋 | 레일 상면 | 갑판 | 도상 노출 | 즉사 존 상한 |
|---|---|---|---|---|---|
| **0 현재** | 0 | 0.976 | 3.566 | 0.55 | 1.2 |
| **1 아래** | −0.30 | 0.676 | 3.266 | 0.25 | 0.9 |
| **2 더 아래** | −0.60 | 0.376 | 2.966 | **−0.05** | 0.6 |

단계 2에서 도상이 지면에 완전히 묻혀 레일·침목만 보인다 — 더 내리면 궤도 실루엣이 사라지므로
이 정도가 "살짝 낮춤"의 실질적 하한이다.

- 단계 값은 씬의 `Train` → `TrainElevationController._stepOffsets`에서 조절한다.
  **0번 항목은 반드시 0**으로 둔다 (씬에 굳어 있는 기준 높이라는 전제).
- 호스트가 단계를 확정해 `NetworkVariable`로 복제하므로 **어느 피어에서 눌러도 전원이 같은 높이**를 보고,
  나중에 접속한 피어도 접속 시점의 단계를 그대로 받는다.
- 단계가 바뀌면 콘솔에 `[TrainElevation] 높이 단계 n/2 — 오프셋 −0.30 m, 갑판 y=3.266`이 찍힌다.
- QA 전용이다. 릴리스에서는 `Train` → `QaDebugHotkeys._enableQaKeys`를 끈다(핫키 전체가 함께 꺼진다).

---

## 4. 함께 움직이는 것 / 움직이지 않는 것

### 4.1 따라 내려간다

| 대상 | 방식 |
|---|---|
| `Train` (칸·연결부·`BoardingRamp`·콜라이더 전부 자식) | `TrainElevationFollower` |
| `Train_Handrails` | `TrainElevationFollower` |
| `EngineFuelPort` / `CraftingStation` | `TrainElevationFollower` (편성 자식이 아니라 각자 붙인다) |
| 궤도 `RailTrack` — **BoxCollider 3장 포함** | `TrainElevationFollower` (프리팹에 부착 → 스트리밍되는 모든 타일에 적용) |
| 갑판 기준선 `DeckHeight` | `TrainLayoutSettings.SetElevationOffset` |
| 즉사 존 `WheelKillHeight` | 같은 오프셋. 바퀴 밑 공간이므로 함께 내려온다 (0 밑으로는 안 간다) |
| 플레이어 스폰·부활 지점 | `DeckHeight` 파생이라 자동 |

궤도는 **씬 오브젝트가 아니라 스트리밍되는 지형 타일의 일부**라(`train-art-layout.md` §7.1) 프리팹에
붙였다. `TrainElevationFollower`는 `OnEnable`에서 현재 오프셋을 직접 물어보므로, **풀에서 뒤늦게
꺼내지는 타일도 이미 내려간 높이로 나온다** — 스트리밍 이음매가 어긋나지 않는다.

### 4.2 일부러 두는 것

| 대상 | 이유 |
|---|---|
| `Main Camera` | 씬 초기 배치용이고 플레이 중에는 플레이어 카메라가 쓰인다. 최종값을 굳힐 때만 함께 내린다(§6). |
| `RailTrack_Preview` / `Ground_Preview` | `EditorPreviewOnly`라 플레이가 시작되면 스스로 꺼진다. 토글과 무관하다. |
| `CarBuildGhost` | 런타임이 칸 중심 기준으로 그리므로 부모가 내려가면 따라온다. |
| 플레이어·갑판 위 물건 | 갑판이 내려간 프레임에 잠깐 뜬 뒤 중력으로 내려앉는다. 단계 전환 직후의 정상 동작이다. |

---

## 5. 구현 지도

| 파일 | 역할 |
|---|---|
| `Gameplay/Train/TrainElevationLogic.cs` | 단계 순환·오프셋 해석·`기준 + 오프셋` 계산 (순수 — EditMode 검증) |
| `Gameplay/Train/ITrainElevation.cs` | 계약. `ServiceLocator`에 등록된다 |
| `Gameplay/Train/TrainElevationEvents.cs` | `TrainElevationChangedEvent` — 갑판 기준선이 **갱신된 뒤** 발행된다 |
| `Gameplay/Train/TrainElevationController.cs` | 권위·복제(`NetworkVariable<int>`). 편성 루트 `Train`에 배치 |
| `Gameplay/Train/TrainElevationFollower.cs` | 표식. 붙이기만 하면 대상이 된다(참조 배선 없음) |
| `Gameplay/Train/TrainLayoutSettings.cs` | `DeckHeight`·`WheelKillHeight`에 오프셋을 얹는다. `BaseDeckHeight`가 에셋 원값 |
| `Gameplay/Train/QaDebugHotkeys.cs` | F2 → 서버 RPC → 단계 순환 |
| `Tests/EditMode/TrainElevationLogicTests.cs` | 순환·방어·스펙 수치·**상대 높이 보존** 검증 |

두 가지 설계 판단을 적어 둔다.

- **오프셋은 직렬화하지 않는다** (`[NonSerialized]`). 플레이 중 바꾼 높이가 에셋 파일에 남으면
  다음 세션이 낮아진 갑판을 물려받는다. 컨트롤러가 `Awake`·`OnDestroy`에서 0으로 되돌린다.
- **Follower는 기준 위치를 `Awake`에서 한 번만 잡는다.** 현재 위치에 더하는 방식이면 단계를
  왕복할 때마다 값이 흘러 어긋난다. 항상 `기준 + 오프셋`으로 다시 쓴다. y만 건드리고 x·z는 두므로
  스트리밍이 z를 옮기는 타일에도 안전하다.

---

## 6. 최종 높이를 굳히는 절차

QA로 단계를 골랐다면, 그 오프셋 `d`를 씬·에셋에 영구 반영하고 토글은 다시 기준 0에서 시작하게 만든다.

1. `Train` · `Train_Handrails` · `EngineFuelPort` · `CraftingStation` · `Main Camera`의 y에 `d`를 더한다.
2. `TerrainTile_Rail.prefab`의 `RailTrack` localPosition.y에 `d`를 더한다 (배리언트가 상속받는다).
3. 에디터 검수용 `RailTrack_Preview`의 y에도 `d`를 더한다 (씬 뷰가 플레이와 같아지도록).
4. `TrainLayoutSettings.asset`의 `_deckHeight`와 `_wheelKillHeight`에 `d`를 더한다.
5. `_stepOffsets`를 새 기준에 맞춰 다시 `{0, …}`으로 잡는다.
6. `TrainElevationLogicTests`의 기준 상수(`BaseDeckHeight` 등)와 이 문서 §2·§3 표를 새 값으로 고친다.

> 씬 YAML은 Unity의 씬 저장을 거치지 않고 직접 고친다 — `manage_scene save`는 수천 줄짜리 재정렬
> diff를 만든다. 편집 전에 다른 씬(`Boot`)을 로드해 둔다. 열려 있는 씬을 밖에서 고치면
> "modified externally" 모달이 떠서 MCP 전체가 막힌다.

---

## 7. 알려진 한계

- **`Game.unity`(본편)에는 적용되지 않았다.** 본편은 아직 궤도 이전 높이(`Train` y 0, 설비 y 3)이고
  공유 에셋 `_deckHeight`(3.566)와 이미 어긋나 있다 — `train-art-layout.md` §7.3의 미결 항목 그대로다.
  본편 이식 때 **궤도 타일 배선·열차 높이·이 토글 배선을 함께** 옮긴다.
- **궤도가 있는 지역은 산림뿐이다.** `Region_Forest`만 `_terrainTilePrefab`이 비어 씬 설정
  (`TerrainTile_Forest_A` — `TerrainTile_Rail`의 배리언트)이 이긴다. 북극·사막·초원은 각자 타일을
  지정하고 있고 그 타일들은 `TerrainTile_Rail` 계열이 아니라 **궤도 자체가 없다**. 지역을 넘어가면
  궤도가 사라지므로 높이 검증은 산림 구간에서 한다.
- 단계 전환은 **한 프레임에** 일어난다. 연출용 보간은 없다 — QA 비교용이기 때문이다.

---

## 관련 문서

- [열차 아트 배치 참조](train-art-layout.md) — §7이 이 높이 체계의 유래(궤도 반입과 +0.916)
- [월드 스크롤·스트리밍](scroll-and-streaming.md) — 궤도가 지형 타일을 타고 흐르는 이유
- [아키텍처 규칙](../../conventions/architecture-rules.md)
