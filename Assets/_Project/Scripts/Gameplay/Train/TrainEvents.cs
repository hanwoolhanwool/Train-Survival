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
    /// 연료 투입이 성사됨 — 호스트가 인벤토리 차감과 충전을 확정한 뒤 전 피어에 발행되는 연출 전용 이벤트
    /// (화구 연료구 교체 계획 §3.2). 화구 화염이 이를 받아 순간적으로 타오른다.
    /// <para>
    /// 잔량 변화(<see cref="World.FuelChangedEvent"/>)로 대신할 수 없다 — 호스트가 매 프레임 연료를
    /// 깎으므로 한 프레임의 증가가 소모와 상쇄돼 씹히고, 관측 시점도 복제 지연에 흔들린다.
    /// </para>
    /// 게임 상태는 담지 않는다. 이 이벤트를 놓쳐도 연출만 빠질 뿐 연료는 이미 확정돼 있다.
    /// </summary>
    public readonly struct EngineFuelDepositedEvent
    {
        /// <summary>투입한 자원의 발열량 — 클수록 크게 타오른다(통나무 vs 넝마).</summary>
        public readonly float FuelValue;

        public EngineFuelDepositedEvent(float fuelValue)
        {
            FuelValue = fuelValue;
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

    /// <summary>건축물 그리드 리스트 변화의 종류 (건축 개편 1차) — 뷰 스포너·표현이 반응 방식을 고른다.</summary>
    public enum StructureListChange : byte
    {
        /// <summary>항목 추가(설치·후발 복제).</summary>
        Added,

        /// <summary>항목 값 갱신(체력 변화 등).</summary>
        Updated,

        /// <summary>항목 제거(파괴·철거·칸 소멸).</summary>
        Removed,

        /// <summary>목록 전체 재설정 — 구독자는 전체 재구성한다.</summary>
        Reset,
    }

    /// <summary>
    /// 건축물 그리드 항목이 바뀜 (건축 개편 1차) — 호스트 변이가 NetworkList에 반영될 때 모든 피어에서
    /// 발행된다(권위 이벤트). 뷰 스포너가 Added/Removed/Reset으로 실물 스폰·회수를,
    /// 각 StructureView가 Updated로 표현 갱신을 맡는다.
    /// </summary>
    public readonly struct StructureEntryChangedEvent
    {
        public readonly StructureListChange Change;

        /// <summary>대상 항목 — Removed면 제거된 항목의 마지막 값, Reset이면 default.</summary>
        public readonly StructureEntry Entry;

        public StructureEntryChangedEvent(StructureListChange change, StructureEntry entry)
        {
            Change = change;
            Entry = entry;
        }
    }

    /// <summary>
    /// 칸 위 건축물이 철거됨 (건축 개편 2차 — 결정 ④·⑤: 망치 X 홀드, 자원 50% 반환) —
    /// 호스트 확정 후 전 피어에서 발행되는 authored 이벤트. 몬스터 파괴(무반환)와 구분된다.
    /// </summary>
    public readonly struct StructureDemolishedEvent
    {
        public readonly int StructureId;

        public readonly int CarIndex;

        public readonly StructureKind Kind;

        public StructureDemolishedEvent(int structureId, int carIndex, StructureKind kind)
        {
            StructureId = structureId;
            CarIndex = carIndex;
            Kind = kind;
        }
    }

    /// <summary>칸 위 건축물이 파괴됨 — 호스트 확정 후 전 피어에서 발행되는 authored 이벤트(§M3, 기획서 §9).</summary>
    public readonly struct StructureDestroyedEvent
    {
        /// <summary>파괴된 항목의 서버 발급 Id.</summary>
        public readonly int StructureId;

        /// <summary>얹혀 있던 칸 인덱스 — HUD 배너용.</summary>
        public readonly int CarIndex;

        /// <summary>파괴된 건축물 종류 — HUD가 종류명을 표시한다.</summary>
        public readonly StructureKind Kind;

        public StructureDestroyedEvent(int structureId, int carIndex, StructureKind kind)
        {
            StructureId = structureId;
            CarIndex = carIndex;
            Kind = kind;
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

    /// <summary>
    /// 칸 위에 건축물이 설치됨 — 호스트 확정 후 전 피어에서 발행되는 authored 이벤트(§M3).
    /// 페이로드는 확정된 그리드 항목 전체 (건축 개편 1차 — 계획서 §2.4).
    /// </summary>
    public readonly struct StructureBuiltEvent
    {
        public readonly StructureEntry Entry;

        public StructureBuiltEvent(StructureEntry entry)
        {
            Entry = entry;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 공유 창고 실물에 근접해 열 수 있는 상태 (M5 3차, 건축 개편 2차 —
    /// 식별 = 건축물 Id). HUD의 "E — 창고" 안내용.
    /// </summary>
    public readonly struct StoragePromptLocalEvent
    {
        public readonly bool IsInRange;

        /// <summary>대상 창고의 건축물 Id — 범위 밖이면 -1.</summary>
        public readonly int StorageId;

        public StoragePromptLocalEvent(bool isInRange, int storageId)
        {
            IsInRange = isInRange;
            StorageId = storageId;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 공유 창고 창 토글 (M5 3차, 건축 개편 2차 — 식별 = 건축물 Id).
    /// I 창과 같은 규약으로 열려 있는 동안 시점 회전·무기 입력이 정지된다.
    /// </summary>
    public readonly struct StoragePanelToggledLocalEvent
    {
        public readonly bool IsOpen;

        /// <summary>연 창고의 건축물 Id — 닫힘이면 -1.</summary>
        public readonly int StorageId;

        public StoragePanelToggledLocalEvent(bool isOpen, int storageId)
        {
            IsOpen = isOpen;
            StorageId = storageId;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 창고 보따리에 근접해 열 수 있는 상태 (M5 8차). HUD의 "E — 보따리" 안내용.
    /// </summary>
    public readonly struct BundlePromptLocalEvent
    {
        public readonly bool IsInRange;

        public BundlePromptLocalEvent(bool isInRange)
        {
            IsInRange = isInRange;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 보따리 창 토글 (M5 8차). 창고 창과 같은 규약으로
    /// 열려 있는 동안 시점 회전·무기 입력이 정지된다.
    /// </summary>
    public readonly struct BundlePanelToggledLocalEvent
    {
        public readonly bool IsOpen;

        /// <summary>연 보따리의 NetworkObjectId — 닫힘이면 0. UI가 슬롯 조회·전송 요청에 쓴다.</summary>
        public readonly ulong BundleObjectId;

        public BundlePanelToggledLocalEvent(bool isOpen, ulong bundleObjectId)
        {
            IsOpen = isOpen;
            BundleObjectId = bundleObjectId;
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

        /// <summary>부위 식별 — 칸·연결부는 편성 인덱스, 건축물은 그리드 항목 Id (건축 개편 1차).</summary>
        public readonly int Index;

        /// <summary>겨눈 건축물의 종류 — Kind가 Structure일 때만 유효 (HUD가 종류명을 표시한다).</summary>
        public readonly StructureKind TargetStructureKind;

        public readonly float Health;

        public readonly float MaxHealth;

        /// <summary>지금 좌클릭으로 수리 효과가 있는지(손상돼 있고 수리 가능한 부위).</summary>
        public readonly bool CanRepair;

        /// <summary>지금 우클릭으로 이 칸에 건축물을 설치할 수 있는지(칸 부위를 겨눌 때만).</summary>
        public readonly bool CanBuildStructure;

        /// <summary>설치하려는 건축물 종류 — R 키 순환으로 고른 로컬 선택 (M5 3차 종류화).</summary>
        public readonly StructureKind SelectedStructureKind;

        public readonly int StructureCost;

        /// <summary>설치 비용을 지불할 자원이 있는지.</summary>
        public readonly bool CanAffordStructure;

        /// <summary>지금 X 홀드로 철거할 수 있는지 — 건축물 표적 + 칸 생존 (건축 개편 2차, 결정 ④).</summary>
        public readonly bool CanDemolish;

        /// <summary>철거 반환량 — floor(건설 비용 × 반환 비율). HUD "반환: N개" 안내용.</summary>
        public readonly int DemolishRefund;

        /// <summary>X 홀드 게이지 진행도 0~1 — 0이면 홀드 중이 아니다.</summary>
        public readonly float DemolishProgress;

        public HammerTargetLocalEvent(bool hasTarget, TrainPartKind kind, int index,
            StructureKind targetStructureKind,
            float health, float maxHealth, bool canRepair,
            bool canBuildStructure, StructureKind selectedStructureKind,
            int structureCost, bool canAffordStructure,
            bool canDemolish, int demolishRefund, float demolishProgress)
        {
            HasTarget = hasTarget;
            Kind = kind;
            Index = index;
            TargetStructureKind = targetStructureKind;
            Health = health;
            MaxHealth = maxHealth;
            CanRepair = canRepair;
            CanBuildStructure = canBuildStructure;
            SelectedStructureKind = selectedStructureKind;
            StructureCost = structureCost;
            CanAffordStructure = canAffordStructure;
            CanDemolish = canDemolish;
            DemolishRefund = demolishRefund;
            DemolishProgress = demolishProgress;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 망치로 칸 갑판 위 건축물 설치 자리를 겨눈 상태 (건축 개편 1차 — 계획서 §2.4).
    /// 점유 셀 영역 프리뷰(<see cref="CarBuildGhostView"/>)가 초록(가능)/빨강(불가)으로 그린다.
    /// 조준 성립·셀 좌표·회전·판정이 바뀔 때마다 발행된다.
    /// </summary>
    public readonly struct StructurePlaceAimLocalEvent
    {
        public readonly bool Aiming;

        public readonly int CarIndex;

        /// <summary>점유 영역 좌하단 셀 (고정 예약 좌표계 — StructureGridLogic).</summary>
        public readonly int CellX;

        public readonly int CellZ;

        /// <summary>설치 회전 0~3 (Q/E) — 점유 스왑·실물 yaw에 반영된다.</summary>
        public readonly int Rotation;

        /// <summary>설치하려는 종류 (R 순환 선택).</summary>
        public readonly StructureKind Kind;

        public readonly int Cost;

        public readonly bool CanAfford;

        /// <summary>그리드 판정(셀 내부·비점유·칸 생존)을 통과했는지.</summary>
        public readonly bool CanPlace;

        /// <summary>설치 자리에 플레이어·몬스터가 들어와 있는지 — 있으면 그 위에 지을 수 없다 (칸 건설과 같은 규약).</summary>
        public readonly bool Occupied;

        /// <summary>점유 셀 영역의 월드 중심 — 프리뷰 박스용.</summary>
        public readonly UnityEngine.Vector3 GhostCenter;

        /// <summary>점유 셀 영역의 크기(폭·높이·길이) — 프리뷰 박스용.</summary>
        public readonly UnityEngine.Vector3 GhostSize;

        /// <summary>지금 우클릭으로 실제로 지어지는지.</summary>
        public bool CanBuild => CanAfford && CanPlace && !Occupied;

        public StructurePlaceAimLocalEvent(bool aiming, int carIndex, int cellX, int cellZ, int rotation,
            StructureKind kind, int cost, bool canAfford, bool canPlace, bool occupied,
            UnityEngine.Vector3 ghostCenter, UnityEngine.Vector3 ghostSize)
        {
            Aiming = aiming;
            CarIndex = carIndex;
            CellX = cellX;
            CellZ = cellZ;
            Rotation = rotation;
            Kind = kind;
            Cost = cost;
            CanAfford = canAfford;
            CanPlace = canPlace;
            Occupied = occupied;
            GhostCenter = ghostCenter;
            GhostSize = ghostSize;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 망치로 칸 옆면 판자 열을 겨눈 상태 (건축 개편 3차 — 계획서 §2.9).
    /// 조준은 갑판 높이 평면을 연장해 잡으므로, 아직 판자가 없어 콜라이더가 없는 자리도 겨눌 수 있다.
    /// 겨눈 열이 빈 자리면 증축(우클릭), 이미 깔린 판자면 철거(X 홀드) 안내가 된다.
    /// </summary>
    public readonly struct PlankAimLocalEvent
    {
        public readonly bool Aiming;

        public readonly int CarIndex;

        public readonly PlankSide Side;

        /// <summary>아직 판자가 없는 다음 자리인가 — true면 증축 대상, false면 이미 깔린 판자(철거 대상).</summary>
        public readonly bool EmptySlot;

        /// <summary>증축 비용 — 빈 자리를 겨눌 때만 유효.</summary>
        public readonly int Cost;

        public readonly bool CanAfford;

        /// <summary>지금 우클릭으로 판자가 지어지는지 (빈 자리 + 상한 미만 + 자원 충족 + 자리 비었음).</summary>
        public readonly bool CanBuild;

        /// <summary>철거 반환량 — 이미 깔린 판자를 겨눌 때만 유효.</summary>
        public readonly int Refund;

        /// <summary>지금 X 홀드로 뜯을 수 있는지 (그 열 위에 건축물이 없어야 한다).</summary>
        public readonly bool CanRemove;

        /// <summary>X 홀드 게이지 진행도 0~1 — 0이면 홀드 중이 아니다.</summary>
        public readonly float RemoveProgress;

        /// <summary>철거 반환 자원 종류 — HUD가 이름을 붙이는 데 쓴다(게임플레이 서비스를 다시 묻지 않게).</summary>
        public readonly Game.Gameplay.Inventory.ResourceType RefundResource;

        /// <summary>판자 열의 월드 중심 — 프리뷰 박스용.</summary>
        public readonly UnityEngine.Vector3 GhostCenter;

        /// <summary>판자 열의 크기(폭·높이·길이) — 프리뷰 박스용.</summary>
        public readonly UnityEngine.Vector3 GhostSize;

        public PlankAimLocalEvent(bool aiming, int carIndex, PlankSide side, bool emptySlot,
            int cost, bool canAfford, bool canBuild,
            int refund, bool canRemove, float removeProgress,
            Game.Gameplay.Inventory.ResourceType refundResource,
            UnityEngine.Vector3 ghostCenter, UnityEngine.Vector3 ghostSize)
        {
            Aiming = aiming;
            CarIndex = carIndex;
            Side = side;
            EmptySlot = emptySlot;
            Cost = cost;
            CanAfford = canAfford;
            CanBuild = canBuild;
            Refund = refund;
            CanRemove = canRemove;
            RemoveProgress = removeProgress;
            RefundResource = refundResource;
            GhostCenter = ghostCenter;
            GhostSize = ghostSize;
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
