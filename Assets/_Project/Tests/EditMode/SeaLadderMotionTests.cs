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
