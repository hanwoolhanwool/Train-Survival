using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 바다 교각 사다리 계산 검증 (바다 지역 구현 계획 §6.3 ③).
    /// 열차 사다리를 재사용하다 <b>일곱 번</b> 실패한 지점들을 계약으로 못 박는다.
    /// </summary>
    public sealed class SeaLadderMotionTests
    {
        // 바다 규격 — 사다리 x ±4.2 · 상판 |x| ≤ 4.0 · 열차 오버행 2.85 · 캡슐 반경 0.35
        private const float LadderX = 4.2f;
        private const float Hold = 0.45f;
        private const float ExitInward = 1.25f;
        private const float TopY = 0.1f;
        private const float BottomY = -7.2f;
        private const float CapsuleRadius = 0.35f;
        private const float DeckHalfWidth = 4f;
        private const float TrainOverhang = 2.85f;

        private static readonly Vector3 RightLadder = new Vector3(LadderX, 0f, -10f);
        private static readonly Vector3 Outward = Vector3.right;   // 오른쪽 사다리는 +X가 물 쪽

        // ── 매달릴 자리 ──

        [Test]
        public void 매달릴_자리는_사다리_바깥이다()
        {
            Vector3 hold = SeaLadderMotion.HoldTarget(RightLadder, Outward, Hold);
            Assert.AreEqual(LadderX + Hold, hold.x, 1e-4f);
            Assert.AreEqual(RightLadder.z, hold.z, 1e-4f);
        }

        [Test]
        public void 매달릴_자리는_높이를_건드리지_않는다()
        {
            Vector3 hold = SeaLadderMotion.HoldTarget(new Vector3(LadderX, 5f, 0f), Outward, Hold);
            Assert.AreEqual(0f, hold.y, 1e-4f);
        }

        [Test]
        public void 왼쪽_사다리는_반대쪽으로_매단다()
        {
            Vector3 hold = SeaLadderMotion.HoldTarget(new Vector3(-LadderX, 0f, 0f), Vector3.left, Hold);
            Assert.AreEqual(-(LadderX + Hold), hold.x, 1e-4f);
        }

        [Test]
        public void 보정은_매달릴_자리를_향한다()
        {
            var position = new Vector3(LadderX + 2f, -3f, -10f);   // 물 쪽으로 떠 있다
            Vector3 c = SeaLadderMotion.HoldCorrection(position, RightLadder, Outward, Hold);
            Assert.Less(c.x, 0f, "사다리 쪽으로 당겨야 한다");
            Assert.AreEqual(0f, c.y, 1e-4f, "높이는 오르기가 담당한다");
        }

        [Test]
        public void 법선이_비면_사다리_축을_쓴다()
        {
            Vector3 hold = SeaLadderMotion.HoldTarget(RightLadder, Vector3.up, Hold);
            Assert.AreEqual(LadderX, hold.x, 1e-4f);
        }

        // ── 흐르는 사다리 따라가기 (8회차 떨림의 계약) ──

        [Test]
        public void 이동량을_반영한_뒤_보정해야_정확히_따라간다()
        {
            // 사다리가 이번 프레임 −Z 로 0.1 흘렀다. 플레이어는 직전 프레임에 정확히 붙어 있었다.
            var delta = new Vector3(0f, 0f, -0.1f);
            Vector3 ladderNow = RightLadder + delta;
            Vector3 playerBefore = SeaLadderMotion.HoldTarget(RightLadder, Outward, Hold);

            Vector3 correct = delta + SeaLadderMotion.HoldCorrection(
                playerBefore + delta, ladderNow, Outward, Hold);

            Assert.AreEqual(delta.z, correct.z, 1e-4f, "사다리가 움직인 만큼만 따라가야 한다");
            Assert.AreEqual(0f, correct.x, 1e-4f);
        }

        [Test]
        public void 이동_전_위치로_보정하면_두_배로_움직인다()
        {
            // 8회차 떨림의 원인 — Origin 은 이미 새 위치인데 이전 위치로 보정을 재면
            // 델타가 두 번 들어가고, 다음 프레임에 되돌아오며 진동한다.
            var delta = new Vector3(0f, 0f, -0.1f);
            Vector3 ladderNow = RightLadder + delta;
            Vector3 playerBefore = SeaLadderMotion.HoldTarget(RightLadder, Outward, Hold);

            Vector3 wrong = delta + SeaLadderMotion.HoldCorrection(
                playerBefore, ladderNow, Outward, Hold);

            Assert.AreEqual(delta.z * 2f, wrong.z, 1e-4f, "이것이 떨림의 정체다");
        }

        [Test]
        public void 이미_붙어_있으면_보정이_없다()
        {
            Vector3 onSpot = SeaLadderMotion.HoldTarget(RightLadder, Outward, Hold);
            Vector3 c = SeaLadderMotion.HoldCorrection(onSpot, RightLadder, Outward, Hold);
            Assert.AreEqual(0f, c.magnitude, 1e-4f);
        }

        // ── 떨림 흡수 (9회차 좌우 떨림의 계약) ──

        private const float DeadZone = 0.04f;
        private const float Damping = 0.35f;

        [Test]
        public void 아주_작은_오차는_보정하지_않는다()
        {
            var tiny = new Vector3(0.02f, 0f, 0f);
            Assert.AreEqual(Vector3.zero, SeaLadderMotion.SmoothCorrection(tiny, DeadZone, Damping));
        }

        [Test]
        public void 데드존_경계는_보정하지_않는다()
        {
            var edge = new Vector3(DeadZone, 0f, 0f);
            Assert.AreEqual(Vector3.zero, SeaLadderMotion.SmoothCorrection(edge, DeadZone, Damping));
        }

        [Test]
        public void 큰_오차는_일부만_좁힌다()
        {
            var big = new Vector3(1f, 0f, 0f);
            Vector3 s = SeaLadderMotion.SmoothCorrection(big, DeadZone, Damping);
            Assert.AreEqual(Damping, s.x, 1e-4f);
            Assert.Less(s.magnitude, big.magnitude, "한 번에 다 좁히면 넘겨서 진동한다");
        }

        [Test]
        public void 부분_수렴은_넘기지_않고_붙는다()
        {
            // 1 m 떨어진 상태에서 반복 적용 — 오버슈트 없이 데드존 안으로 들어와야 한다.
            float remaining = 1f;
            for (int i = 0; i < 30; i++)
            {
                Vector3 step = SeaLadderMotion.SmoothCorrection(
                    new Vector3(remaining, 0f, 0f), DeadZone, Damping);
                remaining -= step.x;
                Assert.GreaterOrEqual(remaining, -1e-4f, "반대편으로 넘어가면 그것이 떨림이다");
            }

            Assert.LessOrEqual(remaining, DeadZone + 1e-4f, "몇 프레임이면 붙어야 한다");
        }

        [Test]
        public void 사다리가_바뀐_프레임의_큰_점프는_걸러진다()
        {
            // 스크롤 6 m/s × dt 는 0.1 m 남짓 — 20 m 점프는 참조가 옮겨간 것이다.
            Assert.IsTrue(SeaLadderMotion.IsFollowJump(new Vector3(0f, 0f, -20f), 2f));
            Assert.IsFalse(SeaLadderMotion.IsFollowJump(new Vector3(0f, 0f, -0.1f), 2f));
        }

        // ── 오르내리기 ──

        [Test]
        public void 입력이_없으면_그_자리에_매달린다()
        {
            Assert.AreEqual(0f, SeaLadderMotion.ClimbVelocity(0f, 2.6f), 1e-4f);
        }

        [Test]
        public void 위_입력은_오르고_아래_입력은_내린다()
        {
            Assert.AreEqual(2.6f, SeaLadderMotion.ClimbVelocity(1f, 2.6f), 1e-4f);
            Assert.AreEqual(-2.6f, SeaLadderMotion.ClimbVelocity(-1f, 2.6f), 1e-4f);
        }

        [Test]
        public void 입력은_1을_넘지_못한다()
        {
            Assert.AreEqual(2.6f, SeaLadderMotion.ClimbVelocity(5f, 2.6f), 1e-4f);
        }

        // ── 경계 ──

        [Test]
        public void 꼭대기에_닿으면_올라선다()
        {
            Assert.IsTrue(SeaLadderMotion.HasReachedTop(TopY, TopY));
            Assert.IsTrue(SeaLadderMotion.HasReachedTop(TopY + 1f, TopY));
            Assert.IsFalse(SeaLadderMotion.HasReachedTop(TopY - 0.01f, TopY));
        }

        [Test]
        public void 밑으로_빠지면_놓아_준다()
        {
            Assert.IsTrue(SeaLadderMotion.HasFallenBelow(BottomY - 0.01f, BottomY));
            Assert.IsFalse(SeaLadderMotion.HasFallenBelow(BottomY, BottomY));
        }

        // ── 붙기 높이 (11회차 "사다리가 끝나도 계속 내려간다"의 계약) ──

        [Test]
        public void 사다리_구간_안에서는_붙는다()
        {
            Assert.IsTrue(SeaLadderMotion.CanAttach(BottomY, BottomY), "밑동은 붙는다");
            Assert.IsTrue(SeaLadderMotion.CanAttach(-3f, BottomY), "중간은 붙는다");
        }

        [Test]
        public void 밑으로_빠진_높이에서는_다시_붙지_않는다()
        {
            // 놓아 주는 조건과 붙는 조건이 겹치면, 계속 눌린 S 하나로 놓기와 붙기가
            // 매 프레임 번갈아 일어나 사다리가 끝난 뒤에도 물속으로 끌려 내려간다.
            float below = BottomY - 0.01f;
            Assert.IsTrue(SeaLadderMotion.HasFallenBelow(below, BottomY));
            Assert.IsFalse(SeaLadderMotion.CanAttach(below, BottomY));
        }

        [Test]
        public void 붙기는_꼭대기를_막지_않는다()
        {
            // 상한을 걸면 상판에서 사다리를 타고 내려가는 경로까지 막힌다 —
            // 위쪽 재부착은 참조 끊기와 차단 시간이 맡는다.
            Assert.IsTrue(SeaLadderMotion.CanAttach(TopY, BottomY));
        }

        // ── 뛰어내릴 방향 — 바라보는 쪽, 사다리 쪽이면 반사 ──

        [Test]
        public void 물_쪽을_보고_뛰면_그_방향_그대로다()
        {
            Vector3 d = SeaLadderMotion.ResolveJumpOffDirection(Outward, Outward);
            Assert.AreEqual(Outward.x, d.x, 1e-4f);
            Assert.AreEqual(Outward.z, d.z, 1e-4f);
        }

        [Test]
        public void 사다리를_마주보고_뛰면_뒤로_간다()
        {
            // 오르는 자세 그대로 점프한 경우 — 시선의 정반대, 곧 물 쪽으로 나가야 한다.
            Vector3 d = SeaLadderMotion.ResolveJumpOffDirection(-Outward, Outward);
            Assert.AreEqual(Outward.x, d.x, 1e-4f);
            Assert.AreEqual(Outward.z, d.z, 1e-4f);
        }

        [Test]
        public void 비스듬히_마주보면_접선은_남고_법선만_뒤집힌다()
        {
            Vector3 look = new Vector3(-1f, 0f, 1f).normalized;   // 사다리 쪽 + 옆
            Vector3 d = SeaLadderMotion.ResolveJumpOffDirection(look, Outward);

            Assert.Greater(d.x, 0f, "법선 성분은 물 쪽으로 뒤집힌다");
            Assert.AreEqual(look.z, d.z, 1e-4f, "접선 성분은 그대로다");
        }

        [Test]
        public void 옆을_보고_뛰면_그대로_옆으로_간다()
        {
            // 반사 경계(법선 성분 0) — 여기서 방향이 튀면 조금만 돌려도 반대편으로 날아간다.
            Vector3 side = Vector3.forward;
            Vector3 d = SeaLadderMotion.ResolveJumpOffDirection(side, Outward);
            Assert.AreEqual(side.z, d.z, 1e-4f);
            Assert.AreEqual(0f, d.x, 1e-4f);
        }

        [Test]
        public void 뛰어내릴_방향은_언제나_단위벡터다()
        {
            Vector3[] looks =
            {
                Outward, -Outward, Vector3.forward, Vector3.back,
                new Vector3(-3f, 0f, 1f), new Vector3(-0.01f, 0f, 5f), new Vector3(0f, 1f, 0f),
            };

            foreach (Vector3 look in looks)
            {
                Vector3 d = SeaLadderMotion.ResolveJumpOffDirection(look, Outward);
                Assert.AreEqual(1f, d.magnitude, 1e-4f, $"시선 {look}");
                Assert.AreEqual(0f, d.y, 1e-4f, "수평만 남는다");
            }
        }

        // ── 올라선 자리 — 일곱 번 실패한 지점 ──

        [Test]
        public void 올라선_자리는_안쪽이다()
        {
            Vector3 exit = SeaLadderMotion.ExitPosition(RightLadder, Outward, Hold, ExitInward, TopY);
            Assert.AreEqual(LadderX + Hold - ExitInward, exit.x, 1e-4f);
            Assert.AreEqual(TopY, exit.y, 1e-4f);
        }

        [Test]
        public void 올라선_캡슐이_상판_안에_온전히_든다()
        {
            Vector3 exit = SeaLadderMotion.ExitPosition(RightLadder, Outward, Hold, ExitInward, TopY);
            Assert.IsTrue(
                SeaLadderMotion.IsExitOnDeck(exit.x, CapsuleRadius, DeckHalfWidth, TrainOverhang),
                $"올라선 중심 {exit.x} · 캡슐 {exit.x - CapsuleRadius}~{exit.x + CapsuleRadius}");
        }

        [Test]
        public void 밀어_넣는_거리가_짧으면_상판_밖으로_나간다()
        {
            // 열차 기본값 0.7 — 갑판이 넓은 열차에서는 충분하지만 바다 통로(1.15 m)에서는 모자란다.
            Vector3 exit = SeaLadderMotion.ExitPosition(RightLadder, Outward, Hold, 0.7f, TopY);
            Assert.IsFalse(SeaLadderMotion.IsExitOnDeck(exit.x, CapsuleRadius, DeckHalfWidth, TrainOverhang),
                "0.7 로는 캡슐이 상판을 넘어선다 — 이것이 네 번째 실패의 원인이었다");
        }

        [Test]
        public void 너무_많이_밀면_열차와_겹친다()
        {
            Vector3 exit = SeaLadderMotion.ExitPosition(RightLadder, Outward, Hold, 1.8f, TopY);
            Assert.IsFalse(SeaLadderMotion.IsExitOnDeck(exit.x, CapsuleRadius, DeckHalfWidth, TrainOverhang),
                "1.8 이면 기관차 오버행 안으로 들어간다");
        }

        [Test]
        public void 안전_구간_전체가_상판_위다()
        {
            for (float inward = 1.0f; inward <= 1.45f; inward += 0.05f)
            {
                Vector3 exit = SeaLadderMotion.ExitPosition(RightLadder, Outward, Hold, inward, TopY);
                Assert.IsTrue(
                    SeaLadderMotion.IsExitOnDeck(exit.x, CapsuleRadius, DeckHalfWidth, TrainOverhang),
                    $"inward {inward:F2} 가 안전 구간을 벗어났다");
            }
        }

        [Test]
        public void 왼쪽_사다리도_같은_판정을_받는다()
        {
            Vector3 exit = SeaLadderMotion.ExitPosition(
                new Vector3(-LadderX, 0f, 0f), Vector3.left, Hold, ExitInward, TopY);
            Assert.AreEqual(-(LadderX + Hold - ExitInward), exit.x, 1e-4f);
            Assert.IsTrue(SeaLadderMotion.IsExitOnDeck(
                Mathf.Abs(exit.x), CapsuleRadius, DeckHalfWidth, TrainOverhang));
        }
    }
}
