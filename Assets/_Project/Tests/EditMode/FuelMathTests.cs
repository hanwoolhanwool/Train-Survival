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

        [Test]
        public void 소모율은_끌고_있는_칸_수에_비례해_가산된다()
        {
            Assert.That(FuelMath.ComputeConsumptionPerSecond(0.5f, 0.15f, 2), Is.EqualTo(0.8f).Within(0.001f),
                "기본 편성(화물 2칸) = 기존 소모율 0.8 유지");
            Assert.That(FuelMath.ComputeConsumptionPerSecond(0.5f, 0.15f, 4), Is.EqualTo(1.1f).Within(0.001f),
                "증설 2칸 → 소모 증가 (기획서 §7.1 트레이드오프)");
            Assert.That(FuelMath.ComputeConsumptionPerSecond(0.5f, 0.15f, 0), Is.EqualTo(0.5f).Within(0.001f),
                "칸을 모두 잃으면 기본 소모만 남는다");
        }

        [Test]
        public void 소모율_음수_입력은_방어된다()
        {
            Assert.That(FuelMath.ComputeConsumptionPerSecond(-1f, 0.15f, 2), Is.EqualTo(0.3f).Within(0.001f));
            Assert.That(FuelMath.ComputeConsumptionPerSecond(0.5f, -1f, 2), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(FuelMath.ComputeConsumptionPerSecond(0.5f, 0.15f, -3), Is.EqualTo(0.5f).Within(0.001f));
        }

        // ── 연료를 태우는 건축물 (M7 3차 강화 난방로 — 결정 ③-ⓑ) ────────────────

        [Test]
        public void 강화_난방로가_없으면_주행_소모와_완전히_같다()
        {
            // 무회귀 — 기존 3지역은 이 건축물 자체가 없으므로 소모율이 한 톨도 달라지지 않는다.
            float driving = FuelMath.ComputeConsumptionPerSecond(0.5f, 0.15f, 2);

            Assert.That(FuelMath.AddStructureConsumption(driving, 0.6f, 0), Is.EqualTo(driving));
        }

        [Test]
        public void 강화_난방로는_수만큼_주행_연료를_더_태운다()
        {
            // "난방을 켜 두면 주행 연료가 준다" — 북극의 트레이드오프 (기획서 §7.1과 같은 축).
            float driving = FuelMath.ComputeConsumptionPerSecond(0.5f, 0.15f, 2);

            Assert.That(FuelMath.AddStructureConsumption(driving, 0.6f, 1),
                Is.EqualTo(1.4f).Within(0.001f), "1기 = 주행(0.8) + 0.6");
            Assert.That(FuelMath.AddStructureConsumption(driving, 0.6f, 2),
                Is.EqualTo(2f).Within(0.001f), "2기 = 주행의 2.5배");
        }

        [Test]
        public void 건축물_소모율도_음수_입력이_방어된다()
        {
            Assert.That(FuelMath.AddStructureConsumption(-1f, 0.6f, 1), Is.EqualTo(0.6f).Within(0.001f));
            Assert.That(FuelMath.AddStructureConsumption(0.8f, -1f, 1), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(FuelMath.AddStructureConsumption(0.8f, 0.6f, -2), Is.EqualTo(0.8f).Within(0.001f));
        }
    }
}
