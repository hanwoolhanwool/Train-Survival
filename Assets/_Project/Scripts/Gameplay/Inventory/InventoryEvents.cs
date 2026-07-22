namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 로컬 표현 이벤트 — 자기 핫바의 선택 슬롯 변경 (숫자 키 1~5). HUD 무기/아이템 표시용.
    /// </summary>
    public readonly struct HotbarSelectionChangedLocalEvent
    {
        public readonly int SlotIndex;

        public readonly HotbarItemType ItemType;

        public HotbarSelectionChangedLocalEvent(int slotIndex, HotbarItemType itemType)
        {
            SlotIndex = slotIndex;
            ItemType = itemType;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 인벤토리/상태 창(I키) 토글. 열려 있는 동안 시점 회전·무기 입력이 정지된다 (기획서 §3.4).
    /// </summary>
    public readonly struct InventoryPanelToggledLocalEvent
    {
        public readonly bool IsOpen;

        public InventoryPanelToggledLocalEvent(bool isOpen)
        {
            IsOpen = isOpen;
        }
    }
}
