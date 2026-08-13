using Game.Gameplay.Monsters;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 밤 웨이브 규모 계산 검증 (기획서 §5 — Day 비례 '수 → 체력' 증가 + 지역 난이도 배율,
    /// 네트워크 문서 §6.2 동시 존재 상한).
    /// </summary>
    public sealed class WaveMathTests
    {
        private static WaveCurve Curve()
        {
            return new WaveCurve(
                baseCount: 6, countGrowthPerDay: 3, totalCountCap: 24,
                baseInterval: 5f, intervalReductionPerDay: 0.4f, minInterval: 1.5f,
                baseMaxAlive: 5, maxAliveGrowthPerDay: 1, maxAliveCap: 12,
                healthGrowthPerDay: 0.08f, healthMultiplierCap: 3f,
                finalNightCountMultiplier: 2f, finalNightHealthMultiplier: 1.5f,
                reinforcedNightCountMultiplier: 1.4f, reinforcedNightHealthMultiplier: 1.2f);
        }

        private static WavePlan Plan(
            int dayNumber, float regionCountMultiplier = 1f, float regionHealthMultiplier = 1f,
            bool isFinalNight = false, bool isReinforcedNight = false)
        {
            return WaveMath.Plan(
                dayNumber, Curve(), regionCountMultiplier, regionHealthMultiplier, isFinalNight, isReinforcedNight);
        }

        [Test]
        public void Day1은_기본_규모다()
        {
            WavePlan plan = Plan(1);

            Assert.That(plan.TotalCount, Is.EqualTo(6));
            Assert.That(plan.SpawnInterval, Is.EqualTo(5f));
            Assert.That(plan.MaxAlive, Is.EqualTo(5));
            Assert.That(plan.HealthMultiplier, Is.EqualTo(1f).Within(0.001f));
            Assert.That(plan.IsFinalNight, Is.False);
        }

        [Test]
        public void Day가_지날수록_총량이_늘고_간격이_줄고_체력이_오른다()
        {
            WavePlan plan = Plan(3);

            Assert.That(plan.TotalCount, Is.EqualTo(12), "6 + 3×2");
            Assert.That(plan.SpawnInterval, Is.EqualTo(4.2f).Within(0.001f), "5 − 0.4×2");
            Assert.That(plan.MaxAlive, Is.EqualTo(7), "5 + 1×2");
            Assert.That(plan.HealthMultiplier, Is.EqualTo(1.16f).Within(0.001f), "1 + 0.08×2");
        }

        [Test]
        public void 모든_수치는_상한에서_멈춘다()
        {
            WavePlan plan = Plan(100);

            Assert.That(plan.TotalCount, Is.EqualTo(24));
            Assert.That(plan.SpawnInterval, Is.EqualTo(1.5f));
            Assert.That(plan.MaxAlive, Is.EqualTo(12), "동시 존재 상한 — 대역폭 계측 후 확정하는 미결 수치");
            Assert.That(plan.HealthMultiplier, Is.EqualTo(3f).Within(0.001f), "Day 성장분 체력 배율 상한");
        }

        [Test]
        public void 잘못된_Day_번호도_기본_규모로_처리된다()
        {
            WavePlan plan = Plan(0);

            Assert.That(plan.TotalCount, Is.EqualTo(6));
            Assert.That(plan.MaxAlive, Is.EqualTo(5));
            Assert.That(plan.HealthMultiplier, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 지역_난이도_배율이_물량과_체력에_반영된다()
        {
            // 기획서 §4 — 사막 난이도 4. 같은 Day라도 지역이 바뀌면 규모가 달라진다.
            WavePlan plan = Plan(1, regionCountMultiplier: 4f, regionHealthMultiplier: 2f);

            Assert.That(plan.TotalCount, Is.EqualTo(24), "6 × 4");
            Assert.That(plan.SpawnInterval, Is.EqualTo(1.5f).Within(0.001f), "5 ÷ 4 = 1.25 → 최소 간격 1.5로 클램프");
            Assert.That(plan.MaxAlive, Is.EqualTo(12), "5 × 4 = 20 → 대역폭 상한 12로 클램프");
            Assert.That(plan.HealthMultiplier, Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void 지역_마지막_밤은_대형_웨이브가_된다()
        {
            WavePlan normal = Plan(1);
            WavePlan final = Plan(1, isFinalNight: true);

            Assert.That(final.IsFinalNight, Is.True);
            Assert.That(final.TotalCount, Is.EqualTo(normal.TotalCount * 2), "총량 ×2");
            Assert.That(final.SpawnInterval, Is.EqualTo(normal.SpawnInterval / 2f).Within(0.001f), "물량이 는 만큼 간격도 짧아진다");
            Assert.That(final.MaxAlive, Is.GreaterThan(normal.MaxAlive));
            Assert.That(final.HealthMultiplier, Is.EqualTo(1.5f).Within(0.001f));
        }

        [Test]
        public void 동시_존재_상한은_어떤_배율에서도_넘지_않는다()
        {
            // 대역폭 방어선 — 지역 배율과 마지막 밤 배율이 겹쳐도 MaxAliveCap을 넘으면 안 된다.
            WavePlan plan = Plan(100, regionCountMultiplier: 10f, isFinalNight: true);

            Assert.That(plan.MaxAlive, Is.EqualTo(12));
        }

        [Test]
        public void 배율이_0이어도_웨이브가_사라지거나_간격이_무한대가_되지_않는다()
        {
            WavePlan plan = Plan(5, regionCountMultiplier: 0f, regionHealthMultiplier: 0f);

            Assert.That(plan.TotalCount, Is.GreaterThanOrEqualTo(1));
            Assert.That(plan.MaxAlive, Is.GreaterThanOrEqualTo(1));
            Assert.That(plan.SpawnInterval, Is.GreaterThan(0f));
            Assert.That(plan.HealthMultiplier, Is.GreaterThan(0f));
        }

        [Test]
        public void 지역_중간_강화_밤은_마지막_밤보다_약한_가중을_받는다()
        {
            WavePlan normal = Plan(1);
            WavePlan reinforced = Plan(1, isReinforcedNight: true);
            WavePlan final = Plan(1, isFinalNight: true);

            Assert.That(reinforced.IsReinforcedNight, Is.True);
            Assert.That(reinforced.TotalCount, Is.GreaterThan(normal.TotalCount));
            Assert.That(reinforced.TotalCount, Is.LessThan(final.TotalCount));
            Assert.That(reinforced.HealthMultiplier, Is.EqualTo(1.2f).Within(0.001f));
            Assert.That(reinforced.HealthMultiplier, Is.LessThan(final.HealthMultiplier));
        }

        [Test]
        public void 마지막_밤과_강화_밤이_겹치면_마지막_밤이_우선한다()
        {
            // 두 가중이 곱해지면 졸업 시험의 위상이 흐려진다 — 배타 처리가 규약이다.
            WavePlan both = Plan(1, isFinalNight: true, isReinforcedNight: true);
            WavePlan final = Plan(1, isFinalNight: true);

            Assert.That(both.IsFinalNight, Is.True);
            Assert.That(both.IsReinforcedNight, Is.False);
            Assert.That(both.TotalCount, Is.EqualTo(final.TotalCount));
            Assert.That(both.HealthMultiplier, Is.EqualTo(final.HealthMultiplier).Within(0.001f));
        }
    }
}
