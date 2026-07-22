using UnityEngine;

namespace Game.Gameplay.Inventory
{
    /// <summary>개인 인벤토리 수량·슬롯 표시의 순수 계산 로직 (M2 — 자원 1종 기준 단일 카운트).</summary>
    public static class InventoryMath
    {
        /// <summary>추가 가능 여부 — 상한 초과 시 실패 (부분 추가 없음, 기획서 §3.4 낙하 규칙).</summary>
        public static bool CanAdd(int count, int amount, int capacity)
        {
            return amount > 0 && count + amount <= capacity;
        }

        /// <summary>차감 가능 여부 — 잔량 부족 시 실패.</summary>
        public static bool CanRemove(int count, int amount)
        {
            return amount > 0 && count >= amount;
        }

        /// <summary>슬롯 인덱스(0부터, 앞에서부터 채움)의 표시 수량.</summary>
        public static int GetSlotFill(int count, int slotIndex, int stackSize)
        {
            return Mathf.Clamp(count - slotIndex * stackSize, 0, stackSize);
        }
    }
}
