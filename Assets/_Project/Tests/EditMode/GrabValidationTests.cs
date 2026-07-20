using Game.Gameplay.Harpoon;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>호스트 그랩 검증 규칙 (슬라이스 스펙 §2.4 — 거부 사유 3종 외에는 승인).</summary>
    public sealed class GrabValidationTests
    {
        private const float MaxRange = 20f;
        private const float Tolerance = 2f;

        [Test]
        public void 정상_보고는_승인된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                true, false, Vector3.zero, new Vector3(0f, 0f, 15f), MaxRange, Tolerance);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.Approved));
        }

        [Test]
        public void 소멸한_대상은_거부된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                false, false, Vector3.zero, Vector3.forward, MaxRange, Tolerance);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.TargetGone));
        }

        [Test]
        public void 다른_플레이어가_점유한_대상은_거부된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                true, true, Vector3.zero, Vector3.forward, MaxRange, Tolerance);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.TargetClaimed));
        }

        [Test]
        public void 사거리_상한_초과는_거부된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                true, false, Vector3.zero, new Vector3(0f, 0f, MaxRange + Tolerance + 0.1f), MaxRange, Tolerance);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.OutOfRange));
        }

        [Test]
        public void 여유_구간_안의_거리는_승인된다()
        {
            // §2.4 — 상한은 사거리 20 m + 여유 2 m. 발사 시점 위치 기준이므로 리드샷이 오거부되지 않는다.
            GrabVerdict verdict = GrabValidation.Validate(
                true, false, Vector3.zero, new Vector3(0f, 0f, MaxRange + Tolerance - 0.1f), MaxRange, Tolerance);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.Approved));
        }
    }
}
