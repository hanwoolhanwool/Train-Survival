using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>1인칭 이동 계산 검증 (슬라이스 스펙 §4.1).</summary>
    public sealed class PlayerMotorTests
    {
        [Test]
        public void 점프_속도는_목표_높이에_도달한다()
        {
            const float gravity = 20f;
            const float jumpHeight = 1.2f;

            float jumpSpeed = PlayerMotor.GetJumpSpeed(jumpHeight, gravity);

            // 최고점 h = v² / (2g).
            float apex = jumpSpeed * jumpSpeed / (2f * gravity);
            Assert.That(apex, Is.EqualTo(jumpHeight).Within(0.001f));
        }

        [Test]
        public void 접지_중에는_목표_속도가_즉시_적용된다()
        {
            var desired = new Vector3(0f, 0f, 7f);

            Vector3 result = PlayerMotor.ComputeHorizontalVelocity(
                Vector3.zero, desired, isGrounded: true, airControlRatio: 0.5f, airAcceleration: 20f, deltaTime: 0.02f);

            Assert.That(result, Is.EqualTo(desired));
        }

        [Test]
        public void 공중에서는_제한된_가속으로만_접근한다()
        {
            var desired = new Vector3(0f, 0f, 7f);

            Vector3 result = PlayerMotor.ComputeHorizontalVelocity(
                Vector3.zero, desired, isGrounded: false, airControlRatio: 0.5f, airAcceleration: 20f, deltaTime: 0.02f);

            // 공중 제어 50 % (§4.1): 한 프레임 최대 변화 = 20 × 0.5 × 0.02 = 0.2 m/s.
            Assert.That(result.magnitude, Is.EqualTo(0.2f).Within(0.001f));
            Assert.That(result.magnitude, Is.LessThan(desired.magnitude));
        }

        // ── 접지 마찰 · 미끄러짐 (북극 지역 구현 계획 §5.5) ─────────────────────────

        [Test]
        public void 접지_가속이_0이면_종전과_같다()
        {
            // 다른 네 지역은 표면이 값을 주지 않는다 — 그 경로가 한 톨도 바뀌지 않아야 한다.
            var desired = new Vector3(0f, 0f, 7f);

            Assert.That(
                PlayerMotor.ComputeHorizontalVelocity(
                    Vector3.zero, desired, true, 0.5f, 20f, 0.02f, 0f),
                Is.EqualTo(desired));

            Assert.That(
                PlayerMotor.ComputeHorizontalVelocity(
                    Vector3.zero, desired, true, 0.5f, 20f, 0.02f, -5f),
                Is.EqualTo(desired), "음수도 마찰 무한으로 본다");
        }

        [Test]
        public void 얼음_위에서는_목표_속도에_천천히_붙는다()
        {
            var desired = new Vector3(0f, 0f, 7f);

            // 맨 얼음 3 m/s² · 한 프레임 0.02초 → 0.06 m/s 만 변한다.
            Vector3 result = PlayerMotor.ComputeHorizontalVelocity(
                Vector3.zero, desired, true, 0.5f, 20f, 0.02f, 3f);

            Assert.That(result.magnitude, Is.EqualTo(0.06f).Within(0.0001f));
        }

        [Test]
        public void 얼음_위에서는_멈추는_것도_느리다()
        {
            // 미끄러짐의 본체는 <b>가속</b>이 아니라 <b>감속</b>이다 — 물길 앞에서 못 멈추는 것.
            var running = new Vector3(0f, 0f, 7f);

            Vector3 result = PlayerMotor.ComputeHorizontalVelocity(
                running, Vector3.zero, true, 0.5f, 20f, 0.02f, 3f);

            Assert.That(result.magnitude, Is.EqualTo(6.94f).Within(0.001f));
        }

        [Test]
        public void 제동_거리가_계획_수치와_맞는다()
        {
            // 북극 as-built: 눈 덮인 유빙 12 · 맨 얼음 3 (계획 §5.5 표).
            Assert.That(PlayerMotor.StoppingDistance(4.5f, 12f), Is.EqualTo(0.84f).Within(0.01f), "눈 · 걷기");
            Assert.That(PlayerMotor.StoppingDistance(7f, 12f), Is.EqualTo(2.04f).Within(0.01f), "눈 · 달리기");
            Assert.That(PlayerMotor.StoppingDistance(4.5f, 3f), Is.EqualTo(3.38f).Within(0.01f), "빙판 · 걷기");
            Assert.That(PlayerMotor.StoppingDistance(7f, 3f), Is.EqualTo(8.17f).Within(0.01f), "빙판 · 달리기");
        }

        [Test]
        public void 빙판에서_달리면_넓은_물길_앞에서_못_멈춘다()
        {
            // 이 한 줄이 §5.2와 §5.5를 맞물리게 한다 — 넘든지 빠지든지 둘 중 하나가 된다.
            const float WideChannelWidth = 5f;
            Assert.Greater(PlayerMotor.StoppingDistance(7f, 3f), WideChannelWidth);

            // 반대로 눈 덮인 유빙에서는 멈출 수 있다 — 미끄러짐이 어디서나 벌이 되면 이동이 고문이다.
            Assert.Less(PlayerMotor.StoppingDistance(7f, 12f), WideChannelWidth);
        }

        [Test]
        public void 마찰이_무한이면_제동_거리가_0이다()
        {
            Assert.That(PlayerMotor.StoppingDistance(7f, 0f), Is.EqualTo(0f));
            Assert.That(PlayerMotor.StoppingDistance(7f, -1f), Is.EqualTo(0f));
        }
    }
}
