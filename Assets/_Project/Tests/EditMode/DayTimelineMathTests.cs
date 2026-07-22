using Game.Gameplay.Cycle;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>낮/밤 타임라인 평가 검증 (개발 가이드 M2 — 낮→밤→Day+1 순환).</summary>
    public sealed class DayTimelineMathTests
    {
        private const float DayDuration = 240f;
        private const float NightDuration = 150f;

        [Test]
        public void 시작_시점은_Day1_낮이다()
        {
            DayTimelineState state = DayTimelineMath.Evaluate(0f, DayDuration, NightDuration);

            Assert.That(state.DayNumber, Is.EqualTo(1));
            Assert.That(state.Phase, Is.EqualTo(DayPhase.Day));
            Assert.That(state.PhaseElapsed, Is.EqualTo(0f));
            Assert.That(state.PhaseDuration, Is.EqualTo(DayDuration));
        }

        [Test]
        public void 낮_길이를_지나면_밤으로_전환된다()
        {
            DayTimelineState state = DayTimelineMath.Evaluate(DayDuration + 10f, DayDuration, NightDuration);

            Assert.That(state.DayNumber, Is.EqualTo(1));
            Assert.That(state.Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(state.PhaseElapsed, Is.EqualTo(10f).Within(0.001f));
            Assert.That(state.PhaseDuration, Is.EqualTo(NightDuration));
        }

        [Test]
        public void 한_사이클이_끝나면_Day가_1_증가한_낮이_된다()
        {
            float cycle = DayDuration + NightDuration;

            DayTimelineState state = DayTimelineMath.Evaluate(cycle + 5f, DayDuration, NightDuration);

            Assert.That(state.DayNumber, Is.EqualTo(2));
            Assert.That(state.Phase, Is.EqualTo(DayPhase.Day));
            Assert.That(state.PhaseElapsed, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void 남은_시간은_국면_길이에서_경과를_뺀_값이다()
        {
            DayTimelineState state = DayTimelineMath.Evaluate(100f, DayDuration, NightDuration);

            Assert.That(state.PhaseRemaining, Is.EqualTo(140f).Within(0.001f));
        }

        [Test]
        public void 음수_시간은_시작_상태로_고정된다()
        {
            DayTimelineState state = DayTimelineMath.Evaluate(-5f, DayDuration, NightDuration);

            Assert.That(state.DayNumber, Is.EqualTo(1));
            Assert.That(state.Phase, Is.EqualTo(DayPhase.Day));
        }

        [Test]
        public void 여러_사이클_경과도_정확한_Day_번호를_돌려준다()
        {
            float cycle = DayDuration + NightDuration;

            DayTimelineState state = DayTimelineMath.Evaluate(cycle * 4f + DayDuration + 1f, DayDuration, NightDuration);

            Assert.That(state.DayNumber, Is.EqualTo(5));
            Assert.That(state.Phase, Is.EqualTo(DayPhase.Night));
        }
    }
}
