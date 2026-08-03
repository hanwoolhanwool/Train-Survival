using Game.Gameplay.Crafting;
using Game.Gameplay.Inventory;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>제작 순수 규칙 검증 (기획서 §6.1 탄약 제작, M5) — 재료 차감 + 산출 적재의 원자성.</summary>
    public sealed class CraftingLogicTests
    {
        private const int StackSize = 5;
        private const int AmmoStack = 12;

        private static CraftingLogic.IngredientView[] AmmoIngredients()
        {
            // 권총탄 레시피 초안 — 고철 1 + 화약 원료 1.
            return new[]
            {
                new CraftingLogic.IngredientView(ResourceType.Scrap, 1),
                new CraftingLogic.IngredientView(ResourceType.Niter, 1),
            };
        }

        private static HotbarSlotView[] CreateSlots()
        {
            return new[]
            {
                new HotbarSlotView(HotbarItemType.Harpoon, 1),
                new HotbarSlotView(HotbarItemType.None, 0),
                new HotbarSlotView(HotbarItemType.None, 0),
                new HotbarSlotView(HotbarItemType.None, 0),
                new HotbarSlotView(HotbarItemType.None, 0),
            };
        }

        [Test]
        public void 재료가_부족하면_제작할_수_없다()
        {
            HotbarSlotView[] slots = CreateSlots();
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Scrap);

            Assert.That(CraftingLogic.CanCraft(slots, AmmoIngredients()), Is.False, "화약 원료 없음");
            Assert.That(CraftingLogic.TryCraft(slots, AmmoIngredients(),
                ResourceType.RevolverAmmo, 4, AmmoStack), Is.False);
            Assert.That(slots[1].Count, Is.EqualTo(1), "실패 시 재료가 사라지지 않는다");
        }

        [Test]
        public void 재료를_정확히_차감하고_산출을_적재한다()
        {
            HotbarSlotView[] slots = CreateSlots();
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Scrap);
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Niter);

            Assert.That(CraftingLogic.TryCraft(slots, AmmoIngredients(),
                ResourceType.RevolverAmmo, 4, AmmoStack), Is.True);

            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Scrap), Is.EqualTo(1));
            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Niter), Is.EqualTo(0));
            Assert.That(HotbarLogic.CountResource(slots, ResourceType.RevolverAmmo), Is.EqualTo(4));
        }

        [Test]
        public void 산출을_넣을_자리가_없으면_전량_실패한다()
        {
            // 재료 칸 외 전부 다른 자원으로 가득 — 산출(권총탄) 스택을 만들 빈 칸이 없다.
            HotbarSlotView[] slots = CreateSlots();
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Scrap);
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Niter);
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, StackSize, ResourceType.Wood);
            slots[4] = new HotbarSlotView(HotbarItemType.Resource, StackSize, ResourceType.Stone);

            // 재료 2칸이 차감으로 비면 그 자리에 산출이 들어간다 — 이 배치는 성공 케이스.
            Assert.That(CraftingLogic.TryCraft(slots, AmmoIngredients(),
                ResourceType.RevolverAmmo, 4, AmmoStack), Is.True, "재료가 비운 칸을 산출이 쓴다");

            // 반대로 재료 스택이 남아 빈 칸이 안 생기면 실패해야 한다.
            HotbarSlotView[] full = CreateSlots();
            full[1] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Scrap);
            full[2] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Niter);
            full[3] = new HotbarSlotView(HotbarItemType.Resource, StackSize, ResourceType.Wood);
            full[4] = new HotbarSlotView(HotbarItemType.Resource, StackSize, ResourceType.Stone);

            Assert.That(CraftingLogic.TryCraft(full, AmmoIngredients(),
                ResourceType.RevolverAmmo, 4, AmmoStack), Is.False, "빈 칸 없음 → 전량 실패");
        }

        [Test]
        public void 산출은_기존_스택에_먼저_쌓인다()
        {
            HotbarSlotView[] slots = CreateSlots();
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Scrap);
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Niter);
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 6, ResourceType.RevolverAmmo);

            Assert.That(CraftingLogic.TryCraft(slots, AmmoIngredients(),
                ResourceType.RevolverAmmo, 4, AmmoStack), Is.True);
            Assert.That(slots[3].Count, Is.EqualTo(10), "기존 탄약 스택에 합산");
        }

        [Test]
        public void 빈_레시피와_무효_산출은_실패한다()
        {
            HotbarSlotView[] slots = CreateSlots();

            Assert.That(CraftingLogic.TryCraft(slots, AmmoIngredients(), ResourceType.None, 1, StackSize), Is.False);
            Assert.That(CraftingLogic.TryCraft(slots, AmmoIngredients(), ResourceType.RevolverAmmo, 0, StackSize), Is.False);
            Assert.That(CraftingLogic.CanCraft(null, AmmoIngredients()), Is.False);
            Assert.That(CraftingLogic.CanCraft(slots, null), Is.False);
        }
    }
}
