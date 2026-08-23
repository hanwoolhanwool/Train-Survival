# PC 빌드 계획 — StandaloneWindows64 1차

작성일: 2026-08-23 · 상태: **1차 완료 — 빌드 성공·기동 확인** (2026-08-23) · 브랜치: `build/pc-standalone-1st`
대상: `Builds/StandaloneWindows64/TrainSurvival.exe` (`.gitignore` 대상 — 산출물은 커밋하지 않는다)

> **이 계획이 하는 일**: 지금 저장소 상태 그대로 **실행 파일 하나를 뽑아 손에 쥐는 것**.
> 최적화·패키징·배포는 범위 밖이다. 완료 기준은 "굽혔는가 + 켜지는가" 둘뿐이다.
>
> **main 불가침**: 설정 변경·계획서·기록은 전부 이 브랜치에만 쌓는다. `main`에는 아무것도 올리지 않는다.

---

## 1. 왜 지금 굽는가

레벨 디자인 2차(숲 팔레트 10종)와 숲 시각 보강 2차까지 화면이 섰고,
[개발 방향](../../plans/README.md)이 "보여주는 화면 우선"으로 바뀐 뒤
**에디터 밖에서 한 번도 돌려 본 적이 없다.** 에디터에서만 도는 게임은
빌드 전용 실패(셰이더 스트리핑·리소스 누락·경로 대소문자·도메인 리로드 의존 코드)를
숨긴 채로 자란다. 그 격차를 지금 크기가 작을 때 확인한다.

---

## 2. 실측 현황 (2026-08-23)

| 항목 | 값 | 출처 |
|---|---|---|
| Unity | 6000.5.3f1 (설치 확인) | `C:/Program Files/Unity/Hub/Editor` |
| **StandaloneWindows64 백엔드 모듈** | **Mono 전용 — IL2CPP 미설치** | `PlaybackEngines/windowsstandalonesupport/Variations` 에 `*_mono` 만 존재 |
| 빌드 씬 | 4개 전부 enabled — `Boot`(0) · `Main` · `Game` · `Game_ArtTest` | `ProjectSettings/EditorBuildSettings.asset` |
| 인게임 진입 씬 | **`Game_ArtTest`** (`GameplaySceneRoute.Startup = ArtTest`) | `GameplaySceneRoute.cs` |
| productName / company | `Train Survival` / **`DefaultCompany`** | `ProjectSettings.asset` |
| bundleVersion | `0.1.0` | 〃 |
| 컬러스페이스 / 입력 | Linear(1) / Input System 전용(1) | 〃 |
| 정의 심볼(Standalone) | `STEAMWORKS_NET` | 〃 |
| 트랜스포트 기본값 | **UnityTransport 직결** — Steam은 실행 인자로만 켠다 | `ActiveTransportMode.cs` |
| 빌드 진입점 | `Game.Editor.BuildScript.PerformWindowsBuild` (CLI·CI 공용) | `Assets/_Project/Scripts/Editor/BuildScript.cs` |
| UI 기준 해상도 | 1920×1080 (CanvasScaler 전부) | `Main.unity` · `Ready_Screen.prefab` |

### 2.1 그대로 구우면 걸리는 것 — 화면·창 설정

| 설정 | 현재 | 왜 문제인가 |
|---|---|---|
| `fullscreenMode` | **1 = 전체화면 창(FullScreenWindow)** | 켜자마자 화면을 통째로 덮는다. 첫 검증에서 로그·에디터를 오갈 수 없다 |
| `defaultScreenWidth/Height` | **1024×768 (4:3)** | UI 기준은 16:9 1920×1080 — 4:3 창에서 레터박스·가장자리 잘림이 난다 |
| `runInBackground` | **0** | **호스트/클라 두 벌 동시 실행이 불가능하다.** 비활성 창이 멈추면 NGO 하트비트가 끊겨 접속이 죽는다 |
| `resizableWindow` | 0 | 창 크기를 못 바꾼다 — 두 벌을 나란히 놓고 볼 수 없다 |
| `forceSingleInstance` | 0 | **이대로 둔다.** 1이면 두 번째 인스턴스가 안 뜬다 |

---

## 3. 1차 결정

| # | 결정 | 근거 |
|---|---|---|
| ① | **스크립팅 백엔드는 Mono 유지** | IL2CPP 모듈이 설치돼 있지 않다. 설치는 수 GB 내려받기 + 빌드 시간 수 배 — 첫 빌드 목적(켜지는가)에 필요 없다. 배포 시점에 다시 본다 |
| ② | **릴리스 빌드**(development 끔) + **LZ4 압축** | 첫 산출물이 곧 보여 줄 물건이다. 실행이 실패하면 그때 개발 빌드로 다시 굽는다 |
| ③ | **창 모드 1600×900 · 크기 조절 허용 · 백그라운드 실행** | §2.1 — 두 벌 띄워 협동을 확인하는 것이 이 빌드의 유일한 실사용 목적이다 |
| ④ | **`companyName`은 건드리지 않는다** | 바꾸면 세이브·설정 경로(`LocalLow/<company>/<product>`)가 통째로 이동해 기존 로컬 진행이 끊긴다. 이름 결정은 배포 차수로 이월(§7) |
| ⑤ | **씬 4개 전부 유지** | `Game`(프리미티브 편성)은 규격이 어긋난 채지만 QA 키 씬 토글이 참조한다. 빼면 런타임에 씬을 못 찾는다 |
| ⑥ | **`steam_appid.txt`(480)를 산출물 옆에 복사** | Steam 모드(`-steam` 인자)로 켤 때 필요하다. 기본 실행에는 영향 없다 |
| ⑦ | **버전은 `0.1.0` 그대로** | 첫 빌드다. 버전 규칙은 배포 차수에서 정한다 |

---

## 4. 차수

### 0차 — 사전 점검
- [x] 콘솔 에러 0건 · **플레이 모드가 아님** (플레이 중 컴파일은 NGO 소켓을 고아로 만든다)
- [x] EditMode 테스트 전량 통과
- [x] 워킹트리 상태 파악 — 이전 세션의 미커밋 변경은 **건드리지 않는다**(§6 0차)

### 1차 — 플레이어 설정 정비 (§3 결정 ③⑥)
- [x] `fullscreenMode` 3(창) · `defaultScreenWidth/Height` 1600×900 · `resizableWindow` 1 · `runInBackground` 1
- [x] 변경은 `ProjectSettings.asset` 한 파일에 국한 — diff가 커지면 되돌린다

### 2차 — 빌드 실행
- [x] 대상 `StandaloneWindows64` · 출력 `Builds/StandaloneWindows64/TrainSurvival.exe`
- [x] **에디터가 `Library`를 잡고 있으므로 CLI `-batchmode` 는 쓰지 않는다** — 열려 있는 에디터로 굽는다
- [x] 첫 빌드는 셰이더 컴파일로 오래 걸린다 — 진행 중 에디터를 건드리지 않는다

### 3차 — 산출물 확인
- [x] `TrainSurvival.exe` + `TrainSurvival_Data/` 생성, 용량 기록
- [x] 빌드 리포트의 오류 0 · 경고 수 기록
- [x] `steam_appid.txt` 복사
- [x] 실행 1회 — 창이 뜨고 `Boot → Main`(배너 화면)까지 가는가, `Player.log` 에 예외가 없는가

### 4차 — 기록
- [x] 이 문서 §6에 실행 결과를 적는다
- [x] 브랜치에 커밋 (`build:` / `chore(build):`) — **`main` 푸시 없음**

---

## 5. 리스크

| 리스크 | 징후 | 대응 |
|---|---|---|
| 빌드 중 에디터 조작 | 컴파일·도메인 리로드로 빌드가 깨진다 | 빌드 시작 후 다른 Unity 작업을 하지 않는다 |
| 셰이더 스트리핑 누락 | 실행 시 분홍색 머티리얼 | `Player.log` 확인 → URP 에셋의 셰이더 변형 설정 검토 (2차 이월) |
| 첫 실행에서 예외 | 창은 뜨는데 검은 화면 | `%LOCALAPPDATA%Low/DefaultCompany/Train Survival/Player.log` 확인 |
| 산출물 커밋 사고 | `Builds/` 가 스테이징된다 | `.gitignore` 에 `/[Bb]uilds/` 존재 — 커밋 전 `git status` 확인 |
| 용량 | LFS 아트가 들어가 수 GB | 이번 차수는 기록만 한다 |

---

## 6. 실행 기록

### 0차 — 사전 점검 (2026-08-23)

| 항목 | 결과 |
|---|---|
| 콘솔 에러 | **0건** |
| EditMode | **884 / 884 통과** (9.2초, 실패·스킵 0) |
| 플레이 모드 | 아님 (`EditorApplication.isPlaying = false`) |
| 워킹트리 | 이전 세션의 미커밋 변경 6개 존재 — **손대지 않는다**(다른 세션 작업물). 이 차수의 커밋은 계획서 + `ProjectSettings.asset` 뿐 |

### 1차 — 설정 정비

`PlayerSettings` API로 바꿨다(에디터가 파일을 들고 있어 YAML 직접 편집은 덮어써진다).
`ProjectSettings.asset` **5줄** 외에는 diff가 없다.

| 설정 | 이전 | 이후 |
|---|---|---|
| `fullScreenMode` | FullScreenWindow | **Windowed** |
| 기본 해상도 | 1024×768 | **1600×900** |
| `resizableWindow` | False | **True** |
| `runInBackground` | False | **True** |
| 백엔드(확인만) | Mono2x | Mono2x 유지 |

### 2차 — 빌드 (job `build-5ed9736085`)

| 항목 | 값 |
|---|---|
| 결과 | **Succeeded** |
| 소요 | **217.2초** (3분 37초) |
| 크기 | **282.51 MB** (`data.unity3d` 195 MB) |
| 오류 / 경고 | **0** / 34 |
| 산출물 | `TrainSurvival.exe` · `TrainSurvival_Data/` · `UnityPlayer.dll` · `MonoBleedingEdge/` · `D3D12/` |
| URP | `PC_RPAsset` 1종만 포함 (`PC_Renderer`) |

**경고 34건의 정체** — 전부 컴파일 경고이고 빌드 고유 문제는 없다.

- **CS0618** 다수 — `RpcAttribute.RequireOwnership` 폐기(NGO 2.x가 `InvokePermission` 으로 이전).
  `QaDebugHotkeys` 15건 · `DayCycleController` 3건 · `TrainStorage` 2건 · `CraftingStation`·`EngineFuelPort` 각 1건
- **CS0618** — `FindObjectsSortMode` / `FindFirstObjectByType` 폐기 (`CarViewAnchor`·`QaDebugHotkeys`·`MonoSingleton`)
- **CS0114** — `TrainElevationController.OnDestroy()` 가 `NetworkBehaviour.OnDestroy()` 를 가린다.
  **잠재 결함** — base 호출이 없으면 NGO 정리가 건너뛰어진다. §7로 이월
- **CS0108** — `MenuPlateButton.IsHighlighted` 가 `Selectable.IsHighlighted()` 를 가린다

### 3차 — 산출물 확인

- `steam_appid.txt`(480) 산출물 폴더에 복사 완료
- `Train Survival_BurstDebugInformation_DoNotShip/` (292 KB) 생성 — **배포 시 제외 대상**(§7)

**실행 검증 2회** (`-logFile run1.log` / `run2.log`)

| 확인 | 결과 |
|---|---|
| 기동 | **성공** — 창 모드 1600×900, 제목 `Train Survival`, 크기 조절 가능 |
| 그래픽 | D3D12 [level 12.2] / RTX 4060 Ti — **분홍 머티리얼 없음**(셰이더 스트리핑 누락 없음) |
| 물리 | PhysX 4.1.2 선택 |
| 씬 흐름 | `Boot` → `Main` 도달 — 배너 화면(방 만들기·참가하기·설정·종료)이 정상 렌더 |
| 렌더 확인 | 열차·오로라 배경, 명판 4장, 운행 공고, `v0.1.0` 표기, 입력 힌트(`선택`/`E 확인`)까지 전부 출력 |
| 한글 | TMP 폰트 정상 (모지바케 없음) |
| **로그 예외** | **0건** — 로그 41줄, `d3d12: failed to query info queue`(디버그 레이어 미설치 · 무해) 뿐 |
| 메모리 | 약 692 MB (기동 직후) |

> **미확인 — 이 차수의 범위 밖**: 인게임(`Game_ArtTest`) 진입, 두 벌 동시 실행 협동,
> Steam 모드(`-steam`) 기동. 빌드가 굽히고 켜지는 것까지가 1차 완료 기준이었다(§1).

---

## 7. 이월

- **IL2CPP 전환** — 배포 시점. 모듈 설치부터 필요하다
- **`companyName` 확정** — 세이브 경로 이동을 동반하므로 단독 차수로
- **아이콘·스플래시** — 지금은 Unity 기본
- **빌드 크기 다이어트** — 미사용 에셋·텍스처 압축. 화면이 확정된 뒤
- **CI 빌드 복구** — `.github/workflows/ci.yml` 은 이미 같은 `BuildScript` 를 타므로 시크릿만 살아나면 붙는다
- **`TrainElevationController.OnDestroy()` (CS0114)** — `NetworkBehaviour.OnDestroy()` 를 가린다.
  빌드 경고로 드러났고 **런타임 정리 누락 소지**가 있다. 빌드 차수가 아니라 열차 쪽에서 고칠 일
- **폐기 API 정리 (CS0618 · 30건)** — `RequireOwnership` → `InvokePermission`,
  `FindObjectsSortMode` 제거. 다음 NGO/Unity 갱신 전에 한 번에 치우는 편이 싸다
- **다음 빌드 차수에서 확인할 것** — 인게임 진입 · 두 벌 동시 실행 협동 · Steam 모드 기동
