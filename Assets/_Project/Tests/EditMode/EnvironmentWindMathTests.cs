using Game.Gameplay.Region;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 환경 바람 세기 검증 (천막 계획 3차).
    /// 바람은 <b>날씨 × 국면</b>이고, 값이 없는 기존 날씨는 맑음과 같아야 한다 —
    /// 이 소급 규약이 깨지면 다른 지역 날씨가 켜질 때 천이 멈춰 버린다.
    /// </summary>
    public sealed class EnvironmentWindMathTests
    {
        private const float NightScale = 0.45f;

        [Test]
        public void 맑은_낮이_기준이다()
        {
            Assert.AreEqual(1f, EnvironmentWindMath.ResolveTargetScale(
                EnvironmentWindMath.CalmScale, isNight: false, NightScale), 0.0001f);
        }

        [Test]
        public void 모래폭풍이_불면_세진다()
        {
            float storm = EnvironmentWindMath.ResolveTargetScale(3f, isNight: false, NightScale);
            Assert.AreEqual(3f, storm, 0.0001f);
            Assert.Greater(storm, EnvironmentWindMath.CalmScale, "폭풍이 맑은 날보다 약하면 안 된다");
        }

        [Test]
        public void 밤에는_잦아든다()
        {
            float day = EnvironmentWindMath.ResolveTargetScale(1f, isNight: false, NightScale);
            float night = EnvironmentWindMath.ResolveTargetScale(1f, isNight: true, NightScale);

            Assert.Less(night, day);
            Assert.AreEqual(NightScale, night, 0.0001f);
        }

        [Test]
        public void 밤의_폭풍은_낮의_폭풍보다_약하고_밤의_맑음보다_세다()
        {
            float dayStorm = EnvironmentWindMath.ResolveTargetScale(3f, isNight: false, NightScale);
            float nightStorm = EnvironmentWindMath.ResolveTargetScale(3f, isNight: true, NightScale);
            float nightCalm = EnvironmentWindMath.ResolveTargetScale(1f, isNight: true, NightScale);

            Assert.Less(nightStorm, dayStorm);
            Assert.Greater(nightStorm, nightCalm, "밤이어도 폭풍은 맑은 밤보다 세야 한다");
        }

        [Test]
        public void 값을_안_넣은_날씨는_맑음과_같다()
        {
            // 소급 규약 — 기존 날씨 에셋은 _windScale이 0이다. 그 날씨가 켜졌다고 천이 굳으면 안 된다.
            Assert.AreEqual(EnvironmentWindMath.CalmScale,
                EnvironmentWindMath.ResolveTargetScale(0f, isNight: false, NightScale), 0.0001f);
            Assert.AreEqual(EnvironmentWindMath.CalmScale,
                EnvironmentWindMath.ResolveTargetScale(-1f, isNight: false, NightScale), 0.0001f);
        }

        [Test]
        public void 밤_배율이_1이면_밤낮이_같다()
        {
            Assert.AreEqual(
                EnvironmentWindMath.ResolveTargetScale(2f, isNight: false, 1f),
                EnvironmentWindMath.ResolveTargetScale(2f, isNight: true, 1f), 0.0001f);
        }

        // ── 전환 ──────────────────

        [Test]
        public void 목표로_초당_속도만큼_다가간다()
        {
            // 1 → 3, 초당 0.6, 1초 경과.
            float next = EnvironmentWindMath.Step(1f, 3f, 0.6f, 1f);
            Assert.AreEqual(1.6f, next, 0.0001f);
        }

        [Test]
        public void 목표를_지나치지_않는다()
        {
            float next = EnvironmentWindMath.Step(1f, 1.2f, 10f, 1f);
            Assert.AreEqual(1.2f, next, 0.0001f, "MoveTowards는 목표에서 멈춘다");
        }

        [Test]
        public void 내려갈_때도_같은_속도다()
        {
            float next = EnvironmentWindMath.Step(3f, 1f, 0.6f, 1f);
            Assert.AreEqual(2.4f, next, 0.0001f);
        }

        [Test]
        public void 속도가_0이면_즉시_목표다()
        {
            // 보간을 끄고 싶을 때의 탈출구 — 튀는 것을 감수하고 바로 맞춘다.
            Assert.AreEqual(3f, EnvironmentWindMath.Step(1f, 3f, 0f, 1f), 0.0001f);
        }
    }
}
