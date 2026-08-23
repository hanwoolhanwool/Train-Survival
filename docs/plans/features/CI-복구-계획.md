# CI 복구 계획 — 55전 55패의 원인은 하나이고, 그 뒤는 아직 아무도 못 봤다

작성일: 2026-08-23 · 대상: `.github/workflows/ci.yml` · 저장소: `hanwoolhanwool/Train-Survival` (public) ·
상태: **원인 확정 · 2단계(워크플로 정리) 적용 완료 · 1단계(Secrets 등록)는 사용자 작업 대기** (2026-08-23)

> **한 줄 요약**: CI가 실패하는 이유는 코드도 테스트도 아니다. **Unity 라이선스 활성화에 필요한 세 값 중 둘이 없다.**
> 그리고 그 관문을 한 번도 넘은 적이 없기 때문에, **그 뒤의 모든 단계(에디터 기동·컴파일·테스트·빌드)는 단 한 번도 실행된 적이 없다.**
> 이 문서는 확정된 원인과, "고치고 나면 그때부터 처음 만나게 될" 위험을 분리해서 적는다.

---

## 0. 관측 사실

`gh run list` 기준 **총 55회 실행, 55회 실패, 성공 0회**. 최초 실행은 2026-07-18, 최근은 2026-08-22.
워크플로 파일은 도입 커밋(`c72d09e`) 이후 **한 번도 수정되지 않았다.**

실패는 시기에 따라 두 얼굴을 하고 있으나 뿌리는 같다.

| 시기 | 실행 시간 | 오류 메시지 | 원인 |
|---|---|---|---|
| 2026-07-18 ~ 08-21 01:42 | 8~15초 | `Missing Unity License File and no Serial was found.` | Secrets 자체가 하나도 없었다 |
| 2026-08-21 01:42 이후 | 1분 40초~2분 30초 | `No valid license activation strategy could be determined. Make sure to provide UNITY_EMAIL, UNITY_PASSWORD, and either a UNITY_SERIAL or UNITY_LICENSE.` | `UNITY_LICENSE`만 등록 · 나머지 둘이 없다 |

시간이 8초에서 100초로 늘어난 것은 **진전이 아니다.** 라이선스 검사 위치가 도커 이미지 pull 뒤로 밀린 것뿐이고,
관문 자체는 그대로 막혀 있다. 두 시기 모두 **Unity 에디터가 기동한 적이 없다.**

---

## 1. 확정 원인 — 라이선스 Secrets 미완성

`gh secret list` 결과:

```
UNITY_LICENSE   2026-08-21T01:42:48Z
```

**등록된 것은 이 하나뿐이다.** `UNITY_EMAIL`도 `UNITY_PASSWORD`도 없다.

GameCI의 활성화 전략 판정은 다음 순서로 이뤄지며, **어느 갈래든 EMAIL·PASSWORD를 함께 요구한다.**

| 전략 | 필요한 값 |
|---|---|
| Serial (Pro/Plus) | `UNITY_SERIAL` + `UNITY_EMAIL` + `UNITY_PASSWORD` |
| Personal `.ulf` | `UNITY_LICENSE` + `UNITY_EMAIL` + `UNITY_PASSWORD` |
| 라이선스 서버 | `UNITY_LICENSING_SERVER` |

셋 다 성립하지 않으므로 `activate.sh`가 즉시 종료하고, 그 결과가 `docker failed with exit code 1`이다.

`.github/workflows/README.md`는 **처음부터 세 개를 모두 등록하라고 정확히 적어두었다.**
문서가 틀린 게 아니라, 등록이 하나에서 멈춰 있다.

---

## 2. 배제된 가설 — 이건 원인이 아니다

CI가 100% 실패 중이면 "프로젝트 어딘가가 CI와 안 맞는다"고 의심하기 쉽다. 아래는 실제로 확인해서 **배제한** 것들이다.
고치는 데 시간을 쓰지 말 것.

| 의심 항목 | 확인 결과 |
|---|---|
| 도커 이미지가 없다 | **있다.** `unityci/editor:ubuntu-6000.5.3f1-linux-il2cpp-3` pull 성공 (로그에 `Status: Downloaded newer image`) |
| Windows 빌드용 이미지가 없다 | **있다.** `ubuntu-6000.5.3f1-windows-mono-3` 태그 Docker Hub에 존재 확인 |
| LFS가 막혔다 | **아니다.** `git lfs fetch` 정상 진행, 할당량 초과 메시지 없음 |
| Linux 대소문자 충돌 | **0건.** 추적 파일 전수 검사 |
| 줄바꿈(CRLF) 때문에 체크아웃이 dirty | **0건.** index에 CRLF/mixed 파일 없음 (`git ls-files --eol`) |
| git 태그가 없어서 versioning이 죽는다 | **아니다.** GameCI는 태그가 없으면 `0.0.<전체 커밋 수>`로 fallback 하고, shallow clone은 스스로 `--unshallow` 한다 |
| MCP 패키지가 배치모드에서 서버를 띄워 멈춘다 | **아니다.** `HttpAutoStartHandler`·`StdioBridgeHost` 등 8곳에 `Application.isBatchMode` 가드가 있다 |
| 테스트 코드가 Windows 경로/화면에 의존 | **아니다.** `Assets/_Project/Tests` 전체에 `Application.dataPath`·`Screen.*`·드라이브 경로 없음 |
| git 패키지 의존성이 흔들린다 | **아니다.** `packages-lock.json`에 두 git 패키지 모두 커밋 해시로 고정 |

---

## 3. 아직 아무도 못 본 구간 — 라이선스를 뚫은 뒤 처음 만날 위험

**중요**: 아래는 "확인된 결함"이 아니라 **"한 번도 실행되지 않아 검증 자체가 불가능했던 구간"**이다.
1단계를 고치면 CI는 처음으로 Unity를 기동하고, 그때 이것들이 순서대로 드러난다.
초록불이 한 번에 켜질 것을 기대하지 말고, 아래를 예상 실패 목록으로 들고 갈 것.

### 3.1 [높음] 애니메이션 FBX가 저장소에 없다 — CI 빌드는 로컬과 다른 게임이다

`.gitignore` 마지막 줄:

```
/[Aa]ssets/_Project/Art/Animations/Anim_Hold_*.fbx
```

Mixamo 약관(재배포 금지) 때문에 **의도적으로** 제외한 것이고, `.meta`만 커밋되어 있다.

| | 로컬 | CI 체크아웃 |
|---|---|---|
| `Anim_Hold_Gunplay.fbx` 외 2종 | 있다 | **없다** |
| `AC_Player.controller`의 클립 참조 | 정상 | **깨진다** |

결과: 컴파일은 통과하겠지만 에디터 콘솔에 임포트 경고가 쌓이고, **CI가 뱉는 빌드 산출물에는 파지 애니메이션이 없다.**
CI가 초록불이어도 그 산출물은 "우리가 만들고 있는 게임"이 아니다. 방향 결정이 필요하다 (§6 선택지 ②).

### 3.2 [중] PlayMode 테스트 3종이 Linux 컨테이너에서 검증된 적 없다

- `NgoNetworkSessionServiceTests` — NGO `UnityTransport` 경로를 실제로 태운다
- `PooledNetworkPrefabHandlerTests`
- `PoolManagerTests`

로컬 Windows 836/836은 **EditMode 기준**이다. 컨테이너에서 루프백 소켓 바인딩과 NGO 수명주기가 어떻게 도는지는 미지수다.
`testMode: all`이 이 셋을 강제로 태우므로, EditMode가 다 통과해도 여기서 잡이 붉어질 수 있다.

### 3.3 [중] 빌드 잡이 `BuildScript.PerformWindowsBuild`를 쓰지 않는다 — **2단계에서 해소**

`ci.yml`의 build 잡에는 `buildMethod`가 없다 → GameCI **기본 빌더**가 돈다.
반면 `Assets/_Project/Scripts/Editor/BuildScript.cs`는 `Builds/StandaloneWindows64/TrainSurvival.exe`로 굽고,
워크플로 README는 이 메서드를 CLI 빌드 경로로 안내한다.

| | 경로 | 사용처 |
|---|---|---|
| `BuildScript.PerformWindowsBuild` | `Builds/StandaloneWindows64/` | 로컬 CLI만 |
| GameCI 기본 빌더 | `build/StandaloneWindows64/` | CI만 |

**둘은 서로를 검증하지 않는다.** 로컬에서 되는 빌드가 CI에서 된다는 보장이 없고 그 반대도 마찬가지다.

> **해소** (2026-08-23) — `buildMethod`를 지정하고 `BuildScript`가 GameCI의 `-customBuildPath`를 읽게 했다. §4 2단계 참조.

### 3.4 [중] Library 캐시 키가 test 잡과 build 잡을 교차 오염시킨다 — **2단계에서 해소**

```yaml
key: Library-test-${{ hashFiles(...) }}
restore-keys: |
  Library-test-
  Library-        # ← 여기
```

마지막 `Library-`가 공통이라, build 잡이 test 잡의 `linux-il2cpp` Library를 복원하거나 그 반대가 일어난다.
서로 다른 이미지·다른 빌드 타겟의 Library를 물려받으면 **Unity가 전량 재임포트**한다.
잡이 죽지는 않지만 실행 시간이 수십 분 단위로 벌어지고, 그 실패는 원인을 찾기 어렵다.

> **해소** (2026-08-23) — 두 잡의 `restore-keys`에서 공통 `Library-`를 뺐다. 이제 잡별 접두어까지만 복원한다.

### 3.5 [중] LFS 대역폭 — 성공하기 시작하면 그때부터 소모된다

추적 중인 LFS 오브젝트 **232개 · 총 445.9 MB**. 두 잡 모두 `lfs: true`이므로 **한 번 실행에 약 892 MB**를 당긴다.
GitHub 무료 LFS 대역폭은 **월 1 GB**다.

지금까진 문제가 안 됐지만(현재 초과 메시지 없음), CI가 실제로 돌기 시작하면 **하루 한두 번 push로 한도에 닿는다.**
실측 후 대응이 필요하다 (§6 선택지 ③).

### 3.6 [낮] Node 20 deprecation 경고

`actions/checkout@v4`·`cache@v4`·`upload-artifact@v4`·`game-ci/unity-test-runner@v4` 전부 경고 대상.
지금은 Node 24로 강제 실행되어 동작하지만, 예고된 만료다.

---

## 4. 수정 계획

원인이 하나이므로 **1단계만이 진짜 수정**이고, 나머지는 "그 뒤에 만날 것"에 대한 준비다.
2단계까지는 되돌리기 쉬우니 먼저 하고, 3단계는 1·2단계 결과를 본 뒤에 판단한다.

### 1단계 — 라이선스 관문 통과 (이것만으로 CI가 처음 움직인다)

사용자가 직접 해야 하는 작업이다. 코드 변경 없음.

1. `.ulf` 확보 — [game.ci/docs/github/activation](https://game.ci/docs/github/activation) 절차대로
   `.alf` 생성 → [license.unity3d.com/manual](https://license.unity3d.com/manual) 수동 활성화 → `.ulf` 발급
   (이미 `UNITY_LICENSE`가 등록되어 있으므로, 그 값이 유효한 `.ulf` 전문인지 먼저 확인할 것)
2. Secrets 두 개 추가 — Settings → Secrets and variables → Actions
   - `UNITY_EMAIL` — Unity 계정 이메일
   - `UNITY_PASSWORD` — Unity 계정 비밀번호
3. 빈 커밋이나 수동 실행으로 CI를 한 번 태우고, **`Run tests` 단계에서 Unity 에디터 기동 로그가 나오는지**만 확인

**이 단계의 완료 기준은 "테스트 통과"가 아니다.** 라이선스 오류가 사라지고 에디터가 뜨는 것까지다.
그 다음 실패는 §3에서 예고한 것들이며, 그건 진전이다.

### 2단계 — 워크플로 정리 ✅ **적용 완료 (2026-08-23)**

`.github/workflows/ci.yml` · `Assets/_Project/Scripts/Editor/BuildScript.cs` 수정.

| 항목 | 이전 | 적용한 것 | 이유 |
|---|---|---|---|
| Library 캐시 `restore-keys` | 공통 `Library-` 포함 | 잡별 접두어(`Library-test-`/`Library-build-`)까지만 | §3.4 교차 오염 차단 |
| build 잡 checkout | `fetch-depth` 미지정 | `fetch-depth: 0` | Semantic versioning이 커밋 수를 센다. 없으면 액션이 스스로 `--unshallow` 하며 시간만 쓴다 |
| build 잡 `buildMethod` | 없음(GameCI 기본 빌더) | `Game.Editor.BuildScript.PerformWindowsBuild` | §3.3 로컬/CI 빌드 경로 일원화 |
| 테스트 실패 가시성 | 업로드만 | `Summarize failed tests` 스텝 (`if: failure()`) | 실패한 테스트 이름·메시지를 로그 끝에 `::error::`로 출력 |
| 트리거 범위 | 모든 push·PR | `paths-ignore`로 `docs/**`·`**/*.md`·`LICENSE`·`LICENSE-ASSETS` 제외 | §6 선택지 ④ 채택. 문서 커밋마다 LFS 892MB를 태우지 않는다 |

> `paths-ignore`는 **변경된 파일이 전부 패턴에 걸릴 때만** 건너뛴다. 코드가 한 파일이라도 섞이면 평소대로 돈다.
> `main`에 브랜치 보호가 걸려 있지 않아(확인함) 스킵된 실행이 머지를 막는 문제도 없다.
> GitHub Actions는 YAML 앵커를 지원하지 않으므로 목록을 push·pull_request 양쪽에 그대로 적었다.

**`BuildScript` 확장** — 커스텀 `buildMethod`를 쓰면 GameCI 기본 빌더의 인자 처리를 우리가 떠안게 된다.
확인 결과 GameCI는 `CUSTOM_BUILD_PATH="$BUILD_PATH_FULL/$BUILD_FILE"` 형태로
**확장자까지 포함한 절대 경로**를 `-customBuildPath`에 넘긴다 (`buildsPath`=`build`, `buildName`=`targetPlatform`).

- `-customBuildPath`를 읽고, 없으면 기존 `Builds/StandaloneWindows64/TrainSurvival.exe`를 쓴다 → **로컬 CLI 동작 무변경**
- 확장자가 빠져 오는 경우에도 `.exe`를 보장한다
- `-buildVersion`을 `PlayerSettings.bundleVersion`에 반영한다 (기본 빌더가 하던 일). `none`은 거른다
- 켜진 씬이 0개면 즉시 실패시킨다. 빌드 요약(결과·크기·시간·오류/경고 수)을 `Debug.Log`로 남긴다
  — CLI 툴의 결과 출력이므로 [architecture-rules.md](../../conventions/architecture-rules.md) §3 "예외 둘"에 해당해 `GameLog`를 쓰지 않는다

**검증한 것**

| 대상 | 방법 | 결과 |
|---|---|---|
| YAML 문법·구조 | `js-yaml` 파싱 후 스텝/키 값 확인 | `restore-keys`·`fetch-depth: 0`·`buildMethod`·`if: failure()` 모두 의도대로 |
| 요약 스크립트 로직 | 가짜 NUnit XML로 실행 | 실패 케이스 2건 추출 · 자기닫힘 태그 처리 · `Passed` 제외 · 메시지 첫 줄 표시 |
| heredoc 들여쓰기 | 워크플로에서 run 블록을 추출해 bash로 실행 | 정상 (YAML 블록 들여쓰기 제거 후에도 `NODE_EOF` 인식) |
| XML 부재 경로 | 빈 디렉터리에서 실행 | `::warning::` 출력 후 exit 0 — 잡 실패 원인을 덮지 않는다 |
| `BuildScript.cs` | Unity `validate_script`(standard) + 실제 컴파일 | 진단 0건 · 도메인 리로드 완료 · 콘솔 오류 0건 |

**아직 검증되지 않은 것** — 위는 전부 로컬 검증이다.
`buildMethod` 경로가 실제 GameCI 컨테이너에서 도는 것은 **1단계 이후에만 확인할 수 있다.**

### 3단계 — 1·2단계 결과를 보고 판단

CI가 실제로 돌기 시작한 뒤에만 의미가 있는 작업이다. **지금 미리 하지 말 것.**

- PlayMode 테스트가 컨테이너에서 죽으면 → 원인 규명 후 수정하거나, `testMode`를 분리해 EditMode를 먼저 초록불로 만든다
- 빌드 잡이 애니메이션 없이 나오는 것을 어떻게 다룰지 결정 (§6 선택지 ②)
- LFS 대역폭 실측 후 대응 (§6 선택지 ③)
- 액션 버전 갱신 (§3.6)

---

## 5. 완료 기준

단계별로 나눈다. **한 번에 전부 초록불이 되는 것을 목표로 삼지 않는다.**

| 차수 | 기준 |
|---|---|
| **1차** | 라이선스 오류가 사라지고 CI 로그에 Unity 에디터 기동이 찍힌다 |
| **2차** | `Test (EditMode + PlayMode)` 잡이 통과하고 `test-results` 아티팩트가 실제로 올라온다 (지금은 `No files were found`) |
| **3차** | `Build (StandaloneWindows64)` 잡이 통과하고 산출물이 아티팩트로 올라온다 |
| **4차** | main push 기준 연속 3회 초록불 — 그때 비로소 "CI가 있다"고 말할 수 있다 |

---

## 6. 결정이 필요한 선택지

계획을 실행하기 전에 방향을 정해야 하는 지점들이다. 각 항목에 권장안을 적었다.

### ① 라이선스 방식

| 안 | 내용 | 비고 |
|---|---|---|
| **A (권장)** | Personal `.ulf` + EMAIL/PASSWORD | 이미 `UNITY_LICENSE`가 등록되어 있어 둘만 추가하면 된다 |
| B | Pro/Plus `UNITY_SERIAL` 방식 | 유료 구독 보유 시에만 |

### ② 애니메이션 FBX 부재 (§3.1)

| 안 | 내용 | 대가 |
|---|---|---|
| **A (권장)** | 그대로 두고, **CI 빌드는 "컴파일·테스트 검증용"으로만 규정**한다 | 배포 가능한 빌드는 로컬에서만 나온다. 문서에 명시 필요 |
| B | FBX를 LFS로 커밋한다 | 약관 위반 소지 — 저장소가 public이라 사실상 재배포다 |
| C | 프라이빗 서브모듈/외부 스토리지에서 CI가 내려받는다 | 구성 비용이 크고, 이번 복구의 범위를 넘는다 |

### ③ LFS 대역폭 (§3.5)

| 안 | 내용 |
|---|---|
| **A (권장)** | 1단계 후 실제 소모를 측정하고 나서 판단 — 지금은 추정치일 뿐이다 |
| B | test 잡의 `lfs: true`를 끈다 — EditMode 테스트는 바이너리 에셋이 필요 없다 |
| C | LFS 대역폭 구매 |

### ④ 트리거 범위 — **A 채택 · 적용 완료 (2026-08-23)**

지금은 `main`/`develop`의 모든 push에서 돈다. 최근 실패 55건 중 **상당수가 `docs(...)` 커밋**이다.
문서만 바뀐 커밋에 Unity 빌드를 통째로 돌리는 것은 LFS 대역폭과 실행 시간을 그냥 태우는 일이다.

| 안 | 내용 |
|---|---|
| **A (채택)** | `paths-ignore`로 `docs/**`, `*.md`를 제외한다 |
| B | 지금대로 둔다 |

---

## 7. 참고

- 실패 로그 원본: `gh run view 32604584379 --log-failed` (최근) · `gh run view 29640055046` (최초)
- 워크플로 설정 안내: [.github/workflows/README.md](../../../.github/workflows/README.md) — **내용은 정확하다. 따르지 않았을 뿐이다**
- GameCI 활성화 절차: https://game.ci/docs/github/activation
