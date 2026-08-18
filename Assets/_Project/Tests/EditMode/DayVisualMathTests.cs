using Game.Gameplay.Cycle;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 낮/밤 시각 연출의 순수 수식 검증 (M8 2차). 연출이 "예쁜가"는 여기서 판정할 수 없다 —
    /// 이 테스트가 지키는 것은 <b>경계·연속성·하한</b> 셋뿐이다.
    /// </summary>
    public sealed class DayVisualMathTests
    {
        private const float FadeSeconds = 6f;
        private const float DayDuration = 240f;
        private const float NightDuration = 150f;

        private const float RiseElevation = 10f;
        private const float NoonElevation = 80f;
        private const float NightElevation = 8f;

        /// <summary>하루는 정확히 한 바퀴여야 한다 — 종료각은 항상 시작각 + 360.</summary>
        private const float YawStart = -30f;

        private static readonly Color DaySky = new Color(1f, 1f, 1f, 1f);
        private static readonly Color NightSky = new Color(0.2f, 0.2f, 0.2f, 1f);

        /// <summary>밝기 하한을 끈 기본 프로파일 — 색 검증을 하한이 방해하지 않게 한다.</summary>
        private static DayVisualProfile MakeProfile(float minLuminanceGuard = 0f)
        {
            return new DayVisualProfile(
                new AmbientTone(DaySky, new Color(0.6f, 0.6f, 0.6f, 1f), new Color(0.3f, 0.3f, 0.3f, 1f), 1f),
                new AmbientTone(NightSky, new Color(0.1f, 0.1f, 0.1f, 1f), new Color(0.05f, 0.05f, 0.05f, 1f), 0.5f),
                new SunTone(new Color(1f, 1f, 1f, 1f), 1f),
                new SunTone(new Color(1f, 0.6f, 0.3f, 1f), 0.5f),
                new SunTone(new Color(0.5f, 0.6f, 0.9f, 1f), 0.1f),
                new SkyTone(new Color(0.5f, 0.5f, 0.5f, 1f), new Color(0.4f, 0.4f, 0.4f, 1f), 1f, 1.3f),
                new SkyTone(new Color(0.1f, 0.1f, 0.2f, 1f), new Color(0.05f, 0.05f, 0.05f, 1f), 0.5f, 0.5f),
                FadeSeconds,
                RiseElevation, NoonElevation, NightElevation,
                YawStart, YawStart + 360f,
                minLuminanceGuard);
        }

        private static void AssertColorEqual(Color actual, Color expected, float tolerance = 0.0001f)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance), "r");
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance), "g");
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance), "b");
        }

        // ── 1. 크로스페이드 경계 ────────────────────────────────────────────

        [Test]
        public void 국면_시작_순간은_직전_국면_색에서_출발한다()
        {
            DayVisualProfile profile = MakeProfile();

            AmbientTone tone = DayVisualMath.EvaluateAmbientCrossfade(DayPhase.Night, 0f, profile);

            AssertColorEqual(tone.Sky, DaySky);
            Assert.That(tone.Intensity, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void 크로스페이드가_끝나면_목표_국면_색에_도달한다()
        {
            DayVisualProfile profile = MakeProfile();

            AmbientTone tone = DayVisualMath.EvaluateAmbientCrossfade(DayPhase.Night, FadeSeconds, profile);

            AssertColorEqual(tone.Sky, NightSky);
            Assert.That(tone.Intensity, Is.EqualTo(0.5f).Within(0.0001f));
        }

        // ── 2. 국면 전환 연속성 ─────────────────────────────────────────────

        [Test]
        public void 낮의_끝_색과_밤의_시작_색이_같다()
        {
            DayVisualProfile profile = MakeProfile();

            // 낮이 끝날 무렵은 이미 크로스페이드가 끝나 낮 색으로 고정돼 있다.
            AmbientTone dayEnd = DayVisualMath.EvaluateAmbientCrossfade(DayPhase.Day, DayDuration, profile);
            AmbientTone nightStart = DayVisualMath.EvaluateAmbientCrossfade(DayPhase.Night, 0f, profile);

            AssertColorEqual(nightStart.Sky, dayEnd.Sky);
            Assert.That(nightStart.Intensity, Is.EqualTo(dayEnd.Intensity).Within(0.0001f));
        }

        [Test]
        public void 상시_보간도_국면_경계에서_이어진다()
        {
            DayVisualProfile profile = MakeProfile();

            DayVisualState dayEnd = DayVisualMath.EvaluateContinuous(DayPhase.Day, DayDuration, DayDuration, profile);
            DayVisualState nightStart = DayVisualMath.EvaluateContinuous(DayPhase.Night, 0f, NightDuration, profile);

            AssertColorEqual(nightStart.Ambient.Sky, dayEnd.Ambient.Sky);
            AssertColorEqual(nightStart.Sun.Color, dayEnd.Sun.Color);
            Assert.That(nightStart.SunElevation, Is.EqualTo(dayEnd.SunElevation).Within(0.0001f));
            Assert.That(nightStart.Sun.Intensity, Is.EqualTo(dayEnd.Sun.Intensity).Within(0.0001f));
            Assert.That(nightStart.SunYaw, Is.EqualTo(dayEnd.SunYaw).Within(0.0001f), "방위도 이어져야 한다");
        }

        /// <summary>
        /// Day가 바뀌는 순간(밤 끝 → 다음 낮 시작)의 방위 연속성. 하루가 한 바퀴를 채우지 못하면
        /// 여기서 태양이 모자란 각도만큼 <b>반대편으로 순간이동</b>한다 — 국면 경계만 보던 테스트가
        /// 놓쳤던 구멍이다.
        /// </summary>
        [Test]
        public void 태양_방위는_Day가_바뀔_때도_이어진다()
        {
            DayVisualProfile profile = MakeProfile();

            DayVisualState nightEnd = DayVisualMath.EvaluateContinuous(
                DayPhase.Night, NightDuration, NightDuration, profile);
            DayVisualState nextDayStart = DayVisualMath.EvaluateContinuous(
                DayPhase.Day, 0f, DayDuration, profile);

            // 360도 차이는 같은 방향이므로 최단 각도차로 본다.
            float gap = Mathf.DeltaAngle(nightEnd.SunYaw, nextDayStart.SunYaw);

            Assert.That(gap, Is.EqualTo(0f).Within(0.01f),
                $"Day 경계에서 방위가 {gap:F1}도 튄다 — 야우 종료각이 시작각 + 360이어야 한다");
        }

        [Test]
        public void 태양_방위는_하루에_정확히_한_바퀴_돈다()
        {
            DayVisualProfile profile = MakeProfile();

            float start = DayVisualMath.EvaluateContinuous(DayPhase.Day, 0f, DayDuration, profile).SunYaw;
            float end = DayVisualMath.EvaluateContinuous(DayPhase.Night, NightDuration, NightDuration, profile).SunYaw;

            Assert.That(end - start, Is.EqualTo(360f).Within(0.01f));
        }

        [Test]
        public void 태양_방위는_하루_동안_되돌아가지_않는다()
        {
            DayVisualProfile profile = MakeProfile();
            float previous = float.NegativeInfinity;

            for (int i = 0; i <= 20; i++)
            {
                float yaw = DayVisualMath.EvaluateContinuous(
                    DayPhase.Day, DayDuration * (i / 20f), DayDuration, profile).SunYaw;

                Assert.That(yaw, Is.GreaterThanOrEqualTo(previous), "낮 구간에서 방위가 역행했다");
                previous = yaw;
            }

            for (int i = 0; i <= 20; i++)
            {
                float yaw = DayVisualMath.EvaluateContinuous(
                    DayPhase.Night, NightDuration * (i / 20f), NightDuration, profile).SunYaw;

                Assert.That(yaw, Is.GreaterThanOrEqualTo(previous), "밤 구간에서 방위가 역행했다");
                previous = yaw;
            }
        }

        [Test]
        public void 국면_경계의_밤_정도는_양쪽_모두_절반이다()
        {
            Assert.That(DayVisualMath.Nightness(DayPhase.Day, 1f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(DayVisualMath.Nightness(DayPhase.Night, 0f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(DayVisualMath.Nightness(DayPhase.Day, 0.5f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(DayVisualMath.Nightness(DayPhase.Night, 0.5f), Is.EqualTo(1f).Within(0.0001f));
        }

        // ── 3. 크로스페이드 클램프 ──────────────────────────────────────────

        [Test]
        public void 크로스페이드_이후에는_색이_계속_흐르지_않는다()
        {
            DayVisualProfile profile = MakeProfile();

            AmbientTone justAfter = DayVisualMath.EvaluateAmbientCrossfade(DayPhase.Night, FadeSeconds, profile);
            AmbientTone muchLater = DayVisualMath.EvaluateAmbientCrossfade(DayPhase.Night, NightDuration, profile);

            AssertColorEqual(muchLater.Sky, justAfter.Sky);
            Assert.That(muchLater.Intensity, Is.EqualTo(justAfter.Intensity).Within(0.0001f));
        }

        [Test]
        public void 전환_시간이_0이면_즉시_목표_색이_된다()
        {
            DayVisualProfile profile = new DayVisualProfile(
                new AmbientTone(DaySky, Color.gray, Color.gray, 1f),
                new AmbientTone(NightSky, Color.gray, Color.gray, 0.5f),
                new SunTone(Color.white, 1f), new SunTone(Color.white, 1f), new SunTone(Color.white, 1f),
                new SkyTone(Color.gray, Color.gray, 1f, 1f), new SkyTone(Color.gray, Color.gray, 1f, 1f),
                0f, RiseElevation, NoonElevation, NightElevation, 0f, 360f, 0f);

            AmbientTone tone = DayVisualMath.EvaluateAmbientCrossfade(DayPhase.Night, 0f, profile);

            AssertColorEqual(tone.Sky, NightSky);
        }

        // ── 4. 0 나눗셈 방어 ────────────────────────────────────────────────

        [Test]
        public void 국면_길이가_0이어도_진행도가_터지지_않는다()
        {
            Assert.That(DayVisualMath.PhaseProgress(10f, 0f), Is.EqualTo(0f));
            Assert.That(DayVisualMath.PhaseProgress(10f, -5f), Is.EqualTo(0f));
        }

        [Test]
        public void 국면_길이가_0인_상시_보간은_국면_시작_상태를_돌려준다()
        {
            DayVisualProfile profile = MakeProfile();

            DayVisualState state = DayVisualMath.EvaluateContinuous(DayPhase.Day, 10f, 0f, profile);

            // 진행도 0 = 국면 시작 = 일출 고도.
            Assert.That(state.SunElevation, Is.EqualTo(RiseElevation).Within(0.0001f));
        }

        // ── 5. 밝기 하한 ────────────────────────────────────────────────────

        [Test]
        public void 실효_밝기가_하한_밑이면_색은_두고_강도만_올린다()
        {
            const float Guard = 0.5f;
            DayVisualProfile profile = MakeProfile(Guard);

            // 밤 하늘 휘도 0.2 × 강도 0.5 = 0.1 → 하한 0.5 미달.
            AmbientTone tone = DayVisualMath.EvaluateAmbientCrossfade(DayPhase.Night, FadeSeconds, profile);

            AssertColorEqual(tone.Sky, NightSky, 0.001f);
            Assert.That(tone.Sky.grayscale * tone.Intensity, Is.EqualTo(Guard).Within(0.001f));
            Assert.That(tone.Intensity, Is.GreaterThan(0.5f));
        }

        [Test]
        public void 하늘색이_사실상_검정이면_회색으로_최소_보정된다()
        {
            const float Guard = 0.3f;
            AmbientTone black = new AmbientTone(Color.black, Color.black, Color.black, 1f);

            AmbientTone tone = DayVisualMath.ApplyLuminanceGuard(black, Guard);

            Assert.That(tone.Sky.grayscale, Is.GreaterThan(0f));
            Assert.That(tone.Sky.grayscale * tone.Intensity, Is.GreaterThanOrEqualTo(Guard - 0.001f));
        }

        [Test]
        public void 하한이_0이면_아무것도_보정하지_않는다()
        {
            AmbientTone dim = new AmbientTone(Color.black, Color.black, Color.black, 0.01f);

            AmbientTone tone = DayVisualMath.ApplyLuminanceGuard(dim, 0f);

            Assert.That(tone.Intensity, Is.EqualTo(0.01f));
            AssertColorEqual(tone.Sky, Color.black);
        }

        [Test]
        public void 밤_태양_강도도_하한_밑으로_내려가지_않는다()
        {
            const float Guard = 0.3f;
            DayVisualProfile profile = MakeProfile(Guard);

            // 한밤의 달빛 강도는 프로파일상 0.1 — 하한 0.3에 걸린다.
            SunTone sun = DayVisualMath.EvaluateSunTone(DayPhase.Night, 0.5f, profile);

            Assert.That(sun.Intensity, Is.EqualTo(Guard).Within(0.0001f));
        }

        // ── 6. 하루 진행도 ──────────────────────────────────────────────────

        [Test]
        public void 하루_진행도는_낮이_전반부_밤이_후반부다()
        {
            Assert.That(DayVisualMath.CycleProgress(DayPhase.Day, 0f), Is.EqualTo(0f).Within(0.0001f));
            Assert.That(DayVisualMath.CycleProgress(DayPhase.Day, 1f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(DayVisualMath.CycleProgress(DayPhase.Night, 0f), Is.EqualTo(0.5f).Within(0.0001f));
            Assert.That(DayVisualMath.CycleProgress(DayPhase.Night, 1f), Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void 하루_진행도는_단조_증가한다()
        {
            float previous = -1f;

            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;

                float day = DayVisualMath.CycleProgress(DayPhase.Day, t);
                Assert.That(day, Is.GreaterThanOrEqualTo(previous));
                previous = day;
            }

            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;

                float night = DayVisualMath.CycleProgress(DayPhase.Night, t);
                Assert.That(night, Is.GreaterThanOrEqualTo(previous));
                previous = night;
            }
        }

        // ── 7. 태양 고도 범위 ───────────────────────────────────────────────

        [Test]
        public void 낮의_태양은_일출각과_정오각_사이에_머문다()
        {
            DayVisualProfile profile = MakeProfile();

            for (int i = 0; i <= 20; i++)
            {
                float elevation = DayVisualMath.EvaluateSunElevation(DayPhase.Day, i / 20f, profile);

                Assert.That(elevation, Is.GreaterThanOrEqualTo(RiseElevation - 0.0001f));
                Assert.That(elevation, Is.LessThanOrEqualTo(NoonElevation + 0.0001f));
            }
        }

        [Test]
        public void 밤의_태양은_야간_최저각_밑으로_내려가지_않는다()
        {
            DayVisualProfile profile = MakeProfile();

            for (int i = 0; i <= 20; i++)
            {
                float elevation = DayVisualMath.EvaluateSunElevation(DayPhase.Night, i / 20f, profile);

                Assert.That(elevation, Is.GreaterThanOrEqualTo(NightElevation - 0.0001f));
                Assert.That(elevation, Is.LessThanOrEqualTo(RiseElevation + 0.0001f));
            }
        }

        [Test]
        public void 정오가_가장_높고_한밤이_가장_낮다()
        {
            DayVisualProfile profile = MakeProfile();

            Assert.That(DayVisualMath.EvaluateSunElevation(DayPhase.Day, 0.5f, profile),
                Is.EqualTo(NoonElevation).Within(0.0001f));
            Assert.That(DayVisualMath.EvaluateSunElevation(DayPhase.Night, 0.5f, profile),
                Is.EqualTo(NightElevation).Within(0.0001f));
        }

        // ── 8. 진행도 클램프 ────────────────────────────────────────────────

        [Test]
        public void 국면_길이를_넘긴_경과도_진행도_1로_고정된다()
        {
            Assert.That(DayVisualMath.PhaseProgress(DayDuration * 2f, DayDuration), Is.EqualTo(1f));
        }

        [Test]
        public void 상시_보간의_밤_한가운데는_밤_색에_도달한다()
        {
            DayVisualProfile profile = MakeProfile();

            DayVisualState state = DayVisualMath.EvaluateContinuous(
                DayPhase.Night, NightDuration * 0.5f, NightDuration, profile);

            AssertColorEqual(state.Ambient.Sky, NightSky);
        }

        [Test]
        public void 상시_보간의_정오는_낮_색에_도달한다()
        {
            DayVisualProfile profile = MakeProfile();

            DayVisualState state = DayVisualMath.EvaluateContinuous(
                DayPhase.Day, DayDuration * 0.5f, DayDuration, profile);

            AssertColorEqual(state.Ambient.Sky, DaySky);
        }
    }
}
