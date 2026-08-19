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

    /// <summary>핫바 선택이 거부된 사유 — 지금은 1단계 집게의 손 점유 하나뿐이다.</summary>
    public enum HotbarSwitchRejectReason : byte
    {
        /// <summary>1단계 집게가 그랩을 유지 중이라 오른손이 묶여 있다 (기획서 §3.1).</summary>
        HarpoonTier1HandsFull = 0
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 숫자 키 선택이 <b>거부</b>됐다 (집게 단계별 파지 계획 §3.6).
    /// 잡은 채로 무기를 바꾸려는 시도는 "눌렀는데 아무 일도 안 남"으로 보이면 고장으로 읽히므로,
    /// 왜 안 되는지를 화면이 알려야 한다. 판정에는 영향이 없다.
    /// </summary>
    public readonly struct HotbarSelectionRejectedLocalEvent
    {
        /// <summary>거부된 슬롯 (연출을 그 칸에 건다).</summary>
        public readonly int SlotIndex;

        public readonly HotbarSwitchRejectReason Reason;

        /// <summary>
        /// 문구를 띄울 차례인가 — <b>같은 그랩의 첫 회만</b> true. 슬롯을 연타할 때 토스트가
        /// 쌓이지 않게 하되, 연출(슬롯 흔들림)은 매번 나간다 (확정 ⑥).
        /// </summary>
        public readonly bool ShowMessage;

        public HotbarSelectionRejectedLocalEvent(int slotIndex, HotbarSwitchRejectReason reason, bool showMessage)
        {
            SlotIndex = slotIndex;
            Reason = reason;
            ShowMessage = showMessage;
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
