using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Logging;
using Game.Core.Services;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 거치 무기 축의 네트워크 경계 (M7 4차 §2.2) — 점유 리스트 복제·승인·강제 하차를 호스트가 소유한다.
    /// 규칙 판정은 순수 <see cref="MountOccupancyLogic"/>이, 장탄은 서버 내부
    /// <see cref="MountedMagazineStore"/>가 담당하고 여기서는 확정·전파만 맡는다
    /// (<see cref="TrainState"/>가 그리드에 대해 하는 것과 같은 역할 분담).
    /// <para>
    /// 건축물 자체는 <see cref="TrainState"/>의 그리드 항목이라 설치·철거·피해·파괴·이탈 추종이
    /// 전부 기존 경로다 — 이 컴포넌트가 더하는 것은 <b>점유와 조준</b>뿐이다.
    /// 건축물 뷰는 <see cref="NetworkObject"/>가 아니므로 네트워크 프리팹 목록이 늘지 않는다 (§2.7).
    /// </para>
    /// Train 루트(씬 NetworkObject)에 1개 배치한다.
    /// </summary>
    public sealed class MountedWeaponHost : NetworkBehaviour, IMountedWeapons
    {
        [Tooltip("건축물 카탈로그 — 어느 종류가 거치 무기인지(설정 참조 유무)의 진실.")]
        [SerializeField] private StructureCatalog _catalog;

        // 점유 중인 것만 항목이 있다 (없음 = 비점유). 조준각은 여기 싣지 않는다 — 결정 ⑤.
        private readonly NetworkList<MountOccupancy> _occupancy = new NetworkList<MountOccupancy>();

        // 서버 전용 — 무기별 장탄 (결정 ⑦: 리스트에 싣지 않는다).
        private readonly MountedMagazineStore _magazines = new MountedMagazineStore();

        // 전 피어 표현 캐시 — 건축물 Id → 조준각(yaw, pitch). 판정에 쓰지 않는다.
        private readonly Dictionary<int, Vector2> _aim = new Dictionary<int, Vector2>();

        // 순수 함수용 스냅샷 버퍼 — 조회마다 새로 할당하지 않는다 (TrainState.QueryStructures와 같은 규약).
        private MountOccupancy[] _query;

        /// <summary>조준각 중계 주기 — 10 Hz (결정 ⑥). 표현 전용이라 유실돼도 판정이 흔들리지 않는다.</summary>
        private const float AimRelayInterval = 0.1f;

        private float _nextAimRelayTime;
        private int _localAimStructureId = -1;
        private float _localAimYaw;
        private float _localAimPitch;
        private bool _localAimDirty;

        public override void OnNetworkSpawn()
        {
            if (!ServiceLocator.IsRegistered<IMountedWeapons>())
            {
                ServiceLocator.Register<IMountedWeapons>(this);
            }

            if (IsServer)
            {
                _magazines.ClearAll();
                EventBus<StructureDestroyedEvent>.Subscribe(OnStructureDestroyed);
                EventBus<StructureDemolishedEvent>.Subscribe(OnStructureDemolished);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                EventBus<StructureDestroyedEvent>.Unsubscribe(OnStructureDestroyed);
                EventBus<StructureDemolishedEvent>.Unsubscribe(OnStructureDemolished);
            }

            if (ServiceLocator.TryGet(out IMountedWeapons service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<IMountedWeapons>();
            }

            _aim.Clear();
            _localAimStructureId = -1;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                ServerScanForcedDismount();
            }

            RelayLocalAim();
        }

        // ── IMountedWeapons — 로컬 조회면 (복제된 리스트를 읽는다) ────────────

        public MountedWeaponSettings GetSettings(StructureKind kind)
        {
            return _catalog != null ? _catalog.GetMountedWeapon(kind) : null;
        }

        public bool TryGetOccupant(int structureId, out ulong clientId)
        {
            if (MountOccupancyLogic.TryFindByStructure(QueryOccupancies(), structureId, out int index))
            {
                clientId = _occupancy[index].OccupantClientId;
                return true;
            }

            clientId = 0;
            return false;
        }

        public bool TryGetMountedStructure(ulong clientId, out int structureId)
        {
            if (MountOccupancyLogic.TryFindByClient(QueryOccupancies(), clientId, out int index))
            {
                structureId = _occupancy[index].StructureId;
                return true;
            }

            structureId = -1;
            return false;
        }

        public void RequestMount(int structureId)
        {
            if (IsSpawned && structureId > 0)
            {
                RequestMountServerRpc(structureId);
            }
        }

        public void RequestDismount()
        {
            if (IsSpawned)
            {
                _localAimStructureId = -1;
                RequestDismountServerRpc();
            }
        }

        public bool TryGetAim(int structureId, out float yawDeg, out float pitchDeg)
        {
            if (_aim.TryGetValue(structureId, out Vector2 aim))
            {
                yawDeg = aim.x;
                pitchDeg = aim.y;
                return true;
            }

            yawDeg = 0f;
            pitchDeg = 0f;
            return false;
        }

        public void PublishLocalAim(int structureId, float yawDeg, float pitchDeg)
        {
            if (structureId <= 0)
            {
                return;
            }

            // 로컬 화면의 포신은 중계를 기다리지 않는다 — 캐시를 즉시 갱신하고 원격 전파만 솎아 보낸다.
            _aim[structureId] = new Vector2(yawDeg, pitchDeg);
            _localAimStructureId = structureId;
            _localAimYaw = yawDeg;
            _localAimPitch = pitchDeg;
            _localAimDirty = true;
        }

        // ── 호스트: 점유 승인·해제 ─────────────────────────────────────────

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestMountServerRpc(int structureId, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            MountRejectReason reason = ServerEvaluateMount(structureId, clientId);
            if (reason != MountRejectReason.None)
            {
                GameLog.Info(LogCategory.Train,
                    $"거치 무기 점유 기각({reason}): client={clientId} structure=#{structureId}");
                return;
            }

            // 한 사람은 하나만 — 다른 무기를 잡고 있었다면 먼저 놓는다 (경합은 서버 도착 순서로 끝난다).
            ServerRemoveOccupancyOf(clientId);

            _occupancy.Add(new MountOccupancy
            {
                StructureId = (ushort)structureId,
                OccupantClientId = clientId,
            });
        }

        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestDismountServerRpc(RpcParams rpcParams = default)
        {
            ServerRemoveOccupancyOf(rpcParams.Receive.SenderClientId);
        }

        /// <summary>
        /// 점유 승인 판정 — 조회(건축물·설정·요청자 위치)는 여기서, <b>규칙은 순수 함수가</b> 소유한다.
        /// 좌석 기준점은 건축물 점유 영역 중심이다: 뷰가 아직 스폰되지 않은 피어에서도 같은 값이 나오는
        /// 유일한 지점이고, 프리뷰·창고 접근·보따리 배출이 이미 쓰는 그 지점이다.
        /// </summary>
        private MountRejectReason ServerEvaluateMount(int structureId, ulong clientId)
        {
            if (!ServiceLocator.TryGet(out ITrainState train)
                || !train.TryGetStructureById(structureId, out StructureEntry entry))
            {
                return MountRejectReason.Destroyed;
            }

            MountedWeaponSettings settings = GetSettings(entry.Kind);
            if (settings == null)
            {
                return MountRejectReason.NotMountedWeapon;
            }

            bool occupiedByOther = TryGetOccupant(structureId, out ulong current) && current != clientId;

            float distanceSq = float.PositiveInfinity;
            if (TryGetClientPosition(clientId, out Vector3 position)
                && train.TryGetStructureCenter(structureId, out Vector3 center))
            {
                distanceSq = (position - center).sqrMagnitude;
            }

            return MountOccupancyLogic.CanMount(
                true, settings.Manned, IsStructureUsable(entry), occupiedByOther,
                distanceSq, settings.SeatRadiusSqr);
        }

        /// <summary>
        /// 강제 하차 주사 (§2.7 · 리스크 1) — 건축물 파괴·철거·칸 파괴·점유자 사망·끊김.
        /// <b>서버가 먼저 점유를 지운다</b>: 항목이 사라지면 각 피어의 조작 계층이 복제로 즉시 해제한다.
        /// 항목 수는 동시 접속 수(≤4)로 유계라 매 프레임 훑어도 비용이 없다.
        /// </summary>
        private void ServerScanForcedDismount()
        {
            for (int i = _occupancy.Count - 1; i >= 0; i--)
            {
                MountOccupancy item = _occupancy[i];
                bool structureUsable = TryGetStructure(item.StructureId, out StructureEntry entry)
                    && IsStructureUsable(entry);

                if (!MountOccupancyLogic.ShouldForceDismount(
                        structureUsable,
                        IsOccupantAlive(item.OccupantClientId),
                        IsClientConnected(item.OccupantClientId)))
                {
                    continue;
                }

                _occupancy.RemoveAt(i);
            }
        }

        private void ServerRemoveOccupancyOf(ulong clientId)
        {
            if (MountOccupancyLogic.TryFindByClient(QueryOccupancies(), clientId, out int index))
            {
                _occupancy.RemoveAt(index);
            }
        }

        // ── 조준각 중계 (표현 전용 — 결정 ⑥) ───────────────────────────────

        private void RelayLocalAim()
        {
            if (!_localAimDirty || _localAimStructureId <= 0 || Time.time < _nextAimRelayTime)
            {
                return;
            }

            _nextAimRelayTime = Time.time + AimRelayInterval;
            _localAimDirty = false;
            RelayAimServerRpc(_localAimStructureId, _localAimYaw, _localAimPitch);
        }

        [Rpc(SendTo.Server, RequireOwnership = false, Delivery = RpcDelivery.Unreliable)]
        private void RelayAimServerRpc(int structureId, float yawDeg, float pitchDeg, RpcParams rpcParams = default)
        {
            // 점유자만 그 무기의 포신을 돌린다 — 조작된 중계는 여기서 끊는다(표현이라도 남의 것은 못 돌린다).
            if (!MountOccupancyLogic.IsOccupiedBy(
                    QueryOccupancies(), structureId, rpcParams.Receive.SenderClientId))
            {
                return;
            }

            BroadcastAimRpc(structureId, yawDeg, pitchDeg);
        }

        [Rpc(SendTo.Everyone, Delivery = RpcDelivery.Unreliable)]
        private void BroadcastAimRpc(int structureId, float yawDeg, float pitchDeg)
        {
            // 자기가 쏘고 있는 무기의 각은 로컬 값이 진실이다 — 왕복해 돌아온 값으로 덮으면 한 박자 늦는다.
            if (structureId == _localAimStructureId)
            {
                return;
            }

            _aim[structureId] = new Vector2(yawDeg, pitchDeg);
        }

        // ── 조회 보조 ─────────────────────────────────────────────────────

        /// <summary>
        /// 지금 쓸 수 있는 건축물인가 — 건축물이 살아 있고 <b>얹힌 칸이 부서지지 않았다</b>.
        /// <see cref="StructureView.IsAlive"/>(표적 등록 축)와 달리 <b>이탈 여부는 보지 않는다</b>:
        /// 이탈 칸 위에서도 전투가 성립해야 하고, 점유자는 좌석에 붙어 함께 끌려간다 (§2.7).
        /// </summary>
        private bool IsStructureUsable(StructureEntry entry)
        {
            if (!StructureGridLogic.IsAlive(entry))
            {
                return false;
            }

            return ServiceLocator.TryGet(out ITrainState train)
                && train.TryGetCar(entry.CarIndex, out CarState car)
                && car.Health > 0f;
        }

        private bool TryGetStructure(int structureId, out StructureEntry entry)
        {
            if (ServiceLocator.TryGet(out ITrainState train))
            {
                return train.TryGetStructureById(structureId, out entry);
            }

            entry = default;
            return false;
        }

        private bool IsOccupantAlive(ulong clientId)
        {
            return TryGetPlayerObject(clientId, out NetworkObject player)
                && player.TryGetComponent(out Player.PlayerHealth health)
                && health.IsAlive;
        }

        private bool IsClientConnected(ulong clientId)
        {
            NetworkManager manager = NetworkManager;
            return manager != null && manager.ConnectedClients.ContainsKey(clientId);
        }

        private bool TryGetClientPosition(ulong clientId, out Vector3 position)
        {
            if (TryGetPlayerObject(clientId, out NetworkObject player))
            {
                position = player.transform.position;
                return true;
            }

            position = default;
            return false;
        }

        private bool TryGetPlayerObject(ulong clientId, out NetworkObject player)
        {
            NetworkManager manager = NetworkManager;
            if (manager != null && manager.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
            {
                player = client.PlayerObject;
                return player != null;
            }

            player = null;
            return false;
        }

        private MountOccupancy[] QueryOccupancies()
        {
            if (_query == null || _query.Length != _occupancy.Count)
            {
                _query = new MountOccupancy[_occupancy.Count];
            }

            for (int i = 0; i < _occupancy.Count; i++)
            {
                _query[i] = _occupancy[i];
            }

            return _query;
        }

        private void OnStructureDestroyed(StructureDestroyedEvent evt)
        {
            // 남은 탄은 소실한다 — 보따리 배출 대상이 아니다 (§2.5).
            _magazines.Clear(evt.StructureId);
        }

        private void OnStructureDemolished(StructureDemolishedEvent evt)
        {
            _magazines.Clear(evt.StructureId);
        }
    }
}
