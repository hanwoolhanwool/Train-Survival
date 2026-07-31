using UnityEngine;

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

    /// <summary>
    /// 이탈 칸이 편성에 다시 붙음 — 손잡이로 슬롯까지 끌어온 칸의 재결합이 호스트에서 확정된 뒤
    /// 전 피어에서 발행되는 authored 이벤트 (손잡이-이탈저항 스펙 §4.1). 재결합 연출·알림이 구독한다.
    /// </summary>
    public readonly struct CarRecoupledEvent
    {
        public readonly int Index;

        public CarRecoupledEvent(int index)
        {
            Index = index;
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
    /// 로컬 표현 이벤트 — 망치로 칸 건설 지점(재건 슬롯·후미 연결부)을 겨눈 상태 (M3 피드백 — 건설 포트의 망치 통합).
    /// HUD 안내("우클릭 — 칸 건설")와 초록 테두리 프리뷰(<see cref="CarBuildGhostView"/>)가 그린다.
    /// 조준 성립·건설 슬롯·비용 충족이 바뀔 때마다 발행된다.
    /// </summary>
    public readonly struct CarBuildAimLocalEvent
    {
        public readonly bool Aiming;

        /// <summary>지어질 슬롯 — 재건할 첫 빈 슬롯, 없으면 후미 증설 슬롯.</summary>
        public readonly int SlotIndex;

        public readonly int Cost;

        public readonly bool CanAfford;

        /// <summary>지어질 자리에 플레이어·몬스터가 들어와 있는지 — 있으면 그 안에 칸을 지을 수 없다.</summary>
        public readonly bool Occupied;

        /// <summary>지어질 칸의 월드 중심 — 프리뷰 박스용. 열차는 원점 고정이라 슬롯이 정해지면 상수다.</summary>
        public readonly Vector3 GhostCenter;

        /// <summary>지어질 칸의 크기(폭·높이·길이) — 프리뷰 박스용.</summary>
        public readonly Vector3 GhostSize;

        /// <summary>지금 우클릭으로 실제로 지어지는지 — 비용을 치를 수 있고 자리도 비어 있어야 한다.</summary>
        public bool CanBuild => CanAfford && !Occupied;

        public CarBuildAimLocalEvent(bool aiming, int slotIndex, int cost, bool canAfford, bool occupied,
            Vector3 ghostCenter, Vector3 ghostSize)
        {
            Aiming = aiming;
            SlotIndex = slotIndex;
            Cost = cost;
            CanAfford = canAfford;
            Occupied = occupied;
            GhostCenter = ghostCenter;
            GhostSize = ghostSize;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 망치로 이탈 칸의 재결합 지점(앞 연결 지점)을 겨눈 상태 (손잡이-이탈저항 스펙 §4.1).
    /// HUD 안내와 연결부 자리 테두리 프리뷰(<see cref="CarBuildGhostView"/>)가 그린다.
    /// 칸 건설(<see cref="CarBuildAimLocalEvent"/>)과 필드 의미가 달라 한 타입에 섞지 않는다 —
    /// 조준 성립·대상 칸·안내 상태·남은 거리가 바뀔 때마다 발행된다.
    /// </summary>
    public readonly struct CarRecoupleAimLocalEvent
    {
        public readonly bool Aiming;

        /// <summary>되붙일 칸 — 선두부터 첫 이탈 중(미소실) 칸.</summary>
        public readonly int CarIndex;

        public readonly int Cost;

        /// <summary>지금 무엇이 막고 있는지(또는 붙일 수 있는지) — 안내 문구·테두리 색이 이 하나로 갈린다.</summary>
        public readonly RecouplePrompt Prompt;

        /// <summary>슬롯까지 남은 거리(m) — "N m 남음" 안내용. 곧 이탈 오프셋이다.</summary>
        public readonly float RemainingMeters;

        /// <summary>이어질 연결부 자리의 월드 중심 — 프리뷰 박스용. 슬롯 기준 고정 좌표라 칸이 멀어도 그대로다.</summary>
        public readonly Vector3 GhostCenter;

        /// <summary>이어질 연결부 자리의 크기(폭·높이·연결 간격) — 프리뷰 박스용.</summary>
        public readonly Vector3 GhostSize;

        /// <summary>지금 우클릭으로 실제로 붙는지.</summary>
        public bool CanRecouple => Prompt == RecouplePrompt.Ready;

        public CarRecoupleAimLocalEvent(bool aiming, int carIndex, int cost, RecouplePrompt prompt,
            float remainingMeters, Vector3 ghostCenter, Vector3 ghostSize)
        {
            Aiming = aiming;
            CarIndex = carIndex;
            Cost = cost;
            Prompt = prompt;
            RemainingMeters = remainingMeters;
            GhostCenter = ghostCenter;
            GhostSize = ghostSize;
        }
    }
}
