namespace Game.Systems.Networking
{
    /// <summary>
    /// 호스트 전용 — 접속 승인 시 기록된 식별 토큰 ↔ clientId 매핑 조회 계약.
    /// 재접속 판정(PlayerSessionRegistry)이 끊김(despawn)·스폰 시점에 clientId로 토큰을 찾는 데 쓴다.
    /// </summary>
    public interface IConnectionIdentityRegistry
    {
        /// <summary>clientId로 승인 시 기록된 토큰을 찾는다. 매핑이 없으면 false.</summary>
        bool TryGetToken(ulong clientId, out string token);
    }
}
