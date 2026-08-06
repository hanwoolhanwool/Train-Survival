namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 자기(로컬 소유자) 핫바의 조회·조작 계약 — HUD가 상태를 소유하지 않고 읽기만 하는 창구.
    /// 소유자 플레이어 스폰 시 ServiceLocator에 등록된다. 슬롯 내용의 진실은 호스트 권위다.
    /// </summary>
    public interface ILocalHotbar
    {
        /// <summary>전체 슬롯 수 (핫바 + 가방) — I 창이 그리는 범위.</summary>
        int SlotCount { get; }

        /// <summary>앞쪽 핫바 칸 수 — 화면 하단 핫바·숫자 키 1~5가 그리는 범위.</summary>
        int HotbarSize { get; }

        int StackSize { get; }

        /// <summary>현재 선택(든) 슬롯 인덱스 — 로컬 결정.</summary>
        int SelectedIndex { get; }

        HotbarSlotView GetSlot(int index);

        /// <summary>두 슬롯의 교환을 요청한다 (자유 배치, I 창 드래그). 확정은 호스트.</summary>
        void RequestSwap(int a, int b);

        /// <summary>
        /// 칸의 자원 스택 전량 버리기를 요청한다 (I 창에서 패널 밖 드롭, M5 3차).
        /// 무기·도구 칸은 서버가 기각한다. 확정은 호스트 — 버린 자원은 열차 측면 지상에 낙하한다.
        /// </summary>
        void RequestDrop(int slotIndex);
    }
}
