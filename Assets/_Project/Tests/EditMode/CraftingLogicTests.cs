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

        // ── 무기 산출 (M5 2차) ──────────────────────────────────────────────

        private static CraftingLogic.IngredientView[] MeleeIngredients()
        {
            // 마체테 레시피 — 고철 2 + 목재 1.
            return new[]
            {
                new CraftingLogic.IngredientView(ResourceType.Scrap, 2),
                new CraftingLogic.IngredientView(ResourceType.Wood, 1),
            };
        }

        [Test]
        public void 무기_제작은_재료를_차감하고_아이템_1개를_지급한다()
        {
            HotbarSlotView[] slots = CreateSlots();
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Scrap);
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Wood);

            Assert.That(CraftingLogic.TryCraftItem(slots, MeleeIngredients(), HotbarItemType.Melee), Is.True);

            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Scrap), Is.EqualTo(0));
            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Wood), Is.EqualTo(0));
            Assert.That(slots[1].ItemType, Is.EqualTo(HotbarItemType.Melee), "재료가 비운 첫 칸에 지급");
            Assert.That(slots[1].Count, Is.EqualTo(1), "무기는 스택 없음");
        }

        [Test]
        public void 빈_칸이_없으면_무기_제작은_전량_실패한다()
        {
            // 재료가 남아 칸이 비지 않고, 나머지 칸도 가득 — 지급할 자리가 없다.
            HotbarSlotView[] slots = CreateSlots();
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Scrap);
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Wood);
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, StackSize, ResourceType.Stone);
            slots[4] = new HotbarSlotView(HotbarItemType.Resource, StackSize, ResourceType.Niter);

            Assert.That(CraftingLogic.TryCraftItem(slots, MeleeIngredients(), HotbarItemType.Melee), Is.False,
                "빈 칸 없음 → 전량 실패 (호출자가 복사본을 버려 재료가 보존된다)");
        }

        // ── M7 1차 — 요리·보존식 레시피 회귀 (기획서 §4.3 소금 절임·§7.3) ──

        private static CraftingLogic.IngredientView[] PreservedMealIngredients()
        {
            // 보존식 레시피 — 식재료 2 + 소금 1 → 보존식 2 (결정 ② — 고스택 비축형).
            return new[]
            {
                new CraftingLogic.IngredientView(ResourceType.RawFood, 2),
                new CraftingLogic.IngredientView(ResourceType.Salt, 1),
            };
        }

        [Test]
        public void 보존식은_식재료와_소금을_차감하고_2개를_적재한다()
        {
            HotbarSlotView[] slots = CreateSlots();
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.RawFood);
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Salt);

            // 스택 10 = 결정 ② — 일반 요리(5) 대비 2배로 "창고 슬롯당 끼니 수"를 키운다.
            Assert.That(CraftingLogic.TryCraft(slots, PreservedMealIngredients(),
                ResourceType.PreservedMeal, 2, 10), Is.True);

            Assert.That(HotbarLogic.CountResource(slots, ResourceType.RawFood), Is.EqualTo(1));
            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Salt), Is.EqualTo(1));
            Assert.That(HotbarLogic.CountResource(slots, ResourceType.PreservedMeal), Is.EqualTo(2));
        }

        [Test]
        public void 소금이_없으면_보존식은_전량_실패한다()
        {
            HotbarSlotView[] slots = CreateSlots();
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 5, ResourceType.RawFood);

            Assert.That(CraftingLogic.TryCraft(slots, PreservedMealIngredients(),
                ResourceType.PreservedMeal, 2, 10), Is.False, "사막에서 소금을 챙겨 왔는가가 비축 효율을 가른다");
            Assert.That(slots[1].Count, Is.EqualTo(5), "실패 시 식재료 무변경 — 원자성");
        }

        [Test]
        public void 밥은_벼_2개를_차감하고_2개를_적재한다()
        {
            HotbarSlotView[] slots = CreateSlots();
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Rice);

            var ingredients = new[] { new CraftingLogic.IngredientView(ResourceType.Rice, 2) };

            Assert.That(CraftingLogic.TryCraft(slots, ingredients, ResourceType.CookedRice, 2, StackSize), Is.True);
            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Rice), Is.EqualTo(0));
            Assert.That(HotbarLogic.CountResource(slots, ResourceType.CookedRice), Is.EqualTo(2));
        }

        [Test]
        public void 무효_무기_산출_종류는_실패한다()
        {
            HotbarSlotView[] slots = CreateSlots();
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Scrap);
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Wood);

            Assert.That(CraftingLogic.TryCraftItem(slots, MeleeIngredients(), HotbarItemType.None), Is.False);
            Assert.That(CraftingLogic.TryCraftItem(slots, MeleeIngredients(), HotbarItemType.Resource), Is.False);
            Assert.That(slots[1].Count, Is.EqualTo(2), "거부 시 재료 무변경");
        }
    }
}
