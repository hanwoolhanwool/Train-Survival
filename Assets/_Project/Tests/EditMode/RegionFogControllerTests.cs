using Game.Gameplay.Cycle;
using Game.Gameplay.Region;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 지역 × 국면 fog의 국면 선택과 크로스페이드 (사막 지역 구현 계획 §4.2 · 결정 ⑦).
    /// 밤에 크림색 헤이즈가 남던 어긋남을 이 판정이 막는다 — 밤은 하루의 38.5 %다.
    /// </summary>
    public sealed class RegionFogControllerTests
    {
        private static readonly Color DesertDay = new Color(0.910f, 0.863f, 0.753f, 1f);   // #E8DCC0
        private static readonly Color DesertNight = new Color(0.169f, 0.227f, 0.388f, 1f); // #2B3A63

        [Test]
        public void 낮에는_낮_한_벌을_고른다()
        {
            RegionFogController.ResolvePhaseFog(
                DayPhase.Day, DesertDay, 0.0015f, DesertNight, 0.0015f,
                out Color color, out float density);

            Assert.AreEqual(DesertDay, color);
            Assert.AreEqual(0.0015f, density, 1e-6f);
        }

        [Test]
        public void 밤에는_밤_한_벌을_고른다()
        {
            RegionFogController.ResolvePhaseFog(
                DayPhase.Night, DesertDay, 0.0015f, DesertNight, 0.0015f,
                out Color color, out float density);

            Assert.AreEqual(DesertNight, color);
            Assert.AreEqual(0.0015f, density, 1e-6f);
        }

        [Test]
        public void 밀도도_국면별로_갈린다()
        {
            // 사막은 낮·밤 동일 0.0015지만, 북극 블리자드처럼 밤에 짙게 할 여지를 필드가 남긴다.
            RegionFogController.ResolvePhaseFog(
                DayPhase.Night, Color.white, 0.0015f, Color.black, 0.02f,
                out _, out float density);

            Assert.AreEqual(0.02f, density, 1e-6f);
        }

        [Test]
        public void 두_벌이_같으면_국면이_바뀌어도_같은_값이다()
        {
            // 숲·바다의 회귀 방어선 — 낮·밤에 씬 값(#C8DDE8 · 0.0062)을 똑같이 배선한다.
            Color scene = new Color(0.784f, 0.867f, 0.91f, 1f);

            RegionFogController.ResolvePhaseFog(
                DayPhase.Day, scene, 0.0062f, scene, 0.0062f, out Color day, out float dayDensity);
            RegionFogController.ResolvePhaseFog(
                DayPhase.Night, scene, 0.0062f, scene, 0.0062f, out Color night, out float nightDensity);

            Assert.AreEqual(day, night);
            Assert.AreEqual(dayDensity, nightDensity, 1e-6f);
            Assert.AreEqual(scene, day);
        }

        [Test]
        public void 크로스페이드는_시작에서_0이다()
        {
            Assert.AreEqual(0f, RegionFogController.EvaluateFadeProgress(0f, 6f), 1e-4f);
        }

        [Test]
        public void 크로스페이드는_절반에서_0_5다()
        {
            Assert.AreEqual(0.5f, RegionFogController.EvaluateFadeProgress(3f, 6f), 1e-4f);
        }

        [Test]
        public void 크로스페이드는_6초에_끝난다()
        {
            // 낮/밤 연출의 _fadeSeconds 와 같은 값이라야 전환이 두 번 일어난 것처럼 보이지 않는다.
            Assert.AreEqual(1f, RegionFogController.EvaluateFadeProgress(6f, 6f), 1e-4f);
        }

        [Test]
        public void 크로스페이드는_1을_넘지_않는다()
        {
            Assert.AreEqual(1f, RegionFogController.EvaluateFadeProgress(600f, 6f), 1e-4f);
        }

        [Test]
        public void 페이드_시간이_0이면_즉시_목표다()
        {
            // 날씨가 걷힌 직후의 복원 경로 — 크로스페이드 없이 그 시각의 국면 색으로 곧장 간다.
            Assert.AreEqual(1f, RegionFogController.EvaluateFadeProgress(0f, 0f), 1e-4f);
        }

        [Test]
        public void 사막_안개는_500m_유적을_지우지_않는다()
        {
            // ExponentialSquared 감쇠 = exp(-(density × d)^2).
            // 씬 값 0.0062는 500 m 에서 0.007 % 라 피라미드를 세워도 화면에 남지 않는다.
            Assert.Less(Transmittance(0.0062f, 500f), 0.0005f);

            // 사막 값 0.0015는 500 m 에서 57 %, 800 m 산이 24 % 로 남는다.
            Assert.AreEqual(0.57f, Transmittance(0.0015f, 500f), 0.02f);
            Assert.AreEqual(0.24f, Transmittance(0.0015f, 800f), 0.02f);
        }

        [Test]
        public void 모래폭풍은_원경을_통째로_지운다()
        {
            // §4.8 — 이건 결함이 아니라 연출이다. 걷히면서 유적이 드러나는 것이 사막의 가장 강한 장면이다.
            Assert.Less(Transmittance(0.035f, 100f), 1e-5f);
        }

        private static float Transmittance(float density, float distance)
        {
            float d = density * distance;
            return Mathf.Exp(-(d * d));
        }
    }
}
