using Game.Gameplay.Player;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 허기 감소·섭취 회복·기아 피해 계산 검증 (기획서 §3.4 상태 창, M5 4차 — 요리·허기).
    /// </summary>
    public sealed class HungerMathTests
    {
        private static HungerCurve Curve()
        {
            return new HungerCurve(
                maxHunger: 100f, decayPerSecond: 0.25f, warnThreshold: 30f, starveDamagePerSecond: 2f);
        }

        [Test]
        public void 허기는_초당_감소율만큼_내려간다()
        {
            float next = HungerMath.Step(100f, Curve(), 10f);

            Assert.That(next, Is.EqualTo(97.5f).Within(0.001f), "0.25/s × 10s = 2.5");
        }

        [Test]
        public void 허기는_0_밑으로_내려가지_않는다()
        {
            Assert.That(HungerMath.Step(1f, Curve(), 100f), Is.EqualTo(0f));
        }

        [Test]
        public void 낮_밤_1사이클에_식사_1회쯤_필요하다()
        {
            // 기본값 검산 — 낮 240초 + 밤 150초 = 390초 동안 0.25/s면 97.5 소진 (경고 임계 훨씬 밑).
            float afterCycle = HungerMath.Step(100f, Curve(), 390f);

            Assert.That(afterCycle, Is.EqualTo(2.5f).Within(0.001f));
            Assert.That(HungerMath.GetStress(afterCycle, Curve()), Is.EqualTo(HungerStress.Hungry),
                "한 사이클을 굶으면 경고 상태다");
        }

        [Test]
        public void 섭취는_회복하되_최대치에서_잘린다()
        {
            HungerCurve curve = Curve();

            Assert.That(HungerMath.Restore(50f, 35f, curve), Is.EqualTo(85f).Within(0.001f));
            Assert.That(HungerMath.Restore(90f, 35f, curve), Is.EqualTo(100f), "최대 100에서 잘린다");
        }

        [Test]
        public void 음수_회복량은_허기를_깎지_못한다()
        {
            Assert.That(HungerMath.Restore(50f, -10f, Curve()), Is.EqualTo(50f));
        }

        [Test]
        public void 기아_피해는_0에_도달했을_때만_발생한다()
        {
            HungerCurve curve = Curve();

            Assert.That(HungerMath.GetDamagePerSecond(1f, curve), Is.EqualTo(0f), "허기가 남으면 무해");
            Assert.That(HungerMath.GetDamagePerSecond(0f, curve), Is.EqualTo(2f).Within(0.001f));
        }

        [Test]
        public void 압박_단계는_피해_전_경고_임계에서_먼저_바뀐다()
        {
            HungerCurve curve = Curve();

            Assert.That(HungerMath.GetStress(100f, curve), Is.EqualTo(HungerStress.None));
            Assert.That(HungerMath.GetStress(30f, curve), Is.EqualTo(HungerStress.Hungry), "경고 임계 자체 포함");
            Assert.That(HungerMath.GetStress(0f, curve), Is.EqualTo(HungerStress.Starving));
        }

        [Test]
        public void 음수_시간에도_허기가_튀지_않는다()
        {
            Assert.That(HungerMath.Step(50f, Curve(), -1f), Is.EqualTo(50f).Within(0.001f));
        }
    }
}
