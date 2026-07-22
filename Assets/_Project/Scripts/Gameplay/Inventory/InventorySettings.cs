using UnityEngine;

namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 개인 인벤토리 밸런스 데이터 (기획서 §3.4 — 핫바 5칸, 슬롯당 스택).
    /// 칸 수·스택 수는 임의 기준값이며 데이터로 조정한다.
    /// </summary>
    [CreateAssetMenu(fileName = "InventorySettings", menuName = "Game/Inventory Settings")]
    public sealed class InventorySettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int _slotCount = 5;
        [SerializeField, Min(1)] private int _stackSize = 5;

        public int SlotCount => _slotCount;

        public int StackSize => _stackSize;

        /// <summary>총 소지 상한 = 칸 수 × 스택.</summary>
        public int Capacity => _slotCount * _stackSize;
    }
}
