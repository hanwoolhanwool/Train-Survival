using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Game.Editor
{
    /// <summary>
    /// 성능 벤치 원클릭 실행기 — 빌드 → N회 주행 → 비교 → 리포트 (성능 프로파일링 자동화 계획 2.3).
    ///
    /// <para><b>이 창은 오케스트레이터일 뿐이다.</b> 판정 규칙은 <c>tools/perf/gates.js</c> 한 곳에
    /// 있고, 측정은 빌드 Player 안의 <c>PerfRunner</c>가 한다. 여기서 임계를 다시 정의하면
    /// CLI와 에디터가 다른 답을 내게 된다.</para>
    ///
    /// <para>출력은 명령에 대한 응답이라 카테고리 필터에 걸리면 안 되므로
    /// 규약(architecture-rules.md §3 "예외 둘")에 따라 <see cref="Debug"/>를 그대로 쓴다.</para>
    /// </summary>
    public sealed class PerfBenchWindow : EditorWindow
    {
        private const string DefaultScenario = "forest-day-60s";
        private const string BuildPath = "Builds/StandaloneWindows64/TrainSurvival.exe";
        private const int SmokeSeconds = 30;

        /// <summary>주행 하나가 이 시간을 넘기면 죽은 것으로 본다 — 60초 측정 + 부팅·로딩 여유.</summary>
        private const int RunTimeoutMs = 5 * 60 * 1000;

        private string _scenario = DefaultScenario;
        private int _repeats = 3;
        private int _width = 1920;
        private int _height = 1080;
        private bool _rebuild = true;
        private bool _busy;
        private Vector2 _scroll;
        private string _log = "아직 실행하지 않았다.";

        // 리눅스 에디터(= CI)에서는 등록하지 않는다 — 메뉴 항목 수가 임계를 넘으면
        // PlayMode 진입에서 세그폴트가 난다 (자동화 1차 구현 계획 §1.8).
#if !UNITY_EDITOR_LINUX
        [MenuItem("Game/QA/Performance")]
#endif
        private static void Open()
        {
            var window = GetWindow<PerfBenchWindow>("성능 벤치");
            window.minSize = new Vector2(560f, 420f);
        }

        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "빌드를 굽고 시나리오를 반복 주행한 뒤 기준선과 비교해 리포트를 남긴다.\n" +
                "측정 중에는 다른 앱을 닫아야 한다 — 이 PC의 다른 부하가 그대로 수치에 섞인다.",
                MessageType.None);

            using (new EditorGUI.DisabledScope(_busy))
            {
                _scenario = EditorGUILayout.TextField("시나리오", _scenario);
                _repeats = EditorGUILayout.IntSlider("반복 횟수", _repeats, 1, 5);

                using (new EditorGUILayout.HorizontalScope())
                {
                    _width = EditorGUILayout.IntField("해상도", _width);
                    _height = EditorGUILayout.IntField(_height, GUILayout.Width(80f));
                }

                _rebuild = EditorGUILayout.Toggle(
                    new GUIContent("개발 빌드를 다시 굽는다",
                        "끄면 기존 산출물을 그대로 쓴다. 코드를 고쳤다면 반드시 켜야 한다."),
                    _rebuild);

                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("스모크만 (30초)", GUILayout.Height(28f)))
                    {
                        RunSmoke();
                    }

                    if (GUILayout.Button($"벤치 {_repeats}회 + 리포트", GUILayout.Height(28f)))
                    {
                        RunBenchmark();
                    }
                }

                EditorGUILayout.Space();

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("기준선 갱신"))
                    {
                        PromoteBaseline();
                    }

                    if (GUILayout.Button("결과 폴더 열기"))
                    {
                        string dir = Path.Combine(ProjectRoot, "Perf");
                        Directory.CreateDirectory(dir);
                        EditorUtility.RevealInFinder(dir);
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("실행 기록", EditorStyles.boldLabel);

            using (var scope = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scope.scrollPosition;
                EditorGUILayout.TextArea(_log, GUILayout.ExpandHeight(true));
            }
        }

        private void RunSmoke()
        {
            _busy = true;
            var log = new StringBuilder();
            if (!PrepareBuild(log))
            {
                Finish(log);
                return;
            }

            log.AppendLine($"스모크 {SmokeSeconds}초 —");
            int exitCode = RunPlayer(
                new[]
                {
                    "-smoke", SmokeSeconds.ToString(),
                    "-screen-width", _width.ToString(),
                    "-screen-height", _height.ToString(),
                    "-screen-fullscreen", "0",
                },
                "smoke",
                log);

            log.AppendLine(exitCode == 0
                ? "통과 — 빌드가 인게임까지 들어가 살아 있었다."
                : $"실패 (종료 코드 {exitCode}) — 로그를 확인해야 한다. 벤치를 돌릴 상태가 아니다.");

            Finish(log);
        }

        private void RunBenchmark()
        {
            _busy = true;
            var log = new StringBuilder();
            if (!PrepareBuild(log))
            {
                Finish(log);
                return;
            }

            // 스모크가 실패하는 빌드는 벤치를 돌릴 필요가 없다 — 순서상 스모크가 먼저다 (§4.8).
            log.AppendLine("사전 스모크 —");
            int smoke = RunPlayer(
                new[] { "-smoke", "15", "-screen-width", _width.ToString(), "-screen-height", _height.ToString(), "-screen-fullscreen", "0" },
                "smoke",
                log);

            if (smoke != 0)
            {
                log.AppendLine($"스모크 실패 (종료 코드 {smoke}) — 벤치를 건너뛴다.");
                Finish(log);
                return;
            }

            var runPaths = new List<string>();
            for (int i = 1; i <= _repeats; i++)
            {
                string relative = $"Perf/runs/{_scenario}-{DateTime.Now:yyyyMMdd-HHmmss}-{i}.json";
                log.AppendLine($"벤치 {i}/{_repeats} —");

                int exitCode = RunPlayer(
                    new[]
                    {
                        "-perfrun", _scenario,
                        "-perfout", relative,
                        "-screen-width", _width.ToString(),
                        "-screen-height", _height.ToString(),
                        "-screen-fullscreen", "0",
                    },
                    $"perfrun-{i}",
                    log);

                if (exitCode != 0)
                {
                    log.AppendLine($"주행 실패 (종료 코드 {exitCode}) — 중단한다.");
                    Finish(log);
                    return;
                }

                runPaths.Add(relative);
            }

            log.AppendLine();
            log.AppendLine("비교 —");
            RunNode("tools/perf/compare.js", runPaths, log);

            log.AppendLine("리포트 —");
            RunNode("tools/perf/report.js", runPaths, log);

            Finish(log);
        }

        /// <summary>
        /// 최근 실행 하나를 기준선으로 올린다. <b>이 창은 사유를 묻지 않는다</b> —
        /// 기준선 갱신은 커밋 메시지에 왜 올랐는지/내렸는지를 적는 것이 규약이다(§7).
        /// </summary>
        private void PromoteBaseline()
        {
            string runsDir = Path.Combine(ProjectRoot, "Perf", "runs");
            if (!Directory.Exists(runsDir))
            {
                _log = "Perf/runs 가 없다 — 먼저 벤치를 돌려야 한다.";
                return;
            }

            string picked = EditorUtility.OpenFilePanel("기준선으로 올릴 실행 결과", runsDir, "json");
            if (string.IsNullOrEmpty(picked))
            {
                return;
            }

            string target = Path.Combine(ProjectRoot, "Perf", "baseline", $"{_scenario}.json");
            Directory.CreateDirectory(Path.GetDirectoryName(target) ?? ".");

            if (File.Exists(target) && !EditorUtility.DisplayDialog(
                    "기준선을 덮어쓴다",
                    $"{_scenario} 의 기준선을 이 실행으로 바꾼다.\n\n" +
                    "나빠진 뒤 갱신하면 게이트가 무력화된다 — 커밋 메시지에 사유를 반드시 적는다.",
                    "덮어쓴다", "취소"))
            {
                return;
            }

            File.Copy(picked, target, true);
            _log = $"기준선 갱신 — {target}\n커밋 메시지에 갱신 사유를 적을 것.";
        }

        private bool PrepareBuild(StringBuilder log)
        {
            string exe = Path.Combine(ProjectRoot, BuildPath);

            if (!_rebuild)
            {
                if (File.Exists(exe))
                {
                    log.AppendLine($"기존 빌드를 쓴다 — {File.GetLastWriteTime(exe):yyyy-MM-dd HH:mm}");
                    return true;
                }

                log.AppendLine("빌드 산출물이 없다 — 다시 굽는다.");
            }

            log.AppendLine("개발 빌드를 굽는다 (카운터가 채워지려면 개발 빌드여야 한다) —");
            try
            {
                BuildScript.PerformPerfBuild();
                log.AppendLine("빌드 완료.");
                return true;
            }
            catch (Exception exception)
            {
                log.AppendLine($"빌드 실패 — {exception.Message}");
                return false;
            }
        }

        private int RunPlayer(string[] arguments, string logTag, StringBuilder log)
        {
            string exe = Path.Combine(ProjectRoot, BuildPath);
            if (!File.Exists(exe))
            {
                log.AppendLine($"산출물이 없다 — {exe}");
                return -1;
            }

            string logPath = Path.Combine(ProjectRoot, "Perf", "logs", $"{logTag}-{DateTime.Now:HHmmss}.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? ".");

            // 플레이어의 표준 출력은 버린다 — boot.config 덤프 수십 줄이 창을 뒤덮고,
            // 정작 필요한 판정 로그는 -logFile 쪽에 있다.
            var argumentList = new List<string>(arguments) { "-logFile", logPath };
            int exitCode = Execute(exe, argumentList, ProjectRoot, log, RunTimeoutMs, captureOutput: false);

            // 주행이 스스로 남긴 판정 로그만 뽑아 온다 — 전체 로그는 파일에 있다.
            if (File.Exists(logPath))
            {
                foreach (string line in File.ReadAllLines(logPath))
                {
                    if (line.Contains("[Performance/"))
                    {
                        log.AppendLine($"  {line.Trim()}");
                    }
                }
            }

            return exitCode;
        }

        private void RunNode(string script, IReadOnlyList<string> runPaths, StringBuilder log)
        {
            var arguments = new List<string> { script };
            arguments.AddRange(runPaths);

            // python 은 이 환경에서 Store 스텁이라 쓰지 않는다 — 도구는 전부 node 다.
            Execute("node", arguments, ProjectRoot, log, RunTimeoutMs);
        }

        private static int Execute(
            string fileName, IReadOnlyList<string> arguments, string workingDirectory,
            StringBuilder log, int timeoutMs, bool captureOutput = true)
        {
            var info = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,

                // 도구 출력이 한국어다. 지정하지 않으면 시스템 ANSI 코드페이지로 읽어
                // "시나리오"가 "?쒕굹由ъ삤"로 깨진다.
                StandardOutputEncoding = new UTF8Encoding(false),
                StandardErrorEncoding = new UTF8Encoding(false),
            };

            foreach (string argument in arguments)
            {
                info.ArgumentList.Add(argument);
            }

            try
            {
                using (var process = new Process { StartInfo = info })
                {
                    // 두 스트림을 순서대로 ReadToEnd 하면 교착한다 — stdout 을 읽는 동안 자식이
                    // stderr 파이프를 가득 채우면 자식은 쓰기에서, 이쪽은 읽기에서 서로를 기다린다.
                    // 플레이어는 부팅 로그를 stdout 으로 수십 줄 쏟으므로 실제로 걸릴 수 있는 경로다.
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();

                    process.OutputDataReceived += (_, e) =>
                    {
                        if (e.Data != null)
                        {
                            stdout.AppendLine(e.Data);
                        }
                    };

                    process.ErrorDataReceived += (_, e) =>
                    {
                        if (e.Data != null)
                        {
                            stderr.AppendLine(e.Data);
                        }
                    };

                    if (!process.Start())
                    {
                        log.AppendLine($"프로세스를 시작하지 못했다 — {fileName}");
                        return -1;
                    }

                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();

                    if (!process.WaitForExit(timeoutMs))
                    {
                        process.Kill();
                        log.AppendLine($"{timeoutMs / 1000}초 안에 끝나지 않아 강제 종료했다.");
                        return -1;
                    }

                    // 인자 없는 WaitForExit — 비동기 읽기가 끝까지 배출되기를 기다린다.
                    process.WaitForExit();

                    if (captureOutput)
                    {
                        AppendTrimmed(log, stdout.ToString());
                    }

                    AppendTrimmed(log, stderr.ToString());
                    return process.ExitCode;
                }
            }
            catch (Exception exception)
            {
                log.AppendLine($"실행 실패 — {fileName}: {exception.Message}");
                return -1;
            }
        }

        private static void AppendTrimmed(StringBuilder log, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            foreach (string line in text.Split('\n'))
            {
                string trimmed = line.TrimEnd('\r');
                if (!string.IsNullOrWhiteSpace(trimmed))
                {
                    log.AppendLine(trimmed);
                }
            }
        }

        private void Finish(StringBuilder log)
        {
            _busy = false;
            _log = log.ToString();
            Debug.Log($"[Perf] 벤치 실행 종료\n{_log}");
            Repaint();
        }
    }
}
