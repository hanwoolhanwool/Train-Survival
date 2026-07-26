namespace Game.Gameplay.Train
{
    /// <summary>
    /// 로컬 표현 이벤트 — 자기 플레이어의 엔진 상호작용 범위 진입/이탈. HUD 투입 안내("E — 연료 투입")용.
    /// 범위 상태가 바뀔 때마다 발행된다.
    /// </summary>
    public readonly struct EnginePromptLocalEvent
    {
        public readonly bool InRange;

        public EnginePromptLocalEvent(bool inRange)
        {
            InRange = inRange;
        }
    }

    /// <summary>
    /// 편성 상태가 준비됨 — TrainState 스폰 시 모든 피어에서 발행(신규·후발 접속 공통). CarView·UI가 현재 편성으로 재동기화한다.
    /// </summary>
    public readonly struct TrainInitializedEvent
    {
        public readonly int CarCount;

        public TrainInitializedEvent(int carCount)
        {
            CarCount = carCount;
        }
    }

    /// <summary>
    /// 한 칸의 상태가 바뀜(체력·연결) — 호스트 변이가 NetworkList에 반영될 때 모든 피어에서 발행된다(권위 이벤트).
    /// 칸 표현(CarView)이 이를 구독해 표현만 갱신한다 (§M3 — 파괴/이탈은 권위 이벤트로 발행).
    /// </summary>
    public readonly struct CarStateChangedEvent
    {
        public readonly int Index;

        public readonly CarState State;

        public CarStateChangedEvent(int index, CarState state)
        {
            Index = index;
            State = state;
        }
    }

    /// <summary>한 연결부의 상태가 바뀜 — CouplingPart 표현이 구독한다.</summary>
    public readonly struct CouplingStateChangedEvent
    {
        public readonly int Index;

        public readonly CouplingState State;

        public CouplingStateChangedEvent(int index, CouplingState state)
        {
            Index = index;
            State = state;
        }
    }

    /// <summary>
    /// 칸이 파괴됨 — 호스트가 확정 후 전 피어에 authored 이벤트로 발행(§M3). 방어 UI·파괴 연출이 구독한다.
    /// </summary>
    public readonly struct CarDestroyedEvent
    {
        public readonly int Index;

        public CarDestroyedEvent(int index)
        {
            Index = index;
        }
    }

    /// <summary>연결부가 끊김 — 방어 목표(연결부)가 뚫렸음을 알리는 authored 이벤트(§M3, 기획서 §9).</summary>
    public readonly struct CouplingBrokenEvent
    {
        public readonly int Index;

        public CouplingBrokenEvent(int index)
        {
            Index = index;
        }
    }

    /// <summary>
    /// 후방 칸들이 연쇄 이탈함 — 한 번의 방어 실패로 통째로 떨어져 나간 칸 묶음(오름차순 인덱스).
    /// 이탈 연출·"N칸 이탈" 경고 UI가 하나의 사건으로 구독한다(§M3 — 연쇄 이탈은 권위 이벤트로).
    /// </summary>
    public readonly struct CarsDetachedEvent
    {
        public readonly int[] Indices;

        public CarsDetachedEvent(int[] indices)
        {
            Indices = indices;
        }
    }
}
