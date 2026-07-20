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
}
