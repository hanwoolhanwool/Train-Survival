using Game.Core.Logging;
using System.Collections;
using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Inventory;
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
        // 승차 램프 상단이 지붕보다 최대 ~15cm 높게 겹치는 저작 여유를 흡수한다.
        private const float CoplanarSurfaceTolerance = 0.3f;

        private static readonly RaycastHit[] GroundProbeHits = new RaycastHit[8];

        private CharacterController _characterController;
        private PlayerHealth _health;
        private IExternalTow _externalTow;
        private IMoveSpeedModifier[] _speedModifiers;
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
                if (lookAllowed)
                {
                    UpdateLook();
                }

                UpdateExternalTow();
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

        private void TeleportTo(Vector3 position)
        {
            _characterController.enabled = false;
            transform.position = position;
            _characterController.enabled = true;
        }
    }
}
