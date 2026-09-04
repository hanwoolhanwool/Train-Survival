# Unity 이슈 리포트 초안 — 리눅스 batchmode에서 `[MenuItem]` 수가 PlayMode 진입을 죽인다

작성일: 2026-09-04 · 상태: **미제출 (초안)** ·
경위: [`자동화 1차 구현 계획`](../plans/features/자동화-1차-구현-계획.md) §1.8 · §1.9 ·
회피책: [`Assets/SETUP.md`](../../Assets/SETUP.md) §4

> **이 문서는 무엇인가** — Unity Bug Reporter에 넣을 본문 초안이다. 아래 영어 블록을 그대로
> 복사해 제출하면 된다. 한국어 부분은 제출 대상이 아니라 우리 쪽 메모다.
>
> **왜 리포트할 값이 있는가** — 이 버그 하나로 이 저장소의 CI가 **아흐레 전면 실패**했고,
> 원인이 에디터 메뉴라는 것을 알아내는 데 CI 왕복 7회가 들었다. 증상(PlayMode 진입 시 세그폴트)과
> 원인(메뉴 항목 수)의 거리가 멀어 **혼자서는 연결하기 어렵다.** 같은 구성의 CI를 쓰는 팀이라면
> 같은 벽을 만난다.
>
> **제출 방법** — 에디터에서 `Help > Report a Bug`. 리눅스 에디터가 없으면
> [웹 폼](https://unity.com/releases/editor/qa/bug-reporting)으로도 낼 수 있다.
> 재현 프로젝트 첨부가 요구되면 아래 §"Minimal repro"의 절차대로 빈 프로젝트를 만들어 붙인다.

---

## 제출 전 확인할 것 (우리 쪽 메모)

- [ ] **빈 프로젝트에서 재현을 확인했는가** — 아래 본문은 *우리 프로젝트에서* 메뉴 수만 바꿔가며
      얻은 결과다. 빈 프로젝트 재현은 아직 해 보지 않았고, 본문에도 그렇게 적어 두었다.
      Unity QA가 요구하면 그때 만든다(30분 이내 예상).
- [ ] 저장소를 계속 public으로 둘 것인가 — 본문이 공개 CI 실행 로그를 증거로 링크한다.
      비공개로 바꾸면 링크가 죽으므로, 그때는 로그를 파일로 첨부해야 한다.
- [ ] Unity 버전을 올렸다면 **먼저 재현 여부부터 다시 확인한다.** 이미 고쳐졌다면 리포트 불필요.

---

## 영어 본문 (여기서부터 복사)

### Title

```
[Linux][batchmode] Editor crashes with SIGSEGV when entering Play Mode if the project
defines 7 or more [MenuItem] entries (ScriptCommands::Rebuild -> MonoMenuItem)
```

### What happened

Running PlayMode tests in batchmode on the Linux editor crashes with `SIGSEGV` while the
editor rebuilds script menu commands after the domain reload that follows entering Play Mode.

The crash is **not caused by any particular menu**. It is triggered by the **number of
`[MenuItem]` entries** the project defines. In our project the threshold is **7**: with 6
entries the run passes, with 7 or 8 it crashes, and with 0 it passes again.

Neither the menu path nor its hierarchy matters. Moving the 7th entry from a newly created
submenu (`Game/Art/...`) into an existing one (`Game/QA/...`) still crashes.

The same project, the same Unity version, and the same tests pass on the **Windows** editor
in batchmode (11/11 PlayMode tests, no crash), so this appears to be Linux-specific.

### Steps to reproduce

Observed in CI. In our project the only variable changed between runs was the number of
`[MenuItem]` attributes present in an Editor assembly.

1. Use the Linux editor `6000.5.3f1` in batchmode
   (docker image `unityci/editor:ubuntu-6000.5.3f1-linux-il2cpp-3`).
2. Have an Editor assembly that defines **7** `[MenuItem("...")]` static methods.
   Their bodies are irrelevant; ours are ordinary editor windows and asset utilities.
3. Have at least one PlayMode test in the project.
4. Run:
   ```
   Unity -batchmode -runTests -testPlatform playmode -projectPath <project> \
         -testResults results.xml -logFile playmode.log
   ```
5. The editor prints `#   Testing in playmode  #` and then crashes with `SIGSEGV`
   before any test result is written. Process exits with code 139.

Reducing the count to 6 (by removing one attribute, or by wrapping it in
`#if !UNITY_EDITOR_LINUX`) makes the same run pass.

`-nographics` does not change the outcome (the `gtk_main` frame disappears from the stack,
the crash does not).

### Actual result

Two distinct stacks were observed, both inside `ScriptCommands::Rebuild()` — one while
**looking up** a menu item, one while **destroying** one.

**Stack A — lookup path** (24 frames)

```
Caught fatal signal - signo:11 code:128 errno:0 addr:(nil)
#1  DoFindItem(core::basic_string_ref<char>, core::vector<core::unique_ptr<MenuItem>, core::allocator<core::unique_ptr<MenuItem>, 0ul> >*)
#2  MenuController::GetChecked(core::basic_string_ref<char>)
#3  ScriptCommands::Rebuild()
#4  ForceReloadScriptCommands()
#5  CallbackArray::Invoke()
#6  ProfilerCallbackInvoke<CallbackArray, &GlobalCallbacks::didReloadMonoDomain>::Invoke(char const*)
#7  MonoManager::FinalizeReload()
#8  ScriptingInitializer::FinalizeReload()
#9  RefreshInternalV2(AssetDatabase::UpdateAssetOptions, ScanFilter const&, InternalRefreshFlagsV2)
#10 StopAssetImportingV2Internal(AssetDatabase::UpdateAssetOptions, InternalRefreshFlagsV2, ScanFilter const*, char const*)
#11 EditorSceneManager::RestoreSceneBackups(core::vector<EditorSceneBackup, core::allocator<EditorSceneBackup, 0ul> >&, EditorSceneManager::PlayModeChange)
#12 PlayerLoopController::EnterPlayMode()
#13 PlayerLoopController::SetIsPlaying(bool)
```

**Stack B — destruction path** (26 frames)

```
Caught fatal signal - signo:11 code:1 errno:0 addr:0x563bc8068038
#1  MemoryManager::GetAllocator(MemLabelId const&)
#2  MemoryManager::TryDeallocateWithLabel(void*, MemLabelId, char const*, int)
#3  free_alloc_internal(void*, MemLabelId const&, char const*, int)
#4  MonoMenuItem::~MonoMenuItem()
#5  ScriptCommands::Rebuild()
#6  ForceReloadScriptCommands()
#7  CallbackArray::Invoke()
```

There are no managed frames in either stack, so this does not appear to originate from user
script code being invoked.

### Expected result

PlayMode tests run to completion regardless of how many `[MenuItem]` entries the project
defines.

### Environment

| | |
|---|---|
| Unity | `6000.5.3f1` (`c2eb47b3a2a9`) |
| Editor image | `unityci/editor:ubuntu-6000.5.3f1-linux-il2cpp-3`, digest `sha256:99d18f2b18cbfdb159f007849b7c977d8deee10eef8167abc9220984a38f1e21` |
| Host | GitHub Actions `ubuntu-24.04` |
| Test action | `game-ci/unity-test-runner@v4` (`0ff419b913a3630032cbe0de48a0099b5a9f0ed9`) |
| Render pipeline | URP 17.5 |
| Also installed | Netcode for GameObjects 2.13.0, Input System 1.19.0, Multiplayer Tools 2.2.9 |

Not reproducible on the Windows editor `6000.5.3f1` in batchmode with the same project and
the same commit.

### Evidence — public CI runs

Repository: <https://github.com/hanwoolhanwool/Train-Survival> (public).
The only difference between these runs is the number of `[MenuItem]` entries.

| `[MenuItem]` count | Result | Run |
|---:|---|---|
| 6 | pass | <https://github.com/hanwoolhanwool/Train-Survival/actions/runs/33802287518> |
| 7 | **SIGSEGV** (stack A) | <https://github.com/hanwoolhanwool/Train-Survival/actions/runs/33800486811> |
| 7, moved into an existing submenu | **SIGSEGV** | <https://github.com/hanwoolhanwool/Train-Survival/actions/runs/33801556129> |
| 7 | **SIGSEGV** (stack B) — note: the workflow shows green because the PlayMode step was temporarily marked `continue-on-error` while we were bisecting; the step itself exited 139 | <https://github.com/hanwoolhanwool/Train-Survival/actions/runs/33795638669> |
| 8 | **SIGSEGV** (stack A) | <https://github.com/hanwoolhanwool/Train-Survival/actions/runs/33815287667> |
| 0 | pass | <https://github.com/hanwoolhanwool/Train-Survival/actions/runs/33816316725> |

Full editor logs are attached to each run as the `Test-Results` artifact
(`playmode.log` contains the stack).

### Minimal repro

Not yet reduced to a blank project — the results above come from changing only the menu
count in a real project. If a minimal project is required, this should reproduce it:

1. New empty 3D (URP) project on `6000.5.3f1`.
2. `Assets/Editor/Menus.cs` with seven methods, each carrying a distinct
   `[MenuItem("Test/Item N")]` and an empty body.
3. `Assets/Tests/PlayMode/Smoke.cs` — one `[UnityTest]` that yields a single frame.
4. Run the batchmode command from "Steps to reproduce" on the Linux editor.
5. Remove one `[MenuItem]` and run again — expected to pass.

### Workaround

Compile the attributes out on the Linux editor, keeping the methods callable:

```csharp
#if !UNITY_EDITOR_LINUX
        [MenuItem("Game/QA/Something")]
#endif
        private static void Something() { ... }
```

We applied this to all 9 `[MenuItem]` entries in the project, which puts the Linux count at 0
and makes CI stable again. The methods remain reachable through `-executeMethod`.

## (여기까지 복사)

---

## 우리 쪽 후속

- 회피책은 코드에 들어가 있고 규칙으로 문서화돼 있다([`SETUP.md`](../../Assets/SETUP.md) §4).
  **리포트 결과와 무관하게 유지한다** — 고쳐진 버전으로 올릴 때 걷어낼 수 있는지 다시 본다.
- Unity가 재현 프로젝트를 요구하면 위 "Minimal repro" 절차대로 만든다. 그때는 **그 프로젝트에서
  임계가 정말 7인지** 먼저 확인해야 한다. 우리 임계는 이 프로젝트 구성에서 관측된 값이고,
  패키지·에디터 확장이 다른 프로젝트에서는 다를 수 있다.
- 리포트가 접수되면 이슈 번호를 이 문서 머리에 적는다.
