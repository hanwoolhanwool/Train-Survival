using Game.Gameplay.Inventory;

namespace Game.Gameplay.Crafting
{
    /// <summary>
    /// 제작의 순수 규칙 — 재료 차감 + 산출 적재를 한 슬롯 배열 위에서 원자적으로 수행한다
    /// (엔진·SO 무의존, EditMode 테스트 대상). 실패 시 배열이 부분 변경될 수 있으므로
    /// 호출자(PlayerInventory)는 복사본을 넘기고 성공했을 때만 반영한다.
    /// </summary>
    public static class CraftingLogic
    {
        /// <summary>레시피 재료 1종의 순수 표현 — SO를 모르는 계층의 입력.</summary>
        public readonly struct IngredientView
        {
            public readonly ResourceType Type;

            public readonly int Count;

            public IngredientView(ResourceType type, int count)
            {
                Type = type;
                Count = count;
            }
        }

        /// <summary>재료가 전부 충분한지 — 제작 버튼 활성 표시용 (차감하지 않는다).</summary>
        public static bool CanCraft(HotbarSlotView[] slots, IngredientView[] ingredients)
        {
            if (slots == null || ingredients == null)
            {
                return false;
            }

            for (int i = 0; i < ingredients.Length; i++)
            {
                if (HotbarLogic.CountResource(slots, ingredients[i].Type) < ingredients[i].Count)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 제작을 수행한다 — 재료를 전부 차감한 뒤 산출을 적재한다.
        /// 산출을 넣을 자리가 없으면 전량 실패(false) — 재료만 사라지는 결과를 만들지 않는다.
        /// outputStackSize는 산출 종류의 스택 상한 — 호출자가 카탈로그에서 푼다.
        /// </summary>
        public static bool TryCraft(HotbarSlotView[] slots, IngredientView[] ingredients,
            ResourceType output, int outputCount, int outputStackSize)
        {
            if (output == ResourceType.None || outputCount <= 0 ||
                !TryConsumeIngredients(slots, ingredients))
            {
                return false;
            }

            for (int n = 0; n < outputCount; n++)
            {
                if (!HotbarLogic.TryAddResource(slots, output, outputStackSize))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 무기·도구 제작을 수행한다 (M5 2차) — 재료 차감 후 아이템 1개를 첫 빈 칸에 지급한다.
        /// 빈 칸이 없으면 전량 실패(false) — 자원 제작과 같은 원자 규약이다.
        /// </summary>
        public static bool TryCraftItem(HotbarSlotView[] slots, IngredientView[] ingredients,
            HotbarItemType outputItem)
        {
            if (outputItem == HotbarItemType.None || outputItem == HotbarItemType.Resource ||
                !TryConsumeIngredients(slots, ingredients))
            {
                return false;
            }

            return HotbarLogic.TryAddItem(slots, outputItem);
        }

        /// <summary>
        /// 재료만 소모한다 (M5 5차 집게 승급) — 산출이 인벤토리 밖(플레이어 상태)에 있는 레시피용.
        /// 성공 시 슬롯 복사본에서 재료가 빠진 상태가 되고, 실패면 호출자가 복사본을 버린다.
        /// </summary>
        public static bool TryConsume(HotbarSlotView[] slots, IngredientView[] ingredients)
        {
            return TryConsumeIngredients(slots, ingredients);
        }

        private static bool TryConsumeIngredients(HotbarSlotView[] slots, IngredientView[] ingredients)
        {
            if (!CanCraft(slots, ingredients))
            {
                return false;
            }

            for (int i = 0; i < ingredients.Length; i++)
            {
                for (int n = 0; n < ingredients[i].Count; n++)
                {
                    if (!HotbarLogic.TryRemoveResource(slots, ingredients[i].Type))
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
