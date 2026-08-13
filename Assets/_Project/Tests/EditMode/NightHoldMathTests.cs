using Game.Gameplay.Cycle;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 새벽 보류 클램프 검증 (M7 2차 결정 ④ — "보스를 잡기 전까지 새벽이 오지 않는다").
    /// 보류는 새 복제 상태를 만들지 않고 누적 시간을 잘라낼 뿐이므로, 이 수식이 규칙 전부다.
    /// </summary>
    public sealed class NightHoldMathTests
    {
        private const float DayDuration = 240f;
        private const float NightDuration = 150f;
        private const float CycleDuration = DayDuration + NightDuration;

        private static float Clamp(float previous, float delta, bool holding)
        {
            return NightHoldMath.ClampAccumulation(
                previous, previous + delta, DayDuration, NightDuration, holding);
        }

        private static DayTimelineState Evaluate(float totalSeconds)
        {
            return DayTimelineMath.Evaluate(totalSeconds, DayDuration, NightDuration);
        }

        [Test]
        public void 보류가_꺼져_있으면_시간이_그대로_흐른다()
        {
            float next = Clamp(CycleDuration - 1f, 2f, holding: false);

            Assert.That(next, Is.EqualTo(CycleDuration + 1f).Within(0.0001f));
        }

        [Test]
        public void 낮에는_보류가_켜져_있어도_시간이_흐른다()
        {
            // 보류는 밤을 붙잡는 규칙이다 — 낮까지 멈추면 QA 소환이 게임을 정지시킨다.
            float next = Clamp(10f, 1f, holding: true);

            Assert.That(next, Is.EqualTo(11f).Within(0.0001f));
        }

        [Test]
        public void 밤_중간에는_보류_중에도_평소처럼_흐른다()
        {
            float midNight = DayDuration + 10f;
            float next = Clamp(midNight, 1f, holding: true);

            Assert.That(next, Is.EqualTo(midNight + 1f).Within(0.0001f));
        }

        [Test]
        public void 보류_중에는_밤_끝_경계를_넘지_못한다()
        {
            float justBeforeDawn = CycleDuration - 0.02f;
            float next = Clamp(justBeforeDawn, 5f, holding: true);

            Assert.That(next, Is.LessThan(CycleDuration));
            Assert.That(Evaluate(next).Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(Evaluate(next).DayNumber, Is.EqualTo(1), "Day가 넘어가지 않는다");
        }

        [Test]
        public void 보류가_길어져도_같은_밤에_머무른다()
        {
            float seconds = DayDuration + NightDuration - 1f;
            for (int i = 0; i < 600; i++)
            {
                seconds = Clamp(seconds, 1f, holding: true);
            }

            DayTimelineState state = Evaluate(seconds);

            Assert.That(state.Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(state.DayNumber, Is.EqualTo(1), "10분을 붙잡아도 Day 1의 밤이다");
        }

        [Test]
        public void 보류가_풀리면_즉시_새벽으로_넘어간다()
        {
            float held = CycleDuration - NightHoldMath.HoldMarginSeconds;
            float released = Clamp(held, 0.2f, holding: false);

            DayTimelineState state = Evaluate(released);

            Assert.That(state.Phase, Is.EqualTo(DayPhase.Day));
            Assert.That(state.DayNumber, Is.EqualTo(2));
        }

        [Test]
        public void 둘째_밤에서도_그_밤의_경계에서_멈춘다()
        {
            float secondNight = CycleDuration + DayDuration + NightDuration - 0.5f;
            float next = Clamp(secondNight, 10f, holding: true);

            DayTimelineState state = Evaluate(next);

            Assert.That(state.Phase, Is.EqualTo(DayPhase.Night));
            Assert.That(state.DayNumber, Is.EqualTo(2));
        }

        [Test]
        public void 사이클_길이가_0이면_클램프하지_않는다()
        {
            float next = NightHoldMath.ClampAccumulation(5f, 6f, 0f, 0f, holding: true);

            Assert.That(next, Is.EqualTo(6f));
        }
    }
}
