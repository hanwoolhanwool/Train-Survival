using Game.Gameplay.Harpoon;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>Q3 견인 이동 분석(워프·역행 검출) 검증.</summary>
    public sealed class TowMotionAnalyzerTests
    {
        private const float ReelSpeed = 8f;
        private const float Dt = 1f / 60f;

        private static readonly Vector3 Anchor = Vector3.zero;

        private static TowMotionAnalyzer Create()
        {
            // 유예 0 — 판정 규칙 자체를 검증하는 테스트용.
            return new TowMotionAnalyzer(ReelSpeed, 0f);
        }

        private static TowMotionAnalyzer CreateWithGrace()
        {
            return new TowMotionAnalyzer(ReelSpeed);
        }

        private static Vector3 AtDistance(float distance)
        {
            return new Vector3(0f, 0f, distance);
        }

        [Test]
        public void 첫_샘플은_정상이고_기준점만_세운다()
        {
            TowMotionAnalyzer analyzer = Create();

            TowSampleVerdict verdict = analyzer.Feed(AtDistance(20f), Anchor, Dt);

            Assert.That(verdict, Is.EqualTo(TowSampleVerdict.Normal));
            Assert.That(analyzer.SampleCount, Is.EqualTo(1));
            Assert.That(analyzer.IsClean, Is.True);
        }

        [Test]
        public void 릴_속도의_정상_접근은_이상이_없다()
        {
            TowMotionAnalyzer analyzer = Create();

            float distance = 20f;
            analyzer.Feed(AtDistance(distance), Anchor, Dt);
            for (int i = 0; i < 120; i++)
            {
                distance -= ReelSpeed * Dt;
                TowSampleVerdict verdict = analyzer.Feed(AtDistance(distance), Anchor, Dt);
                Assert.That(verdict, Is.EqualTo(TowSampleVerdict.Normal));
            }

            Assert.That(analyzer.IsClean, Is.True);
            Assert.That(analyzer.SampleCount, Is.EqualTo(121));
        }

        [Test]
        public void 한_프레임_순간이동은_워프로_집계된다()
        {
            TowMotionAnalyzer analyzer = Create();
            analyzer.Feed(AtDistance(20f), Anchor, Dt);

            TowSampleVerdict verdict = analyzer.Feed(AtDistance(15f), Anchor, Dt);

            Assert.That(verdict, Is.EqualTo(TowSampleVerdict.Warp));
            Assert.That(analyzer.WarpCount, Is.EqualTo(1));
        }

        [Test]
        public void 거리가_다시_늘어나면_역행으로_집계된다()
        {
            TowMotionAnalyzer analyzer = Create();
            analyzer.Feed(AtDistance(10f), Anchor, Dt);

            TowSampleVerdict verdict = analyzer.Feed(AtDistance(10.5f), Anchor, Dt);

            Assert.That(verdict, Is.EqualTo(TowSampleVerdict.Regression));
            Assert.That(analyzer.RegressionCount, Is.EqualTo(1));
        }

        [Test]
        public void 한_번의_되밀림은_여러_프레임이어도_1회로_집계된다()
        {
            TowMotionAnalyzer analyzer = Create();
            analyzer.Feed(AtDistance(10f), Anchor, Dt);

            // 허용치(0.3 m)를 넘겨 역행 진입 후, 역행 상태에 머무는 동안은 추가 집계가 없어야 한다.
            analyzer.Feed(AtDistance(10.4f), Anchor, Dt);
            analyzer.Feed(AtDistance(10.45f), Anchor, Dt);
            analyzer.Feed(AtDistance(10.5f), Anchor, Dt);

            Assert.That(analyzer.RegressionCount, Is.EqualTo(1));
        }

        [Test]
        public void 미세한_보간_변동은_역행이_아니다()
        {
            TowMotionAnalyzer analyzer = Create();
            analyzer.Feed(AtDistance(10f), Anchor, Dt);

            TowSampleVerdict verdict = analyzer.Feed(AtDistance(10.1f), Anchor, Dt);

            Assert.That(verdict, Is.EqualTo(TowSampleVerdict.Normal));
            Assert.That(analyzer.IsClean, Is.True);
        }

        [Test]
        public void 역행에서_회복하면_다음_되밀림은_새로_집계된다()
        {
            TowMotionAnalyzer analyzer = Create();
            analyzer.Feed(AtDistance(10f), Anchor, Dt);

            analyzer.Feed(AtDistance(10.4f), Anchor, Dt);  // 1차 역행
            analyzer.Feed(AtDistance(9.9f), Anchor, Dt);   // 회복 (새 최소 거리)
            analyzer.Feed(AtDistance(10.3f), Anchor, Dt);  // 2차 역행

            Assert.That(analyzer.RegressionCount, Is.EqualTo(2));
        }

        [Test]
        public void 시작_유예_중에는_이상을_집계하지_않는다()
        {
            TowMotionAnalyzer analyzer = CreateWithGrace();
            analyzer.Feed(AtDistance(20f), Anchor, Dt);

            // 유예(0.2 s) 안의 큰 점프·되밀림 — 견인 시작 전환 스냅에 해당, 정상으로 취급.
            TowSampleVerdict warpLike = analyzer.Feed(AtDistance(15f), Anchor, Dt);
            TowSampleVerdict regressionLike = analyzer.Feed(AtDistance(16f), Anchor, Dt);

            Assert.That(warpLike, Is.EqualTo(TowSampleVerdict.Normal));
            Assert.That(regressionLike, Is.EqualTo(TowSampleVerdict.Normal));
            Assert.That(analyzer.IsClean, Is.True);
            Assert.That(analyzer.MaxStep, Is.EqualTo(0f));
        }

        [Test]
        public void 유예가_끝나면_이상을_집계한다()
        {
            TowMotionAnalyzer analyzer = CreateWithGrace();

            // 유예(0.2 s = 12프레임)를 정상 접근으로 소진한 뒤의 순간이동은 워프여야 한다.
            float distance = 20f;
            analyzer.Feed(AtDistance(distance), Anchor, Dt);
            for (int i = 0; i < 15; i++)
            {
                distance -= ReelSpeed * Dt;
                analyzer.Feed(AtDistance(distance), Anchor, Dt);
            }

            TowSampleVerdict verdict = analyzer.Feed(AtDistance(distance - 5f), Anchor, Dt);

            Assert.That(verdict, Is.EqualTo(TowSampleVerdict.Warp));
            Assert.That(analyzer.WarpCount, Is.EqualTo(1));
        }

        [Test]
        public void 최대_프레임_이동량을_추적한다()
        {
            TowMotionAnalyzer analyzer = Create();
            analyzer.Feed(AtDistance(20f), Anchor, Dt);
            analyzer.Feed(AtDistance(19.8f), Anchor, Dt);
            analyzer.Feed(AtDistance(19.3f), Anchor, Dt);

            Assert.That(analyzer.MaxStep, Is.EqualTo(0.5f).Within(0.001f));
        }
    }
}
