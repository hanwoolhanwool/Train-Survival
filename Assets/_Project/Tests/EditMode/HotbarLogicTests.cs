using Game.Gameplay.Inventory;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>통합 핫바 규칙 검증 (기획서 §3.4 — 무기+자원 5칸, 자유 배치, 슬롯당 스택).</summary>
    public sealed class HotbarLogicTests
    {
        private const int StackSize = 5;

        private static HotbarSlotView[] CreateDefaultSlots()
        {
            // 시작 배치: 1번 집게 · 2번 리볼버 · 3~5번 빈 칸.
            return new[]
            {
                new HotbarSlotView(HotbarItemType.Harpoon, 1),
                new HotbarSlotView(HotbarItemType.Revolver, 1),
                new HotbarSlotView(HotbarItemType.None, 0),
                new HotbarSlotView(HotbarItemType.None, 0),
                new HotbarSlotView(HotbarItemType.None, 0),
            };
        }

        [Test]
        public void 자원은_빈_칸에_새_스택으로_적재된다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();

            Assert.That(HotbarLogic.TryAddResource(slots, StackSize), Is.True);
            Assert.That(slots[2].ItemType, Is.EqualTo(HotbarItemType.Resource));
            Assert.That(slots[2].Count, Is.EqualTo(1));
        }

        [Test]
        public void 여유_있는_기존_스택을_빈_칸보다_먼저_채운다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 2);

            HotbarLogic.TryAddResource(slots, StackSize);

            Assert.That(slots[3].Count, Is.EqualTo(3), "기존 스택 우선");
            Assert.That(slots[2].IsEmpty, Is.True, "빈 칸은 그대로");
        }

        [Test]
        public void 전_슬롯이_차면_적재가_실패한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            for (int i = 2; i < 5; i++)
            {
                slots[i] = new HotbarSlotView(HotbarItemType.Resource, StackSize);
            }

            Assert.That(HotbarLogic.TryAddResource(slots, StackSize), Is.False, "가득 → 획득 불가·낙하 규칙");
        }

        [Test]
        public void 차감은_뒤에서부터_스택을_비우고_빈_칸으로_되돌린다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 3);
            slots[4] = new HotbarSlotView(HotbarItemType.Resource, 1);

            Assert.That(HotbarLogic.TryRemoveResource(slots), Is.True);
            Assert.That(slots[4].IsEmpty, Is.True, "뒤쪽 스택(1개)이 먼저 비워진다");
            Assert.That(slots[2].Count, Is.EqualTo(3));
        }

        [Test]
        public void 자원이_없으면_차감이_실패한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();

            Assert.That(HotbarLogic.TryRemoveResource(slots), Is.False);
        }

        [Test]
        public void 총량과_상한은_현재_배치를_따른다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 4);

            Assert.That(HotbarLogic.CountResource(slots), Is.EqualTo(4));
            Assert.That(HotbarLogic.ResourceCapacity(slots, StackSize), Is.EqualTo(15), "무기 2칸 제외 3칸 × 5");
        }

        [Test]
        public void 슬롯_교환은_범위_안의_서로_다른_두_칸만_유효하다()
        {
            Assert.That(HotbarLogic.IsValidSwap(0, 4, 5), Is.True);
            Assert.That(HotbarLogic.IsValidSwap(2, 2, 5), Is.False, "같은 칸");
            Assert.That(HotbarLogic.IsValidSwap(-1, 3, 5), Is.False);
            Assert.That(HotbarLogic.IsValidSwap(0, 5, 5), Is.False);
        }
    }
}
