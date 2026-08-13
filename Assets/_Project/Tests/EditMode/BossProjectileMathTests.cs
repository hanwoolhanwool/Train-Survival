using Game.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 보스 투사체 탄도 검증 (M7 2차 결정 ②-b — 사막 고유 패턴, 6차 최종 보스 재사용 축).
    /// 탄체를 복제하지 않고 각 피어가 같은 수식으로 궤적을 재생하므로, 이 수식이 곧 계약이다.
    /// </summary>
    public sealed class BossProjectileMathTests
    {
        private const float Gravity = BossProjectileMath.Gravity;

        [Test]
        public void 발사_속도는_지정_시간에_낙점에_도달한다()
        {
            var origin = new Vector3(-12f, 2f, 5f);
            var impact = new Vector3(3f, 0f, 18f);
            const float flight = 1.8f;

            Vector3 velocity = BossProjectileMath.ComputeLaunchVelocity(origin, impact, flight, Gravity);
            Vector3 landed = BossProjectileMath.EvaluatePosition(origin, velocity, flight, Gravity);

            Assert.That(landed.x, Is.EqualTo(impact.x).Within(0.001f));
            Assert.That(landed.y, Is.EqualTo(impact.y).Within(0.001f));
            Assert.That(landed.z, Is.EqualTo(impact.z).Within(0.001f));
        }

        [Test]
        public void 궤적은_포물선이라_중간에_발사점보다_높이_뜬다()
        {
            var origin = new Vector3(0f, 2f, 0f);
            var impact = new Vector3(0f, 0f, 20f);
            const float flight = 2f;

            Vector3 velocity = BossProjectileMath.ComputeLaunchVelocity(origin, impact, flight, Gravity);
            Vector3 mid = BossProjectileMath.EvaluatePosition(origin, velocity, flight * 0.5f, Gravity);

            Assert.That(mid.y, Is.GreaterThan(origin.y), "곡선을 그려야 낙점 예고를 보고 피할 시간이 생긴다");
            Assert.That(mid.z, Is.EqualTo(10f).Within(0.001f), "수평은 등속");
        }

        [Test]
        public void 비행_시간이_0이어도_수식이_발산하지_않는다()
        {
            Vector3 velocity = BossProjectileMath.ComputeLaunchVelocity(
                Vector3.zero, new Vector3(0f, 0f, 10f), 0f, Gravity);

            Assert.That(float.IsInfinity(velocity.z), Is.False);
            Assert.That(float.IsNaN(velocity.z), Is.False);
        }

        [Test]
        public void 낙점_판정은_수평_거리로만_본다()
        {
            var impact = new Vector3(0f, 0f, 0f);

            // 갑판 위(높이 차 2 m)에 서 있어도 수평으로 범위 안이면 맞는다.
            Assert.That(
                BossProjectileMath.IsWithinImpact(impact, new Vector3(2f, 2f, 0f), 3.5f), Is.True);
            Assert.That(
                BossProjectileMath.IsWithinImpact(impact, new Vector3(4f, 0f, 0f), 3.5f), Is.False);
        }

        [Test]
        public void 낙점_경계는_반경과_같을_때_포함이다()
        {
            Assert.That(
                BossProjectileMath.IsWithinImpact(Vector3.zero, new Vector3(3f, 0f, 4f), 5f), Is.True);
            Assert.That(
                BossProjectileMath.IsWithinImpact(Vector3.zero, new Vector3(3f, 0f, 4f), 4.99f), Is.False);
        }

        [Test]
        public void 지상_표적은_비행_동안_밀려갈_거리만큼_앞당겨_조준한다()
        {
            var target = new Vector3(5f, 0f, 10f);
            Vector3 drift = Vector3.back * 8f;

            Vector3 impact = BossProjectileMath.PredictImpactPoint(target, drift, 1.5f);

            Assert.That(impact.z, Is.EqualTo(-2f).Within(0.001f), "10 − 8 × 1.5");
            Assert.That(impact.x, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void 갑판_위_표적은_보정이_없다()
        {
            var target = new Vector3(1f, 2.5f, 4f);

            Vector3 impact = BossProjectileMath.PredictImpactPoint(target, Vector3.zero, 1.8f);

            Assert.That(impact, Is.EqualTo(target));
        }
    }
}
