using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 이탈 칸 이동·손잡이 저항 순수 로직 검증 (손잡이-이탈저항 스펙 §4·§9).
    /// 목표 순 속도 = (스크롤 + 추가) - 인원 × 1인 견인력이고, 현재 속도는 항상 목표로 가속도만큼 램프한다
    /// — 잡고 놓는 순간에도 속도가 점프하지 않는다(회수 모션 개선). 순 속도 부호가 후퇴/전진을 가른다.
    /// </summary>
    public sealed class EjectMotionMathTests
    {
        [Test]
        public void 목표_밀림_속도는_스크롤과_추가_후퇴의_합이다()
        {
            Assert.That(EjectMotionMath.ComputeTargetPushSpeed(6f, 2f), Is.EqualTo(8f).Within(0.001f));
            Assert.That(EjectMotionMath.ComputeTargetPushSpeed(-5f, -1f), Is.EqualTo(0f), "음수 입력 방어");
        }

        [Test]
        public void 목표_순_속도는_잡은_인원이_늘수록_줄고_충분하면_음수가_된다()
        {
            // 목표 밀림 = 8. pull = 6.
            Assert.That(EjectMotionMath.ComputeTargetVelocity(8f, 0, 6f), Is.EqualTo(8f).Within(0.001f), "0인 = 전부 후퇴");
            Assert.That(EjectMotionMath.ComputeTargetVelocity(8f, 1, 6f), Is.EqualTo(2f).Within(0.001f), "1인 = 감속");
            Assert.That(EjectMotionMath.ComputeTargetVelocity(8f, 2, 6f), Is.EqualTo(-4f).Within(0.001f), "2인 = 전진(끌어당김)");
            Assert.That(EjectMotionMath.ComputeTargetVelocity(-5f, -3, -2f), Is.EqualTo(0f), "음수 입력 방어");
        }

        [Test]
        public void 속도는_가속도만큼_목표로_램프하고_넘어서지_않는다()
        {
            // 가속도 4 m/s² × 0.5s = 2 m/s씩 접근.
            Assert.That(EjectMotionMath.StepVelocity(0f, 8f, 4f, 0.5f), Is.EqualTo(2f).Within(0.001f), "분리 직후 서서히 뒤처짐");
            Assert.That(EjectMotionMath.StepVelocity(7f, 8f, 4f, 0.5f), Is.EqualTo(8f).Within(0.001f), "목표에서 멈춤(오버슛 없음)");
            Assert.That(EjectMotionMath.StepVelocity(8f, 5f, 4f, 0.5f), Is.EqualTo(6f).Within(0.001f), "목표가 낮아지면 따라 내려옴");
        }

        [Test]
        public void 손잡이를_잡으면_속도가_점프_없이_감속_정지_회수로_반전한다()
        {
            // 후퇴 8 m/s로 밀리던 칸을 2인이 잡아 목표가 -4가 됨. 가속도 4 m/s², 0.5s 스텝.
            float velocity = 8f;
            velocity = EjectMotionMath.StepVelocity(velocity, -4f, 4f, 0.5f);
            Assert.That(velocity, Is.EqualTo(6f).Within(0.001f), "잡는 순간 -4로 점프하지 않고 2씩 감속");

            for (int i = 0; i < 20; i++)
            {
                velocity = EjectMotionMath.StepVelocity(velocity, -4f, 4f, 0.5f);
            }

            Assert.That(velocity, Is.EqualTo(-4f).Within(0.001f), "충분한 시간 뒤 목표 회수 속도로 수렴");
        }

        [Test]
        public void 오프셋은_순_속도로_전진후퇴하며_슬롯_앞으로는_못_간다()
        {
            Assert.That(EjectMotionMath.StepOffset(0f, 8f, 0.5f), Is.EqualTo(4f).Within(0.001f), "후퇴");
            Assert.That(EjectMotionMath.StepOffset(4f, -4f, 0.5f), Is.EqualTo(2f).Within(0.001f), "전진");
            Assert.That(EjectMotionMath.StepOffset(1f, -10f, 0.5f), Is.EqualTo(0f), "슬롯(0)에서 멈춤");
        }

        [Test]
        public void 소실은_아무도_안_잡은_채_소실거리_이상_멀어졌을_때만_참이다()
        {
            Assert.That(EjectMotionMath.IsCarLost(45f, 45f, 0), Is.True, "경계 포함");
            Assert.That(EjectMotionMath.IsCarLost(44.9f, 45f, 0), Is.False, "아직 안 멀어짐");
            Assert.That(EjectMotionMath.IsCarLost(60f, 45f, 1), Is.False, "잡고 있으면 소실 아님");
        }
    }
}
