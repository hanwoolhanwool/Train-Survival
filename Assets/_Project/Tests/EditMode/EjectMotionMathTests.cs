using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 이탈 칸 이동·손잡이 저항 순수 로직 검증 (손잡이-이탈저항 스펙 §4·§9).
    /// 밀림 속도는 0(관성)에서 목표(스크롤 + 추가)까지 감속도로 램프하고, 저항 = 인원 × 1인 견인력.
    /// 순 속도 부호가 후퇴/전진을 가른다.
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
        public void 밀림_속도는_감속도만큼_목표로_램프하고_넘어서지_않는다()
        {
            // 감속도 4 m/s² × 0.5s = 2 m/s씩 접근.
            Assert.That(EjectMotionMath.StepPushSpeed(0f, 8f, 4f, 0.5f), Is.EqualTo(2f).Within(0.001f), "분리 직후 서서히 뒤처짐");
            Assert.That(EjectMotionMath.StepPushSpeed(7f, 8f, 4f, 0.5f), Is.EqualTo(8f).Within(0.001f), "목표에서 멈춤(오버슛 없음)");
            Assert.That(EjectMotionMath.StepPushSpeed(8f, 5f, 4f, 0.5f), Is.EqualTo(6f).Within(0.001f), "목표가 낮아지면 따라 내려옴");
            Assert.That(EjectMotionMath.StepPushSpeed(-3f, -1f, -4f, 0.5f), Is.EqualTo(0f), "음수 입력 방어");
        }

        [Test]
        public void 아무도_안_잡으면_밀림_속도_전부가_후퇴로_남는다()
        {
            float net = EjectMotionMath.ComputeNetVelocity(pushSpeed: 8f, grabberCount: 0, pullPerGrabber: 6f);
            Assert.That(net, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void 잡은_인원이_늘수록_순_속도가_줄고_충분하면_음수가_된다()
        {
            // push = 8. pull = 6.
            Assert.That(EjectMotionMath.ComputeNetVelocity(8f, 1, 6f), Is.EqualTo(2f).Within(0.001f), "1인 = 감속");
            Assert.That(EjectMotionMath.ComputeNetVelocity(8f, 2, 6f), Is.EqualTo(-4f).Within(0.001f), "2인 = 전진(끌어당김)");
        }

        [Test]
        public void 음수_입력은_방어된다()
        {
            Assert.That(EjectMotionMath.ComputeNetVelocity(-5f, -3, -2f), Is.EqualTo(0f));
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
