using Game.Gameplay.Inventory;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>개인 인벤토리 수량·슬롯 표시 계산 검증 (기획서 §3.4 — 핫바 5칸, 슬롯당 스택).</summary>
    public sealed class InventoryMathTests
    {
        [Test]
        public void 상한_안에서는_추가할_수_있다()
        {
            Assert.That(InventoryMath.CanAdd(count: 0, amount: 1, capacity: 25), Is.True);
            Assert.That(InventoryMath.CanAdd(count: 24, amount: 1, capacity: 25), Is.True);
        }

        [Test]
        public void 상한을_넘는_추가는_전량_실패한다()
        {
            // 부분 추가 없음 — 가득이면 자원 낙하 (기획서 §3.4).
            Assert.That(InventoryMath.CanAdd(count: 25, amount: 1, capacity: 25), Is.False);
            Assert.That(InventoryMath.CanAdd(count: 24, amount: 2, capacity: 25), Is.False);
        }

        [Test]
        public void 잔량이_부족하면_차감이_실패한다()
        {
            Assert.That(InventoryMath.CanRemove(count: 1, amount: 1), Is.True);
            Assert.That(InventoryMath.CanRemove(count: 0, amount: 1), Is.False);
        }

        [Test]
        public void 음수나_0개_증감은_거부된다()
        {
            Assert.That(InventoryMath.CanAdd(count: 0, amount: 0, capacity: 25), Is.False);
            Assert.That(InventoryMath.CanAdd(count: 0, amount: -1, capacity: 25), Is.False);
            Assert.That(InventoryMath.CanRemove(count: 5, amount: 0), Is.False);
        }

        [Test]
        public void 슬롯은_앞에서부터_스택_단위로_채워진다()
        {
            // 총 12개, 스택 5 → 슬롯 [5, 5, 2, 0, 0].
            Assert.That(InventoryMath.GetSlotFill(12, 0, 5), Is.EqualTo(5));
            Assert.That(InventoryMath.GetSlotFill(12, 1, 5), Is.EqualTo(5));
            Assert.That(InventoryMath.GetSlotFill(12, 2, 5), Is.EqualTo(2));
            Assert.That(InventoryMath.GetSlotFill(12, 3, 5), Is.EqualTo(0));
        }
    }
}
