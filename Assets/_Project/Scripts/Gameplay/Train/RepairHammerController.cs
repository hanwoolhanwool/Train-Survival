using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 수리 망치 (기획서 §9 — 수리 망치로 수리. §M3). 좌클릭 홀드 = 겨눈 부위 수리,
    /// 우클릭 = 겨눈 칸에 건축물(온실 돔) 설치(자원 소모).
    /// 파이프라인은 리볼버와 동일 구조: 소유자 로컬 레이캐스트로 부위(칸·연결부·건축물)를 식별해
    /// 호스트에 보고 → 호스트가 거리 재검증 후 수리·설치를 확정 → 상태 복제로 전 피어 반영.
    /// 겨눈 부위와 체력은 <see cref="HammerTargetLocalEvent"/>로 발행해 조준 HUD가 그린다(수리 과정 가시화).
    /// 열차 부위는 NetworkObject가 아니므로 (부위 종류, 인덱스)로 식별한다. Player 프리팹에 부착한다.
    /// </summary>
    public sealed class RepairHammerController : NetworkBehaviour
    {
        [SerializeField] private RepairHammerSettings _settings;
        [SerializeField] private Transform _aimSource;

        private float _nextSwingTime;

        // 마지막으로 HUD에 알린 조준 상태 — 바뀔 때만 다시 발행한다.
        private bool _sentHasTarget;
        private TrainPartKind _sentKind;
        private int _sentIndex;
        private float _sentHealth;
        private bool _sentCanRepair;
        private bool _sentCanBuild;
        private bool _sentAfford;

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
                return;
            }

            UpdateAimAndInput();
        }

        // ── 소유자: 조준 판정·입력 계층 ────────────────────

        private void UpdateAimAndInput()
        {
            Vector3 origin = _aimSource != null ? _aimSource.position : transform.position;
            Vector3 forward = _aimSource != null ? _aimSource.forward : transform.forward;

            bool hasHit = TryRaycastHit(origin, forward, out RaycastHit hit);
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

            ReadPartState(train, kind, index, out float health, out float maxHealth, out bool canRepair);

            bool canBuild = kind == TrainPartKind.Car
                && ServiceLocator.TryGet(out ITrainExpansion expansion)
                && expansion.CanBuildStructure(index);
            int structureCost = ServiceLocator.TryGet(out ITrainExpansion costSource) ? costSource.StructureBuildCost : 0;
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

        /// <summary>복제 상태에서 부위 체력·수리 가능 여부를 읽는다(전 피어 동일 판정 — HUD 표시용).</summary>
        private static void ReadPartState(ITrainState train, TrainPartKind kind, int index,
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
                    }

                    break;

                case TrainPartKind.Coupling:
                    if (train.TryGetCoupling(index, out CouplingState coupling))
                    {
                        health = coupling.Health;
                        maxHealth = coupling.MaxHealth;
                        canRepair = !coupling.Broken && coupling.Health < coupling.MaxHealth
                            && train.TryGetCar(index, out CarState front) && TrainStateLogic.IsCarPresent(front)
                            && train.TryGetCar(index + 1, out CarState rear) && TrainStateLogic.IsCarPresent(rear);
                    }

                    break;

                case TrainPartKind.Structure:
                    if (train.TryGetStructure(index, out StructureState structure))
                    {
                        health = structure.Health;
                        maxHealth = structure.MaxHealth;
                        canRepair = TrainStateLogic.IsStructureAlive(structure)
                            && structure.Health < structure.MaxHealth
                            && train.TryGetCar(index, out CarState owner) && TrainStateLogic.IsCarPresent(owner);
                    }

                    break;
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

        /// <summary>거리 검증 — 요청자(이 오브젝트는 소유자의 플레이어) 위치 기준 사거리 초과 보고는 기각한다.</summary>
        private bool IsWithinRange(Vector3 hitPoint)
        {
            float maxDistance = _settings.MaxRange + _settings.RangeTolerance;
            return (hitPoint - transform.position).sqrMagnitude <= maxDistance * maxDistance;
        }
    }
}
