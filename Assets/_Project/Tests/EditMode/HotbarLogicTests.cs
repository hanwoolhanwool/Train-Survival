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

            Assert.That(HotbarLogic.TryAddResource(slots, ResourceType.Wood, StackSize), Is.True);
            Assert.That(slots[2].ItemType, Is.EqualTo(HotbarItemType.Resource));
            Assert.That(slots[2].Resource, Is.EqualTo(ResourceType.Wood));
            Assert.That(slots[2].Count, Is.EqualTo(1));
        }

        [Test]
        public void 여유_있는_기존_스택을_빈_칸보다_먼저_채운다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Wood);

            HotbarLogic.TryAddResource(slots, ResourceType.Wood, StackSize);

            Assert.That(slots[3].Count, Is.EqualTo(3), "기존 스택 우선");
            Assert.That(slots[2].IsEmpty, Is.True, "빈 칸은 그대로");
        }

        [Test]
        public void 전_슬롯이_차면_적재가_실패한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            for (int i = 2; i < 5; i++)
            {
                slots[i] = new HotbarSlotView(HotbarItemType.Resource, StackSize, ResourceType.Wood);
            }

            Assert.That(HotbarLogic.TryAddResource(slots, ResourceType.Wood, StackSize), Is.False,
                "가득 → 획득 불가·낙하 규칙");
        }

        [Test]
        public void 차감은_뒤에서부터_스택을_비우고_빈_칸으로_되돌린다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Wood);
            slots[4] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Wood);

            Assert.That(HotbarLogic.TryRemoveResource(slots, ResourceType.Wood), Is.True);
            Assert.That(slots[4].IsEmpty, Is.True, "뒤쪽 스택(1개)이 먼저 비워진다");
            Assert.That(slots[2].Count, Is.EqualTo(3));
        }

        [Test]
        public void 자원이_없으면_차감이_실패한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();

            Assert.That(HotbarLogic.TryRemoveResource(slots, ResourceType.Wood), Is.False);
        }

        [Test]
        public void 지정_칸_차감은_다른_칸을_건드리지_않는다()
        {
            // 엔진 투입은 든 칸(선택 슬롯)만 소모해야 한다 — 3번 칸을 들고 눌러도 4번 칸이 줄면 안 된다.
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 5);
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 3);

            Assert.That(HotbarLogic.TryRemoveResourceAt(slots, 2), Is.True);
            Assert.That(slots[2].Count, Is.EqualTo(4), "든 칸(3번)만 줄어든다");
            Assert.That(slots[3].Count, Is.EqualTo(3), "다른 칸(4번)은 그대로");
        }

        [Test]
        public void 지정_칸이_자원이_아니면_차감이_실패한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();

            Assert.That(HotbarLogic.TryRemoveResourceAt(slots, 0), Is.False, "집게 칸은 소모 대상이 아니다");
            Assert.That(HotbarLogic.TryRemoveResourceAt(slots, 4), Is.False, "빈 칸도 실패");
        }

        [Test]
        public void 총량과_상한은_현재_배치를_따른다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 4, ResourceType.Wood);

            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Wood), Is.EqualTo(4));
            Assert.That(HotbarLogic.ResourceCapacity(slots, StackSize), Is.EqualTo(15), "무기 2칸 제외 3칸 × 5");
        }

        [Test]
        public void 같은_종류의_스택에만_병합된다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Wood);

            Assert.That(HotbarLogic.TryAddResource(slots, ResourceType.Stone, StackSize), Is.True);
            Assert.That(slots[2].Count, Is.EqualTo(2), "목재 스택은 돌과 병합되지 않는다");
            Assert.That(slots[3].Resource, Is.EqualTo(ResourceType.Stone), "돌은 새 칸에 새 스택");
            Assert.That(slots[3].Count, Is.EqualTo(1));
        }

        [Test]
        public void 같은_종류는_기존_스택을_먼저_채운다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Wood);

            Assert.That(HotbarLogic.TryAddResource(slots, ResourceType.Wood, StackSize), Is.True);
            Assert.That(slots[3].Count, Is.EqualTo(3));
            Assert.That(slots[2].IsEmpty, Is.True, "빈 칸은 그대로");
        }

        [Test]
        public void 종류_차감은_그_종류만_뒤에서부터_비운다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Wood);
            slots[4] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Stone);

            Assert.That(HotbarLogic.TryRemoveResource(slots, ResourceType.Wood), Is.True);
            Assert.That(slots[2].Count, Is.EqualTo(2), "목재만 줄어든다");
            Assert.That(slots[4].Count, Is.EqualTo(1), "돌(더 뒤 칸)은 종류가 달라 건드리지 않는다");

            Assert.That(HotbarLogic.TryRemoveResource(slots, ResourceType.Scrap), Is.False, "없는 종류는 실패");
        }

        [Test]
        public void 조건_차감은_조건에_맞는_종류를_건너뛰지_않고_고른다()
        {
            // 건설 비용은 건자재만 소모해야 한다 — 탄약이 비용으로 새면 안 된다.
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Wood);
            slots[4] = new HotbarSlotView(HotbarItemType.Resource, 4, ResourceType.RevolverAmmo);

            static bool IsBuildMaterial(ResourceType type) => type == ResourceType.Wood;

            Assert.That(HotbarLogic.TryRemoveAnyResource(slots, IsBuildMaterial), Is.True);
            Assert.That(slots[2].Count, Is.EqualTo(1), "건자재(목재)가 소모된다");
            Assert.That(slots[4].Count, Is.EqualTo(4), "탄약(뒤 칸)은 조건 밖이라 그대로");
        }

        [Test]
        public void 조건_차감은_소모된_종류를_돌려준다()
        {
            // HUD 소모 미리보기가 이 반환값으로 내역을 집계한다 (M5 검증 E1).
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Wood);
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Scrap);

            Assert.That(HotbarLogic.TryRemoveAnyResource(slots, _ => true, out ResourceType first), Is.True);
            Assert.That(first, Is.EqualTo(ResourceType.Scrap), "뒤 칸(고철)부터 소모");
            Assert.That(HotbarLogic.TryRemoveAnyResource(slots, _ => true, out ResourceType second), Is.True);
            Assert.That(second, Is.EqualTo(ResourceType.Wood));
            Assert.That(HotbarLogic.TryRemoveAnyResource(slots, _ => true, out ResourceType none), Is.False);
            Assert.That(none, Is.EqualTo(ResourceType.None), "실패 시 None");
        }

        [Test]
        public void 종류별_총량과_조건_총량을_구분해_센다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Wood);
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Niter);

            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Wood), Is.EqualTo(3));
            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Scrap), Is.EqualTo(0));
            Assert.That(HotbarLogic.CountResource(slots, t => t == ResourceType.Wood || t == ResourceType.Niter), Is.EqualTo(5));
        }

        [Test]
        public void 지정_칸_차감은_종류를_보존한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Scrap);

            Assert.That(HotbarLogic.TryRemoveResourceAt(slots, 2), Is.True);
            Assert.That(slots[2].Resource, Is.EqualTo(ResourceType.Scrap), "부분 차감 후에도 종류 유지");
            Assert.That(slots[2].Count, Is.EqualTo(2));
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
