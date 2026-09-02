using System;
using System.Globalization;

namespace Game.Utilities.Performance
{
    /// <summary>
    /// 벤치·스모크 실행 인자 해석 결과. 값 타입이라 파서 밖에서 변형될 수 없다.
    /// </summary>
    /// <remarks>
    /// 두 모드는 <b>같은 주행 경로</b>를 탄다 — 다른 것은 "재고 남기는가"뿐이다
    /// (성능 프로파일링 자동화 계획 §4.8).
    /// </remarks>
    public readonly struct PerfRunArgs
    {
        /// <summary>인자가 없을 때의 값 — 평범한 게임 실행이다.</summary>
        public static readonly PerfRunArgs None = default;

        private PerfRunArgs(PerfRunMode mode, string scenario, string outputPath, float durationSeconds)
        {
            Mode = mode;
            Scenario = scenario;
            OutputPath = outputPath;
            DurationSeconds = durationSeconds;
        }

        public PerfRunMode Mode { get; }

        /// <summary>시나리오 이름 (`-perfrun &lt;이름&gt;`). 스모크 모드에서는 null이다.</summary>
        public string Scenario { get; }

        /// <summary>결과 JSON 경로 (`-perfout &lt;경로&gt;`). 지정이 없으면 null — 실행기가 기본 경로를 만든다.</summary>
        public string OutputPath { get; }

        /// <summary>
        /// 스모크 주행 길이(초). `-smoke &lt;초&gt;`로 덮어쓸 수 있고, 생략하면
        /// <see cref="DefaultSmokeSeconds"/>다. 벤치 모드에서는 0 — 길이는 시나리오가 정한다(§1.4).
        /// </summary>
        public float DurationSeconds { get; }

        /// <summary>벤치·스모크 어느 쪽으로든 자동 주행해야 하는가.</summary>
        public bool IsAutomatedRun => Mode != PerfRunMode.None;

        internal static PerfRunArgs Benchmark(string scenario, string outputPath)
        {
            return new PerfRunArgs(PerfRunMode.Benchmark, scenario, outputPath, 0f);
        }

        internal static PerfRunArgs Smoke(float durationSeconds)
        {
            return new PerfRunArgs(PerfRunMode.Smoke, null, null, durationSeconds);
        }
    }

    /// <summary>주행 모드 — 인자가 결정한다.</summary>
    public enum PerfRunMode
    {
        /// <summary>평범한 게임 실행. 벤치 코드는 아무 일도 하지 않는다.</summary>
        None = 0,

        /// <summary>측정하고 JSON을 남긴다 (`-perfrun`).</summary>
        Benchmark = 1,

        /// <summary>측정 없이 인게임 진입과 생존만 확인한다 (`-smoke`).</summary>
        Smoke = 2,
    }

    /// <summary>
    /// 실행 인자 → 주행 모드 결정 순수 로직 (EditMode 대상).
    /// <see cref="Game.Utilities"/>에 두는 이유는 이 판정이 엔진·씬·네트워크를 전혀 모르기 때문이다 —
    /// <c>NetworkTransportModeResolver</c>와 같은 형태를 따른다.
    /// </summary>
    public static class PerfRunArgsResolver
    {
        public const string BenchmarkArgument = "-perfrun";
        public const string OutputArgument = "-perfout";
        public const string SmokeArgument = "-smoke";

        /// <summary>`-smoke`에 초를 붙이지 않았을 때의 주행 길이.</summary>
        public const float DefaultSmokeSeconds = 30f;

        /// <summary>스모크 주행 길이의 허용 범위 — 0초 주행과 무한 대기를 둘 다 막는다.</summary>
        public const float MinSmokeSeconds = 1f;

        public const float MaxSmokeSeconds = 600f;

        /// <summary>
        /// 인자를 해석한다. <b>둘 다 있으면 벤치가 이긴다</b> — 재라고 시킨 실행을 스모크로
        /// 강등시키면 결과 파일이 조용히 사라지고, 그 사실을 종료 코드로는 알 수 없다.
        /// </summary>
        public static PerfRunArgs Resolve(string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return PerfRunArgs.None;
            }

            string scenario = FindValue(args, BenchmarkArgument);
            if (scenario != null)
            {
                return PerfRunArgs.Benchmark(scenario, FindValue(args, OutputArgument));
            }

            if (!HasFlag(args, SmokeArgument))
            {
                return PerfRunArgs.None;
            }

            return PerfRunArgs.Smoke(ResolveSmokeSeconds(args));
        }

        /// <summary>`-smoke [초]` — 값이 없거나 숫자가 아니면 기본값. 범위를 벗어나면 잘라 낸다.</summary>
        public static float ResolveSmokeSeconds(string[] args)
        {
            string raw = FindValue(args, SmokeArgument);
            if (raw == null
                || !float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds))
            {
                return DefaultSmokeSeconds;
            }

            if (seconds < MinSmokeSeconds)
            {
                return MinSmokeSeconds;
            }

            return seconds > MaxSmokeSeconds ? MaxSmokeSeconds : seconds;
        }

        private static bool HasFlag(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// `&lt;이름&gt; &lt;값&gt;` 형태의 값을 찾는다. 뒤가 없거나 다음 인자가 또 다른 옵션(`-`로 시작)이면
        /// 값이 없는 것으로 본다 — `-smoke -perfout x` 를 "smoke 라는 이름의 값"으로 오해하지 않기 위함이다.
        /// </summary>
        private static string FindValue(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (i + 1 >= args.Length)
                {
                    return null;
                }

                string value = args[i + 1];
                return string.IsNullOrEmpty(value) || value[0] == '-' ? null : value;
            }

            return null;
        }
    }
}
