namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 권위 이벤트 — 개인 인벤토리 잔량 변경. 호스트 확정 값의 동기화 수신 시점에 각 피어에서 발행된다.
    /// HUD 핫바가 자기 플레이어 플래그로 걸러 구독한다.
    /// </summary>
    public readonly struct InventoryChangedEvent
    {
        public readonly ulong ClientId;

        /// <summary>이 피어에서 자기 플레이어의 인벤토리인가 (HUD 필터용).</summary>
        public readonly bool IsLocalPlayer;

        public readonly int Count;

        public readonly int Capacity;

        public readonly int SlotCount;

        public readonly int StackSize;

        public InventoryChangedEvent(
            ulong clientId, bool isLocalPlayer, int count, int capacity, int slotCount, int stackSize)
        {
            ClientId = clientId;
            IsLocalPlayer = isLocalPlayer;
            Count = count;
            Capacity = capacity;
            SlotCount = slotCount;
            StackSize = stackSize;
        }
    }
}
