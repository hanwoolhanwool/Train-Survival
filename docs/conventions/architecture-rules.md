# 아키텍처 규칙

폴더 배치·어셈블리 의존성·인프라 사용에 관한 상시 규칙.
초기 세팅 경위·씬 흐름·CI 구성은 [Assets/SETUP.md](../../Assets/SETUP.md) 참고.

## 1. 폴더·스크립트 배치

- 프로젝트 에셋은 모두 `Assets/_Project/` 아래에 둔다.
  (`Assets/Settings/`의 URP 렌더 파이프라인 에셋만 템플릿 기본 위치 유지 — 이동 금지.)
- 새 스크립트는 역할에 맞는 계층 폴더(`Assets/_Project/Scripts/<계층>`)에 배치한다.

| 폴더 | 어셈블리 | 역할 |
|------|----------|------|
| `Scripts/Utilities/` | `Game.Utilities` | 순수 유틸 (엔진 의존 최소) |
| `Scripts/Core/` | `Game.Core` | EventBus·ServiceLocator·PoolManager 등 인프라 |
| `Scripts/Systems/` | `Game.Systems` | 저장·오디오·씬 로딩 등 게임 공통 시스템 |
| `Scripts/Gameplay/` | `Game.Gameplay` | 플레이어·적·전투 등 게임 로직 |
| `Scripts/UI/` | `Game.UI` | HUD·메뉴 등 화면 표시 |
| `Scripts/Editor/` | `Game.Editor` | 에디터 전용 툴 (Editor 플랫폼 한정) |
| `Tests/EditMode/` | `Game.Tests.EditMode` | 에디트 모드 테스트 |
| `Tests/PlayMode/` | `Game.Tests.PlayMode` | 플레이 모드 테스트 |

- 계층이 애매하면 안쪽(하위) 계층을 피하고 상위 계층에 둔 뒤 리팩터링으로 내린다.
- 에셋은 종류별 폴더(`Art/`, `Audio/`, `Data/`, `Prefabs/`, `Scenes/`, `Settings/`)에 둔다.
  밸런싱·타입 데이터는 ScriptableObject로 만들어 `Data/`에 배치한다.
- `.cs`/`.asmdef`와 짝이 되는 `.meta`는 항상 함께 커밋한다.

## 2. 어셈블리 의존성 (단방향)

```
Game.Utilities ← Game.Core ← Game.Systems ← Game.Gameplay ← Game.UI
                                   ↑               ↑ Unity.InputSystem
                                   ↑               ↑ Unity.Netcode.Runtime + Unity.Netcode.Components
                                   ↑ Unity.Netcode.Runtime
Game.Editor        → 전 어셈블리 참조 가능 (Editor 플랫폼 전용)
Game.Tests.EditMode / Game.Tests.PlayMode → 전 어셈블리 참조 가능 (UNITY_INCLUDE_TESTS)
```

- **`Game.Core`는 netcode 무의존을 유지한다.** EventBus·PoolManager·ServiceLocator가 네트워크를 모르게 두어
  서버 로직/클라이언트 표현 분리를 어셈블리 수준에서 보장한다
  ([네트워크 아키텍처 문서](../design/Train-Survival-네트워크-아키텍처.md) §5.1, §7).

- **역방향 참조 금지.** 하위 계층이 상위 계층을 알아야 할 것 같으면
  이벤트(`EventBus<T>`)나 인터페이스를 Core로 내려 의존성을 역전시킨다 (DIP).
- 각 어셈블리는 `[InternalsVisibleTo("Game.Tests.EditMode")]` / `("Game.Tests.PlayMode")`로
  테스트에 internal을 연다. 새 어셈블리 추가 시 동일하게 적용한다.
- 새 어셈블리 추가 시 이 문서의 그래프를 갱신한다.

## 3. 인프라 사용 원칙

### 스폰/소멸 — PoolManager

- `Instantiate`/`Destroy` 직접 호출 지양. 반드시 `PoolManager.Spawn(...)` / `PoolManager.Despawn(...)` 경유.
- 반복 스폰되는 오브젝트는 로딩 구간에서 `PoolManager.Prewarm(prefab, count)`로 미리 채운다.
- 풀 재사용 시 상태 초기화는 `Awake`가 아니라 `IPoolable.OnSpawned()/OnDespawned()`에서 처리한다.

### 시스템 간 통신 — EventBus\<T\>

- 시스템 간 직접 참조 대신 `EventBus<T>` 발행/구독으로 결합을 끊는다.
- 이벤트 타입은 `readonly struct`로 정의한다 (예: `PlayerDiedEvent`).
- 구독(`Subscribe`)했으면 `OnDisable`/`OnDestroy`에서 반드시 해제(`Unsubscribe`)한다.

**네트워크 권위 규약** — 멀티플레이(리슨 서버, [네트워크 아키텍처 문서](../design/Train-Survival-네트워크-아키텍처.md) 참고) 기준:

- 이벤트는 두 종류로 구분해 정의·사용한다.
  - **권위 이벤트**: 게임 상태의 진실이 확정됐음을 알림 (예: 자원 소모 확정, 칸 파괴, 몬스터 사망).
    **호스트가 확정한 뒤에만 발행**한다. 클라이언트에서는 네트워크 동기화 수신 시점에 발행된다.
  - **로컬 표현 이벤트**: 연출·UI 등 로컬 반응용 (예: 발사 이펙트, 히트 마커, HUD 갱신).
    권위 확정 전에 각 클라이언트에서 즉시 발행해도 된다.
- 어느 쪽인지 이벤트 타입의 XML 주석에 명시한다. 애매하면 권위 이벤트로 취급한다.
- **게임 상태를 변경(자원 증감, 오브젝트 파괴 등)하는 구독자는 권위 이벤트만 구독한다.**
  로컬 표현 이벤트를 근거로 상태를 바꾸면 미확정 상태를 진실처럼 소비하는 버그가 생긴다.

### 전역 서비스 — ServiceLocator

- 전역 서비스는 `ServiceLocator.Register<T>()`로 등록하고 `Get<T>()`로 조회한다.
- `T`는 가능하면 **인터페이스**로 등록해 사용처가 추상에 의존하게 한다.
- 등록은 Boot 씬(진입점)에서 일괄 수행한다.

## 4. 테스트 배치

- 순수 로직 테스트는 `Tests/EditMode/`, 씬·GameObject 수명주기가 필요한 테스트는 `Tests/PlayMode/`에 둔다.
- 테스트 클래스는 대상 시스템과 같은 이름 + `Tests` 접미사 (예: `PoolManagerTests`).
- 정적 상태(`EventBus`, `ServiceLocator` 등)를 쓰는 테스트는 `TearDown`에서 반드시 `Clear()`로 정리한다.
