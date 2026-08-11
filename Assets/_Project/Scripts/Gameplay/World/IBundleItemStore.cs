using Game.Gameplay.Inventory;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 보따리 아이템의 서버 내용물 보관소 (M5 8차 — 1차 검증 개선 2). 인벤토리 슬롯은
    /// (종류, 수량, 자원)만 담을 수 있어, 보따리 아이템의 내용물은 서버가 맡아 두고
    /// 슬롯 Count에 보관 id(1~255)만 싣는다. 세션 한정 — 재접속 인벤토리 복원(M6 이월)과
    /// 같은 한계를 공유한다. <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface IBundleItemStore
    {
        /// <summary>서버 전용 — 내용물을 맡기고 보관 id를 받는다. 0 = 보관소 가득 (실패).</summary>
        byte ServerStore(HotbarSlotView[] contents);

        /// <summary>서버 전용 — 보관 내용물 조회 (꺼내지 않는다 — 풀기 성공이 확정된 뒤 제거).</summary>
        bool ServerTryPeek(byte id, out HotbarSlotView[] contents);

        /// <summary>서버 전용 — 보관 해제 (풀기·재설치 확정 후).</summary>
        void ServerRemove(byte id);
    }
}
