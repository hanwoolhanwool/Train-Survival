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

        /// <summary>마체테 — 탄약 0일 때의 최후 근접 수단, 무한 사용 (기획서 §6.2, M5 2차 제작 무기).</summary>
        Melee = 7,

        // ── 장비 (기획서 §6.3 — 의류/방어구, M5 3차 제작 장비). 부위·효과는 EquipmentCatalog가 정한다 ──

        /// <summary>가죽 옷 (상체) — 피해 감소 소 + 보온 대. 사막 낮에는 역효과.</summary>
        LeatherCoat = 8,

        /// <summary>사막 로브 (상체) — 낮 열 차단 + 밤 보온 소량. 사막 전용 해법.</summary>
        DesertRobe = 9,

        /// <summary>고철 투구 (머리) — 피해 감소 특화.</summary>
        ScrapHelmet = 10,

        /// <summary>누비 바지 (하체) — 보온 소량.</summary>
        PaddedPants = 11,

        /// <summary>사막 장화 (신발) — 내열 소량.</summary>
        DesertBoots = 12,

        /// <summary>
        /// 창고 보따리 (M5 8차 — 집게 일괄 획득의 공간 부족 대체 획득물). 스택 없음 — 1칸.
        /// Count에는 수량이 아니라 서버 보관소의 보관 id(1~255)가 실린다
        /// (세션 한정 — 재접속 인벤토리 복원은 M6 이월과 같은 한계).
        /// </summary>
        Bundle = 13,
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
