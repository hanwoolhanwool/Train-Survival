using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Logging;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Game.Gameplay.Inventory;
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

        // 서버 전용 — 무기별 "승인된 최근 발사"의 시드. 명중 보고는 이 시드와 맞아야 피해가 된다.
        private readonly Dictionary<int, uint> _approvedShot = new Dictionary<int, uint>();

        /// <summary>조준각 중계 주기 — 10 Hz (결정 ⑥). 표현 전용이라 유실돼도 판정이 흔들리지 않는다.</summary>
        private const float AimRelayInterval = 0.1f;

        // ── 자동 터렛 (B단계) — 서버 전용 상태 ────────────────────────────

        /// <summary>대상 주사 주기(초) — 발사 주기와 별개다. 포신은 이 주기로 대상을 따라 돈다.</summary>
        private const float TurretScanInterval = 0.2f;

        /// <summary>조준 원점의 갑판 기준 높이(m) — 뷰 없이도 성립해야 하므로 상수다.</summary>
        private const float TurretAimHeight = 0.75f;

        /// <summary>시야 검사 사거리 여유(m) — 대상 표면과 중심의 차이를 흡수한다.</summary>
        private const float TurretLineOfSightSlack = 1.5f;

        private const int TurretMaxCandidates = 24;

        private readonly Collider[] TurretOverlapBuffer = new Collider[64];
        private readonly TurretCandidate[] _turretCandidates = new TurretCandidate[TurretMaxCandidates];
        private readonly NetworkObject[] _turretTargets = new NetworkObject[TurretMaxCandidates];
        private readonly Dictionary<int, float> _turretNextFire = new Dictionary<int, float>();

        private float _nextTurretScan;

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
                ServerTickTurrets();
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

            // 장탄 권위는 서버다 — 붙는 순간 현재 장탄을 알려 줘야 HUD가 남의 잔탄에서 시작한다.
            SyncAmmoRpc(structureId, _magazines.GetRounds(structureId),
                RpcTarget.Single(clientId, RpcTargetUse.Temp));
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

        // ── 사격·장전 (§2.4 · §2.5) ────────────────────────────────────────

        public void ReportFire(int structureId, uint seed, Vector3 aimOrigin, Vector3 aimForward)
        {
            if (IsSpawned && structureId > 0)
            {
                ReportMountedFireServerRpc(structureId, seed, aimOrigin, aimForward);
            }
        }

        public void ReportHit(
            int structureId, uint seed, NetworkObjectReference target, Vector3 hitPoint, int pelletHits)
        {
            if (IsSpawned && structureId > 0 && pelletHits > 0)
            {
                ReportMountedHitServerRpc(structureId, seed, target, hitPoint, pelletHits);
            }
        }

        public void RequestReload(int structureId, int requestedRounds)
        {
            if (IsSpawned && structureId > 0 && requestedRounds > 0)
            {
                RequestMountedReloadServerRpc(structureId, requestedRounds);
            }
        }

        public void RequestAmmoSync(int structureId)
        {
            if (IsSpawned && structureId > 0)
            {
                RequestAmmoSyncServerRpc(structureId);
            }
        }

        /// <summary>
        /// 장탄 조회 — 자동 터렛의 잔탄은 복제되지 않으므로(결정 ⑦) 필요한 사람이 물어본다.
        /// 발사·장전이 있을 때마다 잔탄이 브로드캐스트되므로 이 경로는 <b>후발 접속·남이 채운 터렛</b>처럼
        /// 아직 한 번도 관측하지 못한 경우에만 쓰인다 (조작 계층이 무기당 한 번으로 묶는다).
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestAmmoSyncServerRpc(int structureId, RpcParams rpcParams = default)
        {
            if (!TryGetUsableWeapon(structureId, out _, out _))
            {
                return;
            }

            SyncAmmoRpc(structureId, _magazines.GetRounds(structureId),
                RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        /// <summary>
        /// 발사 확정 — 점유 · 생존 · <b>사각</b> · 장탄 순으로 본다. 사각은 보고된 월드 방향을
        /// 거치대 기준으로 되돌려 검증한다: 조작 계층이 클램프를 지키면 항상 통과하므로,
        /// 실패는 곧 조작된 보고다 (아군 오사와 포신이 칸을 뚫는 그림을 데이터로 막는 축).
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void ReportMountedFireServerRpc(
            int structureId, uint seed, Vector3 aimOrigin, Vector3 aimForward, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!MountOccupancyLogic.IsOccupiedBy(QueryOccupancies(), structureId, clientId)
                || !TryGetUsableWeapon(structureId, out StructureEntry entry, out MountedWeaponSettings settings))
            {
                return;
            }

            Quaternion mount = MountedAimMath.ResolveMountRotation(entry.Rotation);
            if (!MountedAimMath.TryResolveAim(mount, aimForward, out float yaw, out float pitch)
                || !MountedAimMath.IsWithinArc(
                    yaw, pitch, settings.YawLimit, settings.PitchMin, settings.PitchMax))
            {
                GameLog.Info(LogCategory.Combat,
                    $"거치 무기 발사 기각(사각 밖): client={clientId} structure=#{structureId}");
                return;
            }

            if (!_magazines.TryConsume(structureId))
            {
                // 빈 탄창 — 로컬 선반영이 앞서 나갔다는 뜻이므로 확정 장탄으로 되돌린다.
                SyncAmmoRpc(structureId, _magazines.GetRounds(structureId),
                    RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            _approvedShot[structureId] = seed;

            // 사람이 쏘는 무기의 잔탄은 싣지 않는다(-1) — 점유자는 로컬 선반영으로 이미 알고,
            // 원격 피어는 남의 장탄을 알 필요가 없다 (결정 ⑦).
            BroadcastMountedFireRpc(structureId, seed, aimOrigin, aimForward, -1);
        }

        /// <summary>
        /// 명중 확정 — 거리 검증의 기준점이 <b>플레이어가 아니라 좌석</b>이라는 것만 개인 화기와 다르다.
        /// 서버는 점유자가 그 자리에 있음을 이미 알고 있으므로(점유 리스트) 사거리 검증이 더 강하다.
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void ReportMountedHitServerRpc(
            int structureId, uint seed, NetworkObjectReference targetRef, Vector3 hitPoint, int pelletHits,
            RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (!MountOccupancyLogic.IsOccupiedBy(QueryOccupancies(), structureId, clientId)
                || !TryGetUsableWeapon(structureId, out _, out MountedWeaponSettings settings))
            {
                return;
            }

            // 승인된 발사의 명중만 피해가 된다 — 기각된 발사(빈 탄창·사각 밖)의 보고는 여기서 끊긴다.
            if (!_approvedShot.TryGetValue(structureId, out uint approved) || approved != seed)
            {
                return;
            }

            if (!targetRef.TryGet(out NetworkObject targetObject))
            {
                return;
            }

            IDamageable damageable = targetObject.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive
                || !ServiceLocator.TryGet(out ITrainState train)
                || !train.TryGetStructureCenter(structureId, out Vector3 seat))
            {
                return;
            }

            GunSettings gun = settings.Gun;
            float maxDistance = gun.MaxRange + gun.RangeTolerance;
            if ((hitPoint - seat).sqrMagnitude > maxDistance * maxDistance)
            {
                GameLog.Info(LogCategory.Combat,
                    $"거치 무기 명중 보고 기각(사거리 초과): client={clientId} structure=#{structureId}");
                return;
            }

            int pellets = Mathf.Clamp(pelletHits, 1, Mathf.Max(1, gun.PelletCount));
            damageable.ApplyDamage(gun.Damage * pellets, clientId);
        }

        /// <summary>
        /// 재장전 확정 — 개인 인벤에서 <b>먼저</b> 빼고 그만큼만 탄창에 넣는다. 순서를 뒤집으면
        /// 차감이 실패했을 때 공짜 탄이 남는다 (개인 화기 재장전과 같은 규약, 채우는 곳만 무기 탄창이다).
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestMountedReloadServerRpc(
            int structureId, int requestedRounds, RpcParams rpcParams = default)
        {
            ulong clientId = rpcParams.Receive.SenderClientId;
            if (requestedRounds <= 0
                || !TryGetUsableWeapon(structureId, out _, out MountedWeaponSettings settings)
                || !TryGetPlayerObject(clientId, out NetworkObject player)
                || !player.TryGetComponent(out IResourceInventory inventory)
                || !ServerCanReload(structureId, clientId, settings, player))
            {
                ConfirmReloadRpc(structureId, 0, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            GunSettings gun = settings.Gun;
            int capacity = gun.MagazineCapacity;
            int empty = Mathf.Max(0, capacity - _magazines.GetRounds(structureId));
            int want = Mathf.Min(Mathf.Min(requestedRounds, inventory.CountOf(gun.AmmoType)), empty);

            if (want <= 0 || !inventory.ServerTryRemove(gun.AmmoType, want))
            {
                ConfirmReloadRpc(structureId, 0, RpcTarget.Single(clientId, RpcTargetUse.Temp));
                return;
            }

            int granted = _magazines.Reload(structureId, capacity, want);
            ConfirmReloadRpc(structureId, granted, RpcTarget.Single(clientId, RpcTargetUse.Temp));

            if (settings.Manned)
            {
                // 사람 무기는 확정 발수만 보낸다 — 장탄까지 함께 밀면 로컬 장전 시간이 통째로
                // 건너뛰어져, 화면은 "재장전 중"인데 탄은 이미 가득 찬 상태가 된다.
                return;
            }

            // 자동 터렛은 로컬 탄창이 없다 — 채운 결과를 전 피어가 알아야 안내 수량이 맞는다.
            BroadcastAmmoRpc(structureId, _magazines.GetRounds(structureId));
        }

        /// <summary>
        /// 발사 연출 중계 — 판정에 영향이 없다. 쏜 사람은 이미 로컬 재생했으므로 건너뛴다.
        /// <paramref name="roundsLeft"/>가 0 이상이면 자동 무기의 잔탄이다(사람 무기는 -1).
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void BroadcastMountedFireRpc(
            int structureId, uint seed, Vector3 aimOrigin, Vector3 aimForward, int roundsLeft)
        {
            // 잔탄은 연출보다 먼저 푼다 — 쏜 사람이 건너뛰는 갈림길 앞이어야 모두가 같은 값을 받는다.
            if (roundsLeft >= 0)
            {
                EventBus<MountedAmmoSyncedLocalEvent>.Publish(
                    new MountedAmmoSyncedLocalEvent(structureId, roundsLeft));
            }

            if (NetworkManager != null && TryGetOccupant(structureId, out ulong occupant)
                && occupant == NetworkManager.LocalClientId)
            {
                return;
            }

            PlayFireCosmetics(structureId, seed, aimOrigin, aimForward);
        }

        /// <summary>
        /// 거치 무기의 발사 연출을 로컬 재생한다 — 총구는 실물이, 궤적은 시드가 정한다.
        /// 무시할 root는 <b>열차 전체</b>다: 자기 열차를 쏘는 트레이서·탄착이 보이지 않는다.
        /// </summary>
        private void PlayFireCosmetics(int structureId, uint seed, Vector3 aimOrigin, Vector3 aimForward)
        {
            if (!TryGetStructure(structureId, out StructureEntry entry))
            {
                return;
            }

            MountedWeaponSettings settings = GetSettings(entry.Kind);
            if (settings == null || settings.Gun == null)
            {
                return;
            }

            Vector3 muzzle = aimOrigin;
            Transform ignoreRoot = null;
            if (MountedWeaponView.TryGet(structureId, out MountedWeaponView view))
            {
                muzzle = view.MuzzlePosition;
                ignoreRoot = view.transform.root;
            }

            WeaponFireCosmetics.Play(settings.Gun, muzzle, aimOrigin, aimForward, seed, ignoreRoot);
        }

        /// <summary>자동 무기의 잔탄을 전 피어에 알린다 — 누가 채웠든 화면의 수량이 같아진다.</summary>
        [Rpc(SendTo.Everyone)]
        private void BroadcastAmmoRpc(int structureId, int roundsLoaded)
        {
            EventBus<MountedAmmoSyncedLocalEvent>.Publish(
                new MountedAmmoSyncedLocalEvent(structureId, roundsLoaded));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void SyncAmmoRpc(int structureId, int roundsLoaded, RpcParams rpcParams)
        {
            EventBus<MountedAmmoSyncedLocalEvent>.Publish(
                new MountedAmmoSyncedLocalEvent(structureId, roundsLoaded));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ConfirmReloadRpc(int structureId, int grantedRounds, RpcParams rpcParams)
        {
            EventBus<MountedReloadConfirmedLocalEvent>.Publish(
                new MountedReloadConfirmedLocalEvent(structureId, grantedRounds));
        }

        /// <summary>
        /// 장전 자격 — 사람이 붙는 무기는 <b>점유자만</b>, 자동 터렛은 <b>다가온 사람이면</b> 된다
        /// (§2.6 수동 장전: 점유가 아니라 1회 상호작용이다). 터렛의 거리 기준은 좌석 반경을 그대로 쓴다 —
        /// 붙을 수 있는 거리와 채울 수 있는 거리를 따로 둘 이유가 없다.
        /// </summary>
        private bool ServerCanReload(
            int structureId, ulong clientId, MountedWeaponSettings settings, NetworkObject player)
        {
            if (settings.Manned)
            {
                return MountOccupancyLogic.IsOccupiedBy(QueryOccupancies(), structureId, clientId);
            }

            return ServiceLocator.TryGet(out ITrainState train)
                && train.TryGetStructureCenter(structureId, out Vector3 center)
                && (player.transform.position - center).sqrMagnitude <= settings.SeatRadiusSqr;
        }

        /// <summary>쓸 수 있는 거치 무기인가 — 항목·설정·사격 데이터가 모두 갖춰져야 한다.</summary>
        private bool TryGetUsableWeapon(
            int structureId, out StructureEntry entry, out MountedWeaponSettings settings)
        {
            settings = null;
            if (!TryGetStructure(structureId, out entry) || !IsStructureUsable(entry))
            {
                return false;
            }

            settings = GetSettings(entry.Kind);
            return settings != null && settings.Gun != null;
        }

        // ── 자동 터렛 — 조작자만 AI로 바뀐다 (§2.6 · B단계) ────────────────

        /// <summary>
        /// 자동 터렛 주사·사격 (서버 전용). <b>로컬 선반영이 없다</b> — 예측할 입력이 없기 때문이다.
        /// 사격 파이프라인은 A단계 그대로이고, 바뀌는 것은 조작자뿐이다: 사람 대신
        /// <see cref="TurretTargetingMath"/>가 대상을 고른다.
        /// <para>
        /// 상한(<see cref="MountedWeaponSettings.MaxActiveTurrets"/>)은 밤 웨이브와 겹칠 때의
        /// RPC·프레임 방어선이다. <b>탄이 없는 터렛은 물리 조회도 하지 않고 상한도 먹지 않는다</b> —
        /// 빈 터렛이 살아 있는 터렛의 자리를 뺏으면 상한이 방어선이 아니라 고장이 된다.
        /// </para>
        /// </summary>
        private void ServerTickTurrets()
        {
            if (Time.time < _nextTurretScan)
            {
                return;
            }

            _nextTurretScan = Time.time + TurretScanInterval;

            if (!ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            int active = 0;
            int cap = int.MaxValue;

            for (int i = 0; i < train.StructureCount; i++)
            {
                if (!train.TryGetStructureAt(i, out StructureEntry entry))
                {
                    continue;
                }

                MountedWeaponSettings settings = GetSettings(entry.Kind);
                if (settings == null || settings.Manned || settings.Gun == null || !IsStructureUsable(entry))
                {
                    continue;
                }

                // 상한은 터렛 자신의 에셋이 든다 — 처음 만난 터렛의 값이 그 세션의 방어선이다.
                if (cap == int.MaxValue)
                {
                    cap = Mathf.Max(1, settings.MaxActiveTurrets);
                }

                if (_magazines.GetRounds(entry.Id) <= 0)
                {
                    continue;
                }

                if (active >= cap)
                {
                    break;
                }

                active++;
                ServerTickTurret(train, entry, settings);
            }
        }

        private void ServerTickTurret(ITrainState train, StructureEntry entry, MountedWeaponSettings settings)
        {
            if (!train.TryGetStructureCenter(entry.Id, out Vector3 center))
            {
                return;
            }

            // 조준 원점은 뷰가 아니라 상태에서 나온다 — 뷰가 없는 피어·서버가 같은 값을 얻어야 한다.
            Vector3 aimOrigin = center + Vector3.up * TurretAimHeight;
            Quaternion mount = MountedAimMath.ResolveMountRotation(entry.Rotation);

            int candidates = ServerCollectHostiles(center, settings.SearchRadius);
            int best = TurretTargetingMath.SelectTarget(
                _turretCandidates, candidates, aimOrigin, mount,
                settings.SearchRadius, settings.YawLimit, settings.PitchMin, settings.PitchMax);
            if (best < 0)
            {
                return;
            }

            NetworkObject target = _turretTargets[best];
            Vector3 toTarget = _turretCandidates[best].Position - aimOrigin;
            if (!MountedAimMath.TryResolveAim(mount, toTarget, out float yaw, out float pitch))
            {
                return;
            }

            // 포신은 대상을 따라 돈다 — 표현 전용이라 발사 주기와 무관하게 주사마다 갱신한다.
            BroadcastAimRpc(entry.Id, yaw, pitch);

            if (Time.time < ServerGetTurretNextFire(entry.Id))
            {
                return;
            }

            GunSettings gun = settings.Gun;
            Vector3 forward = toTarget.normalized;
            float distance = toTarget.magnitude;

            // 시야가 막히면 쏘지 않는다 (§2.6) — 벽에 탄을 버리지 않는다.
            if (!ServerHasLineOfSight(aimOrigin, forward, distance, gun, target))
            {
                return;
            }

            _turretNextFire[entry.Id] = Time.time + gun.FireInterval;
            if (!_magazines.TryConsume(entry.Id))
            {
                return;
            }

            uint seed = (uint)UnityEngine.Random.Range(1, int.MaxValue);
            ServerApplyTurretDamage(gun, aimOrigin, forward, seed);

            // 자동 무기의 잔탄은 실어 보낸다 — 사람이 붙어 있지 않으니 아무도 로컬로 알 수 없고,
            // 언제 채워야 하는지를 화면에서 읽을 수 있어야 한다. 발사 중계에 얹으므로 추가 통신이 없다.
            BroadcastMountedFireRpc(entry.Id, seed, aimOrigin, forward, _magazines.GetRounds(entry.Id));
        }

        /// <summary>
        /// 반경 안의 <b>적대 대상만</b> 후보로 모은다 (결정 ⑧) — 플레이어·열차·건축물·자원은 후보가 아니다.
        /// <see cref="IDamageable"/>만으로 거르면 아군이 대상이 된다: 자격은 <see cref="IHostileTarget"/>이 쥔다.
        /// 한 대상의 콜라이더가 여럿일 수 있으므로 <see cref="NetworkObject"/> 기준으로 한 번만 담는다.
        /// </summary>
        private int ServerCollectHostiles(Vector3 center, float radius)
        {
            int count = Physics.OverlapSphereNonAlloc(
                center, radius, TurretOverlapBuffer, ~0, QueryTriggerInteraction.Ignore);

            int filled = 0;
            for (int i = 0; i < count && filled < _turretCandidates.Length; i++)
            {
                Collider collider = TurretOverlapBuffer[i];
                if (collider == null)
                {
                    continue;
                }

                NetworkObject candidate = collider.GetComponentInParent<NetworkObject>();
                if (candidate == null || candidate.GetComponent<IHostileTarget>() == null)
                {
                    continue;
                }

                IDamageable damageable = candidate.GetComponent<IDamageable>();
                if (damageable == null)
                {
                    continue;
                }

                bool duplicate = false;
                for (int j = 0; j < filled; j++)
                {
                    if (ReferenceEquals(_turretTargets[j], candidate))
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (duplicate)
                {
                    continue;
                }

                _turretTargets[filled] = candidate;
                _turretCandidates[filled] = new TurretCandidate
                {
                    // 발밑이 아니라 몸통을 겨눈다 — 콜라이더 중심이 사각·시야 판정 모두의 기준이다.
                    Position = collider.bounds.center,
                    IsAlive = damageable.IsAlive,
                };
                filled++;
            }

            return filled;
        }

        private bool ServerHasLineOfSight(
            Vector3 aimOrigin, Vector3 forward, float distance, GunSettings gun, NetworkObject target)
        {
            float range = Mathf.Min(gun.MaxRange, distance + TurretLineOfSightSlack);
            if (!WeaponRaycast.TryGetClosestHit(aimOrigin, forward, range, transform.root, out RaycastHit hit))
            {
                return false;
            }

            NetworkObject blocking = hit.collider.GetComponentInParent<NetworkObject>();
            return blocking != null && ReferenceEquals(blocking, target);
        }

        /// <summary>
        /// 서버 권위 판정 — 사람이 쏠 때와 <b>같은 시드·같은 산탄 수학</b>을 쓰므로 전 피어가 재계산한
        /// 궤적과 어긋나지 않는다. 맞은 것이 적대 대상이 아니면 피해를 주지 않는다: 사각으로 막고
        /// 대상 필터로 한 번 더 막는다 (자동 무기의 아군 오사는 한 겹으로 막지 않는다).
        /// </summary>
        private void ServerApplyTurretDamage(
            GunSettings gun, Vector3 aimOrigin, Vector3 aimForward, uint seed)
        {
            int pellets = Mathf.Max(1, gun.PelletCount);
            uint state = seed;

            for (int p = 0; p < pellets; p++)
            {
                Vector3 direction = WeaponSpreadMath.ApplySpreadSeeded(aimForward, gun.SpreadAngle, ref state);
                if (!WeaponRaycast.TryGetClosestHit(
                        aimOrigin, direction, gun.MaxRange, transform.root, out RaycastHit hit))
                {
                    continue;
                }

                NetworkObject hitObject = hit.collider.GetComponentInParent<NetworkObject>();
                if (hitObject == null || hitObject.GetComponent<IHostileTarget>() == null)
                {
                    continue;
                }

                IDamageable damageable = hitObject.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive)
                {
                    continue;
                }

                damageable.ApplyDamage(gun.Damage, NetworkManager.ServerClientId);
            }
        }

        private float ServerGetTurretNextFire(int structureId)
        {
            return _turretNextFire.TryGetValue(structureId, out float next) ? next : 0f;
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
            ServerForgetWeapon(evt.StructureId);
        }

        private void OnStructureDemolished(StructureDemolishedEvent evt)
        {
            ServerForgetWeapon(evt.StructureId);
        }

        /// <summary>사라진 무기의 서버 상태를 지운다 — 장탄과 터렛 발사 주기.</summary>
        private void ServerForgetWeapon(int structureId)
        {
            _magazines.Clear(structureId);
            _turretNextFire.Remove(structureId);
        }
    }
}
