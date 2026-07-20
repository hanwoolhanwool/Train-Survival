namespace Game.Gameplay.World
{
    /// <summary>
    /// 팀 공유 자원 카운터 계약 (슬라이스 §1.1 — 획득 시 카운터 증가만, 인벤토리 없음).
    /// 증가는 호스트 권위 — 서버에서만 <see cref="AddResource"/>를 호출한다.
    /// </summary>
    public interface ISharedResourceCounter
    {
        int Total { get; }

        /// <summary>서버 전용 — 자원 1개 획득 확정.</summary>
        void AddResource();
    }
}
