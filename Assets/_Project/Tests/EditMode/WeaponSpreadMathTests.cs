using Game.Gameplay.Combat;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>산탄 확산 순수 수학 검증 (M5 2차 — 샷건). 난수는 표본 값으로 주입한다.</summary>
    public sealed class WeaponSpreadMathTests
    {
        private const float SpreadAngle = 6f;
        private const float Epsilon = 0.01f;

        [Test]
        public void 확산각이_0이면_전방_그대로다()
        {
            Vector3 result = WeaponSpreadMath.ApplySpread(Vector3.forward, 0f, 0.7f, 0.3f);

            Assert.That(result, Is.EqualTo(Vector3.forward));
        }

        [Test]
        public void 기울기_표본이_0이면_전방_그대로다()
        {
            Vector3 result = WeaponSpreadMath.ApplySpread(Vector3.forward, SpreadAngle, 0f, 0.5f);

            Assert.That(Vector3.Angle(Vector3.forward, result), Is.LessThan(Epsilon));
        }

        [Test]
        public void 결과는_원뿔_반각_안에_있다()
        {
            for (int i = 0; i <= 10; i++)
            {
                for (int j = 0; j <= 10; j++)
                {
                    Vector3 result = WeaponSpreadMath.ApplySpread(
                        Vector3.forward, SpreadAngle, i / 10f, j / 10f);

                    Assert.That(Vector3.Angle(Vector3.forward, result),
                        Is.LessThanOrEqualTo(SpreadAngle + Epsilon), $"u={i / 10f}, v={j / 10f}");
                }
            }
        }

        [Test]
        public void 최대_표본은_반각_경계에_닿는다()
        {
            // u = 1 → 기울기 = 반각 (√1 = 1). 균일 원판 분포의 경계 검증.
            Vector3 result = WeaponSpreadMath.ApplySpread(Vector3.forward, SpreadAngle, 1f, 0f);

            Assert.That(Vector3.Angle(Vector3.forward, result), Is.EqualTo(SpreadAngle).Within(Epsilon));
        }

        [Test]
        public void 결과는_단위_벡터다()
        {
            Vector3 result = WeaponSpreadMath.ApplySpread(Vector3.forward, SpreadAngle, 0.8f, 0.25f);

            Assert.That(result.magnitude, Is.EqualTo(1f).Within(Epsilon));
        }

        [Test]
        public void 전방이_수직이어도_동작한다()
        {
            // 위/아래 조준 — 기준축(up)과 평행해 수직 벡터 폴백 경로를 탄다.
            Vector3 up = WeaponSpreadMath.ApplySpread(Vector3.up, SpreadAngle, 1f, 0.6f);
            Vector3 down = WeaponSpreadMath.ApplySpread(Vector3.down, SpreadAngle, 1f, 0.6f);

            Assert.That(Vector3.Angle(Vector3.up, up), Is.EqualTo(SpreadAngle).Within(Epsilon));
            Assert.That(Vector3.Angle(Vector3.down, down), Is.EqualTo(SpreadAngle).Within(Epsilon));
        }

        [Test]
        public void 정규화되지_않은_전방도_단위_결과를_낸다()
        {
            Vector3 result = WeaponSpreadMath.ApplySpread(new Vector3(0f, 0f, 5f), SpreadAngle, 0.5f, 0.5f);

            Assert.That(result.magnitude, Is.EqualTo(1f).Within(Epsilon));
        }
    }
}
