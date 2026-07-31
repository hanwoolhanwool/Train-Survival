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

        [Test]
        public void 표시_오프셋은_정지_목표에_수렴하고_슬롯_앞으로는_못_간다()
        {
            float display = 10f;
            for (int i = 0; i < 120; i++)
            {
                display = EjectMotionMath.StepDisplayOffset(display, 12f, 0f, 1f / 60f, 8f, 10f);
            }

            Assert.That(display, Is.EqualTo(12f).Within(0.05f), "감쇠율 8/s → 2초 내 목표 수렴");
            Assert.That(EjectMotionMath.StepDisplayOffset(0.05f, 0f, -8f, 1f / 60f, 8f, 10f), Is.EqualTo(0f),
                "속도 외삽이 음수로 넘어가도 슬롯(0)에서 멈춘다");
        }

        [Test]
        public void 표시_오프셋은_계단_목표에도_매_프레임_고르게_전진한다()
        {
            // 복제 목표는 30Hz(2프레임에 한 번) 계단, 표시는 60fps로 재시뮬 속도 8m/s를 적분한다.
            // 원값을 그대로 쓰면 프레임 전진량이 0 ↔ 0.267m로 널뛰지만, 보간 후에는 이상치(8/60m) 근방에 머물러야 한다.
            const float Dt = 1f / 60f;
            const float Velocity = 8f;
            float target = 0f;
            float display = 0f;

            for (int frame = 1; frame <= 60; frame++)
            {
                if (frame % 2 == 0)
                {
                    target += Velocity * (2f * Dt);
                }

                float previous = display;
                display = EjectMotionMath.StepDisplayOffset(display, target, Velocity, Dt, 8f, 10f);
                float step = display - previous;

                Assert.That(step, Is.InRange(Velocity * Dt * 0.6f, Velocity * Dt * 1.4f),
                    $"{frame}번째 프레임 전진량이 이상치(±40%)를 벗어났다");
            }
        }

        [Test]
        public void 표시_오프셋_오차가_스냅_거리_이상이면_즉시_목표로_붙는다()
        {
            Assert.That(EjectMotionMath.StepDisplayOffset(0f, 20f, 0f, 1f / 60f, 8f, 10f), Is.EqualTo(20f),
                "후발 접속 등 큰 오차는 보간 없이 워프");
        }

        [Test]
        public void 표시_재시뮬은_같은_입력이면_계단_복제_아래서도_호스트_경로를_매끈하게_추종한다()
        {
            // 호스트: 60fps로 램프·저항 시뮬, 복제는 2프레임(≈30Hz)마다 발행. 클라: 같은 수식으로 재시뮬하고
            // 복제 값은 드리프트 보정 목표로만 쓴다. 중간에 2인이 잡아 순 속도가 음수로 반전되는 구간 포함.
            const float Dt = 1f / 60f;
            const float Scroll = 6f, Extra = 2f, Decel = 4f, Pull = 6f;
            float targetPush = EjectMotionMath.ComputeTargetPushSpeed(Scroll, Extra);

            float hostPush = 0f, hostOffset = 0f, replicated = 0f;
            float clientPush = 0f, display = 0f;

            for (int frame = 1; frame <= 360; frame++)
            {
                int grabbers = frame >= 240 ? 2 : 0;

                hostPush = EjectMotionMath.StepPushSpeed(hostPush, targetPush, Decel, Dt);
                hostOffset = EjectMotionMath.StepOffset(
                    hostOffset, EjectMotionMath.ComputeNetVelocity(hostPush, grabbers, Pull), Dt);
                if (frame % 2 == 0)
                {
                    replicated = hostOffset;
                }

                clientPush = EjectMotionMath.StepPushSpeed(clientPush, targetPush, Decel, Dt);
                float netVelocity = EjectMotionMath.ComputeNetVelocity(clientPush, grabbers, Pull);
                float previous = display;
                display = EjectMotionMath.StepDisplayOffset(display, replicated, netVelocity, Dt, 3f, 10f);

                Assert.That(display - hostOffset, Is.InRange(-0.3f, 0.3f),
                    $"{frame}번째 프레임에서 호스트 경로와의 드리프트가 허용치를 벗어났다");
                float step = display - previous;
                Assert.That(step - netVelocity * Dt, Is.InRange(-0.02f, 0.02f),
                    $"{frame}번째 프레임 전진량이 재시뮬 속도에서 벗어났다(떨림)");
            }
        }
    }
}
