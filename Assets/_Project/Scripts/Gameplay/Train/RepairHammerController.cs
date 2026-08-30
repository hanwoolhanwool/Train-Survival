using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Game.Gameplay.Inventory;
using Game.Gameplay.Monsters;
using Game.Gameplay.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 수리 망치 (기획서 §9 — 수리 망치로 수리. §M3). 좌클릭 홀드 = 겨눈 부위 수리,
    /// 우클릭 = 겨눈 칸 갑판의 <b>그리드 셀</b>에 건축물 설치 (건축 개편 1차 — 계획서 §2.4:
    /// hit 지점을 셀로 스냅, Q/E 90° 회전, R 키로 설치 가능 종류 순환, 자원 소모),
    /// 건설 지점(재건 슬롯·후미 연결부)을 겨누면 우클릭 = 칸 건설(M3 피드백 — 건설 포트의 망치 통합),
    /// 슬롯까지 끌어온 이탈 칸의 앞 연결 지점을 겨누면 우클릭 = 재결합(손잡이-이탈저항 스펙 §4.1).
    /// 파이프라인은 리볼버와 동일 구조: 소유자 로컬 레이캐스트로 부위(칸·연결부·건축물)를 식별해
    /// 호스트에 보고 → 호스트가 거리 재검증 후 같은 순수 판정으로 확정 → 상태 복제로 전 피어 반영.
    /// 겨눈 부위와 체력은 <see cref="HammerTargetLocalEvent"/>로, 설치 자리는
    /// <see cref="StructurePlaceAimLocalEvent"/>로 발행해 조준 HUD·프리뷰가 그린다.
    /// 열차 부위는 NetworkObject가 아니므로 (부위 종류, 인덱스 — 건축물은 항목 Id)로 식별한다. Player 프리팹에 부착한다.
    /// </summary>
    public sealed class RepairHammerController : NetworkBehaviour
    {
        [SerializeField] private RepairHammerSettings _settings;
        [SerializeField] private Transform _aimSource;
        [SerializeField] private TrainLayoutSettings _layoutSettings;
        [SerializeField] private StructureCatalog _structureCatalog;

        [Tooltip("칸 건설·재결합 지점을 '겨눴다'고 볼 시선 정렬 하한 (조준 방향·지점 방향 내적).")]
        [SerializeField, Range(0f, 1f)] private float _buildLookDotThreshold = 0.85f;

        [Tooltip("건설·재결합 지점 높이(Y) — 연결부 갑판 근처로 맞춘다.")]
        [SerializeField] private float _buildAnchorHeight = 2.5f;

        private float _nextSwingTime;

        // 마지막으로 HUD에 알린 조준 상태 — 바뀔 때만 다시 발행한다.
        private bool _sentHasTarget;
        private TrainPartKind _sentKind;
        private int _sentIndex;
        private StructureKind _sentTargetStructureKind;
        private float _sentHealth;
        private bool _sentCanRepair;
        private bool _sentCanBuild;
        private bool _sentAfford;
        private StructureKind _sentStructureKind;
        private bool _sentCanDemolish;
        private int _sentDemolishRefund;
        private float _sentDemolishProgress;

        // 설치할 건축물 종류 — R 키 순환으로 고르는 로컬 선택 (설치 가능 종류만 — 돔 제외). 확정은 RPC 페이로드로 보낸다.
        private StructureKind _selectedStructureKind = StructureKind.Dome;
        private bool _selectionInitialized;

        // 설치 프리뷰 회전 (건축 개편 1차 — Q/E, 0~3 × 90°). 종류 변경·망치 해제 시 0으로 리셋한다.
        private int _previewRotation;

        // 가변 크기 드래그 앵커 (천막 계획 결정 ②) — 첫 우클릭이 시작 셀을 잡고 두 번째가 확정한다.
        // -1 = 앵커 없음. 종류를 바꾸거나 갑판에서 조준이 벗어나면 취소한다.
        private int _dragAnchorCar = -1;
        private int _dragAnchorX;
        private int _dragAnchorZ;

        // 드래그를 칸별로 자른 조각 버퍼 (서버 전용) — 설치 확정마다 새로 채워 쓴다.
        private List<ResizablePlacementLogic.Span> _dragSpans;

        // 같은 조각 계산의 소유자 전용 버퍼 (프리뷰) — 서버 버퍼와 섞이지 않게 따로 둔다.
        private List<ResizablePlacementLogic.Span> _previewSpans;

        // X 홀드 철거 게이지 (건축 개편 2·3차 — 결정 ④). 건축물과 판자가 같은 규약을 쓰되 표적이
        // 달라 게이지만 둘로 나눈다 (상태 기계는 HoldGauge 하나).
        private HoldGauge _demolishHold;
        private HoldGauge _plankHold;

        // 마지막으로 알린 판자 조준 상태 — 바뀔 때만 다시 발행한다.
        private PlankAimLocalEvent _sentPlankAim;

        // 게이지 진행도는 매 프레임 미세하게 변하므로, 이 이하 변화로는 HUD 이벤트를 다시 발행하지 않는다.
        private const float DemolishProgressStep = 0.02f;

        // 판자 조준(갑판 평면 교차)에서 "앞이 가려졌다"고 볼 여유(m) — 이미 깔린 판자 자신의 두께,
        // 갑판 상면과의 미세한 높이 차로 조준이 끊기지 않게 한다.
        private const float PlankOcclusionTolerance = 0.5f;

        // 마지막으로 알린 건축물 설치 조준 상태 — 바뀔 때만 다시 발행한다.
        private StructurePlaceAimLocalEvent _sentPlaceAim;

        // 마지막으로 알린 칸 건설 조준 상태 — 바뀔 때만 다시 발행한다.
        private bool _sentBuildAiming;
        private int _sentBuildSlot = -1;
        private bool _sentBuildAfford;
        private bool _sentBuildOccupied;

        // 마지막으로 알린 재결합 조준 상태 — 바뀔 때만 다시 발행한다.
        private bool _sentRecoupleAiming;
        private int _sentRecoupleCar = -1;
        private RecouplePrompt _sentRecouplePrompt;
        private float _sentRecoupleRemaining;

        // 남은 거리 안내는 소수 한 자리로 나오므로, 이보다 작은 변화로는 다시 발행하지 않는다.
        private const float RecoupleRemainingStep = 0.1f;

        // 자리 점유 판정용 재사용 버퍼 — 매 프레임 도는 조준 경로라 할당을 만들지 않는다.
        // 넘치면 초과분이 조용히 버려져 사람을 놓칠 수 있으므로, 지형·자원까지 겹쳐도 남을 만큼 넉넉히 잡는다.
        private readonly Collider[] _occupancyBuffer = new Collider[32];

        private PlayerHealth _health;

        // 자원 인벤토리 — 조준 경로가 매 프레임 묻는 값이라 캐시한다(같은 오브젝트에 고정).
        private IResourceInventory _inventory;

        /// <summary>도구 슬롯 활성 여부 — <see cref="Game.Gameplay.Inventory.HotbarController"/>가 제어한다. 소유자 입력 게이트.</summary>
        public bool InputEnabled { get; set; }

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _inventory = GetComponent<IResourceInventory>();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || _settings == null)
            {
                return;
            }

            // 사망~부활 사이에는 수리·설치·건설 입력을 닫는다 (M5 3차 발견 버그 — 사망 중 건설 가능).
            if (!InputEnabled || (_health != null && !_health.IsAlive))
            {
                _previewRotation = 0;
                _demolishHold.Reset();
                _plankHold.Reset();
                PublishNoTarget();
                PublishNoBuildAim();
                PublishNoRecoupleAim();
                PublishNoPlaceAim();
                PublishNoPlankAim();
                return;
            }

            UpdateAimAndInput();
        }

        // ── 소유자: 조준 판정·입력 계층 ────────────────────

        private void UpdateAimAndInput()
        {
            Vector3 origin = _aimSource != null ? _aimSource.position : transform.position;
            Vector3 forward = _aimSource != null ? _aimSource.forward : transform.forward;

            bool hasRayHit = TryRaycastHit(origin, forward, out RaycastHit hit);
            float blockingDistance = hasRayHit ? hit.distance : float.PositiveInfinity;

            // 재결합 조준이 성립하면 다른 조준을 전부 덮는다 — 우클릭 의미(재결합 vs 칸 건설 vs 돔 설치)가
            // 겹쳐 보이지 않게 한다. 이탈 칸이 점유한 자리는 건설 후보에서 빠지므로 둘은 원래 상호 배타지만,
            // 앞 칸이 파괴돼 건설 지점이 따로 잡히는 경우를 대비해 우선순위를 고정해 둔다.
            if (TryGetRecoupleAim(origin, forward, blockingDistance,
                out int recoupleCar, out int recoupleCost, out RecouplePrompt recouplePrompt, out float remaining))
            {
                PublishRecoupleAim(true, recoupleCar, recoupleCost, recouplePrompt, remaining);
                PublishNoBuildAim();
                PublishNoPlaceAim();
                PublishNoTarget();

                Mouse recoupleMouse = Mouse.current;
                if (recoupleMouse != null && recoupleMouse.rightButton.wasPressedThisFrame
                    && recouplePrompt == RecouplePrompt.Ready)
                {
                    RequestRecoupleServerRpc(recoupleCar);
                }

                return;
            }

            PublishNoRecoupleAim();

            // 건설 조준이 성립하면 부위 조준을 덮는다 — 우클릭 의미(칸 건설 vs 돔 설치)가 겹치지 않게 한다.
            if (TryGetBuildAim(origin, forward, blockingDistance,
                out int buildSlot, out int buildCost, out bool buildAfford, out bool buildOccupied))
            {
                PublishBuildAim(true, buildSlot, buildCost, buildAfford, buildOccupied);
                PublishNoPlaceAim();
                PublishNoTarget();

                Mouse buildMouse = Mouse.current;
                if (buildMouse != null && buildMouse.rightButton.wasPressedThisFrame
                    && buildAfford && !buildOccupied)
                {
                    RequestBuildCarServerRpc();
                }

                return;
            }

            PublishNoBuildAim();

            // 부위 식별을 먼저 한다 — 판자 조준이 "지금 건축물을 겨누는 중인가"를 알아야 하고,
            // 아래 부위 조준도 같은 결과를 쓴다 (조준 대상 해석은 이 한 지점뿐).
            bool hasHit = hasRayHit;
            TrainPartKind kind = default;
            int index = -1;
            if (hasHit)
            {
                hasHit = TryResolvePart(hit, out kind, out index);
            }

            // 판자 조준 (건축 개편 3차 — 계획서 §2.9): 갑판 높이 평면을 연장해 칸 옆 판자 열을 겨눈다.
            // 아직 판자가 없는 빈 자리는 콜라이더가 없어 레이캐스트로는 잡히지 않으므로 평면 교차로 판정한다.
            // 빈 자리를 겨눈 동안에는 다른 조준을 덮는다(우클릭 의미가 겹치지 않게) — 이미 깔린 판자 열은
            // 그 위에 건축물을 지을 수 있어야 하므로 조준을 넘기고 X 홀드 철거만 얹는다.
            bool aimingStructure = hasHit && kind == TrainPartKind.Structure;
            if (TryUpdatePlankAim(origin, forward, blockingDistance, aimingStructure, out bool plankEmptySlot)
                && plankEmptySlot)
            {
                _demolishHold.Reset();
                PublishNoPlaceAim();
                PublishNoTarget();
                return;
            }

            if (!hasHit || !ServiceLocator.TryGet(out ITrainState train))
            {
                _demolishHold.Reset();
                PublishNoPlaceAim();
                PublishNoTarget();
                return;
            }

            // 편성에 없는 부위(증설 예비 슬롯의 칸·연결부)는 표적이 아니다 — 짓기 전 부위의 체력이 뜨면 안 된다.
            if (!ReadPartState(train, kind, index, out float health, out float maxHealth,
                out bool canRepair, out bool canDemolish, out StructureKind targetStructureKind))
            {
                _demolishHold.Reset();
                PublishNoPlaceAim();
                PublishNoTarget();
                return;
            }

            // 건축물 설치 조준 (건축 개편 1차) — 칸 갑판을 겨눌 때만 셀 스냅·회전·프리뷰가 성립한다.
            int cellX = 0;
            int cellZ = 0;
            bool placeAfford = false;
            bool placeOccupied = false;
            bool canBuild = kind == TrainPartKind.Car
                && TryUpdatePlacementAim(train, index, hit.point,
                    out cellX, out cellZ, out placeAfford, out placeOccupied);
            if (!canBuild)
            {
                PublishNoPlaceAim();
            }

            bool hasExpansion = ServiceLocator.TryGet(out ITrainExpansion expansion);
            int structureCost = hasExpansion ? expansion.GetStructureBuildCost(_selectedStructureKind) : 0;

            // X 홀드 철거 (건축 개편 2차 — 결정 ④): 게이지 충족 시 RPC를 쏘고 리셋한다.
            canDemolish = canDemolish && hasExpansion;
            int demolishRefund = canDemolish ? expansion.GetStructureDemolishRefund(targetStructureKind) : 0;
            float demolishProgress = UpdateDemolishHold(kind, index, canDemolish);

            PublishTarget(kind, index, targetStructureKind, health, maxHealth, canRepair, canBuild,
                _selectedStructureKind, structureCost, placeAfford,
                canDemolish, demolishRefund, demolishProgress);

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.isPressed && Time.time >= _nextSwingTime)
            {
                _nextSwingTime = Time.time + _settings.HitInterval;
                RequestRepairServerRpc(kind, index, hit.point);
            }

            if (mouse.rightButton.wasPressedThisFrame && canBuild)
            {
                HandleBuildClick(index, cellX, cellZ, placeAfford, placeOccupied);
            }
        }

        /// <summary>
        /// 건축물 설치 조준 갱신 (건축 개편 1차 — 계획서 §2.4) — 갑판 hit 지점을 셀로 스냅하고
        /// R(설치 가능 종류 순환)·Q/E(90° 회전) 입력을 반영해 프리뷰 이벤트를 발행한다.
        /// 반환 = 지금 우클릭으로 설치가 성립하는지(그리드 판정 통과).
        /// </summary>
        private bool TryUpdatePlacementAim(ITrainState train, int carIndex, Vector3 hitPoint,
            out int cellX, out int cellZ, out bool afford, out bool occupied)
        {
            cellX = 0;
            cellZ = 0;
            afford = false;
            occupied = false;

            if (_layoutSettings == null || _structureCatalog == null
                || !ServiceLocator.TryGet(out ITrainExpansion expansion))
            {
                return false;
            }

            // 갑판 위를 겨눠야 설치다 — 칸 옆면·하부 조준은 수리 전용 (낙하 판정과 같은 여유 폭).
            if (hitPoint.y < _layoutSettings.DeckHeight - 0.5f)
            {
                return false;
            }

            EnsurePlaceableSelection();

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // 종류 순환 (R) — 설치 가능 종류만 돈다(돔 제외). 망치 활성 중에는 총기 입력이 닫혀
                // 있어 재장전 키와 충돌하지 않는다. 종류가 바뀌면 회전은 0으로 리셋한다.
                if (keyboard.rKey.wasPressedThisFrame)
                {
                    _selectedStructureKind = _structureCatalog.NextPlaceableKind(_selectedStructureKind);
                    _previewRotation = 0;

                    // 종류가 바뀌면 잡아 둔 드래그 시작점은 의미가 없다 (천막 계획 §4.2).
                    ClearDragAnchor();
                }

                // 설치 회전 (Q/E — 90° 단위, 계획서 §0-2). 프리뷰에 즉시 반영된다.
                if (keyboard.qKey.wasPressedThisFrame)
                {
                    _previewRotation = (_previewRotation + 3) & 3;
                }

                if (keyboard.eKey.wasPressedThisFrame)
                {
                    _previewRotation = (_previewRotation + 1) & 3;
                }
            }

            _structureCatalog.GetFootprint(_selectedStructureKind, out int width, out int length);
            StructureGridLogic.RotatedFootprint(width, length, _previewRotation,
                out int rotatedWidth, out int rotatedLength);

            float cellSize = _layoutSettings.StructureCellSize;
            float centerZ = _layoutSettings.CarCenterZ(carIndex, train.GetEjectOffset(carIndex));

            // 유효 열은 그 칸의 판자 증축을 반영한다 (건축 개편 3차) — 판자 위에도 지을 수 있다.
            train.TryGetCar(carIndex, out CarState aimedCar);
            if (!StructureGridLogic.TryWorldToPlacementCell(hitPoint.x, hitPoint.z, centerZ,
                _layoutSettings.CarWidth, _layoutSettings.DeckLength, cellSize,
                rotatedWidth, rotatedLength, aimedCar.LeftPlanks, aimedCar.RightPlanks,
                out cellX, out cellZ))
            {
                return false;
            }

            bool resizable = _structureCatalog.IsResizable(_selectedStructureKind);

            // 가변 크기 드래그 중 — 서버와 <b>같은 함수</b>로 칸별 조각을 내고, 그중 지금 겨눈 칸의
            // 조각을 프리뷰 상자로 쓴다 (천막 계획 §4.2·2차). 비용은 여러 칸에 걸쳐도 조각 전체
            // 합계라 확정 금액과 어긋나지 않는다 — 프리뷰와 확정이 갈리지 않는 것이 이 경로의 규약이다.
            int dragCells = 0;
            if (resizable && _dragAnchorCar >= 0)
            {
                dragCells = ResolveDragPreview(train, carIndex, cellSize,
                    ref cellX, ref cellZ, ref rotatedWidth, ref rotatedLength);
            }

            bool canPlace = expansion.CanPlaceStructureSized(carIndex, cellX, cellZ, _previewRotation,
                _selectedStructureKind, rotatedWidth, rotatedLength);

            int cost;
            if (resizable)
            {
                // 아직 시작점을 안 잡았으면 지금 커서 자리의 최소 크기로 미리 보여 준다.
                int cells = dragCells > 0 ? dragCells : rotatedWidth * rotatedLength;
                cost = ResizablePlacementLogic.ResolveCost(cells,
                    _structureCatalog.GetCostPerCell(_selectedStructureKind),
                    expansion.GetStructureBuildCost(_selectedStructureKind));
            }
            else
            {
                cost = expansion.GetStructureBuildCost(_selectedStructureKind);
            }

            afford = CanAfford(cost);

            StructureGhostVolume(cellX, cellZ, rotatedWidth, rotatedLength, centerZ,
                out Vector3 ghostCenter, out Vector3 ghostSize);

            // 자리 점유 판정 — 프리뷰 테두리와 같은 상자다 (칸 건설과 같은 규약: 테두리 안이 비어야 지어진다).
            // 천막은 예외다: 지붕이라 사람·몬스터 위로 덮을 수 있어야 한다 (결정 ⑥ — 점유는 기둥뿐).
            occupied = !resizable && IsVolumeOccupied(ghostCenter, ghostSize);

            PublishPlaceAim(new StructurePlaceAimLocalEvent(true, carIndex, cellX, cellZ, _previewRotation,
                _selectedStructureKind, cost, afford, canPlace, occupied, ghostCenter, ghostSize));

            return canPlace;
        }

        /// <summary>
        /// 판자 조준 갱신 (건축 개편 3차 — 계획서 §2.9). 갑판 높이 평면과 조준 레이의 교차점으로
        /// "어느 칸의 몇 번째 열을 겨누고 있는가"를 구해, 빈 판자 자리면 증축 프리뷰(우클릭)를,
        /// 이미 깔린 판자면 철거 안내(X 홀드)를 발행한다.
        /// 반환 = 판자 조준 성립. <paramref name="emptySlot"/> = 아직 판자가 없는 다음 자리.
        /// </summary>
        private bool TryUpdatePlankAim(Vector3 origin, Vector3 forward, float blockedDistance,
            bool aimingStructure, out bool emptySlot)
        {
            emptySlot = false;

            // 건축물을 겨누는 동안에는 판자 안내를 아예 내지 않는다 — X 홀드의 주인이 하나로 정해져,
            // 프리뷰·HUD가 두 이벤트를 상관시켜 스스로 우선순위를 판단할 필요가 없다 (마무리 패스).
            if (aimingStructure
                || _layoutSettings == null
                || !ServiceLocator.TryGet(out ITrainState train)
                || !ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !PlankAimLogic.TryDeckPlanePoint(origin, forward, _layoutSettings.DeckHeight,
                    _settings.MaxRange, blockedDistance, PlankOcclusionTolerance, out Vector3 point)
                || !train.TryGetCarAtZ(point.z, out int carIndex, out CarState car, out float centerZ)
                || !TrainStateLogic.IsCarPresent(car)
                || !IsWithinRange(point))
            {
                // 이탈 칸 위에서는 안내 자체를 띄우지 않는다 — 건축물 철거(2차)와 같은 게이트다.
                PublishNoPlankAim();
                _plankHold.Reset();
                return false;
            }

            float cellSize = _layoutSettings.StructureCellSize;
            int bodyColumns = StructureGridLogic.BodyColumns(_layoutSettings.CarWidth, cellSize);
            if (!PlankAimLogic.TryResolveColumn(point.x, bodyColumns, cellSize,
                car.LeftPlanks, car.RightPlanks, out PlankSide side, out emptySlot, out int previewColumn))
            {
                // 칸 본체 열이거나 예약 범위 밖 허공 — 판자 조준이 아니다.
                PublishNoPlankAim();
                _plankHold.Reset();
                return false;
            }

            int cost = expansion.PlankBuildCost;
            bool afford = CanAfford(cost);
            bool canBuild = emptySlot && afford && expansion.CanBuildPlank(carIndex, side);
            bool canRemove = !emptySlot && expansion.CanRemovePlank(carIndex, side);
            float removeProgress = _plankHold.Update(
                canRemove && IsHoldingDemolishKey(), PlankHoldKey(carIndex, side),
                _settings.DemolishHoldSeconds, out bool removeCompleted);
            if (removeCompleted)
            {
                RequestRemovePlankServerRpc(carIndex, side);
            }

            PlankAimLogic.ColumnVolume(previewColumn, bodyColumns,
                StructureGridLogic.Rows(_layoutSettings.DeckLength, cellSize),
                centerZ, cellSize, _layoutSettings.DeckHeight, _settings.GhostHeight,
                out Vector3 ghostCenter, out Vector3 ghostSize);

            PublishPlankAim(new PlankAimLocalEvent(true, carIndex, side, emptySlot, cost, afford, canBuild,
                expansion.PlankDemolishRefund, canRemove, removeProgress,
                expansion.PlankRefundResource, ghostCenter, ghostSize));

            Mouse mouse = Mouse.current;
            if (canBuild && mouse != null && mouse.rightButton.wasPressedThisFrame)
            {
                RequestBuildPlankServerRpc(carIndex, side);
            }

            return true;
        }

        /// <summary>
        /// 건축물 점유 영역의 프리뷰·점유 판정 상자 — 소유자 프리뷰와 호스트 재검증이 이 한 함수를 쓴다
        /// (<see cref="CarBuildAimLogic.BuildVolume"/>이 칸 건설에서 맡는 역할과 같다).
        /// </summary>
        private void StructureGhostVolume(int cellX, int cellZ, int rotatedWidth, int rotatedLength,
            float carCenterZ, out Vector3 center, out Vector3 size)
        {
            float cellSize = _layoutSettings.StructureCellSize;
            StructureGridLogic.CellRegionCenterWorld(cellX, cellZ, rotatedWidth, rotatedLength,
                carCenterZ, _layoutSettings.CarWidth, _layoutSettings.DeckLength, cellSize,
                out float worldX, out float worldZ);

            float ghostHeight = _settings.GhostHeight;
            center = new Vector3(worldX, _layoutSettings.DeckHeight + ghostHeight * 0.5f, worldZ);
            size = new Vector3(rotatedWidth * cellSize, ghostHeight, rotatedLength * cellSize);
        }

        /// <summary>비용을 지금 낼 수 있는지 — 조준 안내 네 곳이 같은 판정을 쓴다.</summary>
        private bool CanAfford(int cost)
        {
            return _inventory != null && _inventory.Count >= cost;
        }

        /// <summary>판자 홀드 게이지의 표적 토큰 — 칸과 쪽이 바뀌면 게이지가 리셋된다.</summary>
        private static int PlankHoldKey(int carIndex, PlankSide side)
        {
            return (carIndex * 2) + (int)side;
        }

        private static bool IsHoldingDemolishKey()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.xKey.isPressed;
        }

        /// <summary>
        /// 건축물 철거 X 홀드 게이지 (건축 개편 2차 — 결정 ④: 짧은 홀드 + 게이지로 오철거 방지).
        /// 상태 기계는 <see cref="HoldGauge"/>가 들고, 여기서는 표적·완료 처리만 한다. 반환 = 진행도 0~1.
        /// </summary>
        private float UpdateDemolishHold(TrainPartKind kind, int index, bool canDemolish)
        {
            bool holding = canDemolish && kind == TrainPartKind.Structure && IsHoldingDemolishKey();
            float progress = _demolishHold.Update(holding, index, _settings.DemolishHoldSeconds,
                out bool completed);
            if (completed)
            {
                RequestDemolishStructureServerRpc(index);
            }

            return progress;
        }

        /// <summary>초기 선택(돔)이 설치 불가가 된 개편 이후를 흡수한다 — 첫 조준 때 설치 가능 종류로 옮긴다.</summary>
        private void EnsurePlaceableSelection()
        {
            if (!_selectionInitialized)
            {
                _selectionInitialized = true;
                if (!_structureCatalog.IsPlaceable(_selectedStructureKind))
                {
                    _selectedStructureKind = _structureCatalog.NextPlaceableKind(_selectedStructureKind);
                }
            }
        }

        /// <summary>칸 건설 조준 판정 — 다음 건설 슬롯의 연결 지점을 사거리 안에서 겨누고 있는지.</summary>
        private bool TryGetBuildAim(Vector3 origin, Vector3 forward, float blockingDistance,
            out int slot, out int cost, out bool afford, out bool occupied)
        {
            slot = -1;
            cost = 0;
            afford = false;
            occupied = false;

            if (_layoutSettings == null || !ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !expansion.TryGetBuildSlot(out slot))
            {
                return false;
            }

            if (!CarBuildAimLogic.IsAiming(origin, forward, SlotCouplingAnchor(slot),
                _settings.MaxRange, _buildLookDotThreshold, blockingDistance))
            {
                return false;
            }

            cost = expansion.CarBuildCost;
            afford = CanAfford(cost);
            occupied = IsBuildVolumeOccupied(slot);
            return true;
        }

        /// <summary>
        /// 재결합 조준 판정 — 이탈 칸의 앞 연결 지점을 사거리 안에서 겨누고 있는지 (스펙 §4.1).
        /// 확정 조건보다 느슨하게 성립시키고(아직 못 붙이는 이유는 <see cref="RecouplePrompt"/>로 알린다),
        /// 지점이 슬롯 기준 고정 좌표라 칸이 아직 멀리 있어도 겨누는 자리가 도망가지 않는다.
        /// </summary>
        private bool TryGetRecoupleAim(Vector3 origin, Vector3 forward, float blockingDistance,
            out int carIndex, out int cost, out RecouplePrompt prompt, out float remainingMeters)
        {
            carIndex = -1;
            cost = 0;
            prompt = RecouplePrompt.None;
            remainingMeters = 0f;

            if (_layoutSettings == null
                || !ServiceLocator.TryGet(out ITrainRecouple recouple)
                || !ServiceLocator.TryGet(out ITrainState train)
                || !recouple.TryGetRecoupleTarget(out carIndex))
            {
                return false;
            }

            if (!CarBuildAimLogic.IsAiming(origin, forward, SlotCouplingAnchor(carIndex),
                _settings.MaxRange, _buildLookDotThreshold, blockingDistance))
            {
                return false;
            }

            cost = recouple.RecoupleCost;
            remainingMeters = train.GetEjectOffset(carIndex);

            bool frontPresent = train.TryGetCar(carIndex - 1, out CarState front)
                && TrainStateLogic.IsCarPresent(front);
            bool afford = CanAfford(cost);

            prompt = CarRecoupleAimLogic.ResolvePrompt(frontPresent, remainingMeters, afford);
            return true;
        }

        /// <summary>
        /// 슬롯의 앞 연결부 중앙 — 칸 건설과 재결합이 공유하는 조준·거리 검증 기준점이다
        /// (같은 도구의 같은 조작이므로 겨누는 자리를 하나로 맞춘다).
        /// </summary>
        private Vector3 SlotCouplingAnchor(int slot)
        {
            float z = CarBuildAimLogic.AnchorZ(
                _layoutSettings.CarCenterZ(slot), _layoutSettings.CarLength, _layoutSettings.CouplingGap);
            return new Vector3(0f, _buildAnchorHeight, z);
        }

        /// <summary>
        /// 지어질 자리에 플레이어·몬스터가 들어와 있는지 — 새 칸이 사람·몬스터를 안에 가두지 않도록 막는다.
        /// 판정 영역은 프리뷰 테두리와 같은 상자다(초록 테두리 안이 비어야 지어진다).
        /// 소유자 조준(안내·프리뷰)과 호스트 확정이 같은 함수를 쓴다 — 판정이 갈리지 않는다.
        /// </summary>
        private bool IsBuildVolumeOccupied(int slot)
        {
            CarBuildAimLogic.BuildVolume(_layoutSettings.CarCenterZ(slot), _layoutSettings.CarWidth,
                _layoutSettings.DeckHeight, _layoutSettings.CarBodyHeight, _layoutSettings.CarLength,
                out Vector3 center, out Vector3 size);

            return IsVolumeOccupied(center, size);
        }

        /// <summary>
        /// 상자 부피 안에 플레이어·몬스터가 들어와 있는지 — 칸 건설·건축물 설치가 같은 판정을 쓴다
        /// (초록 테두리 안이 비어야 지어진다). 소유자 조준(안내·프리뷰)과 호스트 확정이 같은 함수를 쓴다.
        /// </summary>
        private bool IsVolumeOccupied(Vector3 center, Vector3 size)
        {
            int count = Physics.OverlapBoxNonAlloc(center, size * 0.5f, _occupancyBuffer,
                Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = _occupancyBuffer[i];
                if (hit == null)
                {
                    continue;
                }

                // 플레이어는 CharacterController, 몬스터는 CapsuleCollider가 각자 루트에 붙어 있다.
                if (hit.GetComponentInParent<NetworkPlayerController>() != null
                    || hit.GetComponentInParent<MonsterAgent>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>맞은 콜라이더에서 열차 부위를 식별한다 — 건축물은 칸의 자식이라 먼저 검사한다.
        /// 건축물의 식별자는 그리드 항목 Id다 (건축 개편 1차).</summary>
        private static bool TryResolvePart(RaycastHit hit, out TrainPartKind kind, out int index)
        {
            StructureView structure = hit.collider.GetComponentInParent<StructureView>();
            if (structure != null)
            {
                kind = TrainPartKind.Structure;
                index = structure.StructureId;
                return true;
            }

            CouplingPart coupling = hit.collider.GetComponentInParent<CouplingPart>();
            if (coupling != null)
            {
                kind = TrainPartKind.Coupling;
                index = coupling.CouplingIndex;
                return true;
            }

            CarView car = hit.collider.GetComponentInParent<CarView>();
            if (car != null)
            {
                kind = TrainPartKind.Car;
                index = car.CarIndex;
                return true;
            }

            kind = default;
            index = -1;
            return false;
        }

        /// <summary>
        /// 복제 상태에서 부위 체력·수리 가능 여부를 읽는다(전 피어 동일 판정 — HUD 표시용).
        /// 그 부위가 편성에 존재할 때만 true — 아직 짓지 않은 예비 슬롯의 칸·연결부는 false다.
        /// </summary>
        private static bool ReadPartState(ITrainState train, TrainPartKind kind, int index,
            out float health, out float maxHealth, out bool canRepair, out bool canDemolish,
            out StructureKind targetStructureKind)
        {
            health = 0f;
            maxHealth = 0f;
            canRepair = false;
            canDemolish = false;
            targetStructureKind = default;

            switch (kind)
            {
                case TrainPartKind.Car:
                    if (train.TryGetCar(index, out CarState car))
                    {
                        health = car.Health;
                        maxHealth = car.MaxHealth;
                        canRepair = TrainStateLogic.IsCarPresent(car)
                            && TrainStateLogic.IsDestructible(car.Type)
                            && car.Health < car.MaxHealth;
                        return true;
                    }

                    return false;

                case TrainPartKind.Coupling:
                    if (train.TryGetCoupling(index, out CouplingState coupling))
                    {
                        health = coupling.Health;
                        maxHealth = coupling.MaxHealth;
                        canRepair = !coupling.Broken && coupling.Health < coupling.MaxHealth
                            && train.TryGetCar(index, out CarState front) && TrainStateLogic.IsCarPresent(front)
                            && train.TryGetCar(index + 1, out CarState rear) && TrainStateLogic.IsCarPresent(rear);
                        return true;
                    }

                    return false;

                case TrainPartKind.Structure:
                    // index = 그리드 항목 Id (건축 개편 1차) — 파괴되면 항목이 사라져 자연히 false다.
                    if (train.TryGetStructureById(index, out StructureEntry entry))
                    {
                        health = entry.Health;
                        maxHealth = entry.MaxHealth;
                        targetStructureKind = entry.Kind;
                        bool carPresent = train.TryGetCar(entry.CarIndex, out CarState owner)
                            && TrainStateLogic.IsCarPresent(owner);
                        canRepair = StructureGridLogic.IsAlive(entry)
                            && entry.Health < entry.MaxHealth && carPresent;

                        // 철거 게이트 (건축 개편 2차) — 피해 규칙과 같이 이탈 칸 위는 불가.
                        canDemolish = StructureGridLogic.IsAlive(entry) && carPresent;
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        private bool TryRaycastHit(Vector3 origin, Vector3 direction, out RaycastHit hit)
        {
            return WeaponRaycast.TryGetClosestHit(
                origin, direction, _settings.MaxRange, transform.root, out hit);
        }

        // ── 조준 HUD 이벤트 — 바뀔 때만 발행 ────────────────────

        private void PublishNoTarget()
        {
            if (!_sentHasTarget)
            {
                return;
            }

            _sentHasTarget = false;
            EventBus<HammerTargetLocalEvent>.Publish(new HammerTargetLocalEvent(
                false, default, -1, default, 0f, 0f, false, false, default, 0, false,
                false, 0, 0f));
        }

        private void PublishTarget(TrainPartKind kind, int index, StructureKind targetStructureKind,
            float health, float maxHealth,
            bool canRepair, bool canBuild, StructureKind structureKind, int structureCost, bool afford,
            bool canDemolish, int demolishRefund, float demolishProgress)
        {
            bool unchanged = _sentHasTarget && _sentKind == kind && _sentIndex == index
                && _sentTargetStructureKind == targetStructureKind
                && Mathf.Approximately(_sentHealth, health)
                && _sentCanRepair == canRepair && _sentCanBuild == canBuild && _sentAfford == afford
                && _sentStructureKind == structureKind
                && _sentCanDemolish == canDemolish && _sentDemolishRefund == demolishRefund
                && Mathf.Abs(_sentDemolishProgress - demolishProgress) < DemolishProgressStep;
            if (unchanged)
            {
                return;
            }

            _sentHasTarget = true;
            _sentKind = kind;
            _sentIndex = index;
            _sentTargetStructureKind = targetStructureKind;
            _sentHealth = health;
            _sentCanRepair = canRepair;
            _sentCanBuild = canBuild;
            _sentAfford = afford;
            _sentStructureKind = structureKind;
            _sentCanDemolish = canDemolish;
            _sentDemolishRefund = demolishRefund;
            _sentDemolishProgress = demolishProgress;
            EventBus<HammerTargetLocalEvent>.Publish(new HammerTargetLocalEvent(
                true, kind, index, targetStructureKind, health, maxHealth, canRepair, canBuild,
                structureKind, structureCost, afford, canDemolish, demolishRefund, demolishProgress));
        }

        /// <summary>설치 조준 프리뷰 발행 — 셀·회전·판정이 바뀔 때만 다시 발행한다.</summary>
        private void PublishPlaceAim(StructurePlaceAimLocalEvent evt)
        {
            bool unchanged = _sentPlaceAim.Aiming == evt.Aiming
                && _sentPlaceAim.CarIndex == evt.CarIndex
                && _sentPlaceAim.CellX == evt.CellX && _sentPlaceAim.CellZ == evt.CellZ
                && _sentPlaceAim.Rotation == evt.Rotation && _sentPlaceAim.Kind == evt.Kind
                && _sentPlaceAim.CanAfford == evt.CanAfford && _sentPlaceAim.CanPlace == evt.CanPlace
                && _sentPlaceAim.Occupied == evt.Occupied
                && _sentPlaceAim.GhostCenter == evt.GhostCenter;
            if (unchanged)
            {
                return;
            }

            _sentPlaceAim = evt;
            EventBus<StructurePlaceAimLocalEvent>.Publish(evt);
        }

        private void PublishNoPlaceAim()
        {
            if (_sentPlaceAim.Aiming)
            {
                PublishPlaceAim(new StructurePlaceAimLocalEvent(
                    false, -1, 0, 0, 0, default, 0, false, false, false, default, default));
            }
        }

        /// <summary>판자 조준 프리뷰 발행 (건축 개편 3차) — 겨눈 열·판정·게이지가 바뀔 때만 다시 발행한다.</summary>
        private void PublishPlankAim(PlankAimLocalEvent evt)
        {
            bool unchanged = _sentPlankAim.Aiming == evt.Aiming
                && _sentPlankAim.CarIndex == evt.CarIndex
                && _sentPlankAim.Side == evt.Side
                && _sentPlankAim.CanAfford == evt.CanAfford
                && _sentPlankAim.CanBuild == evt.CanBuild
                && _sentPlankAim.CanRemove == evt.CanRemove
                && Mathf.Abs(_sentPlankAim.RemoveProgress - evt.RemoveProgress) < DemolishProgressStep
                && _sentPlankAim.GhostCenter == evt.GhostCenter;
            if (unchanged)
            {
                return;
            }

            _sentPlankAim = evt;
            EventBus<PlankAimLocalEvent>.Publish(evt);
        }

        private void PublishNoPlankAim()
        {
            if (_sentPlankAim.Aiming)
            {
                PublishPlankAim(new PlankAimLocalEvent(
                    false, -1, default, false, 0, false, false, 0, false, 0f, default, default, default));
            }
        }

        private void PublishNoBuildAim()
        {
            PublishBuildAim(false, -1, 0, false, false);
        }

        private void PublishNoRecoupleAim()
        {
            PublishRecoupleAim(false, -1, 0, RecouplePrompt.None, 0f);
        }

        private void PublishBuildAim(bool aiming, int slot, int cost, bool afford, bool occupied)
        {
            if (_sentBuildAiming == aiming && _sentBuildSlot == slot
                && _sentBuildAfford == afford && _sentBuildOccupied == occupied)
            {
                return;
            }

            _sentBuildAiming = aiming;
            _sentBuildSlot = slot;
            _sentBuildAfford = afford;
            _sentBuildOccupied = occupied;

            Vector3 ghostCenter = default;
            Vector3 ghostSize = default;
            if (aiming && _layoutSettings != null)
            {
                CarBuildAimLogic.BuildVolume(_layoutSettings.CarCenterZ(slot), _layoutSettings.CarWidth,
                    _layoutSettings.DeckHeight, _layoutSettings.CarBodyHeight, _layoutSettings.CarLength,
                    out ghostCenter, out ghostSize);
            }

            EventBus<CarBuildAimLocalEvent>.Publish(new CarBuildAimLocalEvent(
                aiming, slot, cost, afford, occupied, ghostCenter, ghostSize));
        }

        private void PublishRecoupleAim(bool aiming, int carIndex, int cost,
            RecouplePrompt prompt, float remainingMeters)
        {
            bool unchanged = _sentRecoupleAiming == aiming && _sentRecoupleCar == carIndex
                && _sentRecouplePrompt == prompt
                && Mathf.Abs(_sentRecoupleRemaining - remainingMeters) < RecoupleRemainingStep;
            if (unchanged)
            {
                return;
            }

            _sentRecoupleAiming = aiming;
            _sentRecoupleCar = carIndex;
            _sentRecouplePrompt = prompt;
            _sentRecoupleRemaining = remainingMeters;

            Vector3 ghostCenter = default;
            Vector3 ghostSize = default;
            if (aiming && _layoutSettings != null)
            {
                CarRecoupleAimLogic.CouplingVolume(_layoutSettings.CarCenterZ(carIndex),
                    _layoutSettings.CarLength, _layoutSettings.CouplingGap,
                    _layoutSettings.CarWidth, _layoutSettings.DeckHeight, _layoutSettings.CarBodyHeight,
                    out ghostCenter, out ghostSize);
            }

            EventBus<CarRecoupleAimLocalEvent>.Publish(new CarRecoupleAimLocalEvent(
                aiming, carIndex, cost, prompt, remainingMeters, ghostCenter, ghostSize));
        }

        // ── 호스트: 권위 계층 (거리 검증·수리·설치 확정) ──────────────────────

        [Rpc(SendTo.Server)]
        private void RequestRepairServerRpc(
            TrainPartKind kind, int index, Vector3 hitPoint, RpcParams rpcParams = default)
        {
            if (_settings == null || !IsSenderAlive() || !IsWithinRange(hitPoint))
            {
                return;
            }

            if (ServiceLocator.TryGet(out ITrainRepairSink sink))
            {
                sink.ServerApplyRepair(kind, index, _settings.RepairPerHit);
            }
        }

        /// <summary>
        /// 건축물 설치 (건축 개편 1차) — 호스트가 생존·사거리를 재검증하고 프리뷰와 같은 순수 판정
        /// (<see cref="ITrainExpansion.CanPlaceStructure"/>)을 다시 통과시킨 뒤
        /// (자원 차감 + 설치)를 원자적으로 확정한다. 설치 실패 시 자원을 되돌린다.
        /// 조작된 셀·종류 값은 그리드 판정·카탈로그 재검증에서 기각된다.
        /// </summary>
        /// <summary>
        /// 설치 우클릭 — 고정 크기 종류는 즉시 확정하고, 가변 크기 종류(천막)는 <b>두 번</b> 받는다
        /// (천막 계획 결정 ②): 첫 클릭이 시작 셀을 잡고 두 번째가 범위를 확정한다.
        /// 앵커 시점에는 크기가 없으므로 비용·점유 판정을 걸지 않는다 — 확정 때 서버가 전부 다시 본다.
        /// </summary>
        private void HandleBuildClick(int carIndex, int cellX, int cellZ, bool afford, bool occupied)
        {
            if (_structureCatalog == null)
            {
                return;
            }

            if (!_structureCatalog.IsResizable(_selectedStructureKind))
            {
                if (afford && !occupied)
                {
                    RequestBuildStructureServerRpc(carIndex, cellX, cellZ, _previewRotation, _selectedStructureKind);
                }

                return;
            }

            if (_dragAnchorCar < 0)
            {
                _dragAnchorCar = carIndex;
                _dragAnchorX = cellX;
                _dragAnchorZ = cellZ;
                return;
            }

            RequestBuildResizableServerRpc(_dragAnchorCar, _dragAnchorX, _dragAnchorZ,
                carIndex, cellX, cellZ, _selectedStructureKind);
            ClearDragAnchor();
        }

        /// <summary>드래그 앵커를 버린다 — 종류 변경·망치 해제·조준 이탈에서 부른다.</summary>
        private void ClearDragAnchor()
        {
            _dragAnchorCar = -1;
        }

        /// <summary>
        /// 드래그 프리뷰 (천막 계획 2차) — 서버 확정과 같은 <see cref="ResizablePlacementLogic.ResolveSpans"/>로
        /// 칸별 조각을 내고, <b>지금 겨눈 칸</b>의 조각을 프리뷰 상자 좌표로 돌려준다.
        /// 반환값은 <b>조각 전체</b>의 셀 수라, 여러 칸에 걸쳐 끌어도 표시 비용이 확정 금액과 같다.
        ///
        /// 겨눈 칸에 조각이 없으면(칸 경계에 한 행만 걸쳐 버려진 경우) 상자를 건드리지 않는다 —
        /// 그 칸에는 실제로 아무것도 서지 않으므로 프리뷰도 그대로 두는 것이 정직하다.
        /// </summary>
        private int ResolveDragPreview(ITrainState train, int carIndex, float cellSize,
            ref int cellX, ref int cellZ, ref int width, ref int length)
        {
            if (_previewSpans == null)
            {
                _previewSpans = new List<ResizablePlacementLogic.Span>();
            }

            int rows = StructureGridLogic.Rows(_layoutSettings.DeckLength, cellSize);
            ResizablePlacementLogic.ResolveSpans(_dragAnchorCar, _dragAnchorX, _dragAnchorZ,
                carIndex, cellX, cellZ, rows, _previewSpans);

            int bodyColumns = StructureGridLogic.BodyColumns(_layoutSettings.CarWidth, cellSize);
            for (int i = 0; i < _previewSpans.Count; i++)
            {
                ResizablePlacementLogic.Span span = _previewSpans[i];
                int left = 0;
                int right = 0;
                if (train.TryGetCar(span.CarIndex, out CarState car))
                {
                    left = StructureGridLogic.ClampPlankColumns(car.LeftPlanks);
                    right = StructureGridLogic.ClampPlankColumns(car.RightPlanks);
                }

                int spanX = span.CellX;
                int spanWidth = span.Width;
                ResizablePlacementLogic.ClampToColumns(ref spanX, ref spanWidth,
                    StructureGridLogic.FirstBodyColumn - left, bodyColumns + left + right);

                span.CellX = spanX;
                span.Width = spanWidth;
                _previewSpans[i] = span;

                if (span.CarIndex == carIndex && spanWidth > 0)
                {
                    cellX = span.CellX;
                    cellZ = span.CellZ;
                    width = span.Width;
                    length = span.Length;
                }
            }

            return ResizablePlacementLogic.TotalCells(_previewSpans);
        }

        /// <summary>
        /// 가변 크기 설치 (천막 계획 §4.2·§4.3) — 드래그 사각형을 칸별 조각으로 잘라 <b>칸마다 한 채</b>를
        /// 세운다. 비용은 조각 합계로 <b>한 번</b> 계산해 원자적으로 지불한다(칸마다 올림하면
        /// 쪼개질수록 비싸진다). 조각별 자리 판정은 고정 크기 경로와 같은 순수 함수를 다시 통과한다.
        ///
        /// 사거리는 <b>확정 지점</b>만 본다 — 열차 전체를 덮는 차양은 반대쪽 끝이 망치 사거리 밖이라
        /// 전 조각을 검사하면 설계가 성립하지 않는다. 조작 방어는 그리드·자원 판정이 맡는다.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestBuildResizableServerRpc(int anchorCar, int anchorX, int anchorZ,
            int cursorCar, int cursorX, int cursorZ, StructureKind structureKind,
            RpcParams rpcParams = default)
        {
            if (_settings == null || _layoutSettings == null || _structureCatalog == null
                || !IsSenderAlive()
                || !_structureCatalog.IsResizable(structureKind)
                || !ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            float cellSize = _layoutSettings.StructureCellSize;
            int rows = StructureGridLogic.Rows(_layoutSettings.DeckLength, cellSize);
            if (_dragSpans == null)
            {
                _dragSpans = new List<ResizablePlacementLogic.Span>();
            }

            ResizablePlacementLogic.ResolveSpans(anchorCar, anchorX, anchorZ,
                cursorCar, cursorX, cursorZ, rows, _dragSpans);
            if (_dragSpans.Count == 0)
            {
                return;
            }

            // 확정 지점 사거리 — 조각 하나하나가 아니라 지금 겨눈 자리 기준이다(위 주석).
            float cursorCenterZ = _layoutSettings.CarCenterZ(cursorCar, train.GetEjectOffset(cursorCar));
            StructureGridLogic.CellRegionCenterWorld(cursorX, cursorZ, 1, 1, cursorCenterZ,
                _layoutSettings.CarWidth, _layoutSettings.DeckLength, cellSize,
                out float cursorWorldX, out float cursorWorldZ);
            if (!IsWithinRange(new Vector3(cursorWorldX, _layoutSettings.DeckHeight, cursorWorldZ)))
            {
                return;
            }

            int totalCells = ClampSpansToGrid(train, expansion, structureKind);
            if (totalCells <= 0)
            {
                return;
            }

            int cost = ResizablePlacementLogic.ResolveCost(totalCells,
                _structureCatalog.GetCostPerCell(structureKind),
                expansion.GetStructureBuildCost(structureKind));

            IResourceInventory inventory = _inventory;
            inventory?.ServerTrySpend(cost, () => BuildResolvedSpans(expansion, structureKind));
        }

        /// <summary>
        /// 조각들을 그 칸의 유효 열로 자르고 설치 불가한 것을 걷어낸 뒤, 남은 셀 수를 돌려준다.
        /// 유효 열은 칸마다 다르다(판자 증축) — 확정 시점의 칸 상태로 다시 잰다.
        /// </summary>
        private int ClampSpansToGrid(ITrainState train, ITrainExpansion expansion, StructureKind structureKind)
        {
            int bodyColumns = StructureGridLogic.BodyColumns(
                _layoutSettings.CarWidth, _layoutSettings.StructureCellSize);

            for (int i = _dragSpans.Count - 1; i >= 0; i--)
            {
                ResizablePlacementLogic.Span span = _dragSpans[i];
                int left = 0;
                int right = 0;
                if (train.TryGetCar(span.CarIndex, out CarState car))
                {
                    left = StructureGridLogic.ClampPlankColumns(car.LeftPlanks);
                    right = StructureGridLogic.ClampPlankColumns(car.RightPlanks);
                }

                int cellX = span.CellX;
                int width = span.Width;
                ResizablePlacementLogic.ClampToColumns(ref cellX, ref width,
                    StructureGridLogic.FirstBodyColumn - left, bodyColumns + left + right);

                span.CellX = cellX;
                span.Width = width;
                if (width <= 0 || !expansion.CanPlaceStructureSized(span.CarIndex, span.CellX, span.CellZ,
                    0, structureKind, span.Width, span.Length))
                {
                    _dragSpans.RemoveAt(i);
                    continue;
                }

                _dragSpans[i] = span;
            }

            return ResizablePlacementLogic.TotalCells(_dragSpans);
        }

        /// <summary>남은 조각을 전부 세운다 — 하나도 못 세우면 false라 자원이 차감되지 않는다.</summary>
        private bool BuildResolvedSpans(ITrainExpansion expansion, StructureKind structureKind)
        {
            bool any = false;
            for (int i = 0; i < _dragSpans.Count; i++)
            {
                ResizablePlacementLogic.Span span = _dragSpans[i];
                any |= expansion.ServerTryBuildStructureSized(span.CarIndex, span.CellX, span.CellZ,
                    0, structureKind, span.Width, span.Length);
            }

            return any;
        }

        [Rpc(SendTo.Server)]
        private void RequestBuildStructureServerRpc(int carIndex, int cellX, int cellZ, int rotation,
            StructureKind structureKind, RpcParams rpcParams = default)
        {
            if (_settings == null || _layoutSettings == null || _structureCatalog == null
                || !IsSenderAlive()
                || !ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !ServiceLocator.TryGet(out ITrainState train)
                || !expansion.CanPlaceStructure(carIndex, cellX, cellZ, rotation, structureKind))
            {
                return;
            }

            // 거리·자리 재검증 — 프리뷰와 <b>같은 함수</b>로 같은 상자를 만든다 (판정이 갈리지 않는다).
            _structureCatalog.GetFootprint(structureKind, out int width, out int length);
            StructureGridLogic.RotatedFootprint(width, length, rotation, out int rotatedWidth, out int rotatedLength);
            float centerZ = _layoutSettings.CarCenterZ(carIndex, train.GetEjectOffset(carIndex));
            StructureGhostVolume(cellX, cellZ, rotatedWidth, rotatedLength, centerZ,
                out Vector3 volumeCenter, out Vector3 volumeSize);

            var groundPoint = new Vector3(volumeCenter.x, _layoutSettings.DeckHeight, volumeCenter.z);
            if (!IsWithinRange(groundPoint) || IsVolumeOccupied(volumeCenter, volumeSize))
            {
                return;
            }

            // 건자재 차감과 건설을 원자적으로 확정한다 — 건설 실패 시 차감도 반영되지 않는다.
            IResourceInventory inventory = _inventory;
            inventory?.ServerTrySpend(expansion.GetStructureBuildCost(structureKind),
                () => expansion.ServerTryBuildStructure(carIndex, cellX, cellZ, rotation, structureKind));
        }

        /// <summary>
        /// 건축물 철거 (건축 개편 2차 — 결정 ④·⑤·⑧): 호스트가 생존·사거리를 재검증하고 철거를
        /// 확정한 뒤 반환을 처리한다 — 반환량 floor(비용 × 비율), 자원 종류는 카탈로그. 1개씩
        /// 가방에 수납하고(부분 수납), 잔여는 보따리 하나로 철거 자리 갑판에 배출한다 (§1.1 —
        /// 가방 여유가 있으면 보따리가 생기지 않는다. 자원 소실 0).
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestDemolishStructureServerRpc(int structureId, RpcParams rpcParams = default)
        {
            if (_settings == null || _layoutSettings == null || _structureCatalog == null
                || !IsSenderAlive()
                || !ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !ServiceLocator.TryGet(out ITrainState train)
                || !train.TryGetStructureCenter(structureId, out Vector3 point))
            {
                return;
            }

            if (!IsWithinRange(point))
            {
                return;
            }

            if (!expansion.ServerTryDemolishStructure(structureId, out StructureEntry removed))
            {
                return;
            }

            // 반환 — 몬스터 파괴 경로(ServerApplyStructureDamage)는 이 코드를 지나지 않는다 (무반환 규칙).
            GrantRefund(expansion.GetStructureDemolishRefund(removed.Kind),
                _structureCatalog.GetRefundResource(removed.Kind), point);
        }

        /// <summary>
        /// 판자 증축 (건축 개편 3차 — 결정 ⑥): 호스트가 생존·사거리를 재검증하고 프리뷰와 같은 순수
        /// 판정(<see cref="ITrainExpansion.CanBuildPlank"/>)을 다시 통과시킨 뒤 (자원 차감 + 증축)을
        /// 원자적으로 확정한다. 조작된 칸·쪽 값은 판정에서 기각된다.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestBuildPlankServerRpc(int carIndex, PlankSide side, RpcParams rpcParams = default)
        {
            if (_settings == null || _layoutSettings == null || !IsSenderAlive()
                || !ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !ServiceLocator.TryGet(out ITrainState train)
                || !expansion.CanBuildPlank(carIndex, side)
                || !IsWithinRange(PlankColumnWorldCenter(train, carIndex, side, nextColumn: true)))
            {
                return;
            }

            IResourceInventory inventory = _inventory;
            inventory?.ServerTrySpend(expansion.PlankBuildCost, () => expansion.ServerTryBuildPlank(carIndex, side));
        }

        /// <summary>
        /// 판자 철거 (건축 개편 3차) — 건축물 철거와 같은 반환 규약: 반환량 floor(판자 비용 × 비율),
        /// 1개씩 가방에 수납하고 잔여는 보따리 하나로 그 자리 갑판에 배출한다.
        /// 그 열 위에 건축물이 있으면 <see cref="ITrainExpansion.CanRemovePlank"/>가 기각한다.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestRemovePlankServerRpc(int carIndex, PlankSide side, RpcParams rpcParams = default)
        {
            if (_settings == null || _layoutSettings == null || !IsSenderAlive()
                || !ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !ServiceLocator.TryGet(out ITrainState train)
                || !expansion.CanRemovePlank(carIndex, side))
            {
                return;
            }

            Vector3 point = PlankColumnWorldCenter(train, carIndex, side, nextColumn: false);
            if (!IsWithinRange(point) || !expansion.ServerTryRemovePlank(carIndex, side))
            {
                return;
            }

            GrantRefund(expansion.PlankDemolishRefund, expansion.PlankRefundResource, point);
        }

        /// <summary>
        /// 판자 열의 월드 중심 — 거리 검증·반환 보따리 스폰이 같은 지점을 쓴다.
        /// <paramref name="nextColumn"/> = 아직 없는 다음 열(증축), false = 가장 바깥 기존 열(철거).
        /// </summary>
        private Vector3 PlankColumnWorldCenter(ITrainState train, int carIndex, PlankSide side, bool nextColumn)
        {
            float cellSize = _layoutSettings.StructureCellSize;
            int bodyColumns = StructureGridLogic.BodyColumns(_layoutSettings.CarWidth, cellSize);
            int columns = train.TryGetCar(carIndex, out CarState car)
                ? StructureGridLogic.ClampPlankColumns(car.Planks(side))
                : 0;

            int ordinal = nextColumn ? columns : columns - 1;
            float worldX = StructureGridLogic.ColumnCenterWorldX(
                StructureGridLogic.PlankColumn(side, Mathf.Max(0, ordinal), bodyColumns), bodyColumns, cellSize);
            float centerZ = _layoutSettings.CarCenterZ(carIndex, train.GetEjectOffset(carIndex));
            return new Vector3(worldX, _layoutSettings.DeckHeight, centerZ);
        }

        /// <summary>
        /// 철거 반환 지급 (건축 개편 2차 §1.1 규약) — 1개씩 가방에 수납(부분 수납)하고, 잔여는
        /// 보따리 하나로 그 자리 갑판에 배출한다. 가방 여유가 있으면 보따리가 생기지 않는다(자원 소실 0).
        /// </summary>
        private void GrantRefund(int refund, ResourceType resource, Vector3 point)
        {
            IResourceInventory inventory = _inventory;

            int accepted = 0;
            while (accepted < refund && inventory != null && inventory.ServerTryAdd(resource, 1))
            {
                accepted++;
            }

            int remainder = refund - accepted;
            if (remainder > 0 && ServiceLocator.TryGet(out World.IStorageBundleSpawner spawner))
            {
                var contents = new[]
                {
                    new HotbarSlotView(HotbarItemType.Resource, remainder, resource),
                };
                spawner.ServerSpawnResting(contents, point);
            }
        }

        /// <summary>
        /// 칸 건설 — 건설 슬롯·지점을 권위 상태로 재계산해 거리 검증 후
        /// (자원 차감 + 건설)을 원자적으로 확정한다. 건설 실패 시 자원을 되돌린다.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestBuildCarServerRpc(RpcParams rpcParams = default)
        {
            if (_settings == null || _layoutSettings == null || !IsSenderAlive()
                || !ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !expansion.TryGetBuildSlot(out int slot)
                || !IsWithinRange(SlotCouplingAnchor(slot))
                || IsBuildVolumeOccupied(slot))
            {
                return;
            }

            IResourceInventory inventory = _inventory;
            inventory?.ServerTrySpend(expansion.CarBuildCost, () => expansion.ServerTryBuildCar());
        }

        /// <summary>
        /// 이탈 칸 재결합 — 대상 칸·조준 지점을 권위 상태로 재계산해 거리 검증 후
        /// (자원 차감 + 재결합)을 원자적으로 확정한다. 슬롯 도달·앞 칸 존재는 호스트가 다시 본다.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestRecoupleServerRpc(int carIndex, RpcParams rpcParams = default)
        {
            if (_settings == null || _layoutSettings == null || !IsSenderAlive()
                || !ServiceLocator.TryGet(out ITrainRecouple recouple)
                || !recouple.TryGetRecoupleTarget(out int target) || target != carIndex
                || !IsWithinRange(SlotCouplingAnchor(target)))
            {
                return;
            }

            IResourceInventory inventory = _inventory;
            inventory?.ServerTrySpend(recouple.RecoupleCost, () => recouple.ServerTryRecouple(target));
        }

        /// <summary>요청자 생존 재검증 — 사망 중 도착한 수리·설치·건설·재결합 요청은 기각한다 (호스트 검증 원칙).</summary>
        private bool IsSenderAlive()
        {
            return _health == null || _health.IsAlive;
        }

        /// <summary>거리 검증 — 요청자(이 오브젝트는 소유자의 플레이어) 위치 기준 사거리 초과 보고는 기각한다.</summary>
        private bool IsWithinRange(Vector3 hitPoint)
        {
            float maxDistance = _settings.MaxRange + _settings.RangeTolerance;
            return (hitPoint - transform.position).sqrMagnitude <= maxDistance * maxDistance;
        }
    }
}
