using Game.Gameplay.Region;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 지역 타임라인 파생 검증 (기획서 §4 — 지역당 3~5일 주기, §4.5 재순환).
    /// 기준안인 숲 5일 → 사막 4일 구성으로 경계·순환·예고 구간을 확인한다.
    /// </summary>
    public sealed class RegionTimelineMathTests
    {
        private static readonly int[] ForestThenDesert = { 5, 4 };

        private const int ForecastLeadDays = 2;

        private static RegionTimelineState Evaluate(int dayNumber, bool loop = true, int forecastLead = ForecastLeadDays)
        {
            return RegionTimelineMath.Evaluate(dayNumber, ForestThenDesert, forecastLead, loop);
        }

        [Test]
        public void Day1은_첫_지역_1일차다()
        {
            RegionTimelineState state = Evaluate(1);

            Assert.That(state.IsValid, Is.True);
            Assert.That(state.RegionIndex, Is.EqualTo(0));
            Assert.That(state.CycleNumber, Is.EqualTo(0));
            Assert.That(state.DayInRegion, Is.EqualTo(1));
            Assert.That(state.RegionDayCount, Is.EqualTo(5));
            Assert.That(state.NextRegionIndex, Is.EqualTo(1));
            Assert.That(state.IsFinalDayOfRegion, Is.False);
            Assert.That(state.IsForecastWindow, Is.False);
        }

        [Test]
        public void 지역_마지막_이틀부터_다음_지역을_예고한다()
        {
            // 숲 5일 · 예고 2일 → 4·5일차가 예고 구간 (기획서 §2 — 마지막 1~2일로 앞당김).
            Assert.That(Evaluate(3).IsForecastWindow, Is.False, "3일차는 아직 예고 전");
            Assert.That(Evaluate(4).IsForecastWindow, Is.True);
            Assert.That(Evaluate(5).IsForecastWindow, Is.True);
        }

        [Test]
        public void 지역_마지막_날에만_졸업_웨이브_플래그가_선다()
        {
            Assert.That(Evaluate(4).IsFinalDayOfRegion, Is.False);
            Assert.That(Evaluate(5).IsFinalDayOfRegion, Is.True, "숲 5일차");
            Assert.That(Evaluate(6).IsFinalDayOfRegion, Is.False, "사막 1일차");
            Assert.That(Evaluate(9).IsFinalDayOfRegion, Is.True, "사막 4일차");
        }

        [Test]
        public void 첫_지역_일수를_넘기면_다음_지역으로_전환된다()
        {
            RegionTimelineState state = Evaluate(6);

            Assert.That(state.RegionIndex, Is.EqualTo(1), "사막");
            Assert.That(state.DayInRegion, Is.EqualTo(1));
            Assert.That(state.RegionDayCount, Is.EqualTo(4));
            Assert.That(state.CycleNumber, Is.EqualTo(0));
        }

        [Test]
        public void 마지막_지역_뒤에는_첫_지역으로_순환하며_주기가_오른다()
        {
            RegionTimelineState state = Evaluate(10);

            Assert.That(state.RegionIndex, Is.EqualTo(0), "숲으로 복귀 (기획서 §4.5 챌린지 순환)");
            Assert.That(state.DayInRegion, Is.EqualTo(1));
            Assert.That(state.CycleNumber, Is.EqualTo(1), "두 번째 바퀴");
        }

        [Test]
        public void 순환을_끄면_마지막_지역에_머문다()
        {
            RegionTimelineState state = Evaluate(12, loop: false);

            Assert.That(state.RegionIndex, Is.EqualTo(1), "사막 유지");
            Assert.That(state.CycleNumber, Is.EqualTo(0));
            Assert.That(state.DayInRegion, Is.EqualTo(7), "사막 진입(Day6) 기준 7일차");
            Assert.That(state.IsFinalDayOfRegion, Is.False, "졸업 웨이브는 다시 트리거되지 않는다");
            Assert.That(state.IsForecastWindow, Is.False);
        }

        [Test]
        public void 잘못된_Day_번호는_시작일로_고정된다()
        {
            RegionTimelineState zero = Evaluate(0);
            RegionTimelineState negative = Evaluate(-10);

            Assert.That(zero.RegionIndex, Is.EqualTo(0));
            Assert.That(zero.DayInRegion, Is.EqualTo(1));
            Assert.That(negative.DayInRegion, Is.EqualTo(1));
        }

        [Test]
        public void 지역_목록이_비면_무효_상태를_돌려준다()
        {
            RegionTimelineState empty = RegionTimelineMath.Evaluate(1, new int[0], ForecastLeadDays, true);
            RegionTimelineState missing = RegionTimelineMath.Evaluate(1, null, ForecastLeadDays, true);

            Assert.That(empty.IsValid, Is.False);
            Assert.That(missing.IsValid, Is.False);
        }

        [Test]
        public void 예고_일수가_0이면_예고_구간이_없다()
        {
            Assert.That(Evaluate(5, forecastLead: 0).IsForecastWindow, Is.False);
            Assert.That(Evaluate(5, forecastLead: 0).IsFinalDayOfRegion, Is.True, "마지막 날 판정은 예고와 독립이다");
        }

        [Test]
        public void 여러_바퀴를_돌아도_지역과_일차가_정확하다()
        {
            // 한 바퀴 = 9일. Day 21 = 2바퀴(18일) + 3일 → 숲 3일차.
            RegionTimelineState state = Evaluate(21);

            Assert.That(state.CycleNumber, Is.EqualTo(2));
            Assert.That(state.RegionIndex, Is.EqualTo(0));
            Assert.That(state.DayInRegion, Is.EqualTo(3));
        }
    }
}
