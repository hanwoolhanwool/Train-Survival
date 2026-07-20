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
    }
}
