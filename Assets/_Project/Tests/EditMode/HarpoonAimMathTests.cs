using Game.Gameplay.Harpoon;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>조준점 수렴 발사 방향 검증 (§11 명중 판정 시차 해소).</summary>
    public sealed class HarpoonAimMathTests
    {
        [Test]
        public void 발사_방향은_총구에서_조준점을_향한다()
        {
            Vector3 muzzle = new Vector3(0.35f, -0.25f, 0.5f);
            Vector3 aimPoint = new Vector3(0f, 0f, 20f);

            Vector3 direction = HarpoonAimMath.ResolveFireDirection(muzzle, aimPoint, Vector3.forward);

            Assert.That(direction, Is.EqualTo((aimPoint - muzzle).normalized).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void 원거리_조준점은_카메라_전방과_거의_평행하다()
        {
            Vector3 muzzle = new Vector3(0.35f, -0.25f, 0.5f);
            Vector3 aimPoint = new Vector3(0f, 0f, 20f);

            Vector3 direction = HarpoonAimMath.ResolveFireDirection(muzzle, aimPoint, Vector3.forward);

            Assert.That(Vector3.Dot(direction, Vector3.forward), Is.GreaterThan(0.999f));
        }

        [Test]
        public void 조준점이_총구_뒤쪽이면_카메라_전방으로_폴백한다()
        {
            Vector3 muzzle = new Vector3(0f, 0f, 0.5f);
            Vector3 aimPoint = new Vector3(0f, 0f, 0.2f); // 초근접 장애물 — 총구보다 카메라 쪽

            Vector3 direction = HarpoonAimMath.ResolveFireDirection(muzzle, aimPoint, Vector3.forward);

            Assert.That(direction, Is.EqualTo(Vector3.forward).Using(Vector3EqualityComparer.Instance));
        }

        [Test]
        public void 조준점이_총구와_겹치면_카메라_전방으로_폴백한다()
        {
            Vector3 muzzle = new Vector3(0f, 0f, 0.5f);

            Vector3 direction = HarpoonAimMath.ResolveFireDirection(muzzle, muzzle, Vector3.forward);

            Assert.That(direction, Is.EqualTo(Vector3.forward).Using(Vector3EqualityComparer.Instance));
        }

        private sealed class Vector3EqualityComparer : System.Collections.Generic.IEqualityComparer<Vector3>
        {
            public static readonly Vector3EqualityComparer Instance = new Vector3EqualityComparer();

            public bool Equals(Vector3 a, Vector3 b)
            {
                return (a - b).sqrMagnitude < 1e-6f;
            }

            public int GetHashCode(Vector3 v)
            {
                return v.GetHashCode();
            }
        }
    }
}
