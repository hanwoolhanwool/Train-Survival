using System.Collections.Generic;
using Game.Core.Diagnostics;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>프레임 통계·병목 판정 검증 (성능 프로파일링 자동화 계획 1차 1.3).</summary>
    public sealed class PerfStatsTests
    {
        [Test]
        public void 표본이_비면_전부_0인_분포를_돌려준다()
        {
            // 예외를 던지면 60초를 주행하고도 결과 파일이 남지 않는다.
            PerfDistribution empty = PerfStats.Describe(null);

            Assert.That(empty.P50, Is.EqualTo(0.0));
            Assert.That(empty.Max, Is.EqualTo(0.0));
        }

        [Test]
        public void 백분위는_관측된_값만_돌려준다()
        {
            // 보간하면 리포트의 값을 원본 표본에서 되찾을 수 없다.
            var values = new double[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            PerfDistribution distribution = PerfStats.Describe(values);

            Assert.That(distribution.P50, Is.EqualTo(5.0));
            Assert.That(distribution.P95, Is.EqualTo(10.0));
            Assert.That(distribution.Max, Is.EqualTo(10.0));
        }

        [Test]
        public void 평균은_스파이크를_감추지만_p95는_감추지_못한다()
        {
            // 이 계획이 평균 FPS를 쓰지 않기로 한 이유를 그대로 못 박는다 (§4.3).
            var values = new List<double>();
            for (int i = 0; i < 99; i++)
            {
                values.Add(10.0);
            }

            values.Add(200.0);

            PerfDistribution distribution = PerfStats.Describe(values);

            Assert.That(distribution.Mean, Is.EqualTo(11.9).Within(0.01), "평균은 거의 움직이지 않는다");
            Assert.That(distribution.Max, Is.EqualTo(200.0), "스파이크는 max 에 그대로 남는다");
        }

        [Test]
        public void 표준편차는_고른_표본에서_0이다()
        {
            PerfDistribution distribution = PerfStats.Describe(new double[] { 8, 8, 8, 8 });

            Assert.That(distribution.StandardDeviation, Is.EqualTo(0.0).Within(1e-9));
        }

        [Test]
        public void 느린_프레임_수를_센다()
        {
            var values = new double[] { 10, 33.2, 33.4, 50, 16 };

            Assert.That(PerfStats.CountOver(values, PerfStats.SlowFrameMs), Is.EqualTo(2));
        }

        [Test]
        public void 중앙값의_3배를_넘는_프레임이_스파이크다()
        {
            var samples = new List<PerfSample>
            {
                Sample(0, 0.0f, 10.0),
                Sample(1, 0.5f, 12.0),
                Sample(2, 1.0f, 45.0),
                Sample(3, 1.5f, 11.0),
            };

            List<PerfSpike> spikes = PerfStats.FindSpikes(samples, 12.0);

            Assert.That(spikes.Count, Is.EqualTo(1));
            Assert.That(spikes[0].FrameIndex, Is.EqualTo(2));
            Assert.That(spikes[0].Milliseconds, Is.EqualTo(45.0));
        }

        [Test]
        public void 스파이크는_시각을_함께_남긴다()
        {
            // 6.67초 간격으로 늘어서면 타일 교체가 범인이라는 것이 그 자리에서 증명된다 (§4.3).
            var samples = new List<PerfSample>
            {
                Sample(0, 6.67f, 40.0),
                Sample(1, 13.34f, 41.0),
            };

            List<PerfSpike> spikes = PerfStats.FindSpikes(samples, 10.0);

            Assert.That(spikes.Count, Is.EqualTo(2));
            Assert.That(spikes[0].TimeSeconds, Is.EqualTo(6.67f).Within(0.001f));
            Assert.That(spikes[1].TimeSeconds, Is.EqualTo(13.34f).Within(0.001f));
        }

        [Test]
        public void 중앙값이_0이면_스파이크를_찾지_않는다()
        {
            var samples = new List<PerfSample> { Sample(0, 0f, 100.0) };

            Assert.That(PerfStats.FindSpikes(samples, 0.0), Is.Empty);
        }

        [Test]
        public void 세_값이_전부_0이면_병목을_판정하지_않는다()
        {
            // "GPU 가 0 ms 라 CPU 바운드"라고 답하면 Frame Timing Stats 가 꺼진 함정에 그대로 빠진다 (§1.1).
            Assert.That(PerfStats.DetermineBottleneck(0, 0, 0), Is.EqualTo(PerfBottleneck.Unknown));
        }

        [Test]
        public void 가장_큰_프레임_시간이_병목이다()
        {
            Assert.That(PerfStats.DetermineBottleneck(6.4, 3.0, 11.2), Is.EqualTo(PerfBottleneck.Gpu));
            Assert.That(PerfStats.DetermineBottleneck(12.0, 3.0, 5.0), Is.EqualTo(PerfBottleneck.CpuMainThread));
            Assert.That(PerfStats.DetermineBottleneck(3.0, 9.0, 5.0), Is.EqualTo(PerfBottleneck.CpuRenderThread));
        }

        [Test]
        public void 반복_실행은_평균이_아니라_중앙값으로_고른다()
        {
            // 한 번 튄 실행이 결과를 끌고 가면 안 된다 (§2 결정 ⑤).
            Assert.That(PerfStats.Median(new double[] { 10.0, 11.0, 40.0 }), Is.EqualTo(11.0));
            Assert.That(PerfStats.Median(new double[] { 10.0 }), Is.EqualTo(10.0));
            Assert.That(PerfStats.Median(null), Is.EqualTo(0.0));
        }

        [Test]
        public void 가장_느린_스레드가_프레임의_주인이다()
        {
            PerfSample sample = Sample(0, 0f, 5.0, cpuRenderMs: 7.0, gpuMs: 12.0);

            Assert.That(sample.SlowestThreadMs, Is.EqualTo(12.0));
        }

        [Test]
        public void 드로우콜은_제출_경로를_합친_값이다()
        {
            // Unity 6 에는 "Draw Calls Count" 단일 카운터가 없다 — 경로별로 쪼개진 값을 더해야
            // 예산 문서 §6 의 게임 뷰 통계값과 같은 자가 된다 (2026-09-02 실측).
            var sample = new PerfSample(0, 0f, 0, 0, 0,
                100, 900, 50, 10, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

            Assert.That(sample.StandardDrawCalls, Is.EqualTo(100));
            Assert.That(sample.SrpBatcherDrawCalls, Is.EqualTo(900));
            Assert.That(sample.DrawCalls, Is.EqualTo(1060));
        }

        private static PerfSample Sample(
            int index, float time, double cpuMainMs, double cpuRenderMs = 0.0, double gpuMs = 0.0)
        {
            // 카운터는 이 검증의 대상이 아니다 — 통계는 프레임 시간만 본다.
            return new PerfSample(index, time, cpuMainMs, cpuRenderMs, gpuMs,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }
    }
}
