namespace Game.Gameplay.Player
{
    /// <summary>
    /// 로컬 표현 이벤트 — 자기 플레이어가 열차 후미에서 경고 구간(30 m~)에 있는 동안 매 프레임 발행.
    /// HUD 가장자리 경고·거리 표시용 (슬라이스 스펙 §4.2).
    /// </summary>
    public readonly struct FallBehindWarningLocalEvent
    {
        /// <summary>후미 기준 뒤처진 거리 (m).</summary>
        public readonly float MetersBehindRear;

        public FallBehindWarningLocalEvent(float metersBehindRear)
        {
            MetersBehindRear = metersBehindRear;
        }
    }

    /// <summary>
    /// 권위 이벤트 — 호스트가 이탈 사망을 확정한 시점에 각 피어에서 발행 (§4.2 이탈 한계 40 m).
    /// </summary>
    public readonly struct PlayerFellBehindEvent
    {
        public readonly ulong ClientId;

        public PlayerFellBehindEvent(ulong clientId)
        {
            ClientId = clientId;
        }
    }

    /// <summary>
    /// 권위 이벤트 — 호스트가 전투 사망을 확정한 시점에 각 피어에서 발행 (기획서 §9.1, M2).
    /// </summary>
    public readonly struct PlayerDiedEvent
    {
        public readonly ulong ClientId;

        /// <summary>이 피어에서 자기 플레이어의 사망인가 (HUD 사망 연출 필터용).</summary>
        public readonly bool IsLocalPlayer;

        public PlayerDiedEvent(ulong clientId, bool isLocalPlayer)
        {
            ClientId = clientId;
            IsLocalPlayer = isLocalPlayer;
        }
    }

    /// <summary>
    /// 권위 이벤트 — 플레이어 체력 변경. 호스트 확정 값의 동기화 수신 시점에 각 피어에서 발행된다.
    /// HUD 체력 표시가 자기 클라이언트 ID로 걸러 구독한다.
    /// </summary>
    public readonly struct PlayerHealthChangedEvent
    {
        public readonly ulong ClientId;

        /// <summary>이 피어에서 자기 플레이어의 체력인가 (HUD 체력 표시 필터용).</summary>
        public readonly bool IsLocalPlayer;

        public readonly float Health;

        public readonly float MaxHealth;

        public PlayerHealthChangedEvent(ulong clientId, bool isLocalPlayer, float health, float maxHealth)
        {
            ClientId = clientId;
            IsLocalPlayer = isLocalPlayer;
            Health = health;
            MaxHealth = maxHealth;
        }
    }
}
