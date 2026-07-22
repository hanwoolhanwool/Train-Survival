namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 통합 핫바의 순수 규칙 (기획서 §3.4 — 무기+자원 5칸, 자유 배치, 슬롯당 스택).
    /// 슬롯 배열을 직접 수정한다 — 호출자(PlayerInventory)가 권위 상태로의 반영을 책임진다.
    /// </summary>
    public static class HotbarLogic
    {
        /// <summary>
        /// 자원 1개를 적재한다 — 여유 있는 기존 자원 스택(앞에서부터)을 먼저 채우고, 없으면 첫 빈 칸에 새 스택.
        /// 전부 차 있으면 실패 (획득 불가·낙하 규칙).
        /// </summary>
        public static bool TryAddResource(HotbarSlotView[] slots, int stackSize)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ItemType == HotbarItemType.Resource && slots[i].Count < stackSize)
                {
                    slots[i] = new HotbarSlotView(HotbarItemType.Resource, slots[i].Count + 1);
                    return true;
                }
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i] = new HotbarSlotView(HotbarItemType.Resource, 1);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 자원 1개를 차감한다 — 뒤에서부터(부분 스택이 먼저 비도록) 찾는다. 스택이 비면 빈 칸이 된다.
        /// </summary>
        public static bool TryRemoveResource(HotbarSlotView[] slots)
        {
            for (int i = slots.Length - 1; i >= 0; i--)
            {
                if (slots[i].ItemType == HotbarItemType.Resource && slots[i].Count > 0)
                {
                    int remaining = slots[i].Count - 1;
                    slots[i] = remaining > 0
                        ? new HotbarSlotView(HotbarItemType.Resource, remaining)
                        : new HotbarSlotView(HotbarItemType.None, 0);
                    return true;
                }
            }

            return false;
        }

        /// <summary>현재 소지한 자원 총량.</summary>
        public static int CountResource(HotbarSlotView[] slots)
        {
            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ItemType == HotbarItemType.Resource)
                {
                    total += slots[i].Count;
                }
            }

            return total;
        }

        /// <summary>현재 배치 기준 자원 소지 상한 — 자원 스택 칸 + 빈 칸이 전부 만탄일 때의 총량.</summary>
        public static int ResourceCapacity(HotbarSlotView[] slots, int stackSize)
        {
            int capacity = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ItemType == HotbarItemType.Resource || slots[i].IsEmpty)
                {
                    capacity += stackSize;
                }
            }

            return capacity;
        }

        /// <summary>슬롯 교환(자유 배치)이 유효한 요청인지 — 범위 안의 서로 다른 두 칸.</summary>
        public static bool IsValidSwap(int a, int b, int slotCount)
        {
            return a != b &&
                a >= 0 && a < slotCount &&
                b >= 0 && b < slotCount;
        }
    }
}
