using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Player;
using Game.Gameplay.Train;
using Game.Systems.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 거치 무기 조작 계층 (M7 4차 §2.3) — <b>소유자 로컬 전용</b>. 붙기·내리기 입력과 좌석 해석,
    /// 조준 발행을 맡는다. 승인·강제 하차의 진실은 <see cref="IMountedWeapons"/>의 복제 리스트이고,
    /// 이 컴포넌트는 그 리스트를 따라가며 로컬 구속(<see cref="NetworkPlayerController"/>)을 켜고 끈다.
    /// <para>
    /// 소유가 아니라 점유다 — 핫바 슬롯을 차지하지 않으므로 <see cref="GunController"/>처럼
    /// 핫바 게이트를 받지 않고, 반대로 <b>점유 중에는 핫바 전체를 잠근다</b>
    /// (<see cref="MountStateChangedLocalEvent"/> 구독자가 처리한다).
    /// </para>
    /// 플레이어 프리팹에 1개 배치한다.
    /// </summary>
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class MountedWeaponOperator : NetworkBehaviour
    {
        [Tooltip("좌석 구속을 받는 이동 컨트롤러 — 비면 같은 오브젝트에서 찾는다.")]
        [SerializeField] private NetworkPlayerController _controller;

        /// <summary>내리기 요청 뒤 서버 확정을 기다리는 동안 다시 앉지 않는 시간(초).</summary>
        private const float RemountSuppressSeconds = 0.5f;

        /// <summary>점유는 됐는데 실물이 아직 없을 때 버티는 시간(초) — 넘으면 점유를 반납한다.</summary>
        private const float SeatWaitTimeoutSeconds = 2f;

        private int _mountedStructureId = -1;
        private MountedWeaponSettings _settings;
        private float _remountSuppressedUntil;
        private float _seatWaitDeadline;
        private bool _promptVisible;
        private int _promptStructureId = -1;

        /// <summary>지금 붙어 있는 거치 무기의 건축물 Id — 아니면 -1. 사격·장전 계층이 읽는다.</summary>
        public int MountedStructureId => _mountedStructureId;

        /// <summary>붙어 있는 무기의 설정 — 아니면 null.</summary>
        public MountedWeaponSettings Settings => _settings;

        private void Awake()
        {
            if (_controller == null)
            {
                _controller = GetComponent<NetworkPlayerController>();
            }
        }

        public override void OnNetworkSpawn()
        {
            if (IsOwner)
            {
                EventBus<UiCloseRequestedLocalEvent>.Subscribe(OnUiCloseRequested);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                EventBus<UiCloseRequestedLocalEvent>.Unsubscribe(OnUiCloseRequested);
                EndLocalMount();
            }
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
        /// 반대로 로컬이 먼저 풀린 경우(순간이동·구속·좌석 소실)에는 서버에 반납을 알린다.
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
                // 로컬이 스스로 풀었다면(§2.3 — 순간이동·구속형 전환·좌석 소실) 점유도 반납한다.
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
            if (settings == null || !MountedWeaponView.TryGet(structureId, out MountedWeaponView view))
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
            _seatWaitDeadline = 0f;
            _controller.EndMount();
            EventBus<MountStateChangedLocalEvent>.Publish(new MountStateChangedLocalEvent(false, -1));
        }

        // ── 점유 중 입력 ──────────────────────────────────────────────────

        private void UpdateMountedInput(IMountedWeapons mounted)
        {
            // 포신은 매 프레임 로컬 각을 받는다 — 원격 전파(10 Hz)는 구현이 솎는다.
            mounted.PublishLocalAim(_mountedStructureId, _controller.MountedYaw, _controller.MountedPitch);

            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.eKey.wasPressedThisFrame)
            {
                RequestDismount(mounted);
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
