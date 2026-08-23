using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 사다리 오르기 판정 (사다리 승하차 계획 §3.3·§5) — 이동 규칙을 컨트롤러에 들이기 <b>전에</b>
    /// "언제 붙고 언제 떨어지는가"를 코드로 고정한다.
    /// </summary>
    public sealed class LadderClimbLogicTests
    {
        // 사다리가 +X 쪽에 서는 사람을 향해 열려 있는 배치 — 오르는 사람은 −X를 본다.
        private static readonly Vector3 Approach = Vector3.left;
        private static readonly Vector3 Normal = Vector3.right;

        private const float Dot = LadderClimbLogic.DefaultApproachDot;

        // ── 진입 ─────────────────────────────────────────────────────

        [Test]
        public void 볼륨_밖이면_붙지_않는다()
        {
            Assert.IsFalse(LadderClimbLogic.ShouldAttach(false, Approach, Approach, false, Dot));
        }

        [Test]
        public void 이미_오르는_중이면_다시_붙지_않는다()
        {
            Assert.IsFalse(LadderClimbLogic.ShouldAttach(true, Approach, Approach, true, Dot));
        }

        [Test]
        public void 입력이_없으면_볼륨_안이어도_붙지_않는다()
        {
            // 볼륨 안에 가만히 선 것만으로 매달리면 옆을 지나가려던 사람이 붙잡힌다.
            Assert.IsFalse(LadderClimbLogic.ShouldAttach(true, Vector3.zero, Approach, false, Dot));
        }

        [Test]
        public void 사다리_반대쪽으로_가면_붙지_않는다()
        {
            Assert.IsFalse(LadderClimbLogic.ShouldAttach(true, -Approach, Approach, false, Dot));
        }

        [Test]
        public void 옆으로_스치는_입력은_붙지_않는다()
        {
            // 사다리와 직각 = dot 0 — 기준(0.3)에 못 미친다.
            Assert.IsFalse(LadderClimbLogic.ShouldAttach(true, Vector3.forward, Approach, false, Dot));
        }

        [Test]
        public void 사다리_쪽으로_전진하면_붙는다()
        {
            Assert.IsTrue(LadderClimbLogic.ShouldAttach(true, Approach, Approach, false, Dot));
        }

        [Test]
        public void 비스듬히_다가와도_기준을_넘으면_붙는다()
        {
            Vector3 diagonal = (Approach + Vector3.forward).normalized;   // 45° — dot ≈ 0.707
            Assert.IsTrue(LadderClimbLogic.ShouldAttach(true, diagonal, Approach, false, Dot));
        }

        [Test]
        public void 수직_성분은_정렬_판정에_끼지_않는다()
        {
            // 점프 중 입력에 y가 섞여도 수평 정렬만 본다 — 안 그러면 공중에서 판정이 흔들린다.
            Vector3 withUp = Approach + Vector3.up * 5f;
            Assert.IsTrue(LadderClimbLogic.ShouldAttach(true, withUp, Approach, false, Dot));
        }

        // ── 오르내림 ─────────────────────────────────────────────────

        [Test]
        public void 입력이_0이면_움직이지_않는다()
        {
            Assert.AreEqual(Vector3.zero, LadderClimbLogic.ComputeClimbMotion(0f, 2.4f, 0.02f));
        }

        [Test]
        public void 위아래가_부호로_갈린다()
        {
            Vector3 up = LadderClimbLogic.ComputeClimbMotion(1f, 2.4f, 0.5f);
            Vector3 down = LadderClimbLogic.ComputeClimbMotion(-1f, 2.4f, 0.5f);
            Assert.Greater(up.y, 0f);
            Assert.Less(down.y, 0f);
            Assert.AreEqual(up.y, -down.y, 1e-4f);
        }

        [Test]
        public void 이동은_수직_성분만_갖는다()
        {
            Vector3 motion = LadderClimbLogic.ComputeClimbMotion(1f, 2.4f, 0.02f);
            Assert.AreEqual(0f, motion.x, 1e-6f);
            Assert.AreEqual(0f, motion.z, 1e-6f);
        }

        [Test]
        public void 입력은_1을_넘지_못한다()
        {
            // 입력을 그대로 곱하면 조작 축이 큰 값을 낼 때 속도 상한이 무너진다.
            Vector3 clamped = LadderClimbLogic.ComputeClimbMotion(5f, 2f, 1f);
            Assert.AreEqual(2f, clamped.y, 1e-4f);
        }

        // ── 이탈 ─────────────────────────────────────────────────────

        [Test]
        public void 구간_안에서_아무_일도_없으면_계속_매달린다()
        {
            Assert.AreEqual(LadderDetachReason.None,
                LadderClimbLogic.ResolveDetach(2f, 0.5f, 3.5f, false, true));
        }

        [Test]
        public void 점프는_다른_모든_사유보다_앞선다()
        {
            // 꼭대기에서 점프하면 "올라서기"가 아니라 "뛰어내리기"다.
            Assert.AreEqual(LadderDetachReason.Jump,
                LadderClimbLogic.ResolveDetach(3.6f, 0.5f, 3.5f, true, true));
        }

        [Test]
        public void 볼륨을_벗어나면_상단_하단보다_먼저_판정된다()
        {
            // 사다리가 사라진 상황에서는 상단·하단 좌표를 믿을 수 없다.
            Assert.AreEqual(LadderDetachReason.LeftVolume,
                LadderClimbLogic.ResolveDetach(3.6f, 0.5f, 3.5f, false, false));
        }

        [Test]
        public void 꼭대기에_닿으면_올라서기다()
        {
            Assert.AreEqual(LadderDetachReason.TopReached,
                LadderClimbLogic.ResolveDetach(3.5f, 0.5f, 3.5f, false, true));
        }

        [Test]
        public void 발치에_닿으면_지상으로_돌아간다()
        {
            Assert.AreEqual(LadderDetachReason.BottomReached,
                LadderClimbLogic.ResolveDetach(0.5f, 0.5f, 3.5f, false, true));
        }

        // ── 평면 유지 ────────────────────────────────────────────────

        [Test]
        public void 옆으로_밀린_몸을_사다리_평면으로_되당긴다()
        {
            Vector3 origin = new Vector3(2.45f, 0f, -16.5f);
            Vector3 drifted = new Vector3(2.9f, 2f, -15.6f);   // z로 0.9 샜다
            Vector3 correction = LadderClimbLogic.ResolvePlaneCorrection(drifted, origin, Normal, 0.45f);

            Assert.AreEqual(-0.9f, correction.z, 1e-4f);
            Assert.AreEqual(0f, correction.x, 1e-4f);   // x는 이미 유지 거리(2.45 + 0.45)에 맞다
        }

        [Test]
        public void 보정은_높이를_건드리지_않는다()
        {
            Vector3 origin = new Vector3(2.45f, 0f, -16.5f);
            Vector3 climbing = new Vector3(3.5f, 2.8f, -16.5f);
            Vector3 correction = LadderClimbLogic.ResolvePlaneCorrection(climbing, origin, Normal, 0.45f);

            Assert.AreEqual(0f, correction.y, 1e-6f);
        }

        [Test]
        public void 이미_평면에_있으면_보정이_없다()
        {
            Vector3 origin = new Vector3(2.45f, 0f, -16.5f);
            Vector3 onPlane = new Vector3(2.9f, 1.5f, -16.5f);
            Vector3 correction = LadderClimbLogic.ResolvePlaneCorrection(onPlane, origin, Normal, 0.45f);

            Assert.AreEqual(0f, correction.magnitude, 1e-4f);
        }

        [Test]
        public void 작은_보정은_따라간다()
        {
            Assert.IsFalse(LadderClimbLogic.IsPlaneCorrectionTooFar(new Vector3(0.1f, 0f, 0.2f), 0.5f));
        }

        [Test]
        public void 사다리가_통째로_옮겨간_거리는_따라가지_않는다()
        {
            // 후미 칸이 이탈해 사다리가 재배치되면 보정 거리가 칸 길이만큼 튄다.
            Assert.IsTrue(LadderClimbLogic.IsPlaneCorrectionTooFar(new Vector3(0f, 0f, 16.5f), 0.5f));
        }

        // ── 점프 탈출 ────────────────────────────────────────────────

        [Test]
        public void 점프_탈출은_사다리_바깥과_위로_동시에_민다()
        {
            // 밀어내지 않으면 다음 프레임에 다시 붙어 제자리에서 튀기만 한다.
            Vector3 velocity = LadderClimbLogic.ComputeJumpOffVelocity(Normal, 3f, 4f);

            Assert.AreEqual(3f, velocity.x, 1e-4f);
            Assert.AreEqual(4f, velocity.y, 1e-4f);
        }

        // ── 올라서기 ─────────────────────────────────────────────────

        [Test]
        public void 올라서기는_갑판_안쪽으로_밀고_발을_갑판에_올린다()
        {
            Vector3 motion = LadderClimbLogic.ComputeMantleMotion(Normal, 3.5f, 3.566f, 0.7f, 0.05f);

            Assert.AreEqual(-0.7f, motion.x, 1e-4f);          // 법선 반대 = 갑판 안쪽
            Assert.AreEqual(0.116f, motion.y, 1e-3f);         // 3.566 + 0.05 − 3.5
        }

        [Test]
        public void 이미_갑판보다_높으면_끌어내리지_않는다()
        {
            // 올려놓기이지 높이 맞추기가 아니다.
            Vector3 motion = LadderClimbLogic.ComputeMantleMotion(Normal, 4.2f, 3.566f, 0.7f, 0.05f);

            Assert.AreEqual(0f, motion.y, 1e-6f);
        }
    }
}
