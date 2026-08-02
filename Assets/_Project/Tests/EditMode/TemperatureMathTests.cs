using Game.Gameplay.Player;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 체온 표류·회복·피해 계산 검증 (기획서 §4.2 — 사막 낮 열사병 / 밤 급랭,
    /// M4의 완화 수단은 건축물 아래 차폐).
    /// </summary>
    public sealed class TemperatureMathTests
    {
        private const float Normal = 36.5f;

        private static TemperatureCurve Curve()
        {
            return new TemperatureCurve(
                normalBody: Normal, minBody: 30f, maxBody: 42f,
                comfortMin: 10f, comfortMax: 32f,
                driftRatePerDegree: 0.012f, recoveryRate: 0.35f,
                heatWarnThreshold: 38f, heatDamageThreshold: 39f,
                coldWarnThreshold: 35f, coldDamageThreshold: 34f,
                damagePerDegreePerSecond: 3f, shelterFactor: 0.8f);
        }

        [Test]
        public void 쾌적대_안에서는_정상_체온으로_회복한다()
        {
            TemperatureCurve curve = Curve();

            float hot = TemperatureMath.Step(39f, 22f, curve, 1f);
            float cold = TemperatureMath.Step(34f, 22f, curve, 1f);

            Assert.That(hot, Is.LessThan(39f), "정상 체온 쪽으로 내려간다");
            Assert.That(cold, Is.GreaterThan(34f), "정상 체온 쪽으로 올라간다");
        }

        [Test]
        public void 쾌적대를_넘는_더위는_체온을_올린다()
        {
            // 사막 낮 45℃ — 쾌적 상한 32를 13℃ 초과 → 0.012 × 13 = 0.156 ℃/s
            float next = TemperatureMath.Step(Normal, 45f, Curve(), 1f);

            Assert.That(next, Is.EqualTo(Normal + 0.156f).Within(0.001f));
        }

        [Test]
        public void 쾌적대를_밑도는_추위는_체온을_내린다()
        {
            // 사막 밤 2℃ — 쾌적 하한 10을 8℃ 밑돎 → 0.012 × 8 = 0.096 ℃/s
            float next = TemperatureMath.Step(Normal, 2f, Curve(), 1f);

            Assert.That(next, Is.EqualTo(Normal - 0.096f).Within(0.001f));
        }

        [Test]
        public void 체온은_최소_최대_범위를_벗어나지_않는다()
        {
            TemperatureCurve curve = Curve();

            float veryHot = TemperatureMath.Step(41.9f, 100f, curve, 100f);
            float veryCold = TemperatureMath.Step(30.1f, -100f, curve, 100f);

            Assert.That(veryHot, Is.EqualTo(42f).Within(0.001f));
            Assert.That(veryCold, Is.EqualTo(30f).Within(0.001f));
        }

        [Test]
        public void 차폐는_더위를_쾌적대로_당긴다()
        {
            TemperatureCurve curve = Curve();

            // 쾌적 중심 21℃, 차폐 계수 0.8 → 45 → 45 + (21−45)×0.8 = 25.8
            float shelteredHot = TemperatureMath.ResolveAmbient(45f, true, curve);

            Assert.That(shelteredHot, Is.EqualTo(25.8f).Within(0.001f));
            Assert.That(shelteredHot, Is.LessThan(curve.ComfortMax), "차폐 안에서는 쾌적대에 들어온다");
        }

        [Test]
        public void 차폐는_추위를_막지_못한다()
        {
            // 건축물 아래는 '그늘'이다 — 지붕이 햇빛은 가려도 난방은 되지 않는다.
            // 추위 대응은 난방 건축물·방한 장비(M5)의 몫이다.
            TemperatureCurve curve = Curve();

            Assert.That(TemperatureMath.ResolveAmbient(2f, true, curve), Is.EqualTo(2f), "밤 급랭은 그대로");
            Assert.That(TemperatureMath.ResolveAmbient(-20f, true, curve), Is.EqualTo(-20f), "혹한도 그대로");
        }

        [Test]
        public void 쾌적대_안에서는_차폐가_아무것도_바꾸지_않는다()
        {
            TemperatureCurve curve = Curve();

            Assert.That(TemperatureMath.ResolveAmbient(22f, true, curve), Is.EqualTo(22f));
            Assert.That(TemperatureMath.ResolveAmbient(32f, true, curve), Is.EqualTo(32f), "쾌적 상한 경계");
        }

        [Test]
        public void 사막_밤에는_그늘에_있어도_체온이_계속_내려간다()
        {
            TemperatureCurve curve = Curve();

            float sheltered = TemperatureMath.Step(Normal, TemperatureMath.ResolveAmbient(2f, true, curve), curve, 1f);

            Assert.That(sheltered, Is.LessThan(Normal), "그늘 안에서도 추위는 진행된다");
        }

        [Test]
        public void 차폐가_없으면_환경_온도가_그대로다()
        {
            Assert.That(TemperatureMath.ResolveAmbient(45f, false, Curve()), Is.EqualTo(45f));
        }

        [Test]
        public void 사막_낮에_그늘로_들어가면_체온이_회복으로_돌아선다()
        {
            TemperatureCurve curve = Curve();

            float exposed = TemperatureMath.Step(38f, TemperatureMath.ResolveAmbient(45f, false, curve), curve, 1f);
            float sheltered = TemperatureMath.Step(38f, TemperatureMath.ResolveAmbient(45f, true, curve), curve, 1f);

            Assert.That(exposed, Is.GreaterThan(38f), "노출 상태에서는 계속 오른다");
            Assert.That(sheltered, Is.LessThan(38f), "차폐 안에서는 내려간다");
        }

        [Test]
        public void 피해는_임계를_벗어난_만큼만_발생한다()
        {
            TemperatureCurve curve = Curve();

            Assert.That(TemperatureMath.GetDamagePerSecond(Normal, curve), Is.EqualTo(0f));
            Assert.That(TemperatureMath.GetDamagePerSecond(39f, curve), Is.EqualTo(0f), "임계 자체는 피해 없음");
            Assert.That(TemperatureMath.GetDamagePerSecond(40f, curve), Is.EqualTo(3f).Within(0.001f));
            Assert.That(TemperatureMath.GetDamagePerSecond(33f, curve), Is.EqualTo(3f).Within(0.001f));
        }

        [Test]
        public void 압박_단계는_피해_전_경고_임계에서_먼저_바뀐다()
        {
            TemperatureCurve curve = Curve();

            Assert.That(TemperatureMath.GetStress(Normal, curve), Is.EqualTo(TemperatureStress.None));
            Assert.That(TemperatureMath.GetStress(38.5f, curve), Is.EqualTo(TemperatureStress.Heat), "피해(39) 전에 경고");
            Assert.That(TemperatureMath.GetStress(34.5f, curve), Is.EqualTo(TemperatureStress.Cold), "피해(34) 전에 경고");
        }

        [Test]
        public void 음수_시간과_음수_속도에도_체온이_튀지_않는다()
        {
            TemperatureCurve curve = Curve();

            Assert.That(TemperatureMath.Step(Normal, 45f, curve, -1f), Is.EqualTo(Normal).Within(0.001f));
        }
    }
}
