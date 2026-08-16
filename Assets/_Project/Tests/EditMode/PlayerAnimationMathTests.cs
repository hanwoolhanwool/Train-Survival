using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 애니메이션 파라미터 산출 검증 (플레이어 확장 계획 §2.1~2.2 · §6).
    /// 고정하는 계약은 셋이다: <b>지상에서 스크롤로 밀리는 것은 걸음이 아니다</b> ·
    /// <b>Walk↔Run 경계에서 파라미터가 진동하지 않는다</b> ·
    /// <b>텔레포트 델타는 이동으로 읽지 않는다</b>.
    /// </summary>
    public sealed class PlayerAnimationMathTests
    {
        private const float WalkEnter = 0.5f;

        private const float IdleEnter = 0.2f;

        /// <summary>Walk 4.5 · Run 7의 중간 — 드라이버와 같은 유도식.</summary>
        private const float RunBoundary = 5.75f;

        private const float Band = 0.3f;

        private const float TeleportThreshold = 15f;

        // ── 속도 추정·스크롤 제거 ─────────────────────────────────────

        [Test]
        public void 지상_정지_플레이어는_스크롤_밀림을_빼면_속도_0이다()
        {
            // 스크롤 5 m/s로 −Z 밀림만 있는 프레임 델타.
            Vector3 delta = Vector3.back * (5f * 0.02f);

            float speed = PlayerAnimationMath.EstimateHorizontalSpeed(
                delta, 0.02f, 5f, onWorldFrame: true, TeleportThreshold);

            Assert.That(speed, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void 열차_위에서는_스크롤을_빼지_않는다()
        {
            // 열차 위 정지(델타 0) — 스크롤을 빼 버리면 +Z로 걷는 것처럼 보인다.
            float idle = PlayerAnimationMath.EstimateHorizontalSpeed(
                Vector3.zero, 0.02f, 5f, onWorldFrame: false, TeleportThreshold);

            Assert.That(idle, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void 지상_걷기는_스크롤을_뺀_나머지가_속도다()
        {
            // 스크롤 밀림(−Z 5 m/s) + 실제 걷기(+X 4.5 m/s).
            Vector3 delta = (Vector3.back * 5f + Vector3.right * 4.5f) * 0.02f;

            float speed = PlayerAnimationMath.EstimateHorizontalSpeed(
                delta, 0.02f, 5f, onWorldFrame: true, TeleportThreshold);

            Assert.That(speed, Is.EqualTo(4.5f).Within(0.001f));
        }

        [Test]
        public void 수직_성분은_속도에_들어가지_않는다()
        {
            Vector3 delta = new Vector3(0f, 3f, 4f) * 0.02f;

            float speed = PlayerAnimationMath.EstimateHorizontalSpeed(
                delta, 0.02f, 0f, onWorldFrame: false, TeleportThreshold);

            Assert.That(speed, Is.EqualTo(4f).Within(0.001f));
        }

        [Test]
        public void 텔레포트_델타는_0으로_버린다()
        {
            // 부활 텔레포트 — 한 프레임에 40 m.
            Vector3 delta = Vector3.forward * 40f;

            float speed = PlayerAnimationMath.EstimateHorizontalSpeed(
                delta, 0.02f, 0f, onWorldFrame: false, TeleportThreshold);

            Assert.That(speed, Is.EqualTo(0f));
        }

        [Test]
        public void 델타타임_0은_속도_0이다()
        {
            float speed = PlayerAnimationMath.EstimateHorizontalSpeed(
                Vector3.one, 0f, 0f, onWorldFrame: false, TeleportThreshold);

            Assert.That(speed, Is.EqualTo(0f));
        }

        // ── Walk↔Run 히스테리시스 ────────────────────────────────────

        [Test]
        public void 경계_안쪽_진동에서는_단계가_바뀌지_않는다()
        {
            // 경계(5.75) ± 밴드(0.3) 안쪽에서 속도가 떨려도 단계가 고정된다.
            LocomotionTier walking = LocomotionTier.Walk;
            LocomotionTier running = LocomotionTier.Run;

            for (int i = 0; i < 20; i++)
            {
                float wobble = (i % 2 == 0) ? RunBoundary + 0.25f : RunBoundary - 0.25f;
                walking = PlayerAnimationMath.StepTier(walking, wobble, WalkEnter, IdleEnter, RunBoundary, Band);
                running = PlayerAnimationMath.StepTier(running, wobble, WalkEnter, IdleEnter, RunBoundary, Band);
            }

            Assert.That(walking, Is.EqualTo(LocomotionTier.Walk), "Walk에서 진입 문턱(+0.3) 미만은 Walk 유지");
            Assert.That(running, Is.EqualTo(LocomotionTier.Run), "Run에서 이탈 문턱(−0.3) 이상은 Run 유지");
        }

        [Test]
        public void 진입_문턱을_넘으면_Run으로_이탈_문턱_아래면_Walk로_바뀐다()
        {
            LocomotionTier toRun = PlayerAnimationMath.StepTier(
                LocomotionTier.Walk, RunBoundary + Band + 0.01f, WalkEnter, IdleEnter, RunBoundary, Band);
            LocomotionTier toWalk = PlayerAnimationMath.StepTier(
                LocomotionTier.Run, RunBoundary - Band - 0.01f, WalkEnter, IdleEnter, RunBoundary, Band);

            Assert.That(toRun, Is.EqualTo(LocomotionTier.Run));
            Assert.That(toWalk, Is.EqualTo(LocomotionTier.Walk));
        }

        [Test]
        public void 미세_떨림은_Idle에서_걸음으로_읽히지_않는다()
        {
            // 원격 보간 잔진동 수준(0.4 m/s < 진입 0.5)은 Idle 유지, 복귀는 0.2 아래에서만.
            LocomotionTier still = PlayerAnimationMath.StepTier(
                LocomotionTier.Idle, 0.4f, WalkEnter, IdleEnter, RunBoundary, Band);
            LocomotionTier keepWalking = PlayerAnimationMath.StepTier(
                LocomotionTier.Walk, 0.3f, WalkEnter, IdleEnter, RunBoundary, Band);
            LocomotionTier toIdle = PlayerAnimationMath.StepTier(
                LocomotionTier.Walk, 0.1f, WalkEnter, IdleEnter, RunBoundary, Band);

            Assert.That(still, Is.EqualTo(LocomotionTier.Idle));
            Assert.That(keepWalking, Is.EqualTo(LocomotionTier.Walk), "이탈 문턱(0.2) 위는 Walk 유지");
            Assert.That(toIdle, Is.EqualTo(LocomotionTier.Idle));
        }

        [Test]
        public void 단계_목표값은_판정된_단계의_영역_밖으로_나가지_않는다()
        {
            float idle = PlayerAnimationMath.TierTargetSpeed(LocomotionTier.Idle, 0.3f, RunBoundary);
            float walkClamped = PlayerAnimationMath.TierTargetSpeed(LocomotionTier.Walk, 6.5f, RunBoundary);
            float walkRaw = PlayerAnimationMath.TierTargetSpeed(LocomotionTier.Walk, 4.5f, RunBoundary);
            float runFloor = PlayerAnimationMath.TierTargetSpeed(LocomotionTier.Run, 5.2f, RunBoundary);
            float runRaw = PlayerAnimationMath.TierTargetSpeed(LocomotionTier.Run, 7f, RunBoundary);

            Assert.That(idle, Is.EqualTo(0f));
            Assert.That(walkClamped, Is.EqualTo(RunBoundary), "Walk 판정 중에는 경계 위로 못 간다");
            Assert.That(walkRaw, Is.EqualTo(4.5f));
            Assert.That(runFloor, Is.EqualTo(RunBoundary), "Run 판정 중에는 경계 아래로 못 간다");
            Assert.That(runRaw, Is.EqualTo(7f));
        }

        // ── 스무딩 수렴 ──────────────────────────────────────────────

        [Test]
        public void 반감기만큼_지나면_목표와의_거리가_절반이_된다()
        {
            // 한 번에 0.15 s를 넘겨도, 잘게 쪼개도 같은 수렴 속도여야 한다 (프레임레이트 무관).
            float once = PlayerAnimationMath.SmoothTowards(0f, 8f, 0.15f, 0.15f);

            float stepped = 0f;
            for (int i = 0; i < 15; i++)
            {
                stepped = PlayerAnimationMath.SmoothTowards(stepped, 8f, 0.15f, 0.01f);
            }

            Assert.That(once, Is.EqualTo(4f).Within(0.001f));
            Assert.That(stepped, Is.EqualTo(4f).Within(0.01f));
        }

        [Test]
        public void 반감기_0은_즉시_스냅이다()
        {
            float snapped = PlayerAnimationMath.SmoothTowards(0f, 8f, 0f, 0.02f);

            Assert.That(snapped, Is.EqualTo(8f));
        }

        // ── 점프 감지 (이음새 스파이크 필터) ──────────────────────────

        [Test]
        public void 일프레임_수직_스파이크는_점프가_아니다()
        {
            // 칸 모듈 이음새·StepOffset 스냅 — 공중 1프레임 + 큰 상승 속도 후 즉시 재접지.
            var state = new JumpDetectState();

            bool spike = PlayerAnimationMath.StepJumpDetect(ref state, grounded: false, 12f, 3f, 2);
            bool landed = PlayerAnimationMath.StepJumpDetect(ref state, grounded: true, -2f, 3f, 2);

            Assert.That(spike, Is.False, "연속 프레임 확증 전에는 판정하지 않는다");
            Assert.That(landed, Is.False);
            Assert.That(state.RisingFrames, Is.EqualTo(0), "접지가 누적을 리셋한다");
        }

        [Test]
        public void 상승이_연속_유지되면_체공당_한_번만_점프다()
        {
            var state = new JumpDetectState();

            bool first = PlayerAnimationMath.StepJumpDetect(ref state, false, 6.9f, 3f, 2);
            bool second = PlayerAnimationMath.StepJumpDetect(ref state, false, 6.5f, 3f, 2);
            bool third = PlayerAnimationMath.StepJumpDetect(ref state, false, 6.1f, 3f, 2);

            Assert.That(first, Is.False);
            Assert.That(second, Is.True, "2프레임 연속 상승에서 확증");
            Assert.That(third, Is.False, "래치 — 착지 전 재판정 없음");
        }

        [Test]
        public void 착지_후에는_다시_점프를_판정할_수_있다()
        {
            var state = new JumpDetectState();
            PlayerAnimationMath.StepJumpDetect(ref state, false, 6.9f, 3f, 2);
            PlayerAnimationMath.StepJumpDetect(ref state, false, 6.5f, 3f, 2);
            PlayerAnimationMath.StepJumpDetect(ref state, true, -2f, 3f, 2);

            PlayerAnimationMath.StepJumpDetect(ref state, false, 6.9f, 3f, 2);
            bool again = PlayerAnimationMath.StepJumpDetect(ref state, false, 6.5f, 3f, 2);

            Assert.That(again, Is.True);
        }

        [Test]
        public void 낙하는_점프가_아니다()
        {
            var state = new JumpDetectState();

            bool falling = false;
            for (int i = 0; i < 10; i++)
            {
                falling |= PlayerAnimationMath.StepJumpDetect(ref state, false, -3f - i, 3f, 2);
            }

            Assert.That(falling, Is.False);
        }

        // ── 피치 변환 ────────────────────────────────────────────────

        [Test]
        public void 오일러_X를_부호_있는_피치로_변환한다()
        {
            Assert.That(PlayerAnimationMath.SignedPitch(30f), Is.EqualTo(30f), "내려다봄 = +");
            Assert.That(PlayerAnimationMath.SignedPitch(330f), Is.EqualTo(-30f), "올려다봄 = −");
            Assert.That(PlayerAnimationMath.SignedPitch(0f), Is.EqualTo(0f));
        }

        // ── 점프 중계 상한 ───────────────────────────────────────────

        [Test]
        public void 초당_상한을_넘는_점프_중계는_거부된다()
        {
            double windowStart = 0.0;
            int used = 0;

            int allowed = 0;
            for (int i = 0; i < 6; i++)
            {
                if (PlayerAnimationMath.TryConsumeJumpBudget(ref windowStart, ref used, now: 0.5, 4))
                {
                    allowed++;
                }
            }

            Assert.That(allowed, Is.EqualTo(4));
        }

        [Test]
        public void 다음_1초_창에서는_상한이_리셋된다()
        {
            double windowStart = 0.0;
            int used = 4;

            bool inWindow = PlayerAnimationMath.TryConsumeJumpBudget(ref windowStart, ref used, now: 0.9, 4);
            bool nextWindow = PlayerAnimationMath.TryConsumeJumpBudget(ref windowStart, ref used, now: 1.1, 4);

            Assert.That(inWindow, Is.False);
            Assert.That(nextWindow, Is.True);
            Assert.That(used, Is.EqualTo(1), "새 창에서 카운트가 다시 시작된다");
        }
    }
}
