namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 자원 수납 계약 — 획득(집게)·소모(엔진 투입) 시스템은 구체 타입이 아니라 이 인터페이스에 의존한다 (DIP).
    /// 증감 확정은 호스트 권위 (네트워크 문서 §4 — 개인 인벤토리).
    /// </summary>
    public interface IResourceInventory
    {
        int Count { get; }

        int Capacity { get; }

        bool IsFull { get; }

        /// <summary>자원을 추가한다. 상한 초과 시 전량 실패. 서버 전용 — 클라이언트 호출은 항상 false.</summary>
        bool ServerTryAdd(int amount);

        /// <summary>자원을 차감한다. 잔량 부족 시 실패. 서버 전용 — 클라이언트 호출은 항상 false.</summary>
        bool ServerTryRemove(int amount);
    }
}
