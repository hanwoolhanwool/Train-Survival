namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 권위 이벤트 — 호스트가 몬스터 사망을 확정한 시점에 각 피어에서 발행 (권위 분담표: 사망 확정 = 호스트).
    /// HUD 처치 수, 사망 연출 등이 이를 구독한다.
    /// </summary>
    public readonly struct MonsterDiedEvent
    {
        /// <summary>처치한 플레이어의 클라이언트 ID (환경 사망은 서버 ID).</summary>
        public readonly ulong KillerClientId;

        /// <summary>무력화(그로기) 상태에서 마무리된 처치인가 — 처형 피드백 구분용 (M5 5차).</summary>
        public readonly bool WasExecuted;

        public MonsterDiedEvent(ulong killerClientId)
            : this(killerClientId, false)
        {
        }

        public MonsterDiedEvent(ulong killerClientId, bool wasExecuted)
        {
            KillerClientId = killerClientId;
            WasExecuted = wasExecuted;
        }
    }
}
