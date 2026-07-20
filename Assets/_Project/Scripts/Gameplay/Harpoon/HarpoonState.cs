namespace Game.Gameplay.Harpoon
{
    /// <summary>집게 조작 상태 (슬라이스 스펙 §2.1).</summary>
    public enum HarpoonState
    {
        /// <summary>발사 가능.</summary>
        Ready,

        /// <summary>투사체 비행 중 — 취소 불가 (§2.1 우회 차단).</summary>
        Firing,

        /// <summary>로컬 명중 → 호스트 승인 대기. 로프 탄성 연출로 지연을 흡수하는 구간 (§2.4).</summary>
        PendingGrab,

        /// <summary>릴 감기 중 — 우클릭 취소 가능.</summary>
        Reeling,

        /// <summary>미스 페널티 — 릴 회수 시간 동안 재발사 불가 (§2.3).</summary>
        MissRecovery,

        /// <summary>명중·취소 후 발사 쿨다운 (§2.2).</summary>
        Cooldown
    }
}
