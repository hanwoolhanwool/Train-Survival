namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 자원 수납 계약 — 획득(집게)·소모(엔진 투입·건설 비용) 시스템은 구체 타입이 아니라 이 인터페이스에 의존한다 (DIP).
    /// 증감 확정은 호스트 권위 (네트워크 문서 §4 — 개인 인벤토리).
    /// 자원은 종류(<see cref="ResourceType"/>)를 가지며, 건설 비용은 "건자재 아무거나 N개"로 지불한다.
    /// </summary>
    public interface IResourceInventory
    {
        /// <summary>건자재 소지 총량 (건설 비용 검증·HUD 표시용 — 탄약 등 비건자재는 제외).</summary>
        int Count { get; }

        int Capacity { get; }

        bool IsFull { get; }

        /// <summary>지정한 종류의 소지 총량.</summary>
        int CountOf(ResourceType type);

        /// <summary>지정한 슬롯의 자원 종류. 자원 칸이 아니면 None.</summary>
        ResourceType GetResourceTypeAt(int slotIndex);

        /// <summary>지정한 종류의 자원을 추가한다. 상한 초과 시 전량 실패. 서버 전용 — 클라이언트 호출은 항상 false.</summary>
        bool ServerTryAdd(ResourceType type, int amount);

        /// <summary>지정한 종류의 자원을 차감한다. 잔량 부족 시 전량 실패. 서버 전용 — 클라이언트 호출은 항상 false.</summary>
        bool ServerTryRemove(ResourceType type, int amount);

        /// <summary>
        /// 지정한 슬롯에서 자원을 차감한다 (엔진 투입 = 든 칸 소모). 그 칸이 자원이 아니면 실패.
        /// 서버 전용 — 클라이언트 호출은 항상 false.
        /// </summary>
        bool ServerTryRemoveAt(int slotIndex, int amount);

        /// <summary>
        /// 건자재 amount개를 차감한 뒤 confirm을 실행하고, confirm이 성공했을 때만 차감을 반영한다 (원자 지불).
        /// 차감·확정 어느 쪽이 실패해도 인벤토리는 원상 유지 — 호출부의 수동 롤백을 없앤다.
        /// 서버 전용 — 클라이언트 호출은 항상 false.
        /// </summary>
        bool ServerTrySpend(int amount, System.Func<bool> confirm);
    }
}
