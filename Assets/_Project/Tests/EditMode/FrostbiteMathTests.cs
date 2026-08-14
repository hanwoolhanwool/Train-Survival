using Game.Gameplay.Player;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 부위별 동상 검증 (기획서 §4.4 북극, M7 3차 결정 ② · ⑥).
    /// 고정하는 계약은 셋이다: <b>부위 단열이 진행 속도를 가른다</b> ·
    /// <b>완화는 네 부위 전부에 적용된다</b> · <b>증상은 단계 합계 1축이다</b>.
    /// </summary>
    public sealed class FrostbiteMathTests
    {
        private const float Freezing = 30f;

        private const float Warm = 36.5f;

        private static FrostbiteCurve Curve()
        {
            return new FrostbiteCurve(
                coldThreshold: 35f,
                progressPerSecond: 0.02f, recoveryPerSecond: 0.05f,
                insulationWeight: 4f, mitigationWeight: 3f,
                mildThreshold: 0.4f, severeThreshold: 1f,
                moveSpeedPenaltyPerStage: 0.05f, minMoveSpeedMultiplier: 0.6f);
        }

        // ── 진행·회복 ─────────────────────────────────────────────────

        [Test]
        public void 저온_경고_임계_미만에서만_진행한다()
        {
            FrostbiteCurve curve = Curve();

            float cold = FrostbiteMath.StepProgress(0f, 34.9f, 0f, 0f, curve, 1f);
            float atThreshold = FrostbiteMath.StepProgress(0.5f, 35f, 0f, 0f, curve, 1f);

            Assert.That(cold, Is.EqualTo(0.02f).Within(0.0001f), "임계 미만 = 진행");
            Assert.That(atThreshold, Is.EqualTo(0.45f).Within(0.0001f), "임계 자체는 회복 쪽");
        }

        [Test]
        public void 쾌적대로_돌아오면_회복한다()
        {
            float next = FrostbiteMath.StepProgress(0.5f, Warm, 0f, 0f, Curve(), 1f);

            Assert.That(next, Is.EqualTo(0.45f).Within(0.0001f));
        }

        [Test]
        public void 진행도는_0과_중증_임계_사이로_잘린다()
        {
            FrostbiteCurve curve = Curve();

            float floor = FrostbiteMath.StepProgress(0.1f, Warm, 0f, 0f, curve, 100f);
            float ceiling = FrostbiteMath.StepProgress(0.9f, Freezing, 0f, 0f, curve, 1000f);

            Assert.That(floor, Is.EqualTo(0f));
            Assert.That(ceiling, Is.EqualTo(curve.SevereThreshold), "중증 위로는 더 쌓이지 않는다");
        }

        // ── 부위 단열이 속도를 가른다 (결정 ②의 핵심) ──────────────────────

        [Test]
        public void 부위_단열이_진행_속도를_나눈다()
        {
            FrostbiteCurve curve = Curve();

            float bare = FrostbiteMath.ResolveProgressRate(0f, 0f, curve);
            float hooded = FrostbiteMath.ResolveProgressRate(0.2f, 0f, curve);
            float parka = FrostbiteMath.ResolveProgressRate(0.4f, 0f, curve);

            Assert.That(bare, Is.EqualTo(0.02f).Within(0.0001f), "맨 부위 = 기본 속도");
            Assert.That(hooded, Is.EqualTo(0.02f / 1.8f).Within(0.0001f), "후드 0.2 → 저항 1.8");
            Assert.That(parka, Is.EqualTo(0.02f / 2.6f).Within(0.0001f), "파카 0.4 → 저항 2.6");
            Assert.That(parka, Is.LessThan(hooded), "두꺼운 부위일수록 느리다");
        }

        [Test]
        public void 머리만_비우면_머리가_먼저_중증이_된다()
        {
            // 결정 ②의 "어느 부위를 비웠는가 = 얼마나 빨리 심해지는가"를 그대로 재현한다.
            FrostbiteCurve curve = Curve();

            float head = 0f;
            float body = 0f;
            for (int i = 0; i < 60; i++)
            {
                head = FrostbiteMath.StepProgress(head, Freezing, 0f, 0f, curve, 1f);
                body = FrostbiteMath.StepProgress(body, Freezing, 0.4f, 0f, curve, 1f);
            }

            Assert.That(FrostbiteMath.GetStage(head, curve), Is.EqualTo(FrostbiteStage.Severe));
            Assert.That(FrostbiteMath.GetStage(body, curve), Is.EqualTo(FrostbiteStage.Mild),
                "같은 시간 뒤 파카 부위는 아직 경증");
        }

        [Test]
        public void 음수_단열은_동상을_가속하지_않는다()
        {
            // 역효과 장비(사막 로브)의 벌은 "맨몸과 같다"까지다 — 동상 가속 축은 두지 않는다.
            FrostbiteCurve curve = Curve();

            Assert.That(
                FrostbiteMath.ResolveProgressRate(-0.5f, 0f, curve),
                Is.EqualTo(FrostbiteMath.ResolveProgressRate(0f, 0f, curve)).Within(0.0001f));
        }

        // ── 완화는 몸 전체 (결정 ⑥의 핵심 규약) ────────────────────────────

        [Test]
        public void 완화_버프는_네_부위_모두의_진행을_늦춘다()
        {
            FrostbiteCurve curve = Curve();

            // 부위 단열이 제각각이어도 같은 완화가 곱해져 전부 느려진다.
            float[] insulations = { 0f, 0.4f, 0.2f, 0.15f };
            for (int i = 0; i < insulations.Length; i++)
            {
                float plain = FrostbiteMath.ResolveProgressRate(insulations[i], 0f, curve);
                float mitigated = FrostbiteMath.ResolveProgressRate(insulations[i], 0.45f, curve);

                Assert.That(mitigated, Is.LessThan(plain), "부위 " + i + " 도 함께 느려진다");
            }
        }

        [Test]
        public void 완화와_부위_단열은_같은_저항에_합산된다()
        {
            FrostbiteCurve curve = Curve();

            // 저항 = 1 + 0.2×4 + 0.5×3 = 3.3
            float rate = FrostbiteMath.ResolveProgressRate(0.2f, 0.5f, curve);

            Assert.That(rate, Is.EqualTo(0.02f / 3.3f).Within(0.0001f));
        }

        // ── 단계 이산화 ───────────────────────────────────────────────

        [Test]
        public void 단계_경계값은_이상이면_그_단계다()
        {
            FrostbiteCurve curve = Curve();

            Assert.That(FrostbiteMath.GetStage(0.399f, curve), Is.EqualTo(FrostbiteStage.None));
            Assert.That(FrostbiteMath.GetStage(0.4f, curve), Is.EqualTo(FrostbiteStage.Mild), "경계 = 경증");
            Assert.That(FrostbiteMath.GetStage(0.999f, curve), Is.EqualTo(FrostbiteStage.Mild));
            Assert.That(FrostbiteMath.GetStage(1f, curve), Is.EqualTo(FrostbiteStage.Severe), "경계 = 중증");
        }

        // ── 증상 1축 (결정 ②) ─────────────────────────────────────────

        [Test]
        public void 단계_합계가_이동속도_배율을_정한다()
        {
            FrostbiteCurve curve = Curve();

            Assert.That(FrostbiteMath.GetMoveSpeedMultiplier(0, curve), Is.EqualTo(1f), "동상 없음 = 저하 없음");
            Assert.That(FrostbiteMath.GetMoveSpeedMultiplier(1, curve), Is.EqualTo(0.95f).Within(0.0001f));
            Assert.That(FrostbiteMath.GetMoveSpeedMultiplier(4, curve), Is.EqualTo(0.8f).Within(0.0001f));
            Assert.That(FrostbiteMath.GetMoveSpeedMultiplier(8, curve), Is.EqualTo(0.6f).Within(0.0001f),
                "전 부위 중증 = 하한");
        }

        [Test]
        public void 이동속도_배율은_하한_아래로_내려가지_않는다()
        {
            // 합계는 8이 최대지만, 곡선이 바뀌어도 이동 불가가 되지 않아야 한다.
            Assert.That(FrostbiteMath.GetMoveSpeedMultiplier(100, Curve()), Is.EqualTo(0.6f).Within(0.0001f));
        }

        [Test]
        public void 화면_결빙_강도는_합계에_비례한다()
        {
            Assert.That(FrostbiteMath.GetFreezeIntensity(0), Is.EqualTo(0f));
            Assert.That(FrostbiteMath.GetFreezeIntensity(4), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(FrostbiteMath.GetFreezeIntensity(FrostbiteMath.MaxStageSum), Is.EqualTo(1f));
        }

        // ── 비트팩 복제 페이로드 ────────────────────────────────────────

        [Test]
        public void 부위_4개_단계가_1바이트를_왕복한다()
        {
            byte packed = FrostbiteMath.Pack(
                FrostbiteStage.Severe, FrostbiteStage.None, FrostbiteStage.Mild, FrostbiteStage.Severe);

            Assert.That(FrostbiteMath.Unpack(packed, 0), Is.EqualTo(FrostbiteStage.Severe), "머리");
            Assert.That(FrostbiteMath.Unpack(packed, 1), Is.EqualTo(FrostbiteStage.None), "상체");
            Assert.That(FrostbiteMath.Unpack(packed, 2), Is.EqualTo(FrostbiteStage.Mild), "하체");
            Assert.That(FrostbiteMath.Unpack(packed, 3), Is.EqualTo(FrostbiteStage.Severe), "발");
            Assert.That(FrostbiteMath.SumStages(packed), Is.EqualTo(5));
        }

        [Test]
        public void 모든_단계_조합이_손실_없이_왕복한다()
        {
            for (int head = 0; head <= 2; head++)
            {
                for (int body = 0; body <= 2; body++)
                {
                    for (int legs = 0; legs <= 2; legs++)
                    {
                        for (int feet = 0; feet <= 2; feet++)
                        {
                            byte packed = FrostbiteMath.Pack(
                                (FrostbiteStage)head, (FrostbiteStage)body,
                                (FrostbiteStage)legs, (FrostbiteStage)feet);

                            Assert.That((int)FrostbiteMath.Unpack(packed, 0), Is.EqualTo(head));
                            Assert.That((int)FrostbiteMath.Unpack(packed, 1), Is.EqualTo(body));
                            Assert.That((int)FrostbiteMath.Unpack(packed, 2), Is.EqualTo(legs));
                            Assert.That((int)FrostbiteMath.Unpack(packed, 3), Is.EqualTo(feet));
                            Assert.That(FrostbiteMath.SumStages(packed),
                                Is.EqualTo(head + body + legs + feet));
                        }
                    }
                }
            }
        }

        [Test]
        public void 범위_밖_부위_인덱스는_단계_없음이다()
        {
            byte packed = FrostbiteMath.Pack(
                FrostbiteStage.Severe, FrostbiteStage.Severe, FrostbiteStage.Severe, FrostbiteStage.Severe);

            Assert.That(FrostbiteMath.Unpack(packed, -1), Is.EqualTo(FrostbiteStage.None));
            Assert.That(FrostbiteMath.Unpack(packed, FrostbiteMath.PartCount), Is.EqualTo(FrostbiteStage.None));
        }

        [Test]
        public void 동상_없음은_0바이트다()
        {
            byte packed = FrostbiteMath.Pack(
                FrostbiteStage.None, FrostbiteStage.None, FrostbiteStage.None, FrostbiteStage.None);

            Assert.That(packed, Is.EqualTo(0), "복제·이벤트 판정이 0 비교로 끝난다");
        }

        // ── 설정 에셋 → 곡선 변환 ───────────────────────────────────────

        [Test]
        public void 설정_곡선의_진행_임계는_저온_경고_임계와_같다()
        {
            // HUD 경고와 동상 진행이 같은 선에서 시작한다 — "떨기 시작하면 얼기 시작한다".
            var settings = UnityEngine.ScriptableObject.CreateInstance<TemperatureSettings>();
            try
            {
                FrostbiteCurve frostbite = settings.ToFrostbiteCurve();
                TemperatureCurve temperature = settings.ToCurve();

                Assert.That(frostbite.ColdThreshold, Is.EqualTo(temperature.ColdWarnThreshold));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(settings);
            }
        }

        [Test]
        public void 음수_시간에도_진행도가_튀지_않는다()
        {
            Assert.That(
                FrostbiteMath.StepProgress(0.5f, Freezing, 0f, 0f, Curve(), -1f),
                Is.EqualTo(0.5f).Within(0.0001f));
        }
    }
}
