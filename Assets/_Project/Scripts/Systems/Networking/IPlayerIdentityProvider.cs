namespace Game.Systems.Networking
{
    /// <summary>
    /// 재접속 식별용 로컬 플레이어 토큰 계약. 세션 간 영속·기기(플레이어) 단위이며,
    /// 접속 시 승인 페이로드(NetworkConfig.ConnectionData)로 호스트에 전달돼
    /// "같은 플레이어로 복귀" 판정의 근거가 된다.
    /// 개발 = <see cref="LocalGuidIdentityProvider"/>(로컬 영속 GUID), 릴리스(M6 2차) = Steam ID
    /// 구현으로 교체된다 — 트랜스포트 이원화와 같은 격리 규약으로, 교체 지점은 이 계약뿐이다.
    /// </summary>
    public interface IPlayerIdentityProvider
    {
        /// <summary>이 기기(플레이어)의 영속 식별 토큰. 없으면 최초 접근 시 생성된다.</summary>
        string LocalPlayerToken { get; }
    }
}
