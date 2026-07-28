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

    /// <summary>한 칸 위 건축물의 상태가 바뀜(체력) — 건축물 표현(StructureView)이 구독한다.</summary>
    public readonly struct StructureStateChangedEvent
    {
        public readonly int Index;

        public readonly StructureState State;

        public StructureStateChangedEvent(int index, StructureState state)
        {
            Index = index;
            State = state;
        }
    }

    /// <summary>칸 위 건축물이 파괴됨 — 호스트 확정 후 전 피어에서 발행되는 authored 이벤트(§M3, 기획서 §9).</summary>
    public readonly struct StructureDestroyedEvent
    {
        public readonly int Index;

        public StructureDestroyedEvent(int index)
        {
            Index = index;
        }
    }

    /// <summary>
    /// 칸이 지어짐 — 후미 증설 또는 빈 슬롯 재건. 호스트 확정 후 전 피어에서 발행되는 authored 이벤트(§M3).
    /// 건설 연출·HUD 안내가 구독한다.
    /// </summary>
    public readonly struct CarBuiltEvent
    {
        public readonly int Index;

        /// <summary>true = 파괴·소실된 슬롯 재건, false = 후미 새 칸 증설.</summary>
        public readonly bool Rebuilt;

        public CarBuiltEvent(int index, bool rebuilt)
        {
            Index = index;
            Rebuilt = rebuilt;
        }
    }

    /// <summary>칸 위에 건축물이 설치됨 — 호스트 확정 후 전 피어에서 발행되는 authored 이벤트(§M3).</summary>
    public readonly struct StructureBuiltEvent
    {
        public readonly int Index;

        public StructureBuiltEvent(int index)
        {
            Index = index;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 수리 망치가 지금 겨누고 있는 열차 부위와 그 상태. 조준 HUD("칸 #2 70/100 — 좌클릭 수리")용.
    /// 겨눈 대상·체력·설치 가능 여부가 바뀔 때마다 발행된다.
    /// </summary>
    public readonly struct HammerTargetLocalEvent
    {
        public readonly bool HasTarget;

        public readonly TrainPartKind Kind;

        public readonly int Index;

        public readonly float Health;

        public readonly float MaxHealth;

        /// <summary>지금 좌클릭으로 수리 효과가 있는지(손상돼 있고 수리 가능한 부위).</summary>
        public readonly bool CanRepair;

        /// <summary>지금 우클릭으로 이 칸에 건축물을 설치할 수 있는지(칸 부위를 겨눌 때만).</summary>
        public readonly bool CanBuildStructure;

        public readonly int StructureCost;

        /// <summary>설치 비용을 지불할 자원이 있는지.</summary>
        public readonly bool CanAffordStructure;

        public HammerTargetLocalEvent(bool hasTarget, TrainPartKind kind, int index,
            float health, float maxHealth, bool canRepair,
            bool canBuildStructure, int structureCost, bool canAffordStructure)
        {
            HasTarget = hasTarget;
            Kind = kind;
            Index = index;
            Health = health;
            MaxHealth = maxHealth;
            CanRepair = canRepair;
            CanBuildStructure = canBuildStructure;
            StructureCost = structureCost;
            CanAffordStructure = canAffordStructure;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 자기 플레이어의 증설 포트 상호작용 범위 진입/이탈. HUD 안내("E — 온실칸 증설")용.
    /// 범위·비용 충족 상태가 바뀔 때마다 발행된다.
    /// </summary>
    public readonly struct ExpansionPromptLocalEvent
    {
        public readonly bool InRange;

        public readonly int Cost;

        public readonly bool CanAfford;

        public ExpansionPromptLocalEvent(bool inRange, int cost, bool canAfford)
        {
            InRange = inRange;
            Cost = cost;
            CanAfford = canAfford;
        }
    }
}
