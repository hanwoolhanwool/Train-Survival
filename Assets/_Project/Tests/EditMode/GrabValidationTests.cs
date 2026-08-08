using Game.Gameplay.Harpoon;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 호스트 그랩 검증 규칙 (슬라이스 스펙 §2.4 — 거부 사유 3종 + M5 5차 무게 등급 게이트).
    /// 기존 자원·손잡이는 전부 무게 1이므로 1단계 집게 기준 판정이 5차 이전과 같아야 한다 (회귀 고정).
    /// </summary>
    public sealed class GrabValidationTests
    {
        private const float MaxRange = 20f;
        private const float Tolerance = 2f;
        private const int Tier1 = 1;
        private const int Weight1 = 1;

        [Test]
        public void 정상_보고는_승인된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                true, false, Vector3.zero, new Vector3(0f, 0f, 15f), MaxRange, Tolerance, Tier1, Weight1);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.Approved));
        }

        [Test]
        public void 소멸한_대상은_거부된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                false, false, Vector3.zero, Vector3.forward, MaxRange, Tolerance, Tier1, Weight1);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.TargetGone));
        }

        [Test]
        public void 다른_플레이어가_점유한_대상은_거부된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                true, true, Vector3.zero, Vector3.forward, MaxRange, Tolerance, Tier1, Weight1);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.TargetClaimed));
        }

        [Test]
        public void 사거리_상한_초과는_거부된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                true, false, Vector3.zero, new Vector3(0f, 0f, MaxRange + Tolerance + 0.1f),
                MaxRange, Tolerance, Tier1, Weight1);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.OutOfRange));
        }

        [Test]
        public void 여유_구간_안의_거리는_승인된다()
        {
            // §2.4 — 상한은 사거리 20 m + 여유 2 m. 발사 시점 위치 기준이므로 리드샷이 오거부되지 않는다.
            GrabVerdict verdict = GrabValidation.Validate(
                true, false, Vector3.zero, new Vector3(0f, 0f, MaxRange + Tolerance - 0.1f),
                MaxRange, Tolerance, Tier1, Weight1);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.Approved));
        }

        // ── M5 5차 — 무게 등급 게이트 ─────────────────────────────────────

        [Test]
        public void 등급이_무게에_못_미치면_거부된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                true, false, Vector3.zero, Vector3.forward, MaxRange, Tolerance,
                grabberTier: 1, targetWeight: 2);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.InsufficientTier));
        }

        [Test]
        public void 등급이_무게와_같으면_승인된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                true, false, Vector3.zero, Vector3.forward, MaxRange, Tolerance,
                grabberTier: 2, targetWeight: 2);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.Approved));
        }

        [Test]
        public void 등급이_무게보다_높으면_승인된다()
        {
            GrabVerdict verdict = GrabValidation.Validate(
                true, false, Vector3.zero, Vector3.forward, MaxRange, Tolerance,
                grabberTier: 3, targetWeight: 1);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.Approved));
        }

        [Test]
        public void 등급_게이트는_사거리_판정보다_뒤에_온다()
        {
            // 둘 다 어긋나면 거리 사유가 먼저다 — 기존 안내(로프가 안 닿는다)를 등급 안내가 덮지 않게 한다.
            GrabVerdict verdict = GrabValidation.Validate(
                true, false, Vector3.zero, new Vector3(0f, 0f, MaxRange + Tolerance + 0.1f),
                MaxRange, Tolerance, grabberTier: 1, targetWeight: 3);

            Assert.That(verdict, Is.EqualTo(GrabVerdict.OutOfRange));
        }

        [Test]
        public void 무게_게이트_순수_규칙은_등급_이상이면_참이다()
        {
            Assert.That(GrabValidation.CanLift(1, 1), Is.True);
            Assert.That(GrabValidation.CanLift(2, 1), Is.True);
            Assert.That(GrabValidation.CanLift(3, 3), Is.True);
            Assert.That(GrabValidation.CanLift(1, 2), Is.False);
            Assert.That(GrabValidation.CanLift(2, 3), Is.False);
        }
    }
}
