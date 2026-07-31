using Game.Core.Events;
using Game.Core.Services;
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
    /// 우클릭 = 겨눈 칸에 건축물(온실 돔) 설치(자원 소모),
    /// 건설 지점(재건 슬롯·후미 연결부)을 겨누면 우클릭 = 칸 건설(M3 피드백 — 건설 포트의 망치 통합),
    /// 슬롯까지 끌어온 이탈 칸의 앞 연결 지점을 겨누면 우클릭 = 재결합(손잡이-이탈저항 스펙 §4.1).
    /// 파이프라인은 리볼버와 동일 구조: 소유자 로컬 레이캐스트로 부위(칸·연결부·건축물)를 식별해
    /// 호스트에 보고 → 호스트가 거리 재검증 후 수리·설치·건설을 확정 → 상태 복제로 전 피어 반영.
    /// 겨눈 부위와 체력은 <see cref="HammerTargetLocalEvent"/>로 발행해 조준 HUD가 그린다(수리 과정 가시화).
    /// 열차 부위는 NetworkObject가 아니므로 (부위 종류, 인덱스)로 식별한다. Player 프리팹에 부착한다.
    /// </summary>
    public sealed class RepairHammerController : NetworkBehaviour
    {
        [SerializeField] private RepairHammerSettings _settings;
        [SerializeField] private Transform _aimSource;
        [SerializeField] private TrainLayoutSettings _layoutSettings;

        [Tooltip("칸 건설·재결합 지점을 '겨눴다'고 볼 시선 정렬 하한 (조준 방향·지점 방향 내적).")]
        [SerializeField, Range(0f, 1f)] private float _buildLookDotThreshold = 0.85f;

        [Tooltip("건설·재결합 지점 높이(Y) — 연결부 갑판 근처로 맞춘다.")]
        [SerializeField] private float _buildAnchorHeight = 2.5f;

        private float _nextSwingTime;

        // 마지막으로 HUD에 알린 조준 상태 — 바뀔 때만 다시 발행한다.
        private bool _sentHasTarget;
        private TrainPartKind _sentKind;
        private int _sentIndex;
        private float _sentHealth;
        private bool _sentCanRepair;
        private bool _sentCanBuild;
        private bool _sentAfford;

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

        /// <summary>도구 슬롯 활성 여부 — <see cref="Game.Gameplay.Inventory.HotbarController"/>가 제어한다. 소유자 입력 게이트.</summary>
        public bool InputEnabled { get; set; }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || _settings == null)
            {
                return;
            }

            if (!InputEnabled)
            {
                PublishNoTarget();
                PublishBuildAim(false, -1, 0, false, false);
                PublishRecoupleAim(false, -1, 0, RecouplePrompt.None, 0f);
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
                PublishBuildAim(false, -1, 0, false, false);
                PublishNoTarget();

                Mouse recoupleMouse = Mouse.current;
                if (recoupleMouse != null && recoupleMouse.rightButton.wasPressedThisFrame
                    && recouplePrompt == RecouplePrompt.Ready)
                {
                    RequestRecoupleServerRpc(recoupleCar);
                }

                return;
            }

            PublishRecoupleAim(false, -1, 0, RecouplePrompt.None, 0f);

            // 건설 조준이 성립하면 부위 조준을 덮는다 — 우클릭 의미(칸 건설 vs 돔 설치)가 겹치지 않게 한다.
            if (TryGetBuildAim(origin, forward, blockingDistance,
                out int buildSlot, out int buildCost, out bool buildAfford, out bool buildOccupied))
            {
                PublishBuildAim(true, buildSlot, buildCost, buildAfford, buildOccupied);
                PublishNoTarget();

                Mouse buildMouse = Mouse.current;
                if (buildMouse != null && buildMouse.rightButton.wasPressedThisFrame
                    && buildAfford && !buildOccupied)
                {
                    RequestBuildCarServerRpc();
                }

                return;
            }

            PublishBuildAim(false, -1, 0, false, false);

            bool hasHit = hasRayHit;
            TrainPartKind kind = default;
            int index = -1;
            if (hasHit)
            {
                hasHit = TryResolvePart(hit, out kind, out index);
            }

            if (!hasHit || !ServiceLocator.TryGet(out ITrainState train))
            {
                PublishNoTarget();
                return;
            }

            // 편성에 없는 부위(증설 예비 슬롯의 칸·연결부)는 표적이 아니다 — 짓기 전 부위의 체력이 뜨면 안 된다.
            if (!ReadPartState(train, kind, index,
                out float health, out float maxHealth, out bool canRepair))
            {
                PublishNoTarget();
                return;
            }

            bool hasExpansion = ServiceLocator.TryGet(out ITrainExpansion expansion);
            bool canBuild = hasExpansion && kind == TrainPartKind.Car && expansion.CanBuildStructure(index);
            int structureCost = hasExpansion ? expansion.StructureBuildCost : 0;
            IResourceInventory inventory = GetComponent<IResourceInventory>();
            bool afford = inventory != null && inventory.Count >= structureCost;

            PublishTarget(kind, index, health, maxHealth, canRepair, canBuild, structureCost, afford);

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

            if (mouse.rightButton.wasPressedThisFrame && canBuild && afford)
            {
                RequestBuildStructureServerRpc(index, hit.point);
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
            IResourceInventory inventory = GetComponent<IResourceInventory>();
            afford = inventory != null && inventory.Count >= cost;
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
            IResourceInventory inventory = GetComponent<IResourceInventory>();
            bool afford = inventory != null && inventory.Count >= cost;

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
                _layoutSettings.DeckHeight, _layoutSettings.CarLength,
                out Vector3 center, out Vector3 size);

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

        /// <summary>맞은 콜라이더에서 열차 부위를 식별한다 — 건축물은 칸의 자식이라 먼저 검사한다.</summary>
        private static bool TryResolvePart(RaycastHit hit, out TrainPartKind kind, out int index)
        {
            StructureView structure = hit.collider.GetComponentInParent<StructureView>();
            if (structure != null)
            {
                kind = TrainPartKind.Structure;
                index = structure.CarIndex;
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
            out float health, out float maxHealth, out bool canRepair)
        {
            health = 0f;
            maxHealth = 0f;
            canRepair = false;

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
                    if (train.TryGetStructure(index, out StructureState structure))
                    {
                        health = structure.Health;
                        maxHealth = structure.MaxHealth;
                        canRepair = TrainStateLogic.IsStructureAlive(structure)
                            && structure.Health < structure.MaxHealth
                            && train.TryGetCar(index, out CarState owner) && TrainStateLogic.IsCarPresent(owner);
                        return true;
                    }

                    return false;

                default:
                    return false;
            }
        }

        private bool TryRaycastHit(Vector3 origin, Vector3 direction, out RaycastHit hit)
        {
            var ray = new Ray(origin, direction);
            RaycastHit[] hits = Physics.RaycastAll(ray, _settings.MaxRange, ~0, QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            hit = default;
            bool found = false;
            for (int i = 0; i < hits.Length; i++)
            {
                if (hits[i].distance < bestDistance && hits[i].transform.root != transform.root)
                {
                    bestDistance = hits[i].distance;
                    hit = hits[i];
                    found = true;
                }
            }

            return found;
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
                false, default, -1, 0f, 0f, false, false, 0, false));
        }

        private void PublishTarget(TrainPartKind kind, int index, float health, float maxHealth,
            bool canRepair, bool canBuild, int structureCost, bool afford)
        {
            bool unchanged = _sentHasTarget && _sentKind == kind && _sentIndex == index
                && Mathf.Approximately(_sentHealth, health)
                && _sentCanRepair == canRepair && _sentCanBuild == canBuild && _sentAfford == afford;
            if (unchanged)
            {
                return;
            }

            _sentHasTarget = true;
            _sentKind = kind;
            _sentIndex = index;
            _sentHealth = health;
            _sentCanRepair = canRepair;
            _sentCanBuild = canBuild;
            _sentAfford = afford;
            EventBus<HammerTargetLocalEvent>.Publish(new HammerTargetLocalEvent(
                true, kind, index, health, maxHealth, canRepair, canBuild, structureCost, afford));
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
                    _layoutSettings.DeckHeight, _layoutSettings.CarLength, out ghostCenter, out ghostSize);
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
                    _layoutSettings.CarWidth, _layoutSettings.DeckHeight, out ghostCenter, out ghostSize);
            }

            EventBus<CarRecoupleAimLocalEvent>.Publish(new CarRecoupleAimLocalEvent(
                aiming, carIndex, cost, prompt, remainingMeters, ghostCenter, ghostSize));
        }

        // ── 호스트: 권위 계층 (거리 검증·수리·설치 확정) ──────────────────────

        [Rpc(SendTo.Server)]
        private void RequestRepairServerRpc(
            TrainPartKind kind, int index, Vector3 hitPoint, RpcParams rpcParams = default)
        {
            if (_settings == null || !IsWithinRange(hitPoint))
            {
                return;
            }

            if (ServiceLocator.TryGet(out ITrainRepairSink sink))
            {
                sink.ServerApplyRepair(kind, index, _settings.RepairPerHit);
            }
        }

        /// <summary>건축물 설치 — (자원 차감 + 설치)를 원자적으로 확정하고, 설치 실패 시 자원을 되돌린다.</summary>
        [Rpc(SendTo.Server)]
        private void RequestBuildStructureServerRpc(
            int carIndex, Vector3 hitPoint, RpcParams rpcParams = default)
        {
            if (_settings == null || !IsWithinRange(hitPoint)
                || !ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !expansion.CanBuildStructure(carIndex))
            {
                return;
            }

            IResourceInventory inventory = GetComponent<IResourceInventory>();
            if (inventory == null || !inventory.ServerTryRemove(expansion.StructureBuildCost))
            {
                return;
            }

            if (!expansion.ServerTryBuildStructure(carIndex))
            {
                inventory.ServerTryAdd(expansion.StructureBuildCost);
            }
        }

        /// <summary>
        /// 칸 건설 — 건설 슬롯·지점을 권위 상태로 재계산해 거리 검증 후
        /// (자원 차감 + 건설)을 원자적으로 확정한다. 건설 실패 시 자원을 되돌린다.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestBuildCarServerRpc(RpcParams rpcParams = default)
        {
            if (_settings == null || _layoutSettings == null
                || !ServiceLocator.TryGet(out ITrainExpansion expansion)
                || !expansion.TryGetBuildSlot(out int slot)
                || !IsWithinRange(SlotCouplingAnchor(slot))
                || IsBuildVolumeOccupied(slot))
            {
                return;
            }

            IResourceInventory inventory = GetComponent<IResourceInventory>();
            if (inventory == null || !inventory.ServerTryRemove(expansion.CarBuildCost))
            {
                return;
            }

            if (!expansion.ServerTryBuildCar())
            {
                inventory.ServerTryAdd(expansion.CarBuildCost);
            }
        }

        /// <summary>
        /// 이탈 칸 재결합 — 대상 칸·조준 지점을 권위 상태로 재계산해 거리 검증 후
        /// (자원 차감 + 재결합)을 원자적으로 확정한다. 슬롯 도달·앞 칸 존재는 호스트가 다시 본다.
        /// </summary>
        [Rpc(SendTo.Server)]
        private void RequestRecoupleServerRpc(int carIndex, RpcParams rpcParams = default)
        {
            if (_settings == null || _layoutSettings == null
                || !ServiceLocator.TryGet(out ITrainRecouple recouple)
                || !recouple.TryGetRecoupleTarget(out int target) || target != carIndex
                || !IsWithinRange(SlotCouplingAnchor(target)))
            {
                return;
            }

            IResourceInventory inventory = GetComponent<IResourceInventory>();
            if (inventory == null || !inventory.ServerTryRemove(recouple.RecoupleCost))
            {
                return;
            }

            if (!recouple.ServerTryRecouple(target))
            {
                inventory.ServerTryAdd(recouple.RecoupleCost);
            }
        }

        /// <summary>거리 검증 — 요청자(이 오브젝트는 소유자의 플레이어) 위치 기준 사거리 초과 보고는 기각한다.</summary>
        private bool IsWithinRange(Vector3 hitPoint)
        {
            float maxDistance = _settings.MaxRange + _settings.RangeTolerance;
            return (hitPoint - transform.position).sqrMagnitude <= maxDistance * maxDistance;
        }
    }
}
