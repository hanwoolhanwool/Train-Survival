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
            if (output == ResourceType.None || outputCount <= 0 || !CanCraft(slots, ingredients))
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

            for (int n = 0; n < outputCount; n++)
            {
                if (!HotbarLogic.TryAddResource(slots, output, outputStackSize))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
