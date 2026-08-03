using UnityEngine;

namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 개인 인벤토리 밸런스 데이터 (기획서 §3.4 — 핫바 5칸 + 별도 가방, 슬롯당 스택).
    /// 화면 하단 핫바(숫자 키 1~5로 선택)와 보관 가방을 분리한다 — 슬롯 목록은 [핫바 | 가방] 순서로 이어 붙인다.
    /// 칸 수·스택 수는 임의 기준값이며 데이터로 조정한다.
    /// </summary>
    [CreateAssetMenu(fileName = "InventorySettings", menuName = "Game/Inventory Settings")]
    public sealed class InventorySettings : ScriptableObject
    {
        [SerializeField, Min(1)] private int _hotbarSize = 5;
        [SerializeField, Min(0)] private int _bagSize = 15;
        [SerializeField, Min(1)] private int _stackSize = 5;

        [Tooltip("시작 시 지급하는 권총탄 수 (M5 — 시작 직후 전투 가능 보장). 0이면 지급 없음.")]
        [SerializeField, Min(0)] private int _initialRevolverAmmo = 8;

        /// <summary>화면 하단 핫바 칸 수 — 숫자 키 1~5로 드는 칸.</summary>
        public int HotbarSize => _hotbarSize;

        /// <summary>보관 전용 가방 칸 수 — I 창에서만 접근, 핫바로 옮겨야 사용한다.</summary>
        public int BagSize => _bagSize;

        /// <summary>전체 슬롯 수 = 핫바 + 가방. 슬롯 목록의 앞쪽 <see cref="HotbarSize"/>칸이 핫바다.</summary>
        public int SlotCount => _hotbarSize + _bagSize;

        public int StackSize => _stackSize;

        /// <summary>총 소지 상한 = 전체 칸 수 × 스택.</summary>
        public int Capacity => SlotCount * _stackSize;

        /// <summary>시작 지급 권총탄 수.</summary>
        public int InitialRevolverAmmo => _initialRevolverAmmo;
    }
}
