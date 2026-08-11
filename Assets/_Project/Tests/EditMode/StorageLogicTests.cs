using Game.Gameplay.Inventory;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>공유 창고 이동 규칙 검증 (M5 3차 — 개인↔창고 이동·병합·스왑).</summary>
    public sealed class StorageLogicTests
    {
        private const int StackSize = 5;

        private static HotbarSlotView[] Slots(params HotbarSlotView[] slots)
        {
            return slots;
        }

        private static HotbarSlotView Empty()
        {
            return new HotbarSlotView(HotbarItemType.None, 0);
        }

        private static HotbarSlotView Wood(int count)
        {
            return new HotbarSlotView(HotbarItemType.Resource, count, ResourceType.Wood);
        }

        [Test]
        public void 빈_칸으로는_전량_이동한다()
        {
            HotbarSlotView[] from = Slots(Wood(3));
            HotbarSlotView[] to = Slots(Empty());

            Assert.That(StorageLogic.TryTransfer(from, 0, to, 0, StackSize), Is.True);
            Assert.That(from[0].IsEmpty, Is.True);
            Assert.That(to[0].Resource, Is.EqualTo(ResourceType.Wood));
            Assert.That(to[0].Count, Is.EqualTo(3));
        }

        [Test]
        public void 같은_자원은_상한까지_병합하고_잔량은_남는다()
        {
            HotbarSlotView[] from = Slots(Wood(4));
            HotbarSlotView[] to = Slots(Wood(3));

            Assert.That(StorageLogic.TryTransfer(from, 0, to, 0, StackSize), Is.True);
            Assert.That(to[0].Count, Is.EqualTo(StackSize), "상한 5까지 채운다");
            Assert.That(from[0].Count, Is.EqualTo(2), "잔량은 출발 칸에 남는다");
        }

        [Test]
        public void 다른_아이템과는_스왑한다()
        {
            HotbarSlotView[] from = Slots(Wood(2));
            HotbarSlotView[] to = Slots(new HotbarSlotView(HotbarItemType.Shotgun, 1));

            Assert.That(StorageLogic.TryTransfer(from, 0, to, 0, StackSize), Is.True);
            Assert.That(from[0].ItemType, Is.EqualTo(HotbarItemType.Shotgun), "무기가 출발 칸으로 온다");
            Assert.That(to[0].Resource, Is.EqualTo(ResourceType.Wood));
        }

        [Test]
        public void 무기도_컨테이너_간_이동이_된다()
        {
            // 무기 처분 경로 — 버리기가 불가한 무기는 창고 보관으로 슬롯을 비운다 (M5 3차).
            HotbarSlotView[] inventory = Slots(new HotbarSlotView(HotbarItemType.Rifle, 1));
            HotbarSlotView[] storage = Slots(Empty());

            Assert.That(StorageLogic.TryTransfer(inventory, 0, storage, 0, StackSize), Is.True);
            Assert.That(inventory[0].IsEmpty, Is.True);
            Assert.That(storage[0].ItemType, Is.EqualTo(HotbarItemType.Rifle));
        }

        [Test]
        public void 가득_찬_같은_자원_스택과는_수량을_교환한다()
        {
            HotbarSlotView[] from = Slots(Wood(2));
            HotbarSlotView[] to = Slots(Wood(StackSize));

            Assert.That(StorageLogic.TryTransfer(from, 0, to, 0, StackSize), Is.True);
            Assert.That(from[0].Count, Is.EqualTo(StackSize), "가득 스택이 출발 칸으로");
            Assert.That(to[0].Count, Is.EqualTo(2));
        }

        [Test]
        public void 같은_배열_안에서도_이동이_된다()
        {
            HotbarSlotView[] slots = Slots(Wood(3), Empty());

            Assert.That(StorageLogic.TryTransfer(slots, 0, slots, 1, StackSize), Is.True);
            Assert.That(slots[0].IsEmpty, Is.True);
            Assert.That(slots[1].Count, Is.EqualTo(3));
        }

        [Test]
        public void 빈_출발_칸과_같은_칸_이동과_범위_밖은_실패한다()
        {
            HotbarSlotView[] slots = Slots(Wood(3), Empty());

            Assert.That(StorageLogic.TryTransfer(slots, 1, slots, 0, StackSize), Is.False, "빈 출발 칸");
            Assert.That(StorageLogic.TryTransfer(slots, 0, slots, 0, StackSize), Is.False, "같은 칸");
            Assert.That(StorageLogic.TryTransfer(slots, 0, slots, 9, StackSize), Is.False, "범위 밖");
            Assert.That(StorageLogic.TryTransfer(null, 0, slots, 0, StackSize), Is.False, "null 배열");
        }

        // ── 보따리 창고 풀기 (M5 8차 — 3차에서 스왑 지원) ────────────────────

        private static int StackOf(ResourceType type)
        {
            return StackSize;
        }

        [Test]
        public void 보따리_풀기는_공간이_충분하면_스왑_없이_풀린다()
        {
            HotbarSlotView[] storage = Slots(Wood(1), Empty());
            HotbarSlotView[] contents = Slots(Wood(2));

            Assert.That(StorageLogic.TryUnpackBundle(storage, 0, contents, StackOf,
                out HotbarSlotView[] result, out HotbarSlotView swappedOut), Is.True);
            Assert.That(swappedOut.IsEmpty, Is.True, "공간이 충분하면 대상 칸을 내보내지 않는다");
            Assert.That(result[0].Count, Is.EqualTo(3), "기존 스택에 병합");
            Assert.That(storage[0].Count, Is.EqualTo(1), "원본은 변형하지 않는다");
        }

        [Test]
        public void 보따리_풀기는_공간_부족_시_대상_칸을_스왑으로_비우고_풀린다()
        {
            // 3차 검증 발견 — 꽉 찬 창고의 마지막 아이템 칸에 보따리를 바꿔 넣으면 풀려야 한다.
            HotbarSlotView[] storage = Slots(Wood(StackSize));
            HotbarSlotView[] contents = Slots(Wood(2));

            Assert.That(StorageLogic.TryUnpackBundle(storage, 0, contents, StackOf,
                out HotbarSlotView[] result, out HotbarSlotView swappedOut), Is.True);
            Assert.That(swappedOut.Count, Is.EqualTo(StackSize), "대상 칸의 가득 스택이 보따리 자리로 나온다");
            Assert.That(result[0].Count, Is.EqualTo(2), "비운 칸에 내용물이 들어간다");
        }

        [Test]
        public void 보따리_풀기는_스왑해도_부족하면_실패하고_원본을_남긴다()
        {
            HotbarSlotView[] storage = Slots(Wood(StackSize));
            HotbarSlotView[] contents = Slots(Wood(2), new HotbarSlotView(HotbarItemType.Shotgun, 1));

            Assert.That(StorageLogic.TryUnpackBundle(storage, 0, contents, StackOf,
                out HotbarSlotView[] result, out HotbarSlotView swappedOut), Is.False);
            Assert.That(result, Is.Null);
            Assert.That(swappedOut.IsEmpty, Is.True);
            Assert.That(storage[0].Count, Is.EqualTo(StackSize), "실패 시 원본 무변");
        }

        [Test]
        public void 보따리_풀기는_대상_칸이_유효하지_않으면_스왑_재시도를_하지_않는다()
        {
            HotbarSlotView[] storage = Slots(Wood(StackSize), Wood(StackSize));
            HotbarSlotView[] contents = Slots(Wood(1));

            Assert.That(StorageLogic.TryUnpackBundle(storage, -1, contents, StackOf,
                out HotbarSlotView[] _, out HotbarSlotView _), Is.False, "대상 칸 없음(범위 밖)");
        }
    }
}
