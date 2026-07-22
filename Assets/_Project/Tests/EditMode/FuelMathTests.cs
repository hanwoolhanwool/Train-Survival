using Game.Gameplay.World;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>연료 소모·감속 계산 검증 (개발 가이드 M2 — 연료 소모→감속).</summary>
    public sealed class FuelMathTests
    {
        [Test]
        public void 소모는_시간에_비례하고_0_미만으로_내려가지_않는다()
        {
            Assert.That(FuelMath.ConsumeFuel(10f, 2f, 1f), Is.EqualTo(8f).Within(0.001f));
            Assert.That(FuelMath.ConsumeFuel(1f, 2f, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void 충전은_최대_저장량을_넘지_않는다()
        {
            Assert.That(FuelMath.AddFuel(95f, 10f, 100f), Is.EqualTo(100f));
            Assert.That(FuelMath.AddFuel(50f, 10f, 100f), Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void 음수_충전량은_무시된다()
        {
            Assert.That(FuelMath.AddFuel(50f, -10f, 100f), Is.EqualTo(50f));
        }

        [Test]
        public void 연료가_있으면_기본_속도_고갈이면_최저_유지_속도다()
        {
            Assert.That(FuelMath.ComputeTargetScrollSpeed(6f, 10f, 0.3f), Is.EqualTo(6f));
            Assert.That(FuelMath.ComputeTargetScrollSpeed(6f, 0f, 0.3f), Is.EqualTo(1.8f).Within(0.001f));
        }

        [Test]
        public void 속도는_가감속률에_따라_목표로_수렴한다()
        {
            float speed = 6f;
            for (int i = 0; i < 300; i++)
            {
                speed = FuelMath.StepScrollSpeed(speed, 1.8f, changeRate: 1.5f, deltaTime: 0.02f);
            }

            Assert.That(speed, Is.EqualTo(1.8f).Within(0.001f));
        }

        [Test]
        public void 한_스텝의_속도_변화는_가감속률을_넘지_않는다()
        {
            float next = FuelMath.StepScrollSpeed(6f, 1.8f, changeRate: 1.5f, deltaTime: 0.02f);

            Assert.That(6f - next, Is.EqualTo(0.03f).Within(0.001f), "1.5 m/s² × 0.02 s = 0.03 m/s");
        }
    }
}
