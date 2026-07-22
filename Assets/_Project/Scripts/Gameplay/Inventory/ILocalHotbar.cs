namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 자기(로컬 소유자) 핫바의 조회·조작 계약 — HUD가 상태를 소유하지 않고 읽기만 하는 창구.
    /// 소유자 플레이어 스폰 시 ServiceLocator에 등록된다. 슬롯 내용의 진실은 호스트 권위다.
    /// </summary>
    public interface ILocalHotbar
    {
        int SlotCount { get; }

        int StackSize { get; }

        /// <summary>현재 선택(든) 슬롯 인덱스 — 로컬 결정.</summary>
        int SelectedIndex { get; }

        HotbarSlotView GetSlot(int index);

        /// <summary>두 슬롯의 교환을 요청한다 (자유 배치, I 창 드래그). 확정은 호스트.</summary>
        void RequestSwap(int a, int b);
    }
}
