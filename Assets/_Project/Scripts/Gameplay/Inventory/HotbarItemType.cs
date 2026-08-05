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

        /// <summary>샷건 — 승차한 근접 몬스터 처리 (기획서 §6.2, M5 2차 제작 무기).</summary>
        Shotgun = 5,

        /// <summary>볼트액션 라이플 — 원거리 접근 저지 (기획서 §6.2, M5 2차 제작 무기).</summary>
        Rifle = 6,
    }

    /// <summary>
    /// 핫바 슬롯의 읽기용 뷰 — UI·순수 로직이 쓰는 엔진/네트워크 무의존 표현.
    /// 네트워크 동기화용 직렬화 구조체는 PlayerInventory 내부에 둔다.
    /// </summary>
    public readonly struct HotbarSlotView
    {
        public readonly HotbarItemType ItemType;

        public readonly int Count;

        /// <summary>ItemType이 <see cref="HotbarItemType.Resource"/>일 때의 자원 종류. 그 외에는 None.</summary>
        public readonly ResourceType Resource;

        public HotbarSlotView(HotbarItemType itemType, int count)
            : this(itemType, count, ResourceType.None)
        {
        }

        public HotbarSlotView(HotbarItemType itemType, int count, ResourceType resource)
        {
            ItemType = itemType;
            Count = count;
            Resource = resource;
        }

        public bool IsEmpty => ItemType == HotbarItemType.None ||
            (ItemType == HotbarItemType.Resource && Count <= 0);
    }
}
