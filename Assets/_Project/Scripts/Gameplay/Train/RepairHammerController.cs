using Game.Core.Services;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 수리 망치 (기획서 §9 — 수리 망치로 수리, 좌클릭 홀드/연타의 간단한 조작. §M3).
    /// 파이프라인은 리볼버와 동일 구조: 소유자 로컬 레이캐스트로 부위(칸·연결부·건축물)를 식별해
    /// 호스트에 보고 → 호스트가 거리 재검증 후 <see cref="ITrainRepairSink"/>로 수리 확정 → 상태 복제로 전 피어 반영.
    /// 열차 부위는 NetworkObject가 아니므로 (부위 종류, 인덱스)로 식별한다. Player 프리팹에 부착한다.
    /// </summary>
    public sealed class RepairHammerController : NetworkBehaviour
    {
        [SerializeField] private RepairHammerSettings _settings;
        [SerializeField] private Transform _aimSource;

        private float _nextSwingTime;

        /// <summary>도구 슬롯 활성 여부 — <see cref="Game.Gameplay.Inventory.HotbarController"/>가 제어한다. 소유자 입력 게이트.</summary>
        public bool InputEnabled { get; set; }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || !InputEnabled || _settings == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null || !mouse.leftButton.isPressed || Time.time < _nextSwingTime)
            {
                return;
            }

            _nextSwingTime = Time.time + _settings.HitInterval;
            Swing();
        }

        // ── 소유자: 로컬 판정 계층 — 겨눈 열차 부위를 식별한다 ────────────────────

        private void Swing()
        {
            Vector3 origin = _aimSource != null ? _aimSource.position : transform.position;
            Vector3 forward = _aimSource != null ? _aimSource.forward : transform.forward;

            if (!TryRaycastHit(origin, forward, out RaycastHit hit))
            {
                return;
            }

            // 건축물 → 연결부 → 칸 순으로 좁혀 식별한다 — 건축물은 칸의 자식이라 먼저 검사해야 한다.
            StructureView structure = hit.collider.GetComponentInParent<StructureView>();
            if (structure != null)
            {
                RequestRepairServerRpc(TrainPartKind.Structure, structure.CarIndex, hit.point);
                return;
            }

            CouplingPart coupling = hit.collider.GetComponentInParent<CouplingPart>();
            if (coupling != null)
            {
                RequestRepairServerRpc(TrainPartKind.Coupling, coupling.CouplingIndex, hit.point);
                return;
            }

            CarView car = hit.collider.GetComponentInParent<CarView>();
            if (car != null)
            {
                RequestRepairServerRpc(TrainPartKind.Car, car.CarIndex, hit.point);
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

        // ── 호스트: 권위 계층 (거리 검증·수리 확정) ──────────────────────

        [Rpc(SendTo.Server)]
        private void RequestRepairServerRpc(
            TrainPartKind kind, int index, Vector3 hitPoint, RpcParams rpcParams = default)
        {
            if (_settings == null)
            {
                return;
            }

            // 거리 검증 — 요청자(이 오브젝트는 소유자의 플레이어) 위치 기준 사거리 초과 보고는 기각한다 (호스트 검증 원칙).
            float maxDistance = _settings.MaxRange + _settings.RangeTolerance;
            if ((hitPoint - transform.position).sqrMagnitude > maxDistance * maxDistance)
            {
                Debug.Log($"[RepairHammerController] 수리 보고 기각(사거리 초과): client={rpcParams.Receive.SenderClientId}");
                return;
            }

            if (ServiceLocator.TryGet(out ITrainRepairSink sink))
            {
                sink.ServerApplyRepair(kind, index, _settings.RepairPerHit);
            }
        }
    }
}
