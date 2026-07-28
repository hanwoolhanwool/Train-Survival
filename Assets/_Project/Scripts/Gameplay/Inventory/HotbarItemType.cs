namespace Game.Gameplay.Inventory
{
    /// <summary>통합 핫바 슬롯의 아이템 종류 (기획서 §3.4 — 무기와 자원이 한 핫바에 들어간다).</summary>
    public enum HotbarItemType : byte
    {
        None = 0,
        Harpoon = 1,
        Revolver = 2,
        Resource = 3,

        /// <summary>수리 망치 (기획서 §9 — 열차 부위 수리 도구, §M3).</summary>
        Hammer = 4,
    }

    /// <summary>
    /// 핫바 슬롯의 읽기용 뷰 — UI·순수 로직이 쓰는 엔진/네트워크 무의존 표현.
    /// 네트워크 동기화용 직렬화 구조체는 PlayerInventory 내부에 둔다.
    /// </summary>
    public readonly struct HotbarSlotView
    {
        public readonly HotbarItemType ItemType;

        public readonly int Count;

        public HotbarSlotView(HotbarItemType itemType, int count)
        {
            ItemType = itemType;
            Count = count;
        }

        public bool IsEmpty => ItemType == HotbarItemType.None ||
            (ItemType == HotbarItemType.Resource && Count <= 0);
    }
}
