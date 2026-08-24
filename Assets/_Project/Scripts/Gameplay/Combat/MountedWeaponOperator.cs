using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using Game.Gameplay.Train;
using Game.Systems.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 거치 무기 조작 계층 (M7 4차 §2.3·§2.4) — <b>소유자 로컬 전용</b>. 붙기·내리기·조준·사격·재장전
    /// 입력을 맡는다. 승인과 강제 하차의 진실은 <see cref="IMountedWeapons"/>의 복제 리스트이고,
    /// 이 컴포넌트는 그 리스트를 따라가며 로컬 구속(<see cref="NetworkPlayerController"/>)을 켜고 끈다.
    /// <para>
    /// 사격은 <see cref="GunController"/>와 <b>같은 파이프라인</b>이다: 로컬 레이 판정(지연 0) →
    /// 로컬 연출 → 호스트 보고 → 호스트가 데미지·장탄을 확정. 다른 것은 기준점이 플레이어가 아니라
    /// <b>좌석</b>이라는 것과, 장탄 권위가 <b>서버</b>에 있다는 것뿐이다 (결정 ⑦).
    /// </para>
    /// <para>
    /// 소유가 아니라 점유다 — 핫바 슬롯을 차지하지 않으므로 핫바 게이트를 받지 않고,
    /// 반대로 점유 중에는 핫바 전체를 잠근다 (<see cref="MountStateChangedLocalEvent"/> 구독자가 처리).
    /// </para>
    /// 플레이어 프리팹에 1개 배치한다.
    /// </summary>
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class MountedWeaponOperator : NetworkBehaviour
    {
        [Tooltip("좌석 구속을 받는 이동 컨트롤러 — 비면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private NetworkPlayerController _controller;

        [Tooltip("조준 원점 — 보통 카메라 피벗. 비면 자식 카메라를 찾는다.")]
        [SerializeField] private Transform _aimSource;

        /// <summary>내리기 요청 뒤 서버 확정을 기다리는 동안 다시 앉지 않는 시간(초).</summary>
        private const float RemountSuppressSeconds = 0.5f;

        /// <summary>점유는 됐는데 실물이 아직 없을 때 버티는 시간(초) — 넘으면 점유를 반납한다.</summary>
        private const float SeatWaitTimeoutSeconds = 2f;

        // 한 발사의 펠릿 명중을 대상별로 모은다 — 발사 시에만 쓰는 작업 버퍼 (GunController와 같은 규약).
        private struct PelletGroup
        {
            public NetworkObject Target;
            public Vector3 HitPoint;
            public int Count;
        }

        private readonly List<PelletGroup> _pelletGroups = new List<PelletGroup>(4);

        private int _mountedStructureId = -1;
        private MountedWeaponSettings _settings;
        private MountedWeaponView _view;
        private GunMagazine _magazine;
        private IResourceInventory _inventory;

        private float _remountSuppressedUntil;
        private float _seatWaitDeadline;
        private bool _promptVisible;
        private int _promptStructureId = -1;

        private int _lastPublishedRounds = -1;
        private bool _lastPublishedReloading;
        private int _lastPublishedReserve = -1;

        /// <summary>지금 붙어 있는 거치 무기의 건축물 Id — 아니면 -1.</summary>
        public int MountedStructureId => _mountedStructureId;

        /// <summary>붙어 있는 무기의 설정 — 아니면 null.</summary>
        public MountedWeaponSettings Settings => _settings;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<NetworkPlayerController>();
            }

            _inventory = GetComponent<IResourceInventory>();
        }

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                return;
            }

            EventBus<UiCloseRequestedLocalEvent>.Subscribe(OnUiCloseRequested);
            EventBus<MountedAmmoSyncedLocalEvent>.Subscribe(OnAmmoSynced);
            EventBus<MountedReloadConfirmedLocalEvent>.Subscribe(OnReloadConfirmed);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsOwner)
            {
                return;
            }

            EventBus<UiCloseRequestedLocalEvent>.Unsubscribe(OnUiCloseRequested);
            EventBus<MountedAmmoSyncedLocalEvent>.Unsubscribe(OnAmmoSynced);
            EventBus<MountedReloadConfirmedLocalEvent>.Unsubscribe(OnReloadConfirmed);
            EndLocalMount();
        }

        private void Update()
        {
            if (!IsSpawned || !IsOwner || !GameplaySceneRoute.IsActiveSceneGameplay())
            {
                return;
            }

            if (!ServiceLocator.TryGet(out IMountedWeapons mounted))
            {
                return;
            }

            SyncOccupancy(mounted);

            if (_mountedStructureId > 0)
            {
                UpdateMountedInput(mounted);
            }
            else
            {
                UpdateApproachInput(mounted);
            }
        }

        // ── 복제된 점유를 로컬 구속으로 옮긴다 ─────────────────────────────

        /// <summary>
        /// 서버의 점유 리스트와 로컬 구속을 맞춘다. 통지 RPC가 따로 없는 이유는 <b>리스트 자체가 통지</b>이기
        /// 때문이다 — 강제 하차(파괴·사망·끊김)도 항목이 사라지는 것으로 같은 경로를 탄다.
        /// 반대로 로컬이 먼저 풀린 경우(순간이동·구속형 전환·좌석 소실)에는 서버에 반납을 알린다.
        /// </summary>
        private void SyncOccupancy(IMountedWeapons mounted)
        {
            bool occupied = mounted.TryGetMountedStructure(OwnerClientId, out int structureId);

            if (!occupied)
            {
                if (_mountedStructureId > 0)
                {
                    EndLocalMount();
                }

                return;
            }

            if (_mountedStructureId == structureId)
            {
                if (!_controller.IsMounted)
                {
                    mounted.RequestDismount();
                    EndLocalMount();
                }

                return;
            }

            if (Time.time < _remountSuppressedUntil)
            {
                return;
            }

            BeginLocalMount(mounted, structureId);
        }

        private void BeginLocalMount(IMountedWeapons mounted, int structureId)
        {
            if (!ServiceLocator.TryGet(out ITrainState train)
                || !train.TryGetStructureById(structureId, out StructureEntry entry))
            {
                return;
            }

            MountedWeaponSettings settings = mounted.GetSettings(entry.Kind);

            // 실물은 각 피어가 로컬 스폰한다 — 승인이 스폰보다 먼저 도착할 수 있으므로 잠시 기다린다.
            if (settings == null || settings.Gun == null
                || !MountedWeaponView.TryGet(structureId, out MountedWeaponView view))
            {
                if (_seatWaitDeadline <= 0f)
                {
                    _seatWaitDeadline = Time.time + SeatWaitTimeoutSeconds;
                }
                else if (Time.time > _seatWaitDeadline)
                {
                    // 끝내 앉지 못했다 — 점유를 쥔 채 서 있으면 남이 쓰지도 못한다.
                    _seatWaitDeadline = 0f;
                    mounted.RequestDismount();
                }

                return;
            }

            _seatWaitDeadline = 0f;
            _mountedStructureId = structureId;
            _settings = settings;
            _view = view;

            GunSettings gun = settings.Gun;
            _magazine = new GunMagazine(gun.MagazineCapacity, gun.FireInterval, gun.ReloadDuration);

            // 장탄 권위는 서버다 — 붙는 순간 0에서 시작하고, 곧 도착할 확정값으로 맞춘다.
            _magazine.SetRounds(0);
            _lastPublishedRounds = -1;
            _lastPublishedReserve = -1;

            _controller.BeginMount(
                view.Seat, MountedAimMath.ResolveMountRotation(entry.Rotation),
                settings.YawLimit, settings.PitchMin, settings.PitchMax);

            SetPrompt(false, -1, null);
            EventBus<MountStateChangedLocalEvent>.Publish(
                new MountStateChangedLocalEvent(true, structureId));
        }

        private void EndLocalMount()
        {
            if (_mountedStructureId <= 0)
            {
                return;
            }

            _mountedStructureId = -1;
            _settings = null;
            _view = null;
            _magazine = null;
            _seatWaitDeadline = 0f;
            _controller.EndMount();

            // HUD 탄약 줄을 거둔다 — 내린 뒤에도 남아 있으면 남의 무기 잔탄처럼 보인다.
            EventBus<WeaponAmmoChangedLocalEvent>.Publish(new WeaponAmmoChangedLocalEvent(
                HotbarItemType.None, string.Empty, 0, 0, false, 0, false));
            EventBus<MountStateChangedLocalEvent>.Publish(new MountStateChangedLocalEvent(false, -1));
        }

        // ── 점유 중 입력 ──────────────────────────────────────────────────

        private void UpdateMountedInput(IMountedWeapons mounted)
        {
            // 포신은 매 프레임 로컬 각을 받는다 — 원격 전파(10 Hz)는 구현이 솎는다.
            mounted.PublishLocalAim(_mountedStructureId, _controller.MountedYaw, _controller.MountedPitch);

            _magazine.Tick(Time.deltaTime);

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                RequestDismount(mounted);
                return;
            }

            if (mouse != null)
            {
                // 거치 무기는 연사다 — 누르고 있는 동안 발사 간격마다 나간다.
                if (mouse.leftButton.isPressed && _magazine.TryFire())
                {
                    Fire(mounted);
                }
                else if (mouse.leftButton.wasPressedThisFrame && _magazine.RoundsLoaded <= 0)
                {
                    TryReload(mounted);
                }
            }

            if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
            {
                TryReload(mounted);
            }

            PublishAmmoIfChanged();
        }

        /// <summary>
        /// 한 발사 — 로컬 레이 판정(지연 0) → 로컬 연출 → 보고. 무시할 root는 <b>열차 전체</b>라
        /// 자기 열차를 쏘지 않는다. 발사 보고를 명중 보고보다 <b>먼저</b> 보낸다: 서버는 승인된
        /// 발사의 명중만 피해로 받는데, 순서가 뒤집히면 정상 명중이 버려진다.
        /// </summary>
        private void Fire(IMountedWeapons mounted)
        {
            GunSettings gun = _settings.Gun;
            Transform aim = ResolveAimSource();
            if (gun == null || aim == null || _view == null)
            {
                return;
            }

            Vector3 aimOrigin = aim.position;
            Vector3 aimForward = aim.forward;
            Transform ignoreRoot = _view.transform.root;
            int pellets = Mathf.Max(1, gun.PelletCount);

            uint seed = (uint)Random.Range(1, int.MaxValue);
            uint state = seed;

            _pelletGroups.Clear();
            for (int p = 0; p < pellets; p++)
            {
                Vector3 direction = WeaponSpreadMath.ApplySpreadSeeded(aimForward, gun.SpreadAngle, ref state);
                if (!WeaponRaycast.TryGetClosestHit(
                        aimOrigin, direction, gun.MaxRange, ignoreRoot, out RaycastHit hit))
                {
                    continue;
                }

                NetworkObject targetObject = hit.collider.GetComponentInParent<NetworkObject>();
                if (targetObject == null)
                {
                    continue;
                }

                IDamageable candidate = targetObject.GetComponent<IDamageable>();
                if (candidate == null || !candidate.IsAlive)
                {
                    continue;
                }

                AccumulatePellet(targetObject, hit.point);
            }

            WeaponFireCosmetics.Play(gun, _view.MuzzlePosition, aimOrigin, aimForward, seed, ignoreRoot);

            mounted.ReportFire(_mountedStructureId, seed, aimOrigin, aimForward);
            for (int i = 0; i < _pelletGroups.Count; i++)
            {
                mounted.ReportHit(
                    _mountedStructureId, seed, _pelletGroups[i].Target,
                    _pelletGroups[i].HitPoint, _pelletGroups[i].Count);
            }
        }

        private void AccumulatePellet(NetworkObject target, Vector3 hitPoint)
        {
            for (int i = 0; i < _pelletGroups.Count; i++)
            {
                if (_pelletGroups[i].Target == target)
                {
                    PelletGroup group = _pelletGroups[i];
                    group.Count += 1;
                    _pelletGroups[i] = group;
                    return;
                }
            }

            _pelletGroups.Add(new PelletGroup { Target = target, HitPoint = hitPoint, Count = 1 });
        }

        /// <summary>
        /// 재장전 — 점유자가 <b>자기 인벤의 탄</b>으로 무기 탄창을 채운다 (결정 ③).
        /// 개인 화기와 같은 선반영 규약: 로컬에서 즉시 시작하고 차감 확정은 서버가 돌려준다.
        /// </summary>
        private void TryReload(IMountedWeapons mounted)
        {
            GunSettings gun = _settings != null ? _settings.Gun : null;
            if (gun == null)
            {
                return;
            }

            int reserve = _inventory != null ? _inventory.CountOf(gun.AmmoType) : 0;
            if (_magazine.TryStartReload(reserve))
            {
                mounted.RequestReload(_mountedStructureId, _magazine.PendingLoad);
            }
        }

        private void PublishAmmoIfChanged()
        {
            GunSettings gun = _settings != null ? _settings.Gun : null;
            if (gun == null)
            {
                return;
            }

            // 예비 칸에는 개인 인벤의 탄약 수가 들어간다 — 지금 화면에 있는 것과 같은 의미다.
            int reserve = _inventory != null ? _inventory.CountOf(gun.AmmoType) : 0;
            if (_magazine.RoundsLoaded == _lastPublishedRounds
                && _magazine.IsReloading == _lastPublishedReloading
                && reserve == _lastPublishedReserve)
            {
                return;
            }

            _lastPublishedRounds = _magazine.RoundsLoaded;
            _lastPublishedReloading = _magazine.IsReloading;
            _lastPublishedReserve = reserve;

            EventBus<WeaponAmmoChangedLocalEvent>.Publish(new WeaponAmmoChangedLocalEvent(
                HotbarItemType.None, _settings.DisplayName,
                _magazine.RoundsLoaded, _magazine.Capacity, _magazine.IsReloading, reserve, true));
        }

        private void OnAmmoSynced(MountedAmmoSyncedLocalEvent evt)
        {
            if (_magazine != null && evt.StructureId == _mountedStructureId)
            {
                _magazine.SetRounds(evt.RoundsLoaded);
            }
        }

        private void OnReloadConfirmed(MountedReloadConfirmedLocalEvent evt)
        {
            if (_magazine != null && evt.StructureId == _mountedStructureId)
            {
                _magazine.ConfirmPendingLoad(evt.GrantedRounds);
            }
        }

        /// <summary>
        /// Esc 우선순위 (§2.3 · 리스크 8) — 세션 메뉴가 열리기 <b>전에</b> 점유 해제가 Esc를 소비한다.
        /// 판정은 이미 그 순서를 아는 <c>SessionExitHud</c>가 하고(열린 것 닫기 &gt; 세션 메뉴),
        /// 여기서는 닫기 요청을 받아 내리기만 한다 — 제작 창·창고 창과 같은 경로다.
        /// </summary>
        private void OnUiCloseRequested(UiCloseRequestedLocalEvent evt)
        {
            if (_mountedStructureId > 0 && ServiceLocator.TryGet(out IMountedWeapons mounted))
            {
                RequestDismount(mounted);
            }
        }

        private void RequestDismount(IMountedWeapons mounted)
        {
            // 로컬은 즉시 풀고(조작감), 서버 확정을 기다리는 동안 다시 앉지 않는다.
            _remountSuppressedUntil = Time.time + RemountSuppressSeconds;
            mounted.RequestDismount();
            EndLocalMount();
        }

        private Transform ResolveAimSource()
        {
            if (_aimSource != null)
            {
                return _aimSource;
            }

            Camera camera = GetComponentInChildren<Camera>(includeInactive: true);
            _aimSource = camera != null ? camera.transform : null;
            return _aimSource;
        }

        // ── 비점유: 근접 안내와 붙기 ───────────────────────────────────────

        private void UpdateApproachInput(IMountedWeapons mounted)
        {
            if (!ServiceLocator.TryGet(out ITrainState train))
            {
                SetPrompt(false, -1, null);
                return;
            }

            NetworkObject player = LocalInteraction.GetLocalPlayerObject();
            if (player == null)
            {
                SetPrompt(false, -1, null);
                return;
            }

            int bestId = -1;
            float bestSqr = float.PositiveInfinity;
            MountedWeaponSettings bestSettings = null;
            Vector3 position = player.transform.position;

            for (int i = 0; i < train.StructureCount; i++)
            {
                if (!train.TryGetStructureAt(i, out StructureEntry entry))
                {
                    continue;
                }

                MountedWeaponSettings settings = mounted.GetSettings(entry.Kind);
                if (settings == null || !settings.Manned || !StructureGridLogic.IsAlive(entry))
                {
                    continue;
                }

                if (mounted.TryGetOccupant(entry.Id, out _)
                    || !train.TryGetStructureCenter(entry.Id, out Vector3 center))
                {
                    continue;
                }

                float sqr = (position - center).sqrMagnitude;
                if (sqr > settings.SeatRadiusSqr || sqr >= bestSqr
                    || !LocalInteraction.IsLookingAt(player, center, settings.LookDotThreshold))
                {
                    continue;
                }

                bestSqr = sqr;
                bestId = entry.Id;
                bestSettings = settings;
            }

            SetPrompt(bestId > 0, bestId, bestSettings != null ? bestSettings.DisplayName : null);

            if (bestId <= 0)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                mounted.RequestMount(bestId);
            }
        }

        private void SetPrompt(bool visible, int structureId, string displayName)
        {
            if (_promptVisible == visible && _promptStructureId == structureId)
            {
                return;
            }

            _promptVisible = visible;
            _promptStructureId = structureId;
            EventBus<MountPromptLocalEvent>.Publish(
                new MountPromptLocalEvent(visible, structureId, displayName ?? string.Empty));
        }
    }
}
