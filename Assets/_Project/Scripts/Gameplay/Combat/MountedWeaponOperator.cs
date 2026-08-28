using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Logging;
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
        private string _promptLabel = string.Empty;

        // 자동 터렛의 확정 장탄 — 서버가 장전 확정마다 알려 준 값이다 (결정 ⑦: 복제하지 않는다).
        // 한 번도 채우지 않은 터렛은 값이 없어 안내에 수량이 빠진다 — 모르는 것을 아는 척하지 않는다.
        private readonly Dictionary<int, int> _knownTurretRounds = new Dictionary<int, int>();

        // 이미 서버에 물어본 터렛 — 모르는 무기당 조회는 한 번뿐이다(답이 없어도 두 번 묻지 않는다).
        private readonly HashSet<int> _ammoAsked = new HashSet<int>();

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
            EventBus<StructureBuiltEvent>.Subscribe(OnStructureBuilt);
            EventBus<StructureDestroyedEvent>.Subscribe(OnStructureGone);
            EventBus<StructureDemolishedEvent>.Subscribe(OnStructureDemolished);
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
            EventBus<StructureBuiltEvent>.Unsubscribe(OnStructureBuilt);
            EventBus<StructureDestroyedEvent>.Unsubscribe(OnStructureGone);
            EventBus<StructureDemolishedEvent>.Unsubscribe(OnStructureDemolished);
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
            _lastPublishedReloading = false;
            _lastPublishedReserve = -1;

            if (_inventory == null)
            {
                // 예비 탄약이 0으로만 보이는 증상의 유일한 코드 원인이다 — 조용히 두지 않는다.
                GameLog.Warn(LogCategory.Combat,
                    "거치 무기: 개인 인벤토리를 찾지 못했다 — 예비 탄약이 0으로 표시된다");
            }

            _controller.BeginMount(
                view.Seat, MountedAimMath.ResolveMountRotation(entry.Rotation),
                settings.YawLimit, settings.PitchMin, settings.PitchMax);

            SetPrompt(false, -1, null);

            // 붙어 있는 동안 초점을 붙잡는다 — 좌석 옆 상자·작업대 안내가 조작 안내 위로 겹치지 않게 한다.
            InteractionArbiter.Capture(InteractionSource.MountedWeapon);

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
            InteractionArbiter.Release(InteractionSource.MountedWeapon);

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
                // 재장전이 진행 중이면 서버 값으로 덮지 않는다 — 확정(ConfirmPendingLoad)이
                // 시간과 함께 끝낸다. 덮으면 장전 시간이 통째로 건너뛰어진다.
                if (!_magazine.IsReloading)
                {
                    _magazine.SetRounds(evt.RoundsLoaded);
                }

                return;
            }

            // 내가 붙어 있지 않은 무기의 장탄 = 자동 터렛이다 (§2.6). 발사 중계·장전 확정·조회 응답이
            // 모두 이 한 경로로 들어온다.
            _knownTurretRounds[evt.StructureId] = evt.RoundsLoaded;
        }

        private void OnReloadConfirmed(MountedReloadConfirmedLocalEvent evt)
        {
            if (_magazine != null && evt.StructureId == _mountedStructureId)
            {
                _magazine.ConfirmPendingLoad(evt.GrantedRounds);
            }
        }

        /// <summary>
        /// 갓 지은 자동 터렛은 <b>빈 탄창</b>이다 — 그 사실을 아는 것도 정보이므로 캐시에 0을 넣는다.
        /// 이것이 없으면 한 번도 쏘지 않은 새 터렛의 안내에 수량이 빠져, 비었다는 것을 알 수 없다.
        /// </summary>
        private void OnStructureBuilt(StructureBuiltEvent evt)
        {
            if (ServiceLocator.TryGet(out IMountedWeapons mounted))
            {
                MountedWeaponSettings settings = mounted.GetSettings(evt.Entry.Kind);
                if (settings != null && !settings.Manned)
                {
                    _knownTurretRounds[evt.Entry.Id] = 0;
                    _ammoAsked.Add(evt.Entry.Id);
                }
            }
        }

        private void OnStructureGone(StructureDestroyedEvent evt)
        {
            ForgetAmmo(evt.StructureId);
        }

        private void OnStructureDemolished(StructureDemolishedEvent evt)
        {
            ForgetAmmo(evt.StructureId);
        }

        private void ForgetAmmo(int structureId)
        {
            _knownTurretRounds.Remove(structureId);
            _ammoAsked.Remove(structureId);
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
            float bestDot = -1f;
            MountedWeaponSettings bestSettings = null;
            Vector3 position = player.transform.position;

            for (int i = 0; i < train.StructureCount; i++)
            {
                if (!train.TryGetStructureAt(i, out StructureEntry entry))
                {
                    continue;
                }

                MountedWeaponSettings settings = mounted.GetSettings(entry.Kind);
                if (settings == null || !StructureGridLogic.IsAlive(entry))
                {
                    continue;
                }

                // 사람이 붙는 무기는 비어 있어야 후보다. 자동 터렛은 점유가 없으므로 늘 후보다 —
                // 다가간 사람이 하는 일이 다를 뿐이다(붙기 vs 채우기).
                if ((settings.Manned && mounted.TryGetOccupant(entry.Id, out _))
                    || !train.TryGetStructureCenter(entry.Id, out Vector3 center))
                {
                    continue;
                }

                float sqr = (position - center).sqrMagnitude;
                if (sqr > settings.SeatRadiusSqr || sqr >= bestSqr)
                {
                    continue;
                }

                float dot = LocalInteraction.GetLookDot(player, center);
                if (dot < settings.LookDotThreshold)
                {
                    continue;
                }

                bestSqr = sqr;
                bestDot = dot;
                bestId = entry.Id;
                bestSettings = settings;
            }

            // 상호작용 대상 중재 — 무기 옆에 상자·작업대가 붙어 있어도 겨눈 쪽 하나만 안내·E키를 받는다.
            if (bestId > 0)
            {
                InteractionArbiter.Submit(InteractionSource.MountedWeapon, bestDot, bestSqr);
            }

            // IsFocused를 먼저 물어 프레임을 넘긴다 — 단락되면 중재가 갱신되지 않는다.
            bool focused = InteractionArbiter.IsFocused(InteractionSource.MountedWeapon) && bestId > 0;

            SetPrompt(focused, focused ? bestId : -1,
                focused ? ResolvePromptLabel(bestId, bestSettings) : null);

            if (!focused)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.eKey.wasPressedThisFrame)
            {
                return;
            }

            if (bestSettings.Manned)
            {
                mounted.RequestMount(bestId);
            }
            else
            {
                // 수동 장전 (§2.6) — 점유가 아니라 1회 상호작용이다. 채울 양은 서버가
                // 빈 약실과 예비량으로 다시 깎으므로 여기서는 탄창 용량을 그대로 요청한다.
                mounted.RequestReload(bestId, bestSettings.MagazineCapacity);
            }
        }

        /// <summary>
        /// 안내 문구 — 붙는 무기는 이름만, 자동 터렛은 <b>할 일과 아는 만큼의 장탄</b>을 함께 보인다.
        /// 한 번도 채우지 않은 터렛은 수량이 빠진다: 장탄은 복제되지 않으므로 모르는 것을 아는 척하지 않는다.
        /// </summary>
        private string ResolvePromptLabel(int structureId, MountedWeaponSettings settings)
        {
            if (structureId <= 0 || settings == null)
            {
                return null;
            }

            if (settings.Manned)
            {
                return settings.DisplayName;
            }

            // 예비는 내 인벤의 탄이다 — 채울 수 있는지를 무기 앞에서 바로 읽을 수 있어야 한다.
            int reserve = _inventory != null && settings.Gun != null
                ? _inventory.CountOf(settings.Gun.AmmoType)
                : 0;

            if (!_knownTurretRounds.TryGetValue(structureId, out int rounds))
            {
                // 아직 한 번도 관측하지 못한 터렛 — 후발 접속이거나 남이 채운 것이다. 한 번만 물어본다.
                if (_ammoAsked.Add(structureId) && ServiceLocator.TryGet(out IMountedWeapons mounted))
                {
                    mounted.RequestAmmoSync(structureId);
                }

                return settings.DisplayName + " 장전  ·  예비 " + reserve;
            }

            string state = rounds <= 0
                ? "비었음"
                : rounds + "/" + settings.MagazineCapacity;
            return settings.DisplayName + " 장전 (" + state + ")  ·  예비 " + reserve;
        }

        /// <summary>
        /// 근접 안내 발행 — <b>문구까지 비교한다</b>. 터렛 안내에는 잔탄·예비가 실려 있어
        /// 같은 무기를 같은 자리에서 보고 있어도 값이 바뀐다: 대상만 비교하면 숫자가 얼어붙는다.
        /// </summary>
        private void SetPrompt(bool visible, int structureId, string displayName)
        {
            string label = displayName ?? string.Empty;
            if (_promptVisible == visible && _promptStructureId == structureId && _promptLabel == label)
            {
                return;
            }

            _promptVisible = visible;
            _promptStructureId = structureId;
            _promptLabel = label;
            EventBus<MountPromptLocalEvent>.Publish(
                new MountPromptLocalEvent(visible, structureId, label));
        }
    }
}
