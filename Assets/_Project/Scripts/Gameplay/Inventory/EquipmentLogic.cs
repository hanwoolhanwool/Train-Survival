namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 장비 착용·해제·효과 합산의 순수 규칙 (기획서 §6.3, M5 3차).
    /// 부위 판정은 카탈로그 소관이라 호출자가 resolver로 주입한다 (순수 로직이 SO를 모르게 하는 경계).
    /// 슬롯 배열을 직접 수정한다 — 호출자(PlayerInventory)가 권위 상태로의 반영을 책임진다.
    /// </summary>
    public static class EquipmentLogic
    {
        /// <summary>부위 판정 — 장비가 아니면 false.</summary>
        public delegate bool SlotResolver(HotbarItemType item, out EquipSlot slot);

        /// <summary>
        /// 인벤토리 칸의 장비를 착용한다 — 착용 칸(부위 인덱스)이 점유돼 있으면 자리를 맞바꾼다.
        /// 장비가 아닌 아이템·빈 칸은 실패.
        /// </summary>
        public static bool TryEquip(
            HotbarSlotView[] slots, HotbarSlotView[] equipment, int slotIndex, SlotResolver resolver)
        {
            if (slots == null || equipment == null || resolver == null
                || slotIndex < 0 || slotIndex >= slots.Length)
            {
                return false;
            }

            HotbarSlotView item = slots[slotIndex];
            if (item.IsEmpty || !resolver(item.ItemType, out EquipSlot part))
            {
                return false;
            }

            int partIndex = (int)part;
            if (partIndex < 0 || partIndex >= equipment.Length)
            {
                return false;
            }

            HotbarSlotView previous = equipment[partIndex];
            equipment[partIndex] = item;
            slots[slotIndex] = previous;
            return true;
        }

        /// <summary>착용 장비를 해제해 첫 빈 인벤토리 칸으로 되돌린다 — 빈 칸이 없으면 실패(장비 보존).</summary>
        public static bool TryUnequip(HotbarSlotView[] equipment, int partIndex, HotbarSlotView[] slots)
        {
            if (equipment == null || slots == null
                || partIndex < 0 || partIndex >= equipment.Length
                || equipment[partIndex].IsEmpty)
            {
                return false;
            }

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i] = equipment[partIndex];
                    equipment[partIndex] = new HotbarSlotView(HotbarItemType.None, 0);
                    return true;
                }
            }

            return false;
        }

        /// <summary>합산 피해 감소를 상한으로 자른 물리 피해 배율 — 1이면 감소 없음.</summary>
        public static float GetDamageMultiplier(float totalReduction, float cap)
        {
            float clamped = totalReduction < 0f ? 0f : totalReduction;
            if (clamped > cap)
            {
                clamped = cap;
            }

            return 1f - clamped;
        }

        /// <summary>부위 합산 단열 계수를 유효 범위로 자른다 — 상한 0.9 (완전 무효화 방지), 하한 −1.</summary>
        public static float ClampInsulation(float total)
        {
            if (total < -1f)
            {
                return -1f;
            }

            return total > 0.9f ? 0.9f : total;
        }
    }
}
