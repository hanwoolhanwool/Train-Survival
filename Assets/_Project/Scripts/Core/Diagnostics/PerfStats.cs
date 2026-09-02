using System;
using System.Collections.Generic;

namespace Game.Core.Diagnostics
{
    /// <summary>한 지표의 분포. <b>평균은 스파이크를 감추므로 판정에 쓰지 않는다</b>(§4.3).</summary>
    public readonly struct PerfDistribution
    {
        public PerfDistribution(double p50, double p95, double p99, double max, double mean, double standardDeviation)
        {
            P50 = p50;
            P95 = p95;
            P99 = p99;
            Max = max;
            Mean = mean;
            StandardDeviation = standardDeviation;
        }

        /// <summary>평상시 체감.</summary>
        public double P50 { get; }

        /// <summary>"가끔 버벅인다"의 정체 — <b>회귀 게이트의 주 판정값</b>(§4.6).</summary>
        public double P95 { get; }

        public double P99 { get; }

        public double Max { get; }

        /// <summary>참고용으로만 남긴다. 게이트가 이 값을 보지 않는다.</summary>
        public double Mean { get; }

        /// <summary>프레임 페이싱의 고름.</summary>
        public double StandardDeviation { get; }
    }

    /// <summary>정상 프레임에서 크게 벗어난 프레임 하나.</summary>
    public readonly struct PerfSpike
    {
        public PerfSpike(int frameIndex, float timeSeconds, double milliseconds)
        {
            FrameIndex = frameIndex;
            TimeSeconds = timeSeconds;
            Milliseconds = milliseconds;
        }

        public int FrameIndex { get; }

        /// <summary>측정 시작으로부터의 시각. 6.67초 간격으로 늘어서면 범인은 타일 교체다(§1.4).</summary>
        public float TimeSeconds { get; }

        public double Milliseconds { get; }
    }

    /// <summary>프레임을 붙잡고 있는 쪽. §8이 3개월째 답하지 못한 질문의 답이다.</summary>
    public enum PerfBottleneck
    {
        /// <summary>세 값이 전부 0 — Frame Timing Stats 가 꺼져 있거나 아직 안 채워졌다(§1.1).</summary>
        Unknown = 0,

        CpuMainThread = 1,

        CpuRenderThread = 2,

        Gpu = 3,
    }

    /// <summary>
    /// 수집한 표본 → 통계. <b>순수 함수만 둔다</b> — 엔진 타입에 의존하지 않으므로
    /// EditMode 테스트가 이 계산을 그대로 못 박을 수 있다(§4.7).
    /// </summary>
    public static class PerfStats
    {
        /// <summary>30 FPS 경계. 이 위로 올라간 프레임 수를 절대 기준으로 센다(§4.3).</summary>
        public const double SlowFrameMs = 33.3;

        /// <summary>스파이크 판정 배수 — 중앙값의 이 배를 넘으면 스파이크다(§4.3).</summary>
        public const double DefaultSpikeMultiplier = 3.0;

        /// <summary>
        /// 분포를 낸다. 표본이 비면 전부 0인 분포를 돌려준다 — 예외를 던지면 60초를 주행하고도
        /// 결과 파일이 남지 않는다.
        /// </summary>
        public static PerfDistribution Describe(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0)
            {
                return default;
            }

            double[] sorted = new double[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                sorted[i] = values[i];
            }

            Array.Sort(sorted);

            double sum = 0.0;
            for (int i = 0; i < sorted.Length; i++)
            {
                sum += sorted[i];
            }

            double mean = sum / sorted.Length;

            double varianceSum = 0.0;
            for (int i = 0; i < sorted.Length; i++)
            {
                double delta = sorted[i] - mean;
                varianceSum += delta * delta;
            }

            double standardDeviation = Math.Sqrt(varianceSum / sorted.Length);

            return new PerfDistribution(
                PercentileOfSorted(sorted, 50),
                PercentileOfSorted(sorted, 95),
                PercentileOfSorted(sorted, 99),
                sorted[sorted.Length - 1],
                mean,
                standardDeviation);
        }

        /// <summary>
        /// 정렬된 표본의 백분위 (nearest-rank). 보간하지 않는다 — 실제로 관측된 프레임 시간만
        /// 결과에 나오게 해서, 리포트의 값을 원본 표본에서 되찾을 수 있게 한다.
        /// </summary>
        public static double PercentileOfSorted(double[] sortedValues, int percentile)
        {
            if (sortedValues == null || sortedValues.Length == 0)
            {
                return 0.0;
            }

            if (percentile <= 0)
            {
                return sortedValues[0];
            }

            if (percentile >= 100)
            {
                return sortedValues[sortedValues.Length - 1];
            }

            int rank = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Length);
            if (rank < 1)
            {
                rank = 1;
            }

            if (rank > sortedValues.Length)
            {
                rank = sortedValues.Length;
            }

            return sortedValues[rank - 1];
        }

        /// <summary>임계(기본 33.3 ms)를 넘은 프레임 수.</summary>
        public static int CountOver(IReadOnlyList<double> values, double thresholdMs)
        {
            if (values == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] > thresholdMs)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// 중앙값의 <paramref name="multiplier"/> 배를 넘은 프레임을 <b>시각과 함께</b> 골라낸다.
        /// 시각을 남기는 이유는 §4.3에 있다 — 6.67초 간격으로 규칙적으로 찍히면
        /// 타일 교체가 범인이라는 것이 그 자리에서 증명된다.
        /// </summary>
        public static List<PerfSpike> FindSpikes(
            IReadOnlyList<PerfSample> samples, double medianMs, double multiplier = DefaultSpikeMultiplier)
        {
            var spikes = new List<PerfSpike>();
            if (samples == null || samples.Count == 0 || medianMs <= 0.0)
            {
                return spikes;
            }

            double threshold = medianMs * multiplier;
            for (int i = 0; i < samples.Count; i++)
            {
                PerfSample sample = samples[i];
                double slowest = sample.SlowestThreadMs;
                if (slowest > threshold)
                {
                    spikes.Add(new PerfSpike(sample.FrameIndex, sample.TimeSeconds, slowest));
                }
            }

            return spikes;
        }

        /// <summary>
        /// 세 프레임 시간의 p50 중 가장 큰 쪽이 병목이다. <b>셋 다 0이면 판정하지 않는다</b> —
        /// "GPU가 0 ms라 CPU 바운드"라고 답하면 §1.1의 함정에 그대로 빠진다.
        /// </summary>
        public static PerfBottleneck DetermineBottleneck(double cpuMainMs, double cpuRenderMs, double gpuMs)
        {
            if (cpuMainMs <= 0.0 && cpuRenderMs <= 0.0 && gpuMs <= 0.0)
            {
                return PerfBottleneck.Unknown;
            }

            if (gpuMs >= cpuMainMs && gpuMs >= cpuRenderMs)
            {
                return PerfBottleneck.Gpu;
            }

            return cpuMainMs >= cpuRenderMs ? PerfBottleneck.CpuMainThread : PerfBottleneck.CpuRenderThread;
        }

        /// <summary>
        /// 여러 번 반복한 실행의 <b>중앙값</b>을 고른다 — 평균을 내면 한 번 튄 실행이 결과를 끌고 간다.
        /// 짝수 개면 아래쪽 중앙값을 쓴다(값을 만들어 내지 않기 위함이다).
        /// </summary>
        public static double Median(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0)
            {
                return 0.0;
            }

            double[] sorted = new double[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                sorted[i] = values[i];
            }

            Array.Sort(sorted);
            return sorted[(sorted.Length - 1) / 2];
        }
    }
}
