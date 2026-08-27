using Game.Core.Logging;
using System.Collections;
using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Game.Gameplay.Inventory;
using Game.Gameplay.Region;
using Game.Gameplay.Train;
using Game.Gameplay.World;
using Game.Systems.Networking;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 1인칭 플레이어 컨트롤러 — 소유자 권위 이동 (네트워크 문서 §4) +
    /// 호스트 개입 상태 머신(Normal/Grabbed/Carried) 골격 (§4.2).
    /// 규칙:
    /// - 지상(WorldFrameSurface) 접지 중에는 스크롤 속도만큼 컨베이어 밀림을 로컬 적용 (상시 외력형).
    /// - 후미 40 m 이탈 사망은 호스트가 확정하고(권위), 부활 절차는 소유자에게 RPC로 지시한다.
    /// - 슬라이스에는 Grabbed/Carried 전환 콘텐츠가 없으나 F9 디버그 RPC로 전환 경로를 검증한다 (§4.3).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class NetworkPlayerController : NetworkBehaviour
    {
        [SerializeField] private PlayerMovementSettings _settings;
        [SerializeField] private TrainLayoutSettings _trainLayout;
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private GameObject _cameraRig;

        private readonly NetworkVariable<PlayerMovementState> _movementState =
            new NetworkVariable<PlayerMovementState>(PlayerMovementState.Normal);

        // 접지 프로브에서 최근접 정적 면과 칸 지붕을 '같은 평면'으로 간주하는 높이 차(m) —
        // 이탈 칸이 정적 지형과 같은 평면으로 겹치는 구간에서 정적 면이 몇 cm 먼저 잡히는 것을 흡수한다.
        // (승차 램프가 사다리로 교체되기 전에는 램프 상단 겹침이 근거였다 — 사다리 계획 §3.11)
        private const float CoplanarSurfaceTolerance = 0.3f;

        // ── 사다리 오르기 (사다리 승하차 계획 §3) ─────────────────────────
        // 점프 탈출은 법선 쪽으로 밀어내야 한다 — 안 그러면 다음 프레임에 다시 붙어 제자리에서 튄다.
        private const float LadderJumpPushSpeed = 3f;
        private const float LadderJumpUpRatio = 0.7f;

        // 꼭대기에서 갑판에 올려놓을 때 갑판면에서 띄우는 여유(m).
        private const float LadderMantleClearance = 0.05f;

        // 한 프레임 평면 보정의 상한(m). 넘으면 사다리가 통째로 옮겨간 것이라 따라가지 않는다.
        private const float LadderMaxPlaneCorrection = 0.5f;

        // 떨어진 뒤 같은 사다리에 다시 붙기까지의 대기(초) — 볼륨 이탈 콜백이 한 프레임 늦는 것을 덮는다.
        private const float LadderReattachDelay = 0.3f;

        // 갑판으로 올려놓는 데 쓰는 시간(초). 1 m를 한 프레임에 옮기면 1인칭 카메라가 툭 튄다.
        private const float LadderMantleDuration = 0.18f;

        private static readonly RaycastHit[] GroundProbeHits = new RaycastHit[8];

        private CharacterController _characterController;
        private PlayerHealth _health;
        private IExternalTow _externalTow;
        private IMoveSpeedModifier[] _speedModifiers;

        // 수영·잠수 (바다 지역 구현 계획 §6) — 네트워크 상태가 아니다.
        // 발 높이와 지역 물면은 이미 모든 피어가 알므로 각자 유도한다 (SwimMotion 주석 참조).
        private bool _isSwimming;
        private float _submergeDepth;

        // 바다 교각 사다리 — 열차 사다리와 별개 경로다 (SeaLadder 주석 참조).
        private World.SeaLadder _seaLadder;
        private bool _onSeaLadder;
        private Vector3 _seaLadderLastPos;
        private bool _seaLadderTracked;
        private float _seaLadderBlockedUntil;

        private Vector3 _horizontalVelocity;
        private float _verticalSpeed;
        private float _pitch;
        private bool _respawning;

        /// <summary>
        /// 이탈 사망(§4.2) 후 부활이 끝나기를 기다리는 중인가 — 호스트 확정, 전 피어 복제.
        /// 이탈 사망은 체력을 거치지 않으므로(즉사 + 자체 부활 흐름) 이 플래그가 "죽어 있다"는
        /// 유일한 표시다. <see cref="PlayerHealth.IsAlive"/>가 이 값을 함께 보고 두 사망을 통합한다
        /// (M5 4차 D5·D10 — 이탈 사망 중에 섭취·버프·설치가 살아 있던 원인).
        /// </summary>
        private readonly NetworkVariable<bool> _respawnPending = new NetworkVariable<bool>();

        /// <summary>
        /// 초기 스폰 순번 — 호스트가 스폰 시 접속자 목록 내 위치로 확정한다 (M6 1차 §0 소규모 5).
        /// clientId를 그대로 쓰면 재접속마다 커지는 값이라 스폰 z가 2 m씩 계속 뒤로 밀려
        /// 반복 시 열차 밖까지 이탈한다 — 순번은 동시 접속 수(≤4)로 유계다.
        /// </summary>
        private readonly NetworkVariable<int> _spawnOrder = new NetworkVariable<int>();

        // 재접속 위치 복원 (M6 결정 ① 개정) — 소유자 초기 배치 전에 도착한 복원 지시를 보관한다.
        private Vector3 _restorePlacement;
        private bool _hasRestorePlacement;
        private bool _needsInitialPlacement;
        private bool _inventoryPanelOpen;
        private bool _sessionMenuOpen;
        private bool _craftingPanelOpen;
        private bool _storagePanelOpen;
        private bool _bundlePanelOpen;
        private bool _standingOnWorldFrame;

        /// <summary>
        /// 지금 겹쳐 있는 사다리 볼륨 — 소유자 로컬이다. 붙을지 말지는 전적으로 소유자가 아는 정보
        /// (내 캡슐이 볼륨 안인가 · 내 입력이 어디를 향하나)로 정해지므로 복제하지 않는다 (계획 §3.2).
        /// </summary>
        private Train.BoardingLadder _ladder;

        private bool _climbing;

        /// <summary>이 시각까지는 사다리에 다시 붙지 않는다 — 꼭대기·점프 탈출 직후의 재부착 왕복을 막는다.</summary>
        private float _ladderReattachAt;

        // 갑판으로 올려놓는 중 — 남은 시간과 초당 이동량(수평만). 진행 중에는 일반 이동·중력을 멈춘다.
        private float _mantleTimer;
        private Vector3 _mantleVelocity;

        // 올라설 발판이 월드 소속인가 (바다 교각 사다리) — 오르는 사이에도 흐른다.
        private bool _mantleWorldFrame;

        // ── 거치 무기 점유 (M7 4차 §2.3) ─────────────────────────────────
        // 사다리 구속(_climbing)이 만든 선례를 그대로 따른다: 로컬 구동, 위치는 OwnerNetworkTransform이
        // 복제, 판정만 서버. 다른 점은 <b>좌석이 움직인다</b>는 것뿐이다 — 이탈 칸 위 좌석은 칸을 따라간다.
        private Transform _mountSeat;
        private Quaternion _mountRotation = Quaternion.identity;
        private float _mountYawLimit = 180f;
        private float _mountPitchMin = -89f;
        private float _mountPitchMax = 89f;
        private float _mountYaw;
        private float _mountPitch;
        private bool _mounted;

        private CarView _ridingCar;
        private Vector3 _ridingCarLastPos;
        private bool _ridingCarTracked;
        private float _groundGraceTimer;

        public PlayerMovementState MovementState => _movementState.Value;

        /// <summary>이탈 사망 후 부활 대기 중인가 (복제 값 — 호스트·클라이언트 판정이 같다).</summary>
        public bool IsRespawnPending => _respawnPending.Value;

        /// <summary>
        /// 접지 프레임 기준 — 현재 지상(월드 프레임) 위에 서 있는지. 공중에서는 이륙 당시 값을 유지한다.
        /// 발사체가 발사 시점의 기준 프레임(정지=열차 위 / 스크롤=지상)을 이어받는 데 쓴다.
        /// </summary>
        public bool StandingOnWorldFrame => _standingOnWorldFrame;

        /// <summary>거치 무기에 붙어 있는가 — 이동·핫바 입력이 잠긴 상태 (M7 4차 §2.3).</summary>
        public bool IsMounted => _mounted;

        /// <summary>거치대 기준 조준각(도) — yaw는 좌우, pitch는 앙각(위 +). 포신 중계·사격이 읽는다.</summary>
        public float MountedYaw => _mountYaw;

        /// <summary>거치대 기준 앙각(도, 위 +).</summary>
        public float MountedPitch => _mountPitch;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _health = GetComponent<PlayerHealth>();
            _externalTow = GetComponent<IExternalTow>();
            _speedModifiers = GetComponents<IMoveSpeedModifier>();
        }

        /// <summary>
        /// 상태 축들이 건 이동속도 배율의 곱 (M7 3차 — 동상이 첫 사용처). 여기서는 <b>배율만</b> 본다:
        /// 어떤 축이 왜 걸었는지는 <see cref="IMoveSpeedModifier"/> 구현이 안다.
        /// </summary>
        private float ResolveSpeedMultiplier()
        {
            if (_speedModifiers == null || _speedModifiers.Length == 0)
            {
                return 1f;
            }

            float multiplier = 1f;
            for (int i = 0; i < _speedModifiers.Length; i++)
            {
                multiplier *= Mathf.Max(0f, _speedModifiers[i].MoveSpeedMultiplier);
            }

            return multiplier;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _spawnOrder.Value = ResolveSpawnOrder();
            }

            bool isOwner = IsOwner;

            // 플레이어는 네트워크 씬 전환 전(Main)에 스폰될 수 있다 — 실제 배치는
            // Game 씬 도착 후 첫 Update에서 수행한다 (열차 지오메트리 위에 착지).
            //
            // **시점과 커서도 같은 게이트를 탄다.** 예전에는 스폰 즉시 카메라 리그를 켜고 커서를
            // 잠갔는데, 그 시절엔 호스트 시작과 씬 로드가 한 호출이라 메뉴에 머무는 시간이
            // 한 프레임뿐이었다. 대기실이 그 사이에 들어오면서(게임 준비 화면 계획 §3.2)
            // **플레이어 카메라가 메뉴를 덮고 커서가 잠겨 대기실을 조작할 수 없게 됐다.**
            bool inGameplay = GameplaySceneRoute.IsGameplayScene(SceneManager.GetActiveScene().name);
            if (_cameraRig != null)
            {
                _cameraRig.SetActive(isOwner && inGameplay);
            }

            if (isOwner)
            {
                _needsInitialPlacement = true;
                if (inGameplay)
                {
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                EventBus<InventoryPanelToggledLocalEvent>.Subscribe(OnInventoryPanelToggled);
                EventBus<SessionMenuToggledLocalEvent>.Subscribe(OnSessionMenuToggled);
                EventBus<Crafting.CraftingPanelToggledLocalEvent>.Subscribe(OnCraftingPanelToggled);
                EventBus<Train.StoragePanelToggledLocalEvent>.Subscribe(OnStoragePanelToggled);
                EventBus<Train.BundlePanelToggledLocalEvent>.Subscribe(OnBundlePanelToggled);

                // 수중 화면 표시 (바다 §6.2 2-5). 소유자 표현 전용이라 런타임에 붙인다 —
                // 프리팹을 건드리면 NetworkObject 해시가 흔들린다.
                if (GetComponent<UnderwaterView>() == null)
                {
                    gameObject.AddComponent<UnderwaterView>();
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                EventBus<InventoryPanelToggledLocalEvent>.Unsubscribe(OnInventoryPanelToggled);
                EventBus<SessionMenuToggledLocalEvent>.Unsubscribe(OnSessionMenuToggled);
                EventBus<Crafting.CraftingPanelToggledLocalEvent>.Unsubscribe(OnCraftingPanelToggled);
                EventBus<Train.StoragePanelToggledLocalEvent>.Unsubscribe(OnStoragePanelToggled);
                EventBus<Train.BundlePanelToggledLocalEvent>.Unsubscribe(OnBundlePanelToggled);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>I 창 토글 (기획서 §3.4) — 열려 있는 동안 시점 회전을 멈추고 커서를 드래그용으로 푼다.</summary>
        private void OnInventoryPanelToggled(InventoryPanelToggledLocalEvent evt)
        {
            _inventoryPanelOpen = evt.IsOpen;
            ApplyCursorState();
        }

        /// <summary>세션 메뉴(Esc) 토글 — I 창과 동일하게 시점 회전을 멈추고 커서를 버튼 클릭용으로 푼다.</summary>
        private void OnSessionMenuToggled(SessionMenuToggledLocalEvent evt)
        {
            _sessionMenuOpen = evt.IsOpen;
            ApplyCursorState();
        }

        /// <summary>제작 창 토글 — I 창과 동일 규약 (시점 정지 + 커서 표시).</summary>
        private void OnCraftingPanelToggled(Crafting.CraftingPanelToggledLocalEvent evt)
        {
            _craftingPanelOpen = evt.IsOpen;
            ApplyCursorState();
        }

        /// <summary>창고 창 토글 — I 창과 동일 규약 (시점 정지 + 커서 표시).</summary>
        private void OnStoragePanelToggled(Train.StoragePanelToggledLocalEvent evt)
        {
            _storagePanelOpen = evt.IsOpen;
            ApplyCursorState();
        }

        /// <summary>보따리 창 토글 (M5 8차) — 창고 창과 동일 규약 (시점 정지 + 커서 표시).</summary>
        private void OnBundlePanelToggled(Train.BundlePanelToggledLocalEvent evt)
        {
            _bundlePanelOpen = evt.IsOpen;
            ApplyCursorState();
        }

        private void ApplyCursorState()
        {
            bool uiOpen = _inventoryPanelOpen || _sessionMenuOpen || _craftingPanelOpen || _storagePanelOpen
                || _bundlePanelOpen;
            Cursor.lockState = uiOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = uiOpen;
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                ServerCheckFallBehind();
            }

            if (!IsOwner || _settings == null || _respawning)
            {
                return;
            }

            if (_needsInitialPlacement)
            {
                // 인게임 씬이 실제로 올라오기 전(메뉴 씬에서 스폰이 먼저 도착)에는 배치를 보류한다.
                // 씬 이름을 상수와 직접 비교하면 아트 검증 씬에서 배치가 영원히 보류되므로
                // 인게임 씬 집합으로 판정한다 — 클라이언트도 같은 판정을 쓴다.
                if (!GameplaySceneRoute.IsGameplayScene(SceneManager.GetActiveScene().name))
                {
                    return;
                }

                // 대기실을 지나 인게임에 도착했다 — 이제서야 시점과 커서를 넘겨받는다.
                if (_cameraRig != null && !_cameraRig.activeSelf)
                {
                    _cameraRig.SetActive(true);
                }

                ApplyCursorState();

                _needsInitialPlacement = false;
                if (_hasRestorePlacement)
                {
                    // 재접속 복원 지시가 먼저 도착해 있으면 스폰 지점 대신 끊김 위치로 (결정 ① 개정).
                    TeleportTo(_restorePlacement);
                    _hasRestorePlacement = false;
                }
                else
                {
                    TeleportTo(_trainLayout != null
                        ? _trainLayout.GetSpawnPosition(_spawnOrder.Value)
                        : new Vector3(0f, 4f, 0f));
                }

                _horizontalVelocity = Vector3.zero;
                _verticalSpeed = 0f;
            }

            bool lookAllowed = !_inventoryPanelOpen && !_sessionMenuOpen && !_craftingPanelOpen
                && !_storagePanelOpen;

            // 구속형 상태(Grabbed/Carried)에서는 소유자 <b>이동</b> 입력을 정지한다 — 호스트 구동 (§4.2).
            // 이동을 대신하는 것은 외부 견인이다 (집게 단계별 파지 계획 §3.5 — 동료가 끌어온다).
            // 시선은 남긴다: 끌려가는 동안 주변을 볼 수 없으면 구조가 사고처럼 보인다.
            if (_movementState.Value != PlayerMovementState.Normal)
            {
                // 견인과 오르기·좌석이 같은 프레임에 트랜스폼을 다투면 안 된다 (계획 §3.7).
                EndMount();
                DetachLadder();
                CancelMantle();

                if (lookAllowed)
                {
                    UpdateLook();
                }

                UpdateExternalTow();
                return;
            }

            // 좌석 구속 (M7 4차 §2.3) — 사다리·견인과 같은 자리에서 갈린다. 이동·점프·중력·
            // 컨베이어가 전부 물러나고, 남는 것은 사각 안의 시선과 좌석 추종뿐이다.
            if (_mounted)
            {
                DetachLadder();
                CancelMantle();

                if (lookAllowed)
                {
                    UpdateMountedLook();
                }

                UpdateSeatPin();
                UpdateDebugInput();
                return;
            }

            if (lookAllowed)
            {
                UpdateLook();
            }

            UpdateMove();
            UpdateFallBehindWarning();
            UpdateDebugInput();
        }

        /// <summary>
        /// 외부 견인 구동 (집게 단계별 파지 계획 §3.5) — 소유자 로컬에서 앵커를 향해 직접 움직인다.
        /// <b>위치 권위는 그대로 소유자</b>다: 서버는 "누가 끄는가"만 복제하고 여기서 실제 이동이 일어나므로,
        /// 끌리는 쪽 화면에 왕복 지연이 보이지 않는다. 서버는 복제돼 올라온 위치로 도착만 판정한다.
        /// 무엇이 끄는지는 알지 않는다 (<see cref="IExternalTow"/>).
        /// </summary>
        private void UpdateExternalTow()
        {
            if (_externalTow == null || !_externalTow.TryGetTowStep(out Vector3 anchor, out float speed))
            {
                return;
            }

            // 앵커로 곧장 당겨진다 — 수직 성분도 그대로다(끌려 올라간다). 중력·컨베이어는 잠시 물러난다:
            // 끌려가는 동안 지면 밀림까지 겹치면 도착 반경에 못 들어오는 구간이 생긴다.
            Vector3 current = transform.position;
            Vector3 next = Vector3.MoveTowards(current, anchor, speed * Time.deltaTime);
            _characterController.Move(next - current);

            // 견인이 끝나면 낙하부터 다시 시작한다 — 관성이 남아 튀지 않게 속도를 비워 둔다.
            _horizontalVelocity = Vector3.zero;
            _verticalSpeed = 0f;
        }

        private void UpdateLook()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue() * _settings.LookSensitivity;
            transform.Rotate(0f, delta.x, 0f);

            _pitch = Mathf.Clamp(_pitch - delta.y, -_settings.MaxPitch, _settings.MaxPitch);
            if (_cameraPivot != null)
            {
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        private void UpdateMove()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            float x = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float z = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            bool run = keyboard.leftShiftKey.isPressed;

            Vector3 wishDirection = transform.right * x + transform.forward * z;
            if (wishDirection.sqrMagnitude > 1f)
            {
                wishDirection.Normalize();
            }

            // ── 사다리 (계획 §3.5·§3.6) ──
            if (_mantleTimer > 0f)
            {
                UpdateMantle();
                return;
            }

            if (_climbing)
            {
                UpdateLadderClimb(z, keyboard.spaceKey.wasPressedThisFrame);
                return;
            }

            if (_ladder != null
                && !LadderClimbLogic.IsReattachBlocked(Time.time, _ladderReattachAt)
                && LadderClimbLogic.ShouldAttach(
                    true, wishDirection, _ladder.ApproachDirection, false, LadderClimbLogic.DefaultApproachDot))
            {
                AttachLadder();
                return;
            }

            // ── 바다 교각 사다리 (§6.3 ③) ──
            // 수영보다 먼저다 — 물에서 사다리를 잡으면 그쪽이 이긴다(= 상판으로 복귀).
            if (UpdateSeaLadder(z, keyboard))
            {
                return;
            }

            // ── 수영·잠수 (바다 지역 §6) ──
            UpdateSwimState();
            if (_isSwimming)
            {
                UpdateSwim(wishDirection, keyboard);
                return;
            }

            float targetSpeed = (run ? _settings.RunSpeed : _settings.WalkSpeed) * ResolveSpeedMultiplier();

            // 스트리밍 타일 지면은 이음새·회수 순간 isGrounded가 깜빡인다 — 코요테 유예로 접지 상태를 유지해
            // 순간 공중 제어 전환(느려짐)·수직 튐을 막는다.
            if (_characterController.isGrounded)
            {
                _groundGraceTimer = _settings.GroundGraceSeconds;
            }
            else
            {
                _groundGraceTimer -= Time.deltaTime;
            }

            bool grounded = _groundGraceTimer > 0f;

            _horizontalVelocity = PlayerMotor.ComputeHorizontalVelocity(
                _horizontalVelocity, wishDirection * targetSpeed,
                grounded, _settings.AirControlRatio, _settings.AirAcceleration, Time.deltaTime);

            if (grounded)
            {
                _verticalSpeed = -2f;
                if (keyboard.spaceKey.wasPressedThisFrame)
                {
                    _verticalSpeed = PlayerMotor.GetJumpSpeed(_settings.JumpHeight, _settings.Gravity);
                    _groundGraceTimer = 0f;
                }
            }
            else
            {
                _verticalSpeed -= _settings.Gravity * Time.deltaTime;
            }

            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalSpeed) * Time.deltaTime;

            // 접지 프레임에 밟고 있는 표면을 기억한다 — 공중에서는 이 값을 유지해 이륙 당시의 기준 프레임을 이어간다.
            if (grounded)
            {
                ProbeGround();
            }

            // 상시 외력형 (§4.2): 지상(월드 프레임) 위에서는 스크롤 속도만큼 로컬로 뒤로 밀린다. RPC 없음.
            // 점프 중에도 이륙한 표면이 지상이면 계속 밀어야 제자리 점프가 열차와 함께 떠내려가지 않는다.
            if (_standingOnWorldFrame && ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                motion += Vector3.back * (scroll.ScrollSpeed * Time.deltaTime);
            }

            // 이탈 칸 지붕에 서 있으면 칸이 실제 이동한 만큼(위치 델타) 함께 실려 간다(무빙 플랫폼).
            // 속도가 아닌 실제 이동량을 쓰므로 dt 스파이크·네트워크 틱 점프에도 칸과 어긋나지 않는다. 정지 칸은 델타 0.
            if (_ridingCar != null)
            {
                Vector3 carPosition = _ridingCar.transform.position;
                if (_ridingCarTracked)
                {
                    motion += carPosition - _ridingCarLastPos;
                }

                _ridingCarLastPos = carPosition;
                _ridingCarTracked = true;
            }
            else
            {
                _ridingCarTracked = false;
            }

            _characterController.Move(motion);
        }

        // ── 바다 교각 사다리 (바다 지역 구현 계획 §6.3 ③) ─────────────────────────

        private const float SeaLadderBlockSeconds = 0.6f;

        // 이 거리 안이면 붙는 보정을 하지 않는다 — 매 프레임 미세 보정이 곧 떨림이다.
        private const float SeaLadderHoldDeadZone = 0.04f;

        // 남은 오차를 한 프레임에 좁히는 비율. 1이면 즉시 붙지만 넘기면 진동한다.
        private const float SeaLadderHoldDamping = 0.35f;

        // 이보다 큰 이동량은 사다리가 바뀐 것으로 본다 (스크롤 6 m/s × dt 는 0.1 m 남짓).
        private const float SeaLadderMaxFollowStep = 2f;

        /// <summary>
        /// 바다 사다리 처리. 붙어 있으면 true 를 돌려 <b>이 프레임의 이동을 여기서 끝낸다</b>.
        ///
        /// <para><b>열차 사다리와 결정적으로 다른 점</b>: 스크롤 속도를 읽어 컨베이어를 계산하지 않고
        /// <b>사다리가 실제로 움직인 양</b>을 따라간다. 속도를 몰라도 정확히 붙어 있고,
        /// 경로가 몇 개든 <b>한 군데서 끝난다</b> — 열차 사다리를 재사용하며 붙기·오르기·올라서기
        /// 세 곳에 각각 컨베이어를 실어야 했던 것이 일곱 번의 실패를 낳았다.</para>
        /// </summary>
        private bool UpdateSeaLadder(float verticalInput, Keyboard keyboard)
        {
            if (_seaLadder == null)
            {
                _onSeaLadder = false;
                _seaLadderTracked = false;
                return false;
            }

            // 붙기 — 볼륨 안이어도 오르려는 입력이 있어야 잡는다.
            // 그냥 지나가려던 사람이 붙잡히면 통로가 좁아 더 답답하다.
            if (!_onSeaLadder)
            {
                if (Time.time < _seaLadderBlockedUntil || Mathf.Abs(verticalInput) < 0.1f)
                {
                    return false;
                }

                _onSeaLadder = true;
                _seaLadderTracked = false;
                _verticalSpeed = 0f;
                _horizontalVelocity = Vector3.zero;
                _isSwimming = false;
                _ridingCar = null;
                _ridingCarTracked = false;
                _standingOnWorldFrame = false;   // 이동은 아래 follow 가 전담한다
                EndMount();
                DetachLadder();
                CancelMantle();
            }

            // 떼기 — 점프로 놓는다. 물에서 잡았으니 놓으면 다시 물이다.
            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                ExitSeaLadder(false);
                return false;
            }

            Vector3 ladderPosition = _seaLadder.transform.position;

            // ① 사다리가 이번 프레임 움직인 만큼 그대로 따라간다.
            //    속도가 아니라 **실제 이동량**이라 dt 스파이크·네트워크 틱에도 어긋나지 않는다.
            Vector3 follow = _seaLadderTracked ? ladderPosition - _seaLadderLastPos : Vector3.zero;

            // 참조가 **다른 사다리로 옮겨간** 프레임에는 이전 위치와 비교하면 큰 점프가 나온다.
            // 그 프레임만 따라가지 않고 넘긴다 — 다음 프레임부터 정상 추종한다.
            if (World.SeaLadderMotion.IsFollowJump(follow, SeaLadderMaxFollowStep))
            {
                follow = Vector3.zero;
            }

            _seaLadderLastPos = ladderPosition;
            _seaLadderTracked = true;

            // ② 사다리 앞면에 붙는 수평 보정.
            //    **이동량을 반영한 뒤** 재야 한다 — Origin 은 이미 이번 프레임 위치라
            //    이전 위치로 재면 델타가 두 번 들어가 과보정된다.
            //    그리고 오차 전부를 한 번에 없애지 않는다 — 조금만 넘겨도 반대편으로 넘어가
            //    다음 프레임에 되돌아오며 **좌우로 떨린다**. 데드존 + 부분 수렴으로 흡수한다.
            Vector3 predicted = transform.position + follow;
            Vector3 rawHold = World.SeaLadderMotion.HoldCorrection(
                predicted, _seaLadder.Origin, _seaLadder.Outward, _seaLadder.HoldDistance);
            Vector3 hold = World.SeaLadderMotion.SmoothCorrection(
                rawHold, SeaLadderHoldDeadZone, SeaLadderHoldDamping);

            // ③ 오르내리기.
            float climb = World.SeaLadderMotion.ClimbVelocity(verticalInput, _seaLadder.ClimbSpeed)
                * Time.deltaTime;

            _characterController.Move(follow + hold + Vector3.up * climb);

            // ④ 꼭대기 — 안쪽으로 밀어 넣고 놓는다. 밀어 넣지 않으면 캡슐 절반이 허공이라 미끄러진다.
            if (World.SeaLadderMotion.HasReachedTop(transform.position.y, _seaLadder.TopY))
            {
                Vector3 exit = World.SeaLadderMotion.ExitPosition(
                    _seaLadder.Origin, _seaLadder.Outward,
                    _seaLadder.HoldDistance, _seaLadder.ExitInward, _seaLadder.TopY);

                _characterController.Move(exit - transform.position);
                ExitSeaLadder(true);
                return true;
            }

            // ⑤ 밑으로 빠졌다 — 계속 붙잡으면 잠수가 막힌다.
            if (World.SeaLadderMotion.HasFallenBelow(transform.position.y, _seaLadder.BottomY))
            {
                ExitSeaLadder(false);
            }

            return true;
        }

        /// <summary>
        /// 사다리를 놓는다. <paramref name="blockReattach"/>면 잠깐 다시 잡히지 않게 한다 —
        /// 올라선 자리가 볼륨과 겹쳐 있어도 곧바로 재부착되지 않도록.
        /// </summary>
        private void ExitSeaLadder(bool blockReattach)
        {
            _onSeaLadder = false;
            _seaLadderTracked = false;
            _verticalSpeed = 0f;
            _groundGraceTimer = 0f;

            if (blockReattach)
            {
                _seaLadderBlockedUntil = Time.time + SeaLadderBlockSeconds;
                _seaLadder = null;
            }
        }

        // ── 수영·잠수 (바다 지역 구현 계획 §6) ─────────────────────────

        /// <summary>
        /// 지역의 물면 높이. 물이 없는 지역(숲·사막·대초원·북극)에서는 false —
        /// 그 지역들은 <c>HasWater</c>가 꺼져 있어 이 경로가 통째로 비활성이다.
        /// </summary>
        private static bool TryGetWaterSurfaceY(out float waterSurfaceY)
        {
            waterSurfaceY = 0f;

            if (!ServiceLocator.TryGet(out IRegionService region))
            {
                return false;
            }

            RegionDefinition definition = region.CurrentRegion;
            if (definition == null || !definition.HasWater)
            {
                return false;
            }

            waterSurfaceY = definition.WaterSurfaceY;
            return true;
        }

        /// <summary>발 높이와 물면에서 수영 여부·잠김 깊이를 유도한다. 복제 없음.</summary>
        private void UpdateSwimState()
        {
            if (!TryGetWaterSurfaceY(out float waterSurfaceY))
            {
                _isSwimming = false;
                _submergeDepth = 0f;
                return;
            }

            float footY = transform.position.y;
            _submergeDepth = SwimMotion.SubmergeDepth(footY, waterSurfaceY);
            _isSwimming = SwimMotion.IsSwimming(
                footY, waterSurfaceY, _isSwimming, _settings.SwimEnterDepth, _settings.SwimExitDepth);
        }

        /// <summary>
        /// 물속 이동. 중력 대신 부력이 작동하고, 컨베이어는 <b>깊이에 따라 약해진다</b> —
        /// 이 감쇠가 없으면 수영 속도가 스크롤을 못 이겨 뛰어드는 순간 복귀가 불가능해진다 (§6.1).
        /// </summary>
        private void UpdateSwim(Vector3 wishDirection, Keyboard keyboard)
        {
            int verticalInput = 0;
            if (keyboard.spaceKey.isPressed)
            {
                verticalInput = 1;
            }
            else if (keyboard.leftCtrlKey.isPressed)
            {
                verticalInput = -1;
            }

            _horizontalVelocity = wishDirection * _settings.SwimSpeed;
            _verticalSpeed = SwimMotion.ComputeVerticalSpeed(
                _submergeDepth, verticalInput,
                _settings.SwimVerticalSpeed, _settings.SwimBuoyancySpeed, _settings.SwimEnterDepth);

            Vector3 motion = (_horizontalVelocity + Vector3.up * _verticalSpeed) * Time.deltaTime;

            // 물은 월드 소속이라 접지와 무관하게 항상 밀린다 — 지상 컨베이어와 같은 상시 외력형(§4.2).
            if (ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                float dragFactor = SwimMotion.ScrollFactor(
                    _submergeDepth, _settings.SwimDragStartDepth,
                    _settings.SwimDragFullDepth, _settings.SubmergedScrollFactor);

                motion += Vector3.back * (scroll.ScrollSpeed * dragFactor * Time.deltaTime);
            }

            // 물에 있는 동안은 갑판·이탈 칸 추종을 놓는다 — 같은 프레임에 트랜스폼을 다투면 안 된다.
            _ridingCar = null;
            _ridingCarTracked = false;
            _groundGraceTimer = 0f;

            _characterController.Move(motion);
        }

        // ── 사다리 오르기 (사다리 승하차 계획 §3) ─────────────────────────

        private void OnTriggerEnter(Collider other)
        {
            if (!IsOwner)
            {
                return;
            }

            var ladder = other.GetComponent<Train.BoardingLadder>();
            if (ladder != null)
            {
                _ladder = ladder;
            }

            var seaLadder = other.GetComponent<World.SeaLadder>();
            if (seaLadder != null && Time.time >= _seaLadderBlockedUntil)
            {
                _seaLadder = seaLadder;
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (!IsOwner)
            {
                return;
            }

            var ladder = other.GetComponent<Train.BoardingLadder>();
            if (ladder != null && ladder == _ladder)
            {
                _ladder = null;
            }

            var seaLadder = other.GetComponent<World.SeaLadder>();
            if (seaLadder != null && seaLadder == _seaLadder)
            {
                _seaLadder = null;
                _onSeaLadder = false;
                _seaLadderTracked = false;
            }
        }

        /// <summary>
        /// 사다리에 붙는다. <b>여기서 컨베이어 밀림을 끄는 것이 이 기능의 핵심</b>이다 (계획 §3.1) —
        /// 안 끄면 붙자마자 몸이 초당 6 m로 뒤로 흘러 사다리를 뚫고 나간다.
        /// </summary>
        private void AttachLadder()
        {
            _climbing = true;

            // 낙하 속도를 끌고 들어가면 붙자마자 미끄러진다.
            _verticalSpeed = 0f;
            _horizontalVelocity = Vector3.zero;

            // 지형에 붙은 사다리(바다 교각)는 뒤로 흐른다 — 매달린 사람도 같이 밀려야
            // 상대 위치가 유지된다. 열차 사다리는 정지 프레임이라 종전대로 false.
            _standingOnWorldFrame = _ladder != null && _ladder.IsWorldFrame;
            _groundGraceTimer = 0f;
            _ridingCar = null;
            _ridingCarTracked = false;

            Vector3 snap = LadderClimbLogic.ResolvePlaneCorrection(
                transform.position, _ladder.Origin, _ladder.Normal, _ladder.HoldDistance);
            _characterController.Move(snap);
        }

        private void DetachLadder()
        {
            if (!_climbing)
            {
                return;
            }

            _climbing = false;

            // 볼륨 이탈 콜백이 한 프레임 늦게 오므로, 그 사이 오르던 입력이 그대로면 곧바로 다시 붙는다.
            // 그러면 평면 보정이 몸을 사다리 앞으로 되돌려 꼭대기에서 1 m 왕복이 생긴다.
            _ladderReattachAt = Time.time + LadderReattachDelay;

            // 떨어진 직후를 접지로 오인하면 공중에서 걷는 속도가 나온다.
            _groundGraceTimer = 0f;
        }

        private void UpdateLadderClimb(float verticalInput, bool jumpPressed)
        {
            // 볼륨이 사라졌다 = 사다리가 없어졌다 (칸 이탈·파괴).
            if (_ladder == null)
            {
                DetachLadder();
                return;
            }

            // transform.position.y 가 곧 발 높이다 — CharacterController center (0, 0.9, 0) · height 1.8.
            LadderDetachReason reason = LadderClimbLogic.ResolveDetach(
                transform.position.y, _ladder.BottomY, _ladder.TopY, jumpPressed, true);

            if (reason == LadderDetachReason.Jump)
            {
                Vector3 jumpOff = LadderClimbLogic.ComputeJumpOffVelocity(
                    _ladder.Normal, LadderJumpPushSpeed,
                    PlayerMotor.GetJumpSpeed(_settings.JumpHeight, _settings.Gravity) * LadderJumpUpRatio);

                _horizontalVelocity = new Vector3(jumpOff.x, 0f, jumpOff.z);
                _verticalSpeed = jumpOff.y;
                DetachLadder();
                return;
            }

            if (reason == LadderDetachReason.TopReached)
            {
                BeginMantle();
                DetachLadder();

                // 올라서기를 시작했으면 이 사다리와는 끝이다 — 참조를 여기서 끊는다.
                // 열차 갑판은 넓어 올라서면 볼륨 밖으로 나가고 Exit 콜백이 참조를 비워 주지만,
                // 바다 통로는 1.15 m뿐이라 <b>올라선 자리가 여전히 볼륨 안</b>이다.
                // 그래서 Exit 가 오지 않고, 재부착 차단(0.3초)이 풀리는 순간 다시 매달린다.
                // 다시 잡으려면 볼륨을 나갔다 들어오면 된다 — 물로 뛰어들면 자연히 그렇게 된다.
                _ladder = null;
                return;
            }

            if (reason == LadderDetachReason.BottomReached || reason == LadderDetachReason.LeftVolume)
            {
                DetachLadder();
                return;
            }

            Vector3 correction = LadderClimbLogic.ResolvePlaneCorrection(
                transform.position, _ladder.Origin, _ladder.Normal, _ladder.HoldDistance);

            // 사다리가 통째로 옮겨갔다 — 따라가면 사람이 순간이동한다 (계획 §6).
            if (LadderClimbLogic.IsPlaneCorrectionTooFar(correction, LadderMaxPlaneCorrection))
            {
                DetachLadder();
                return;
            }

            Vector3 motion = LadderClimbLogic.ComputeClimbMotion(
                verticalInput, _ladder.ClimbSpeed, Time.deltaTime) + correction;

            // 월드 소속 사다리(바다 교각)는 오르는 동안에도 뒤로 흐른다.
            // 이 경로는 아래 일반 이동으로 내려가지 않고 여기서 끝나므로(_climbing 분기가 return한다)
            // **컨베이어를 여기서 직접 실어야 한다** — 안 실으면 매달린 사람만 제자리에 남아
            // 사다리가 빠져나가고, 볼륨을 벗어나 떨어졌다가 다시 붙는 것이 반복된다.
            if (_ladder.IsWorldFrame && ServiceLocator.TryGet(out IWorldScrollService climbScroll))
            {
                motion += Vector3.back * (climbScroll.ScrollSpeed * Time.deltaTime);
            }

            _characterController.Move(motion);
        }

        /// <summary>
        /// 꼭대기에서 갑판으로 올려놓기를 시작한다 (계획 §3.8) — 그냥 놓으면 갑판 <b>옆</b> 허공이라 떨어진다.
        ///
        /// <para><b>수직은 즉시, 수평은 나눠서.</b> 발을 갑판면에 맞추는 것은 몇 cm라 한 프레임에 해도
        /// 안 보이지만, 갑판 안쪽으로 1 m를 한 프레임에 옮기면 1인칭 카메라가 툭 튄다.
        /// 한 벡터로 합쳐 주지 않는 이유이기도 하다 — 대각선 이동은 갑판 모서리에 걸린다.</para>
        /// </summary>
        private void BeginMantle()
        {
            Vector3 mantle = LadderClimbLogic.ComputeMantleMotion(
                _ladder.Normal, transform.position.y, _ladder.TopY,
                _ladder.MantleInwardDistance, LadderMantleClearance);

            _characterController.Move(new Vector3(0f, mantle.y, 0f));

            Vector3 horizontal = new Vector3(mantle.x, 0f, mantle.z);
            _mantleTimer = LadderMantleDuration;
            _mantleVelocity = horizontal / LadderMantleDuration;

            // 월드 소속 사다리(바다 교각)에서는 올라설 상판도 함께 흐른다.
            // 이걸 안 실으면 오르는 동안 발판이 뒤로 빠져나가 **허공에 내려선다.**
            _mantleWorldFrame = _ladder != null && _ladder.IsWorldFrame;

            _verticalSpeed = 0f;
            _horizontalVelocity = Vector3.zero;
        }

        /// <summary>올려놓기를 중단한다 — 구속·사망·순간이동이 끼어들면 남은 이동을 버린다.</summary>
        private void CancelMantle()
        {
            _mantleTimer = 0f;
            _mantleVelocity = Vector3.zero;
            _mantleWorldFrame = false;
        }

        /// <summary>올려놓기 진행 — 끝날 때까지 일반 이동·중력을 멈춘다. 중력이 끼면 모서리에서 미끄러진다.</summary>
        private void UpdateMantle()
        {
            float step = Mathf.Min(Time.deltaTime, _mantleTimer);
            _mantleTimer -= step;

            Vector3 motion = _mantleVelocity * step;

            // 월드 소속 발판은 오르는 사이에도 흐른다 — 같이 밀려야 상판 위에 내려선다.
            if (_mantleWorldFrame && ServiceLocator.TryGet(out IWorldScrollService mantleScroll))
            {
                motion += Vector3.back * (mantleScroll.ScrollSpeed * step);
            }

            _characterController.Move(motion);

            if (_mantleTimer <= 0f)
            {
                _mantleTimer = 0f;
                _mantleVelocity = Vector3.zero;
                _mantleWorldFrame = false;
                _verticalSpeed = -2f;
            }
        }

        /// <summary>접지 표면 판정 — 지상(월드 프레임) 여부와 밟고 있는 칸(무빙 플랫폼)을 함께 갱신한다.</summary>
        private void ProbeGround()
        {
            _standingOnWorldFrame = false;
            CarView car = null;

            // 전체 히트를 모아 최근접 면과 거의 같은 높이의 칸(CarView)이 있으면 칸을 우선한다.
            // 이탈 칸이 승차 램프 등 정적 지형과 같은 평면으로 겹치는 구간에서 정적 면이 몇 cm 먼저
            // 맞더라도 무빙 플랫폼 추적이 끊기지 않게 하기 위함이다(끊기면 칸만 떠나고 플레이어가 남는다).
            Vector3 origin = transform.position + Vector3.up * 0.1f;
            float maxDistance = _characterController.height * 0.5f + 0.4f;
            int count = Physics.RaycastNonAlloc(
                origin, Vector3.down, GroundProbeHits, maxDistance, ~0, QueryTriggerInteraction.Ignore);

            float closest = float.PositiveInfinity;
            Collider closestCollider = null;
            float carDistance = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = GroundProbeHits[i];
                if (candidate.distance < closest)
                {
                    closest = candidate.distance;
                    closestCollider = candidate.collider;
                }

                if (candidate.distance < carDistance)
                {
                    CarView candidateCar = candidate.collider.GetComponentInParent<CarView>();
                    if (candidateCar != null)
                    {
                        carDistance = candidate.distance;
                        car = candidateCar;
                    }
                }
            }

            // 허용 오차보다 확실히 아래에 있는 칸은 실제 지지면이 아니다(예: 램프 중턱 아래로 칸이 지나가는 경우).
            if (car == null || carDistance > closest + CoplanarSurfaceTolerance)
            {
                car = null;
                _standingOnWorldFrame = closestCollider != null
                    && closestCollider.GetComponentInParent<WorldFrameSurface>() != null;
            }

            // 다른 칸으로 옮겨 탔거나 칸에서 내려오면 위치 델타 추적을 리셋한다(엉뚱한 큰 델타 방지).
            if (car != _ridingCar)
            {
                _ridingCarTracked = false;
            }

            _ridingCar = car;
        }

        private void UpdateFallBehindWarning()
        {
            if (_trainLayout == null)
            {
                return;
            }

            float metersBehindRear = _trainLayout.RearZ - transform.position.z;
            if (metersBehindRear >= _trainLayout.RearZ - _trainLayout.WarningZ)
            {
                EventBus<FallBehindWarningLocalEvent>.Publish(new FallBehindWarningLocalEvent(metersBehindRear));
            }
        }

        private void UpdateDebugInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f9Key.wasPressedThisFrame)
            {
                // 슬라이스 §4.3 — 구속형 상태 전환 RPC 경로 디버그 검증.
                PlayerMovementState next = _movementState.Value == PlayerMovementState.Normal
                    ? PlayerMovementState.Grabbed
                    : PlayerMovementState.Normal;
                DebugSetMovementStateServerRpc(next);
            }
        }

        // ── 호스트 권위: 이탈 사망 확정 (§4.2) ─────────────────────────────

        private void ServerCheckFallBehind()
        {
            if (_trainLayout == null || _respawnPending.Value)
            {
                return;
            }

            // 구조가 이탈 사망을 이긴다 (집게 단계별 파지 계획 §3.5) — 뒤처진 동료를 집게로 끌어올리는
            // 중이라면 사망선 판정을 미룬다. 여기서 죽이면 "구해내는 중이었는데 죽었다"가 되어
            // 동료 그랩의 존재 이유가 사라진다. 놓치면 다음 프레임에 곧바로 판정이 돌아온다.
            if (_externalTow != null && _externalTow.IsTowed)
            {
                return;
            }

            if (transform.position.z < _trainLayout.DeathZ)
            {
                _respawnPending.Value = true;

                // 대기 시간은 전투 사망과 같은 Day 비례 계산 (M6 3차 결정 ① — 일원화).
                float delaySeconds = _health != null ? _health.ServerComputeRespawnDelaySeconds() : 5f;

                // 사망 확정 시각 기록 (M6 1차 결정 ⑦) — 재접속 시 잔여 대기 계산의 근거.
                if (_health != null)
                {
                    _health.ServerRecordDeath(delaySeconds);
                }

                NotifyFellBehindRpc(OwnerClientId);
                BeginRespawnOwnerRpc(_trainLayout.RespawnPosition, delaySeconds);
            }
        }

        /// <summary>권위 이벤트 전파 — 호스트 확정 후 전 피어에서 발행된다.</summary>
        [Rpc(SendTo.Everyone)]
        private void NotifyFellBehindRpc(ulong clientId)
        {
            EventBus<PlayerFellBehindEvent>.Publish(new PlayerFellBehindEvent(clientId));
        }

        [Rpc(SendTo.Owner)]
        private void BeginRespawnOwnerRpc(Vector3 respawnPosition, float delaySeconds)
        {
            BeginOwnerRespawn(respawnPosition, delaySeconds);
        }

        /// <summary>
        /// 소유자 부활 절차 — 대기 후 지정 위치로 복귀한다. 이탈 사망(내부)과 전투 사망(PlayerHealth, M2)이
        /// 같은 흐름을 재사용한다. 대기 중에는 입력이 정지된다. 소유자에서만 동작한다.
        /// </summary>
        public void BeginOwnerRespawn(Vector3 respawnPosition, float delaySeconds, System.Action onCompleted = null)
        {
            if (IsOwner && !_respawning)
            {
                StartCoroutine(RespawnRoutine(respawnPosition, delaySeconds, onCompleted));
            }
        }

        private IEnumerator RespawnRoutine(Vector3 respawnPosition, float delaySeconds, System.Action onCompleted)
        {
            _respawning = true;
            yield return new WaitForSeconds(delaySeconds);

            // 종단 가드 (M6 3차 결정 ② — H5 재검): 대기 중 게임오버가 확정되면 부활을 중단한다.
            // 서버 가드(Revive·RespawnComplete 무시)만으로는 부족하다 — 카운트다운·이동 입력이
            // 소유자 로컬(_respawning)이라, 여기서 끊지 않으면 소유자만 되살아나 움직인다.
            // _respawning을 유지해 입력 정지도 계속된다 (게임오버 화면이 복귀를 담당).
            if (ServiceLocator.TryGet(out Session.GameOverMonitor gameOver) && gameOver.IsGameOver)
            {
                yield break;
            }

            TeleportTo(respawnPosition);
            _horizontalVelocity = Vector3.zero;
            _verticalSpeed = 0f;
            _respawning = false;
            RespawnCompleteServerRpc();
            onCompleted?.Invoke();
        }

        [Rpc(SendTo.Server)]
        private void RespawnCompleteServerRpc()
        {
            // 종단 가드 (M6 3차 결정 ②): 게임오버 확정 후에는 부활 완료를 무시한다 —
            // PlayerHealth.ReviveServerRpc의 가드와 짝이다.
            if (ServiceLocator.TryGet(out Session.GameOverMonitor gameOver) && gameOver.IsGameOver)
            {
                return;
            }

            _respawnPending.Value = false;
        }

        // ── 상태 머신 전환 (호스트 확정, §4.2) ─────────────────────────────

        /// <summary>
        /// 서버 전용 — 호스트 개입 상태 확정 (§4.2). 외부 힘에 끌리는 구간의 진입·복귀가 첫 사용처다
        /// (집게 단계별 파지 계획 §3.5). 상태를 바꾸는 쪽이 <b>되돌리는 책임도 진다</b> —
        /// 여기서는 값만 확정하고 정책은 호출자가 갖는다.
        /// </summary>
        public void ServerSetMovementState(PlayerMovementState state)
        {
            if (IsServer)
            {
                _movementState.Value = state;
            }
        }

        [Rpc(SendTo.Server)]
        private void DebugSetMovementStateServerRpc(PlayerMovementState state)
        {
            _movementState.Value = state;
            GameLog.Info(LogCategory.Player, $"디버그 상태 전환 확정: client={OwnerClientId} state={state}");
        }

        // ── 재접속 위치 복원 (M6 결정 ① 개정 — 2026-08-13 사용자 승인 ⓐ) ──────────

        /// <summary>
        /// 끊김 위치 복원 — 서버 전용, 재접속 적용 훅이 부른다. <b>살아있는 칸의 갑판 위</b>는
        /// 그 자리로, 그 외(지상·칸 소실 등)도 <b>사망선(후미 40 m) 앞이면</b> 그 자리로
        /// 소유자에게 배치를 지시한다 (M6 결정 ① 재개정 — 2차 검증 D3 사용자 수정 요청
        /// 2026-08-13: "지상에서 끊겨도 지상 위치에서 부활"). 이탈 중인 칸 위와 사망선 뒤만
        /// 스폰 지점 폴백이다 — 복원 직후 이탈 사망으로 이어질 자리라서다.
        /// 위치는 소유자 권위(OwnerNetworkTransform)라 서버가 직접 옮길 수 없다 — RPC 지시다.
        /// </summary>
        public void ServerRestorePosition(Vector3 position)
        {
            if (!IsServer || !ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            // 살아있는 칸의 갑판 판정 (M5 7차 A3 프레임 판정 재사용 — 이탈 오프셋 반영).
            if (train.TryGetDeckSurface(position, out float deckHeight, out int carIndex))
            {
                // 이탈 중(뒤로 밀려나는) 칸은 제외 — 복원 직후 후미 이탈 사망으로 이어질 자리다.
                if (train.GetEjectOffset(carIndex) > 0f)
                {
                    return;
                }

                // 공중(점프 중) 캡처는 그대로 떨어뜨리되, 갑판 아래로는 들어가지 않게 받친다.
                position.y = Mathf.Max(position.y, deckHeight + 0.1f);
                RestorePlacementOwnerRpc(position);
                return;
            }

            // 갑판 밖(지상·칸 소실로 공중) — 사망선 앞이면 끊긴 자리로. 지상은 복원 즉시
            // 스크롤 밀림이 재개되므로 그 자리의 긴장(추격 복귀)까지 그대로 이어진다.
            if (_trainLayout == null || position.z <= _trainLayout.DeathZ)
            {
                return;
            }

            position.y = Mathf.Max(position.y, 0.1f);
            RestorePlacementOwnerRpc(position);
        }

        [Rpc(SendTo.Owner)]
        private void RestorePlacementOwnerRpc(Vector3 position)
        {
            if (_needsInitialPlacement)
            {
                // 초기 배치(Game 씬 도착 후 첫 Update)가 아직이면 그때 이 위치를 쓴다.
                _restorePlacement = position;
                _hasRestorePlacement = true;
                return;
            }

            TeleportTo(position);
            _horizontalVelocity = Vector3.zero;
            _verticalSpeed = 0f;
        }

        /// <summary>현재 접속자 목록에서의 위치 = 접속 순번. 스폰 승인 직전 AddClient가 끝나 목록에 있다.</summary>
        private int ResolveSpawnOrder()
        {
            var ids = NetworkManager.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == OwnerClientId)
                {
                    return i;
                }
            }

            return ids.Count;
        }

        // ── 거치 무기 좌석 구속 (M7 4차 §2.3) ─────────────────────────────

        /// <summary>
        /// 좌석에 앉힌다 — 점유 승인이 복제돼 내려온 뒤 조작 계층이 호출한다.
        /// 첫 배치만 충돌을 무시하고 통째로 옮기고(구조물 콜라이더에 걸려 좌석 밖에 서는 것을 막는다),
        /// 이후 좌석 추종은 프레임당 미세 이동이라 일반 이동 경로를 그대로 쓴다.
        /// </summary>
        public void BeginMount(
            Transform seat, Quaternion mountRotation, float yawLimit, float pitchMin, float pitchMax)
        {
            if (!IsOwner || seat == null)
            {
                return;
            }

            _mountSeat = seat;
            _mountRotation = mountRotation;
            _mountYawLimit = yawLimit;
            _mountPitchMin = pitchMin;
            _mountPitchMax = pitchMax;
            _mountYaw = 0f;
            _mountPitch = 0f;
            _mounted = true;

            DetachLadder();
            CancelMantle();
            _ladder = null;
            _horizontalVelocity = Vector3.zero;
            _verticalSpeed = 0f;

            _characterController.enabled = false;
            transform.position = seat.position;
            transform.rotation = _mountRotation;
            _characterController.enabled = true;

            _pitch = 0f;
            if (_cameraPivot != null)
            {
                _cameraPivot.localRotation = Quaternion.identity;
            }
        }

        /// <summary>
        /// 좌석에서 내린다 — 자발적 하차와 강제 하차(파괴·사망·끊김)가 같은 경로다.
        /// 몸은 좌석 옆 갑판으로 반 걸음 물러난다: 그대로 두면 다음 프레임 중력이 포신 안에서 시작한다.
        /// </summary>
        public void EndMount()
        {
            if (!_mounted)
            {
                return;
            }

            Vector3 exit = transform.position - transform.forward * 0.8f;
            _mounted = false;
            _mountSeat = null;
            _horizontalVelocity = Vector3.zero;
            _verticalSpeed = 0f;

            _characterController.enabled = false;
            transform.position = exit;
            _characterController.enabled = true;
        }

        /// <summary>
        /// 사각 안에서만 도는 시선 — 클램프는 <see cref="MountedAimMath"/>가 소유한다.
        /// 몸(yaw)이 거치대 기준으로 돌고 카메라 피벗이 앙각을 받는다: 화면 좌표계는 아래가 +라
        /// 앙각을 얹을 때만 부호를 뒤집는다.
        /// </summary>
        private void UpdateMountedLook()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            Vector2 delta = mouse.delta.ReadValue() * _settings.LookSensitivity;
            MountedAimMath.Clamp(
                _mountYaw + delta.x, _mountPitch + delta.y,
                _mountYawLimit, _mountPitchMin, _mountPitchMax,
                out _mountYaw, out _mountPitch);

            transform.rotation = _mountRotation * Quaternion.Euler(0f, _mountYaw, 0f);
            _pitch = -_mountPitch;
            if (_cameraPivot != null)
            {
                _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            }
        }

        /// <summary>
        /// 좌석 추종 — 이탈 칸 위 좌석은 칸을 따라 뒤로 흐르므로, 점유자도 함께 끌려가야
        /// 이탈 칸 위 전투가 성립한다 (§2.7). 좌석 실물이 사라지면(파괴·회수) 서버 통지를
        /// 기다리지 않고 즉시 푼다 — 유령 점유의 이중 방어다 (리스크 1).
        /// </summary>
        private void UpdateSeatPin()
        {
            if (_mountSeat == null || !_mountSeat.gameObject.activeInHierarchy)
            {
                EndMount();
                return;
            }

            Vector3 delta = _mountSeat.position - transform.position;
            if (delta.sqrMagnitude > 0.000001f)
            {
                _characterController.Move(delta);
            }
        }

        private void TeleportTo(Vector3 position)
        {
            // 사망·부활·재접속 복원으로 몸이 통째로 옮겨간다 — 사다리에 매달린 상태를 끌고 가면
            // 다음 프레임 평면 보정이 옛 사다리로 되당긴다.
            // 몸이 통째로 옮겨간다 — 좌석 구속을 끌고 가면 다음 프레임 추종이 옛 좌석으로 되당긴다.
            EndMount();
            DetachLadder();
            CancelMantle();
            _ladder = null;

            _characterController.enabled = false;
            transform.position = position;
            _characterController.enabled = true;
        }
    }
}
