using Game.Gameplay.Monsters;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>밤 웨이브 규모 계산 검증 (기획서 §5 — Day 비례 '수' 증가, 네트워크 문서 §6.2 상한).</summary>
    public sealed class WaveMathTests
    {
        private static WavePlan Plan(int dayNumber)
        {
            return WaveMath.Plan(
                dayNumber,
                baseCount: 6, countGrowthPerDay: 3, totalCountCap: 24,
                baseInterval: 5f, intervalReductionPerDay: 0.4f, minInterval: 1.5f,
                baseMaxAlive: 5, maxAliveGrowthPerDay: 1, maxAliveCap: 12);
        }

        [Test]
        public void Day1은_기본_규모다()
        {
            WavePlan plan = Plan(1);

            Assert.That(plan.TotalCount, Is.EqualTo(6));
            Assert.That(plan.SpawnInterval, Is.EqualTo(5f));
            Assert.That(plan.MaxAlive, Is.EqualTo(5));
        }

        [Test]
        public void Day가_지날수록_총량이_늘고_간격이_줄어든다()
        {
            WavePlan plan = Plan(3);

            Assert.That(plan.TotalCount, Is.EqualTo(12), "6 + 3×2");
            Assert.That(plan.SpawnInterval, Is.EqualTo(4.2f).Within(0.001f), "5 − 0.4×2");
            Assert.That(plan.MaxAlive, Is.EqualTo(7), "5 + 1×2");
        }

        [Test]
        public void 모든_수치는_상한에서_멈춘다()
        {
            WavePlan plan = Plan(100);

            Assert.That(plan.TotalCount, Is.EqualTo(24));
            Assert.That(plan.SpawnInterval, Is.EqualTo(1.5f));
            Assert.That(plan.MaxAlive, Is.EqualTo(12), "동시 존재 상한 — 대역폭 계측 후 확정하는 미결 수치");
        }

        [Test]
        public void 잘못된_Day_번호도_기본_규모로_처리된다()
        {
            WavePlan plan = Plan(0);

            Assert.That(plan.TotalCount, Is.EqualTo(6));
            Assert.That(plan.MaxAlive, Is.EqualTo(5));
        }
    }
}
