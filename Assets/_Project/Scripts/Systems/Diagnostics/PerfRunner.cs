using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Game.Core.Diagnostics;
using Game.Core.Logging;
using Game.Core.Services;
using Game.Systems.Networking;
using Game.Systems.Networking.Lobby;
using Game.Utilities.Performance;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Systems.Diagnostics
{
    /// <summary>
    /// 자동 주행기. 실행 인자가 있을 때만 살아나 <b>Boot → Main → 인게임</b>을 스스로 통과하고,
    /// 정해진 시간을 버틴 뒤 종료 코드를 내고 죽는다.
    ///
    /// <para><b>두 모드가 같은 경로를 탄다</b> — <c>-smoke</c>는 "게임이 뜨긴 하나?"에,
    /// <c>-perfrun</c>은 거기에 더해 "느려졌나?"에 답한다. 다른 것은 재고 남기는가뿐이다(§4.8).</para>
    ///
    /// <para><b>Boot를 우회하지 않는다.</b> 이 프로젝트는 NetworkManager가 Boot 씬에 있어 인게임 씬을
    /// 직접 열 수 없다. 메뉴 UI가 아니라 서비스 계층(<see cref="INetworkSessionService"/> ·
    /// <see cref="ILobbyRoomService"/>)을 직접 부르므로 UI 변경에 흔들리지 않는다(§7).</para>
    /// </summary>
    public sealed class PerfRunner : MonoBehaviour
    {
        /// <summary>서비스 등록을 기다리는 한도(초). 넘으면 Boot 자체가 깨진 것이다.</summary>
        private const float ServiceWaitTimeout = 30f;

        /// <summary>인게임 씬 진입을 기다리는 한도(초). 넘으면 씬 등록·라우트·로딩이 깨진 것이다.</summary>
        private const float SceneEnterTimeout = 90f;

        private const int ExitSuccess = 0;
        private const int ExitFailure = 1;

        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        private PerfRunArgs _args;
        private PerfScenario _scenario;
        private PerfProbe _probe;
        private string _failure;

        /// <summary>
        /// 씬에 배치하지 않고 인자가 있을 때만 스스로 생성한다 — <b>씬 파일을 건드리지 않기 위함</b>이다.
        /// 씬 YAML 편집은 이 프로젝트에서 수천 줄짜리 재정렬 diff를 만들고, 벤치 하나 때문에
        /// Boot 씬이 매번 변경되면 그쪽 리뷰 비용이 벤치의 값을 넘는다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            PerfRunArgs args = PerfRunArgsResolver.Resolve(System.Environment.GetCommandLineArgs());
            if (!args.IsAutomatedRun)
            {
                return;
            }

            var host = new GameObject(nameof(PerfRunner));
            DontDestroyOnLoad(host);
            host.AddComponent<PerfRunner>()._args = args;
        }

        private void Start()
        {
            Application.logMessageReceived += OnLogMessage;
            StartCoroutine(Run());
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessage;
            _probe?.Dispose();
        }

        /// <summary>예외는 그 자리에서 주행을 실패로 만든다 — 살아만 있으면 통과가 되면 안 된다(§4.8).</summary>
        private void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception && _failure == null)
            {
                _failure = $"예외 발생 — {condition}";
            }
        }

        private IEnumerator Run()
        {
            GameLog.Info(LogCategory.Performance,
                $"자동 주행 시작 — 모드={_args.Mode} 시나리오={_args.Scenario ?? "(없음)"}");

            if (_args.Mode == PerfRunMode.Benchmark)
            {
                _scenario = PerfScenarioCatalog.Find(_args.Scenario);
                if (_scenario == null)
                {
                    Finish($"시나리오를 찾지 못했다 — '{_args.Scenario}' " +
                           $"({PerfScenarioCatalog.ResourceFolder} 아래에 에셋이 있어야 한다)");
                    yield break;
                }
            }

            ApplyDeterministicSettings();

            yield return WaitForSession();
            if (_failure != null)
            {
                Finish(_failure);
                yield break;
            }

            yield return EnterGameplay();
            if (_failure != null)
            {
                Finish(_failure);
                yield break;
            }

            if (_args.Mode == PerfRunMode.Smoke)
            {
                yield return Survive(_args.DurationSeconds);
                Finish(_failure);
                yield break;
            }

            yield return Measure();
            Finish(_failure);
        }

        /// <summary>
        /// 실행마다 같은 게임이 돌게 만든다(§4.4). 해상도·프레임률 상한을 여기서 못 박는 이유는
        /// <b>창 크기가 GPU 시간을 지배</b>하기 때문이다.
        /// </summary>
        private void ApplyDeterministicSettings()
        {
            Application.runInBackground = true;
            Application.targetFrameRate = -1;
            QualitySettings.vSyncCount = 0;

            if (_scenario == null)
            {
                return;
            }

            Random.InitState(_scenario.RandomSeed);

            if (!Application.isEditor)
            {
                Screen.SetResolution(_scenario.ScreenWidth, _scenario.ScreenHeight, FullScreenMode.Windowed);
            }
        }

        /// <summary>Boot의 서비스 등록을 기다렸다가 호스트로 세션을 연다.</summary>
        private IEnumerator WaitForSession()
        {
            float waited = 0f;
            while (!ServiceLocator.IsRegistered<INetworkSessionService>())
            {
                waited += Time.unscaledDeltaTime;
                if (waited > ServiceWaitTimeout)
                {
                    _failure = $"{ServiceWaitTimeout}초 안에 세션 서비스가 등록되지 않았다 — Boot 초기화가 끝나지 않았다.";
                    yield break;
                }

                yield return null;
            }

            var session = ServiceLocator.Get<INetworkSessionService>();
            if (!session.IsSessionActive && !session.StartHost())
            {
                _failure = "호스트 세션 시작에 실패했다 — 포트 점유 또는 트랜스포트 설정을 확인해야 한다.";
                yield break;
            }

            // 대기실 상태 — 메뉴가 하던 일을 그대로 한다. 없으면 인게임 진입 뒤 로스터가 비어 예외가 난다.
            if (ServiceLocator.TryGet(out ILobbyRoomService room))
            {
                room.Open();
            }

            GameLog.Info(LogCategory.Performance, "호스트 세션이 섰다.");
        }

        /// <summary>인게임 씬으로 넘어가고, 실제로 그 씬이 활성이 될 때까지 기다린다.</summary>
        private IEnumerator EnterGameplay()
        {
            string sceneName = _scenario != null ? _scenario.SceneName : GameplaySceneRoute.Name;
            var session = ServiceLocator.Get<INetworkSessionService>();

            if (!session.LoadGameplayScene(sceneName))
            {
                _failure = $"인게임 씬 전환 요청이 거부됐다 — scene={sceneName}";
                yield break;
            }

            float waited = 0f;
            while (SceneManager.GetActiveScene().name != sceneName)
            {
                waited += Time.unscaledDeltaTime;
                if (waited > SceneEnterTimeout)
                {
                    _failure = $"{SceneEnterTimeout}초 안에 인게임 씬에 들어가지 못했다 — scene={sceneName}";
                    yield break;
                }

                if (!session.IsSessionActive)
                {
                    _failure = "인게임 진입 도중 세션이 끊겼다 — 프리팹 해시 오염(ClosedByRemote)을 먼저 의심한다.";
                    yield break;
                }

                yield return null;
            }

            GameLog.Info(LogCategory.Performance, $"인게임 씬 진입 — {sceneName}");
        }

        /// <summary>스모크 — 재지 않고 버틴다. 그동안 예외나 세션 단절이 나면 실패로 끝난다.</summary>
        private IEnumerator Survive(float seconds)
        {
            var session = ServiceLocator.Get<INetworkSessionService>();
            float elapsed = 0f;

            while (elapsed < seconds)
            {
                if (_failure != null)
                {
                    yield break;
                }

                if (!session.IsSessionActive)
                {
                    _failure = $"주행 도중 세션이 끊겼다 — {elapsed:F1}초 지점.";
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            GameLog.Info(LogCategory.Performance, $"스모크 주행 완료 — {seconds:F0}초 생존.");
        }

        /// <summary>워밍업을 버리고 시나리오 길이만큼 수집한 뒤 결과를 남긴다.</summary>
        private IEnumerator Measure()
        {
            for (int i = 0; i < _scenario.WarmupFrames; i++)
            {
                if (_failure != null)
                {
                    yield break;
                }

                yield return null;
            }

            GameLog.Info(LogCategory.Performance,
                $"워밍업 {_scenario.WarmupFrames}프레임 폐기 — 측정 {_scenario.DurationSeconds:F0}초 시작.");

            var session = ServiceLocator.Get<INetworkSessionService>();
            _probe = new PerfProbe(Mathf.CeilToInt(_scenario.DurationSeconds * 240f));
            _probe.Start();

            float elapsed = 0f;
            while (elapsed < _scenario.DurationSeconds)
            {
                if (_failure != null)
                {
                    yield break;
                }

                if (!session.IsSessionActive)
                {
                    _failure = $"측정 도중 세션이 끊겼다 — {elapsed:F1}초 지점.";
                    yield break;
                }

                float delta = Time.unscaledDeltaTime;
                elapsed += delta;
                _probe.Sample(delta);
                yield return null;
            }

            if (_probe.SampleCount == 0)
            {
                _failure = "측정 프레임이 하나도 수집되지 않았다.";
                yield break;
            }

            WriteResult();
        }

        private void WriteResult()
        {
            PerfDistribution cpuMain = _probe.Describe(s => s.CpuMainMs);
            PerfDistribution cpuRender = _probe.Describe(s => s.CpuRenderMs);
            PerfDistribution gpu = _probe.Describe(s => s.GpuMs);

            double[] slowest = _probe.Collect(s => s.SlowestThreadMs);
            PerfDistribution slowestDistribution = PerfStats.Describe(slowest);
            List<PerfSpike> spikes = PerfStats.FindSpikes(_probe.Samples, slowestDistribution.P50);
            int framesOver = PerfStats.CountOver(slowest, PerfStats.SlowFrameMs);

            PerfBottleneck bottleneck = PerfStats.DetermineBottleneck(cpuMain.P50, cpuRender.P50, gpu.P50);

            string path = ResolveOutputPath();
            string json = BuildJson(cpuMain, cpuRender, gpu, spikes, framesOver, bottleneck);

            try
            {
                string directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(path, json, Encoding.UTF8);
            }
            catch (IOException exception)
            {
                _failure = $"결과 파일을 쓰지 못했다 — {path} ({exception.Message})";
                return;
            }

            GameLog.Info(LogCategory.Performance,
                $"측정 완료 — {_probe.SampleCount}프레임 · 병목={bottleneck} · 결과={path}");

            if (!_probe.FrameTimingAvailable)
            {
                GameLog.Warn(LogCategory.Performance,
                    "FrameTimingManager 가 값을 채우지 않았다 — Frame Timing Stats 가 꺼진 빌드다. " +
                    "GPU·스레드 시간은 전부 0이므로 병목 판정을 믿으면 안 된다.");
            }
        }

        private string ResolveOutputPath()
        {
            if (!string.IsNullOrEmpty(_args.OutputPath))
            {
                return _args.OutputPath;
            }

            string root = Path.GetDirectoryName(Application.dataPath);
            string stamp = System.DateTime.Now.ToString("yyyyMMdd-HHmmss", Invariant);
            return Path.Combine(root ?? ".", "Perf", "runs", $"{_scenario.ScenarioId}-{stamp}.json");
        }

        /// <summary>
        /// §4.5 스키마대로 조립한다. <c>JsonUtility</c>를 쓰지 않는 이유는 중첩 구조를 표현하려면
        /// 직렬화 전용 클래스가 십여 개 필요해지고, 그 클래스들이 스키마의 실제 출처가 되어
        /// 문서와 조용히 어긋나기 때문이다.
        /// </summary>
        private string BuildJson(
            PerfDistribution cpuMain,
            PerfDistribution cpuRender,
            PerfDistribution gpu,
            IReadOnlyList<PerfSpike> spikes,
            int framesOver33Ms,
            PerfBottleneck bottleneck)
        {
            PerfDistribution drawCalls = _probe.Describe(s => s.DrawCalls);
            PerfDistribution standardDrawCalls = _probe.Describe(s => s.StandardDrawCalls);
            PerfDistribution srpBatcherDrawCalls = _probe.Describe(s => s.SrpBatcherDrawCalls);
            PerfDistribution instancedDrawCalls = _probe.Describe(s => s.InstancedDrawCalls);
            PerfDistribution brgDrawCalls = _probe.Describe(s => s.BrgDrawCalls);
            PerfDistribution setPass = _probe.Describe(s => s.SetPassCalls);
            PerfDistribution triangles = _probe.Describe(s => s.Triangles);
            PerfDistribution shadowCasters = _probe.Describe(s => s.ShadowCasters);
            PerfDistribution gcAlloc = _probe.Describe(s => s.GcAllocBytes);
            PerfDistribution totalMemory = _probe.Describe(s => s.TotalUsedBytes);
            PerfDistribution textureMemory = _probe.Describe(s => s.TextureMemoryBytes);
            PerfDistribution meshMemory = _probe.Describe(s => s.MeshMemoryBytes);

            var builder = new StringBuilder(4096);
            builder.Append("{\n");
            builder.Append("  \"schemaVersion\": 1,\n");
            builder.Append($"  \"scenario\": \"{Escape(_scenario.ScenarioId)}\",\n");
            builder.Append($"  \"forcedConditions\": \"{Escape(_scenario.DescribeForcedConditions())}\",\n");

            // git 정보는 런타임에서 알 수 없다 — 실행기(2차 도구)가 채운다. 필드는 스키마 유지를 위해 남긴다.
            builder.Append("  \"git\": { \"sha\": \"\", \"branch\": \"\", \"dirty\": null },\n");

            builder.Append("  \"machine\": {");
            builder.Append($" \"gpu\": \"{Escape(SystemInfo.graphicsDeviceName)}\",");
            builder.Append($" \"driver\": \"{Escape(SystemInfo.graphicsDeviceVersion)}\",");
            builder.Append($" \"cpu\": \"{Escape(SystemInfo.processorType)}\",");
            builder.Append($" \"os\": \"{Escape(SystemInfo.operatingSystem)}\" }},\n");

            builder.Append("  \"build\": {");
            builder.Append($" \"unityVersion\": \"{Escape(Application.unityVersion)}\",");
            builder.Append($" \"development\": {(Debug.isDebugBuild ? "true" : "false")},");
            builder.Append($" \"buildGuid\": \"{Escape(Application.buildGUID)}\",");
            builder.Append($" \"date\": \"{System.DateTime.Now.ToString("s", Invariant)}\" }},\n");

            builder.Append("  \"config\": {");
            builder.Append($" \"warmupFrames\": {_scenario.WarmupFrames},");
            builder.Append($" \"durationSeconds\": {Number(_scenario.DurationSeconds)},");
            builder.Append($" \"resolution\": \"{Screen.width}x{Screen.height}\",");
            builder.Append($" \"frameTimingAvailable\": {(_probe.FrameTimingAvailable ? "true" : "false")} }},\n");

            builder.Append($"  \"frames\": {_probe.SampleCount},\n");
            builder.Append($"  \"bottleneck\": \"{bottleneck}\",\n");

            builder.Append("  \"median\": {\n");
            AppendDistribution(builder, "cpuMainMs", cpuMain, true);
            AppendDistribution(builder, "cpuRenderMs", cpuRender, true);
            AppendDistribution(builder, "gpuMs", gpu, true);
            AppendDistribution(builder, "drawCalls", drawCalls, false);
            AppendDistribution(builder, "drawCallsStandard", standardDrawCalls, false);
            AppendDistribution(builder, "drawCallsSrpBatcher", srpBatcherDrawCalls, false);
            AppendDistribution(builder, "drawCallsInstanced", instancedDrawCalls, false);
            AppendDistribution(builder, "drawCallsBrg", brgDrawCalls, false);
            AppendDistribution(builder, "setPassCalls", setPass, false);
            AppendDistribution(builder, "triangles", triangles, false);
            AppendDistribution(builder, "shadowCasters", shadowCasters, false);
            AppendDistribution(builder, "gcAllocPerFrameBytes", gcAlloc, false);
            AppendDistribution(builder, "totalUsedBytes", totalMemory, false);
            AppendDistribution(builder, "textureMemoryBytes", textureMemory, false);
            AppendDistribution(builder, "meshMemoryBytes", meshMemory, false);
            builder.Append($"    \"framesOver33ms\": {framesOver33Ms}\n");
            builder.Append("  },\n");

            builder.Append("  \"spikes\": [");
            for (int i = 0; i < spikes.Count; i++)
            {
                PerfSpike spike = spikes[i];
                builder.Append(i == 0 ? "\n" : ",\n");
                builder.Append($"    {{ \"frameIndex\": {spike.FrameIndex}, ");
                builder.Append($"\"timeSeconds\": {Number(spike.TimeSeconds)}, ");
                builder.Append($"\"ms\": {Number(spike.Milliseconds)} }}");
            }

            builder.Append(spikes.Count > 0 ? "\n  ]\n" : "]\n");
            builder.Append("}\n");

            return builder.ToString();
        }

        private static void AppendDistribution(
            StringBuilder builder, string name, PerfDistribution distribution, bool includeSpread)
        {
            builder.Append($"    \"{name}\": {{ \"p50\": {Number(distribution.P50)}, ");
            builder.Append($"\"p95\": {Number(distribution.P95)}, ");
            builder.Append($"\"p99\": {Number(distribution.P99)}, ");
            builder.Append($"\"max\": {Number(distribution.Max)}");

            if (includeSpread)
            {
                builder.Append($", \"mean\": {Number(distribution.Mean)}");
                builder.Append($", \"stdDev\": {Number(distribution.StandardDeviation)}");
            }

            builder.Append(" },\n");
        }

        private static string Number(double value)
        {
            return value.ToString("0.####", Invariant);
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        /// <summary>
        /// 판정은 종료 코드 하나다. 실패 사유는 콘솔로 내보내 <c>-logFile -</c> 실행에서
        /// 그대로 보이게 한다.
        /// </summary>
        private void Finish(string failure)
        {
            if (string.IsNullOrEmpty(failure))
            {
                GameLog.Info(LogCategory.Performance, "자동 주행 성공 — 종료 코드 0.");
                Application.Quit(ExitSuccess);
                return;
            }

            GameLog.Error(LogCategory.Performance, $"자동 주행 실패 — {failure}");
            Application.Quit(ExitFailure);
        }
    }
}
