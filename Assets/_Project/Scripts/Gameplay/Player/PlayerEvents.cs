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
    /// 로컬 표현 이벤트 — Esc의 "열린 창 닫기" 요청 (M5 4차 — Esc 우선순위).
    /// 열린 창(I 창·창고·제작)의 소유자들이 구독해 자기 창을 닫는다. 열린 창이 없을 때만
    /// Esc가 세션 메뉴를 연다 — 발행 판단은 SessionExitHud가 창 토글 이벤트 추적으로 한다.
    /// </summary>
    public readonly struct UiCloseRequestedLocalEvent
    {
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 세션 메뉴(Esc) 열림/닫힘. 열려 있는 동안 시점 회전을 멈추고 커서를 버튼 클릭용으로 푼다
    /// (I 창과 동일한 처리 — 플레이어 컨트롤러·무기 게이트가 구독한다).
    /// </summary>
    public readonly struct SessionMenuToggledLocalEvent
    {
        public readonly bool IsOpen;

        public SessionMenuToggledLocalEvent(bool isOpen)
        {
            IsOpen = isOpen;
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

    /// <summary>
    /// 권위 이벤트 — 플레이어 허기 변경 (기획서 §3.4, M5 4차). 호스트가 계산한 값의 동기화 수신
    /// 시점에 각 피어에서 발행된다. HUD 허기 게이지·경고가 자기 플레이어로 걸러 구독한다.
    /// </summary>
    public readonly struct PlayerHungerChangedEvent
    {
        public readonly ulong ClientId;

        /// <summary>이 피어에서 자기 플레이어의 허기인가 (HUD 필터용).</summary>
        public readonly bool IsLocalPlayer;

        public readonly float Hunger;

        public readonly float MaxHunger;

        /// <summary>경고 임계 기준 압박 단계.</summary>
        public readonly HungerStress Stress;

        public PlayerHungerChangedEvent(
            ulong clientId, bool isLocalPlayer, float hunger, float maxHunger, HungerStress stress)
        {
            ClientId = clientId;
            IsLocalPlayer = isLocalPlayer;
            Hunger = hunger;
            MaxHunger = maxHunger;
            Stress = stress;
        }
    }

    /// <summary>
    /// 권위 이벤트 — 요리 버프 잔여 시간 변경 (기획서 §7.3, M5 4차). 호스트 확정 값의 동기화
    /// 수신 시점에 각 피어에서 발행된다. HUD 버프 표시가 자기 플레이어로 걸러 구독한다.
    /// </summary>
    public readonly struct PlayerBuffsChangedEvent
    {
        public readonly ulong ClientId;

        /// <summary>이 피어에서 자기 플레이어의 버프인가 (HUD 필터용).</summary>
        public readonly bool IsLocalPlayer;

        /// <summary>재생 버프 잔여 초 (0 = 없음).</summary>
        public readonly float RegenRemainingSeconds;

        /// <summary>보온 버프 잔여 초 (0 = 없음).</summary>
        public readonly float WarmthRemainingSeconds;

        public PlayerBuffsChangedEvent(
            ulong clientId, bool isLocalPlayer, float regenRemainingSeconds, float warmthRemainingSeconds)
        {
            ClientId = clientId;
            IsLocalPlayer = isLocalPlayer;
            RegenRemainingSeconds = regenRemainingSeconds;
            WarmthRemainingSeconds = warmthRemainingSeconds;
        }
    }

    /// <summary>
    /// 권위 이벤트 — 플레이어 체온 변경 (기획서 §4.2, M4). 호스트가 계산한 값의 동기화 수신 시점에
    /// 각 피어에서 발행된다. HUD 체온 표시·경고가 자기 플레이어로 걸러 구독한다.
    /// </summary>
    public readonly struct PlayerTemperatureChangedEvent
    {
        public readonly ulong ClientId;

        /// <summary>이 피어에서 자기 플레이어의 체온인가 (HUD 필터용).</summary>
        public readonly bool IsLocalPlayer;

        /// <summary>현재 체온 (℃).</summary>
        public readonly float Temperature;

        /// <summary>경고 임계 기준 압박 단계.</summary>
        public readonly TemperatureStress Stress;

        public PlayerTemperatureChangedEvent(ulong clientId, bool isLocalPlayer, float temperature, TemperatureStress stress)
        {
            ClientId = clientId;
            IsLocalPlayer = isLocalPlayer;
            Temperature = temperature;
            Stress = stress;
        }
    }

    /// <summary>
    /// 권위 이벤트 — 부위별 동상 단계 변경 (기획서 §4.4, M7 3차). <b>단계가 바뀔 때만</b> 발행된다
    /// (진행도는 서버 전용이라 매 프레임 발행할 값이 없다). 화면 결빙 오버레이와 동상 HUD가
    /// 자기 플레이어로 걸러 구독한다 — 둘 다 상태를 소유하지 않고 이 이벤트만 받아 그린다.
    /// </summary>
    public readonly struct PlayerFrostbiteChangedEvent
    {
        public readonly ulong ClientId;

        /// <summary>이 피어에서 자기 플레이어의 동상인가 (HUD·오버레이 필터용).</summary>
        public readonly bool IsLocalPlayer;

        /// <summary>부위 4개 × 2비트 비트팩 — 부위별 단계는 <see cref="FrostbiteMath.Unpack"/>으로 꺼낸다.</summary>
        public readonly byte PackedStages;

        /// <summary>네 부위 단계 합계 (0~8) — 이동속도 배율과 화면 결빙 강도의 공통 입력.</summary>
        public readonly int StageSum;

        public PlayerFrostbiteChangedEvent(ulong clientId, bool isLocalPlayer, byte packedStages)
        {
            ClientId = clientId;
            IsLocalPlayer = isLocalPlayer;
            PackedStages = packedStages;
            StageSum = FrostbiteMath.SumStages(packedStages);
        }
    }
}
