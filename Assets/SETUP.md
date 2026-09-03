# 프로젝트 초기 세팅

Unity 6 (6000.5.3f1) / URP 17.5 / Input System / PC(StandaloneWindows64) 기준.
이 문서는 초기 세팅 상태(씬 흐름, 테스트 실행, CI 구성, 도구)를 기록한다.

> **상시 규칙**(폴더·스크립트 배치, 어셈블리 의존성, 인프라 사용 원칙)은
> [docs/conventions/architecture-rules.md](../docs/conventions/architecture-rules.md)로 이동했다.
> 코드를 작성·배치할 때는 그 문서를 따른다.

## 1. 초기 구성 요약

- `Assets/_Project/` — 프로젝트 에셋 루트 (구조·배치 규칙은 위 아키텍처 규칙 문서 참고)
- 어셈블리 8개: `Game.Utilities` / `Game.Core` / `Game.Systems` / `Game.Gameplay` / `Game.UI` /
  `Game.Editor` / `Game.Tests.EditMode` / `Game.Tests.PlayMode`
- 코어 인프라: `EventBus<T>`, `ServiceLocator`, `PoolManager`(+`IPoolable`), `MonoSingleton<T>`
- `Assets/Settings/` — URP 렌더 파이프라인 에셋 (템플릿 기본 위치, 이동 금지)

## 2. 씬 흐름

```
Boot (인프라 초기화: GameBootstrapper가 서비스 등록 · NetworkManager+UnityTransport 상주)
  → Main (프론트엔드 — 화면 두 개가 한 씬 안에 있다)
       ├─ 배너 화면   Canvas_Scene — 표지판 명판 4장 · 공고대
       └─ 대기실      Canvas_Ready — 로스터 4칸 · 난이도 · 시작/초대/나가기
  → Game / Game_ArtTest (인게임)
```

- **`Main`은 씬을 나누지 않는다.** 배너와 대기실은 같은 배경·같은 열차 위에 겹쳐 있고
  `SetActive` 한 번으로 갈린다. 씬을 나누면 배경·연출을 통째로 복제해야 하고,
  `EnableSceneManagement`가 켜져 있어 **대기 중 씬 전환이 곧 네트워크 씬 동기화**가 된다.
  근거는 [게임 준비 화면 구현 계획](../docs/plans/features/게임-준비-화면-구현-계획.md) §2.
- 대기실은 `Prefabs/UI/Ready_Screen.prefab` 한 덩어리이고 씬에는 인스턴스만 있다 —
  씬 diff를 작게 유지하기 위한 것이다.

- `Assets/_Project/Scenes/`에 Boot/Main/Game 씬이 존재하며 셋 다 Build Settings에 등록되어 있다 (Boot이 0번).
- Boot 씬 구성: `NetworkManager`(NetworkManager + UnityTransport + NetworkPrefabPoolRegistrar,
  풀링 프리팹 목록은 `Assets/_Project/Data/NetworkPoolConfig.asset`) · `GameBootstrapper`(서비스 등록 후 Main 로드).
- 씬 등록은 Build Profiles(Build Settings)에서 관리하고, 씬 전환 로직은 `Game.Systems`에 둔다.
- 템플릿의 `SampleScene`은 더 이상 사용하지 않는다 (추후 삭제 예정).
- 네트워크 세션은 `ServiceLocator.Get<INetworkSessionService>()`로 시작한다
  (게임 시작 = `StartHost()`, 1인 플레이 = 혼자 호스트인 세션).

## 3. 테스트 실행

- Unity 에디터: `Window > General > Test Runner`.
- CLI (에디터 설치 경로 기준):

```
Unity.exe -batchmode -projectPath . -runTests -testPlatform EditMode -testResults results.xml
```

## 4. CI/CD

- `.github/workflows/ci.yml` — GitHub Actions + [GameCI](https://game.ci/) 파이프라인.
  두 잡이 **병렬로** 돈다 (벽시계 약 10분):
  1. **Test (EditMode · PlayMode 격리)** — `game-ci/unity-test-runner`
  2. **Build (StandaloneWindows64)** — `game-ci/unity-builder` → 아티팩트 업로드

**CI가 실제로 지키는 것** (통과 = 무엇이 보장되나):

| 검사 | 게이트인가 | 보장 범위 |
|---|---|---|
| EditMode 테스트 (1,406개) | ✅ 실패하면 워크플로 실패 | 순수 로직·수학·에셋 배선 |
| PlayMode 테스트 (11개) | ✅ 실패하면 워크플로 실패 | NGO 세션·풀링·풀 네트워크 프리팹 핸들러 |
| StandaloneWindows64 빌드 | ✅ 실패하면 워크플로 실패 | 컴파일과 빌드 파이프라인이 성립한다 |

> ### ⚠ 규칙 — 에디터 메뉴에는 **항상** 리눅스 가드를 씌운다
>
> ```csharp
> #if !UNITY_EDITOR_LINUX
>         [MenuItem("Game/QA/무언가")]
> #endif
>         private static void DoSomething() { ... }
> ```
>
> **왜** — 리눅스 batchmode 에디터는 PlayMode에 진입할 때 메뉴를 재구축하다 세그폴트(`signo:11`)로
> 죽는다. 방아쇠는 특정 메뉴가 아니라 **항목 수**이고, 실측상 **7개째부터** 터진다.
> CI가 리눅스라 이것 하나로 2026-08-31 ~ 09-03에 파이프라인 전체가 멈췄다.
>
> **한 번 겪고 또 겪었다.** 09-04에 문제의 메뉴 하나만 빼서 6개로 줄였는데, 같은 날 폰트 도구가
> 메뉴 2개를 더하자 8개가 되어 즉시 재발했다. 그래서 **개별 대응을 그만두고 프로젝트의
> `[MenuItem]` 9개 전부를 감쌌다** — 리눅스에서 0개면 임계와 무관해진다.
>
> Windows 에디터는 영향받지 않는다. 메서드는 남으므로 CI에서 필요하면
> `-executeMethod Game.Editor.<클래스>.<메서드>`로 부른다.
>
> **재발 신호** — 매 실행의 `Detect editor crash` 스텝이 크래시 스택을 경고로 띄운다.
> 이 경고가 뜨면 가드 없는 메뉴가 새로 들어온 것이다.
> 경위는 [자동화 1차 구현 계획](../docs/plans/features/자동화-1차-구현-계획.md) §1.8.
- **선행 조건**: 저장소 시크릿 `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` 등록.
  상세는 [.github/workflows/README.md](../.github/workflows/README.md) 참고.
- 로컬 CLI 빌드: `Game.Editor.BuildScript.PerformWindowsBuild` (결과물 `Builds/StandaloneWindows64/`).

## 5. 도구

- **Unity MCP** (`com.coplaydev.unity-mcp`): Claude Code가 에디터를 직접 조작(씬·컴포넌트 편집, 콘솔 확인 등)할 수 있게 하는 브리지. 에디터 실행 중에만 동작한다.
- **클론 직후 1회 실행** (커밋 템플릿 · LFS · 씬/프리팹 머지 드라이버): [README.md](../README.md#시작하기) 참고.
