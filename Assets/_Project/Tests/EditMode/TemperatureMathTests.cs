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
                driftRatePerDegree: 0.012f, recoveryRate: 0.35f, cooldownRate: 0.05f,
                heatWarnThreshold: 38f, heatDamageThreshold: 39f,
                coldWarnThreshold: 35f, coldDamageThreshold: 34f,
                damagePerDegreePerSecond: 3f, shelterFactor: 0.8f, heaterFactor: 0.8f);
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
        public void 쾌적대_하향은_상향보다_느리다()
        {
            // M5 7차 2차 (검증 발견) — 스튜 온기(38 ℃)가 돔 안에서 순식간에 증발하지 않게,
            // 수렴점 위에서 내려오는 속도(0.05)를 추위 복귀 속도(0.35)와 분리한다.
            TemperatureCurve curve = Curve();

            float down = TemperatureMath.Step(38f, 22f, curve, 1f);
            float up = TemperatureMath.Step(35f, 22f, curve, 1f);

            Assert.That(down, Is.EqualTo(38f - 0.05f).Within(0.001f), "하향 = 느린 계수");
            Assert.That(up, Is.EqualTo(35f + 0.35f).Within(0.001f), "상향 = 기존 회복 속도");
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

        // ── 즉시 체온 상한의 국면화 (M5 6차 — 5차 G3) ─────────────────────────

        [Test]
        public void 더위_국면이면_즉시_체온_상한이_올라간다()
        {
            // 사막 낮 45℃ — 쾌적 상한 32 초과 = 더위 국면 → 39℃ (고온 피해 임계 직전).
            Assert.That(TemperatureMath.ResolveWarmthCeiling(45f, 38f, 39f, Curve()), Is.EqualTo(39f));
        }

        [Test]
        public void 더위_국면이_아니면_기존_상한_그대로다()
        {
            TemperatureCurve curve = Curve();

            Assert.That(TemperatureMath.ResolveWarmthCeiling(22f, 38f, 39f, curve), Is.EqualTo(38f), "쾌적대 안");
            Assert.That(TemperatureMath.ResolveWarmthCeiling(2f, 38f, 39f, curve), Is.EqualTo(38f), "추위 국면");
            Assert.That(TemperatureMath.ResolveWarmthCeiling(32f, 38f, 39f, curve), Is.EqualTo(38f), "쾌적 상한 경계는 더위가 아니다");
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

        // ── 보온 장비 기본 체온 상향 (M5 7차 — 5차 개선 5번) ─────────────────────────

        private static TemperatureCurve WarmedCurve(float bodyWarmthBonus)
        {
            return new TemperatureCurve(
                normalBody: Normal + bodyWarmthBonus, minBody: 30f, maxBody: 42f,
                comfortMin: 10f, comfortMax: 32f,
                driftRatePerDegree: 0.012f, recoveryRate: 0.35f, cooldownRate: 0.05f,
                heatWarnThreshold: 38f, heatDamageThreshold: 39f,
                coldWarnThreshold: 35f, coldDamageThreshold: 34f,
                damagePerDegreePerSecond: 3f, shelterFactor: 0.8f, heaterFactor: 0.8f);
        }

        [Test]
        public void 상향_곡선의_쾌적대_수렴점은_높아진_체온이다()
        {
            // 방한 착용(+0.5) — 쾌적대 안에서 36.5가 아니라 37.0으로 수렴한다.
            TemperatureCurve curve = WarmedCurve(0.5f);

            float fromBelow = TemperatureMath.Step(36.5f, 22f, curve, 100f);
            float fromAbove = TemperatureMath.Step(38f, 22f, curve, 100f);

            Assert.That(fromBelow, Is.EqualTo(37f).Within(0.001f), "평상 체온이 상향 값까지 올라간다");
            Assert.That(fromAbove, Is.EqualTo(37f).Within(0.001f), "돔에 들어가도 상향 값까지만 내려간다");
        }

        [Test]
        public void 상향_곡선도_추위_표류는_그대로다()
        {
            // 체온 상향은 수렴점 축이다 — 표류 속도(쾌적대 이탈 비례)는 바뀌지 않는다.
            float plain = TemperatureMath.Step(Normal, 2f, Curve(), 1f);
            float warmed = TemperatureMath.Step(Normal, 2f, WarmedCurve(0.5f), 1f);

            Assert.That(warmed, Is.EqualTo(plain).Within(0.001f), "같은 시작 체온이면 같은 속도로 식는다");
        }

        [Test]
        public void 설정_곡선_변환은_수렴점만_밀어_올린다()
        {
            var settings = UnityEngine.ScriptableObject.CreateInstance<TemperatureSettings>();
            try
            {
                TemperatureCurve plain = settings.ToCurve();
                TemperatureCurve warmed = settings.ToCurve(0.7f);
                TemperatureCurve negative = settings.ToCurve(-1f);

                Assert.That(warmed.NormalBody, Is.EqualTo(plain.NormalBody + 0.7f).Within(0.001f));
                Assert.That(warmed.ComfortMin, Is.EqualTo(plain.ComfortMin), "쾌적대는 그대로");
                Assert.That(warmed.ComfortMax, Is.EqualTo(plain.ComfortMax), "쾌적대는 그대로");
                Assert.That(warmed.HeatWarnThreshold, Is.EqualTo(plain.HeatWarnThreshold), "경고 임계는 그대로");
                Assert.That(negative.NormalBody, Is.EqualTo(plain.NormalBody), "음수 보너스는 무시된다");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void 차폐는_더위를_쾌적대로_당긴다()
        {
            TemperatureCurve curve = Curve();

            // 쾌적 중심 21℃, 차폐 계수 0.8 → 45 → 45 + (21−45)×0.8 = 25.8
            float shelteredHot = TemperatureMath.ResolveAmbient(45f, true, false, curve);

            Assert.That(shelteredHot, Is.EqualTo(25.8f).Within(0.001f));
            Assert.That(shelteredHot, Is.LessThan(curve.ComfortMax), "차폐 안에서는 쾌적대에 들어온다");
        }

        [Test]
        public void 차폐는_추위를_막지_못한다()
        {
            // 돔 아래는 '그늘'이다 — 지붕이 햇빛은 가려도 난방은 되지 않는다.
            // 추위 대응은 난방 건축물(Heater)·방한 장비의 몫이다 (M5 3차 종류화).
            TemperatureCurve curve = Curve();

            Assert.That(TemperatureMath.ResolveAmbient(2f, true, false, curve), Is.EqualTo(2f), "밤 급랭은 그대로");
            Assert.That(TemperatureMath.ResolveAmbient(-20f, true, false, curve), Is.EqualTo(-20f), "혹한도 그대로");
        }

        [Test]
        public void 난방은_추위를_쾌적대로_당긴다()
        {
            TemperatureCurve curve = Curve();

            // 쾌적 중심 21℃, 난방 계수 0.8 → 2 → 2 + (21−2)×0.8 = 17.2
            float heatedCold = TemperatureMath.ResolveAmbient(2f, false, true, curve);

            Assert.That(heatedCold, Is.EqualTo(17.2f).Within(0.001f));
            Assert.That(heatedCold, Is.GreaterThan(curve.ComfortMin), "난방 칸 위에서는 쾌적대에 들어온다");
        }

        [Test]
        public void 난방은_더위를_완화하지_않는다()
        {
            // 화로는 그늘을 만들지 않는다 — 사막 낮 대응은 여전히 돔(그늘)의 몫이다.
            TemperatureCurve curve = Curve();

            Assert.That(TemperatureMath.ResolveAmbient(45f, false, true, curve), Is.EqualTo(45f));
        }

        [Test]
        public void 사막_밤_난방_칸_위에서는_체온이_회복된다()
        {
            TemperatureCurve curve = Curve();

            float heated = TemperatureMath.Step(35f, TemperatureMath.ResolveAmbient(2f, false, true, curve), curve, 1f);

            Assert.That(heated, Is.GreaterThan(35f), "실효 온도가 쾌적대라 정상 체온으로 돌아선다");
        }

        [Test]
        public void 사막_밤_2도_노숙은_피해_임계까지_내려간다()
        {
            // 복원된 사막 밤 2℃(M5 3차)의 압박 검증 — 난방·차폐 없이 밤 150초를 노숙하면
            // 체온이 피해 임계(34℃) 밑으로 내려가 지속 피해가 시작된다.
            TemperatureCurve curve = Curve();

            float temperature = Normal;
            for (int i = 0; i < 150; i++)
            {
                temperature = TemperatureMath.Step(temperature, TemperatureMath.ResolveAmbient(2f, false, false, curve), curve, 1f);
            }

            Assert.That(temperature, Is.LessThan(curve.ColdDamageThreshold), "밤 노숙은 확정 피해");
            Assert.That(TemperatureMath.GetDamagePerSecond(temperature, curve), Is.GreaterThan(0f));
        }

        [Test]
        public void 쾌적대_안에서는_차폐가_아무것도_바꾸지_않는다()
        {
            TemperatureCurve curve = Curve();

            Assert.That(TemperatureMath.ResolveAmbient(22f, true, false, curve), Is.EqualTo(22f));
            Assert.That(TemperatureMath.ResolveAmbient(32f, true, false, curve), Is.EqualTo(32f), "쾌적 상한 경계");
        }

        [Test]
        public void 사막_밤에는_그늘에_있어도_체온이_계속_내려간다()
        {
            TemperatureCurve curve = Curve();

            float sheltered = TemperatureMath.Step(Normal, TemperatureMath.ResolveAmbient(2f, true, false, curve), curve, 1f);

            Assert.That(sheltered, Is.LessThan(Normal), "그늘 안에서도 추위는 진행된다");
        }

        [Test]
        public void 차폐가_없으면_환경_온도가_그대로다()
        {
            Assert.That(TemperatureMath.ResolveAmbient(45f, false, false, Curve()), Is.EqualTo(45f));
        }

        [Test]
        public void 사막_낮에_그늘로_들어가면_체온이_회복으로_돌아선다()
        {
            TemperatureCurve curve = Curve();

            float exposed = TemperatureMath.Step(38f, TemperatureMath.ResolveAmbient(45f, false, false, curve), curve, 1f);
            float sheltered = TemperatureMath.Step(38f, TemperatureMath.ResolveAmbient(45f, true, false, curve), curve, 1f);

            Assert.That(exposed, Is.GreaterThan(38f), "노출 상태에서는 계속 오른다");
            Assert.That(sheltered, Is.LessThan(38f), "차폐 안에서는 내려간다");
        }

        // ── 장비 단열 (기획서 §6.3, M5 3차 — 양방향 계수, 음수 = 역효과) ──────────────────

        [Test]
        public void 단열은_추위를_쾌적_하한으로_당긴다()
        {
            TemperatureCurve curve = Curve();

            // 사막 밤 2℃ + 방한 0.5 → 2 + (10−2)×0.5 = 6
            float insulated = TemperatureMath.ApplyInsulation(2f, 0.5f, 0f, curve);

            Assert.That(insulated, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void 단열은_더위를_쾌적_상한으로_당긴다()
        {
            TemperatureCurve curve = Curve();

            // 사막 낮 45℃ + 내열 0.5 → 45 + (32−45)×0.5 = 38.5
            float insulated = TemperatureMath.ApplyInsulation(45f, 0f, 0.5f, curve);

            Assert.That(insulated, Is.EqualTo(38.5f).Within(0.001f));
        }

        [Test]
        public void 음수_단열은_이탈을_키운다()
        {
            // 가죽 옷의 사막 낮 역효과 (§6.3 — "북극 필수 / 사막 낮에는 역효과").
            TemperatureCurve curve = Curve();

            float worsened = TemperatureMath.ApplyInsulation(45f, 0f, -0.15f, curve);

            Assert.That(worsened, Is.EqualTo(46.95f).Within(0.001f), "45 + (32−45)×(−0.15) = 46.95");
        }

        [Test]
        public void 쾌적대_안에서는_단열이_아무것도_바꾸지_않는다()
        {
            TemperatureCurve curve = Curve();

            Assert.That(TemperatureMath.ApplyInsulation(22f, 0.5f, 0.5f, curve), Is.EqualTo(22f));
        }

        [Test]
        public void 단열_계수는_상한_0_9로_잘려_완전_무효화되지_않는다()
        {
            TemperatureCurve curve = Curve();

            // 계수 1.5를 넣어도 0.9로 잘린다 → 2 + (10−2)×0.9 = 9.2 (여전히 쾌적대 밖)
            float insulated = TemperatureMath.ApplyInsulation(2f, 1.5f, 0f, curve);

            Assert.That(insulated, Is.EqualTo(9.2f).Within(0.001f));
            Assert.That(insulated, Is.LessThan(curve.ComfortMin), "장비만으로는 혹한을 완전히 못 막는다");
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
