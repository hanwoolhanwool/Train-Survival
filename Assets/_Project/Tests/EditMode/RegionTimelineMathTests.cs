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

        // ── 3지역 구성 (M7 1차 — 숲 5 + 사막 4 + 대초원 4 = 한 바퀴 13일) ────

        private static readonly int[] ThreeRegions = { 5, 4, 4 };

        private static RegionTimelineState Evaluate3(int dayNumber)
        {
            return RegionTimelineMath.Evaluate(dayNumber, ThreeRegions, ForecastLeadDays, true);
        }

        [Test]
        public void 대초원은_Day10에_시작한다()
        {
            RegionTimelineState state = Evaluate3(10);

            Assert.That(state.RegionIndex, Is.EqualTo(2), "숲 5일 + 사막 4일 뒤 = 대초원");
            Assert.That(state.DayInRegion, Is.EqualTo(1));
            Assert.That(state.RegionDayCount, Is.EqualTo(4));
            Assert.That(state.CycleNumber, Is.EqualTo(0));
        }

        [Test]
        public void 사막_마지막_이틀은_대초원을_예고한다()
        {
            // 사막 = Day 6~9. 예고 2일 → 8·9일차가 예고 구간, 다음 지역 = 대초원(2).
            Assert.That(Evaluate3(7).IsForecastWindow, Is.False);
            Assert.That(Evaluate3(8).IsForecastWindow, Is.True);
            Assert.That(Evaluate3(9).IsForecastWindow, Is.True);
            Assert.That(Evaluate3(9).NextRegionIndex, Is.EqualTo(2), "예고 대상 = 대초원");
        }

        [Test]
        public void 대초원_마지막_밤은_Day13이고_다음은_숲_순환이다()
        {
            Assert.That(Evaluate3(13).IsFinalDayOfRegion, Is.True, "대초원 4일차 = 대형 웨이브");
            Assert.That(Evaluate3(13).NextRegionIndex, Is.EqualTo(0), "순환 예고 = 숲");

            RegionTimelineState next = Evaluate3(14);
            Assert.That(next.RegionIndex, Is.EqualTo(0), "숲 복귀 (기획서 §4.5)");
            Assert.That(next.DayInRegion, Is.EqualTo(1));
            Assert.That(next.CycleNumber, Is.EqualTo(1), "두 번째 바퀴 — 난이도 보너스 가산 축");
        }

        // ── 지역 중간 강화 밤 (M7 2차 결정 ⑥) ────────────────────────────────

        [Test]
        public void 강화_밤은_지역_중앙일_하루뿐이다()
        {
            // 숲 5일 → ceil(2.5) = 3일차 · 사막·대초원 4일 → ceil(2) = 2일차.
            Assert.That(RegionTimelineMath.IsReinforcedNight(3, 5), Is.True);
            Assert.That(RegionTimelineMath.IsReinforcedNight(2, 4), Is.True);
            Assert.That(RegionTimelineMath.IsReinforcedNight(2, 3), Is.True, "북극 3일 → 2일차");

            Assert.That(RegionTimelineMath.IsReinforcedNight(2, 5), Is.False);
            Assert.That(RegionTimelineMath.IsReinforcedNight(4, 5), Is.False);
            Assert.That(RegionTimelineMath.IsReinforcedNight(3, 4), Is.False);
        }

        [Test]
        public void 강화_밤은_첫날과_마지막_날을_피한다()
        {
            // 첫날은 지형 전환과 겹치고, 마지막 날은 이미 졸업 시험이다.
            for (int dayCount = 1; dayCount <= 8; dayCount++)
            {
                for (int day = 1; day <= dayCount; day++)
                {
                    if (!RegionTimelineMath.IsReinforcedNight(day, dayCount))
                    {
                        continue;
                    }

                    Assert.That(day, Is.GreaterThan(1), $"{dayCount}일 지역의 {day}일차");
                    Assert.That(day, Is.LessThan(dayCount), $"{dayCount}일 지역의 {day}일차");
                }
            }
        }

        [Test]
        public void 이틀_이하_지역에는_강화_밤이_없다()
        {
            Assert.That(RegionTimelineMath.IsReinforcedNight(1, 1), Is.False);
            Assert.That(RegionTimelineMath.IsReinforcedNight(1, 2), Is.False);
            Assert.That(RegionTimelineMath.IsReinforcedNight(2, 2), Is.False);
        }

        [Test]
        public void 타임라인_상태가_강화_밤을_직접_알려준다()
        {
            // 숲 3일차 = Day 3, 사막 2일차 = Day 7, 대초원 2일차 = Day 11.
            Assert.That(Evaluate3(3).IsReinforcedNight, Is.True);
            Assert.That(Evaluate3(7).IsReinforcedNight, Is.True);
            Assert.That(Evaluate3(11).IsReinforcedNight, Is.True);

            Assert.That(Evaluate3(5).IsReinforcedNight, Is.False, "숲 마지막 밤과 겹치지 않는다");
            Assert.That(Evaluate3(6).IsReinforcedNight, Is.False, "사막 첫날과 겹치지 않는다");
        }
    }
}
