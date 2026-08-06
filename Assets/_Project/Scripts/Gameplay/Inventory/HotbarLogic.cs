namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 통합 핫바의 순수 규칙 (기획서 §3.4 — 무기+자원 5칸, 자유 배치, 슬롯당 스택).
    /// 슬롯 배열을 직접 수정한다 — 호출자(PlayerInventory)가 권위 상태로의 반영을 책임진다.
    /// </summary>
    public static class HotbarLogic
    {
        /// <summary>
        /// 자원 1개를 적재한다 — 같은 종류의 여유 있는 스택(앞에서부터)을 먼저 채우고, 없으면 첫 빈 칸에 새 스택.
        /// 전부 차 있으면 실패 (획득 불가·낙하 규칙). stackSize는 해당 종류의 스택 상한 — 호출자가 카탈로그에서 푼다.
        /// </summary>
        public static bool TryAddResource(HotbarSlotView[] slots, ResourceType type, int stackSize)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ItemType == HotbarItemType.Resource &&
                    slots[i].Resource == type && slots[i].Count < stackSize)
                {
                    slots[i] = new HotbarSlotView(HotbarItemType.Resource, slots[i].Count + 1, type);
                    return true;
                }
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i] = new HotbarSlotView(HotbarItemType.Resource, 1, type);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 무기·도구 아이템 1개를 첫 빈 칸에 적재한다 (제작 무기 지급 — 스택 없음, M5 2차).
        /// 빈 칸이 없으면 실패한다. 자원은 <see cref="TryAddResource"/> 소관.
        /// </summary>
        public static bool TryAddItem(HotbarSlotView[] slots, HotbarItemType item)
        {
            if (item == HotbarItemType.None || item == HotbarItemType.Resource)
            {
                return false;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i] = new HotbarSlotView(item, 1);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 지정한 종류의 자원 1개를 차감한다 — 뒤에서부터(부분 스택이 먼저 비도록) 찾는다. 스택이 비면 빈 칸이 된다.
        /// </summary>
        public static bool TryRemoveResource(HotbarSlotView[] slots, ResourceType type)
        {
            for (int i = slots.Length - 1; i >= 0; i--)
            {
                if (slots[i].ItemType == HotbarItemType.Resource &&
                    slots[i].Resource == type && slots[i].Count > 0)
                {
                    RemoveOneAt(slots, i);
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 조건을 만족하는 종류의 자원 1개를 차감한다 (건설 비용 = "건자재 아무거나 N개" 지불용).
        /// 뒤에서부터 찾아 부분 스택이 먼저 빈다.
        /// </summary>
        public static bool TryRemoveAnyResource(HotbarSlotView[] slots, System.Func<ResourceType, bool> filter)
        {
            return TryRemoveAnyResource(slots, filter, out _);
        }

        /// <summary>
        /// <see cref="TryRemoveAnyResource(HotbarSlotView[], System.Func{ResourceType, bool})"/>와 동일하되
        /// 차감된 종류를 돌려준다 — HUD의 소모 미리보기가 서버와 같은 규칙으로 내역을 집계할 수 있게 한다.
        /// </summary>
        public static bool TryRemoveAnyResource(HotbarSlotView[] slots, System.Func<ResourceType, bool> filter,
            out ResourceType removed)
        {
            for (int i = slots.Length - 1; i >= 0; i--)
            {
                if (slots[i].ItemType == HotbarItemType.Resource &&
                    slots[i].Count > 0 && filter(slots[i].Resource))
                {
                    removed = slots[i].Resource;
                    RemoveOneAt(slots, i);
                    return true;
                }
            }

            removed = ResourceType.None;
            return false;
        }

        /// <summary>
        /// 지정한 칸에서 자원 1개를 차감한다 (엔진 투입 = 든 칸의 자원 소모, 기획서 §3.4).
        /// 그 칸이 자원 스택이 아니거나 범위 밖이면 실패한다 — 어떤 칸이 소모될지 모호하지 않다.
        /// 어느 종류가 소모되는지는 칸이 정하므로 종류 인자가 없다.
        /// </summary>
        public static bool TryRemoveResourceAt(HotbarSlotView[] slots, int index)
        {
            if (index < 0 || index >= slots.Length ||
                slots[index].ItemType != HotbarItemType.Resource || slots[index].Count <= 0)
            {
                return false;
            }

            RemoveOneAt(slots, index);
            return true;
        }

        /// <summary>지정한 종류의 자원 소지 총량.</summary>
        public static int CountResource(HotbarSlotView[] slots, ResourceType type)
        {
            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ItemType == HotbarItemType.Resource && slots[i].Resource == type)
                {
                    total += slots[i].Count;
                }
            }

            return total;
        }

        /// <summary>조건을 만족하는 종류의 자원 소지 총량 (건자재 잔량 표시·비용 검증용).</summary>
        public static int CountResource(HotbarSlotView[] slots, System.Func<ResourceType, bool> filter)
        {
            int total = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].ItemType == HotbarItemType.Resource && filter(slots[i].Resource))
                {
                    total += slots[i].Count;
                }
            }

            return total;
        }

        /// <summary>
        /// 지정한 칸의 자원 스택을 전량 비운다 (아이템 버리기, M5 3차 — hotbar 명세 §11 해소).
        /// 무기·도구 칸은 실패한다 — 버릴 수 없고 처분은 공유 창고 보관이 담당한다.
        /// 비워진 종류·수량을 돌려줘 호출자(버리기 확정)가 지상 낙하 스폰에 쓴다.
        /// </summary>
        public static bool TryClearResourceSlot(
            HotbarSlotView[] slots, int index, out ResourceType type, out int count)
        {
            type = ResourceType.None;
            count = 0;

            if (index < 0 || index >= slots.Length ||
                slots[index].ItemType != HotbarItemType.Resource || slots[index].Count <= 0)
            {
                return false;
            }

            type = slots[index].Resource;
            count = slots[index].Count;
            slots[index] = new HotbarSlotView(HotbarItemType.None, 0);
            return true;
        }

        private static void RemoveOneAt(HotbarSlotView[] slots, int index)
        {
            int remaining = slots[index].Count - 1;
            slots[index] = remaining > 0
                ? new HotbarSlotView(HotbarItemType.Resource, remaining, slots[index].Resource)
                : new HotbarSlotView(HotbarItemType.None, 0);
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
