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
  → Main (타이틀/메뉴 — 현재 플레이스홀더)
  → Game (인게임 — 현재 플레이스홀더)
```

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

- `.github/workflows/ci.yml` — GitHub Actions + [GameCI](https://game.ci/) 파이프라인:
  1. `game-ci/unity-test-runner` — EditMode + PlayMode 테스트 (`testMode: all`)
  2. `game-ci/unity-builder` — StandaloneWindows64 빌드 → 아티팩트 업로드
- **선행 조건**: 저장소 시크릿 `UNITY_LICENSE`/`UNITY_EMAIL`/`UNITY_PASSWORD` 등록.
  상세는 [.github/workflows/README.md](../.github/workflows/README.md) 참고.
- 로컬 CLI 빌드: `Game.Editor.BuildScript.PerformWindowsBuild` (결과물 `Builds/StandaloneWindows64/`).

## 5. 도구

- **Unity MCP** (`com.coplaydev.unity-mcp`): Claude Code가 에디터를 직접 조작(씬·컴포넌트 편집, 콘솔 확인 등)할 수 있게 하는 브리지. 에디터 실행 중에만 동작한다.
- **클론 직후 1회 실행** (커밋 템플릿 · LFS · 씬/프리팹 머지 드라이버): [README.md](../README.md#시작하기) 참고.
