namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 로컬 표현 이벤트 — 발사 입력 즉시 발행 (지연 0, Q1). 발사음·팔 애니메이션 연출용.
    /// 게임 상태를 변경하는 구독자는 이 이벤트를 구독하면 안 된다.
    /// </summary>
    public readonly struct HarpoonFiredLocalEvent
    {
        public readonly ulong OwnerClientId;

        public HarpoonFiredLocalEvent(ulong ownerClientId)
        {
            OwnerClientId = ownerClientId;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 미스 확정(빗나감 또는 호스트 거부) 순간 발행. 릴 회수 연출·사운드용.
    /// </summary>
    public readonly struct HarpoonMissLocalEvent
    {
        /// <summary>true면 호스트 거부에 의한 미스(로프 미끄러짐 연출), false면 단순 빗나감.</summary>
        public readonly bool WasRejected;

        public HarpoonMissLocalEvent(bool wasRejected)
        {
            WasRejected = wasRejected;
        }
    }
}
