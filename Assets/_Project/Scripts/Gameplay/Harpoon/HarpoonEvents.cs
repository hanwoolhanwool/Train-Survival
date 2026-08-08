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

    /// <summary>
    /// 로컬 표현 이벤트 — 호스트가 그랩을 거부한 사유 (M5 5차). 노드 외형이 종류 색으로만 구분되므로
    /// "왜 안 잡히는지"를 HUD가 알려줘야 한다 (등급 부족 = "강화 집게가 필요하다").
    /// 소유자 거부 RPC 안에서만 발행된다 — 판정에는 영향이 없다.
    /// </summary>
    public readonly struct HarpoonGrabRejectedLocalEvent
    {
        public readonly GrabVerdict Verdict;

        public HarpoonGrabRejectedLocalEvent(GrabVerdict verdict)
        {
            Verdict = verdict;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 소유자의 집게 등급이 바뀐 순간 발행 (M5 5차 승급). HUD 표시명 갱신용.
    /// </summary>
    public readonly struct HarpoonTierChangedLocalEvent
    {
        public readonly int Tier;

        public HarpoonTierChangedLocalEvent(int tier)
        {
            Tier = tier;
        }
    }
}
