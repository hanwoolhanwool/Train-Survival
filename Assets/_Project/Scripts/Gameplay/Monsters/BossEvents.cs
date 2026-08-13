namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 보스가 등장했다 (M7 2차) — 각 피어의 <see cref="BossHealth"/>가 스폰 시 발행한다.
    /// HUD는 이 이벤트로 표시를 켜고, 이후 갱신은 <see cref="BossHealthChangedEvent"/>로 받는다.
    /// </summary>
    public readonly struct BossSpawnedEvent
    {
        public readonly string DisplayName;

        public readonly float MaxHealth;

        /// <summary>총 페이즈 수 (1 = 페이즈 전환 없음).</summary>
        public readonly int PhaseCount;

        public BossSpawnedEvent(string displayName, float maxHealth, int phaseCount)
        {
            DisplayName = displayName;
            MaxHealth = maxHealth;
            PhaseCount = phaseCount;
        }
    }

    /// <summary>보스 체력이 갱신됐다 — 복제된 체력 변경을 각 피어가 그대로 옮긴다.</summary>
    public readonly struct BossHealthChangedEvent
    {
        public readonly float Current;

        public readonly float Max;

        public BossHealthChangedEvent(float current, float max)
        {
            Current = current;
            Max = max;
        }
    }

    /// <summary>보스 페이즈가 올랐다 (0 = 1페이즈) — 표시·연출 전용.</summary>
    public readonly struct BossPhaseChangedEvent
    {
        public readonly int PhaseIndex;

        public BossPhaseChangedEvent(int phaseIndex)
        {
            PhaseIndex = phaseIndex;
        }
    }

    /// <summary>
    /// 보스가 처치됐다 — 호스트 확정 후 전 피어에서 발행되는 권위 이벤트.
    /// 새벽 보류(<see cref="Cycle.INightHoldGate"/>) 해제와 같은 시점이다.
    /// </summary>
    public readonly struct BossDiedEvent
    {
        public readonly ulong KillerClientId;

        public readonly string DisplayName;

        public BossDiedEvent(ulong killerClientId, string displayName)
        {
            KillerClientId = killerClientId;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// 보스 표시가 끝났다 — 처치·회수(QA·Day 스킵) 공통. HUD 정리는 이 하나만 보면 된다
    /// (사망 배너는 <see cref="BossDiedEvent"/>가 따로 담당한다).
    /// </summary>
    public readonly struct BossDespawnedEvent
    {
    }
}
