using System.Collections;
using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Inventory;
using Game.Gameplay.Train;
using Game.Gameplay.World;
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

        private const string GameplaySceneName = "Game";

        // 접지 프로브에서 최근접 정적 면과 칸 지붕을 '같은 평면'으로 간주하는 높이 차(m) —
        // 승차 램프 상단이 지붕보다 최대 ~15cm 높게 겹치는 저작 여유를 흡수한다.
        private const float CoplanarSurfaceTolerance = 0.3f;

        private static readonly RaycastHit[] GroundProbeHits = new RaycastHit[8];

        private CharacterController _characterController;
        private PlayerHealth _health;
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
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _spawnOrder.Value = ResolveSpawnOrder();
            }

            bool isOwner = IsOwner;
            if (_cameraRig != null)
            {
                _cameraRig.SetActive(isOwner);
            }

            if (isOwner)
            {
                // 플레이어는 네트워크 씬 전환 전(Main)에 스폰될 수 있다 — 실제 배치는
                // Game 씬 도착 후 첫 Update에서 수행한다 (열차 지오메트리 위에 착지).
                _needsInitialPlacement = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
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
                if (SceneManager.GetActiveScene().name != GameplaySceneName)
                {
                    return;
                }

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

            // 구속형 상태(Grabbed/Carried)에서는 소유자 입력을 정지한다 — 호스트 구동 (§4.2).
            if (_movementState.Value != PlayerMovementState.Normal)
            {
                return;
            }

            if (!_inventoryPanelOpen && !_sessionMenuOpen && !_craftingPanelOpen && !_storagePanelOpen)
            {
                UpdateLook();
            }

            UpdateMove();
            UpdateFallBehindWarning();
            UpdateDebugInput();
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

            float targetSpeed = run ? _settings.RunSpeed : _settings.WalkSpeed;

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

            if (transform.position.z < _trainLayout.DeathZ)
            {
                _respawnPending.Value = true;

                // 사망 확정 시각 기록 (M6 1차 결정 ⑦) — 재접속 시 잔여 대기 계산의 근거.
                if (_health != null)
                {
                    _health.ServerRecordDeath(_trainLayout.RespawnDelaySeconds);
                }

                NotifyFellBehindRpc(OwnerClientId);
                BeginRespawnOwnerRpc(_trainLayout.RespawnPosition, _trainLayout.RespawnDelaySeconds);
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
            _respawnPending.Value = false;
        }

        // ── 상태 머신 전환 (호스트 확정, §4.2) ─────────────────────────────

        [Rpc(SendTo.Server)]
        private void DebugSetMovementStateServerRpc(PlayerMovementState state)
        {
            _movementState.Value = state;
            Debug.Log($"[NetworkPlayerController] 디버그 상태 전환 확정: client={OwnerClientId} state={state}");
        }

        // ── 재접속 위치 복원 (M6 결정 ① 개정 — 2026-08-13 사용자 승인 ⓐ) ──────────

        /// <summary>
        /// 끊김 위치 복원 — 서버 전용, 재접속 적용 훅이 부른다. 위치가 <b>편성에 붙어 있는
        /// 살아있는 칸의 갑판 위</b>일 때만 소유자에게 배치를 지시하고, 그 외(이탈 칸·지상·
        /// 그 사이 칸이 사라진 경우)는 아무것도 하지 않아 현행 스폰 지점 폴백이 된다.
        /// 위치는 소유자 권위(OwnerNetworkTransform)라 서버가 직접 옮길 수 없다 — RPC 지시다.
        /// </summary>
        public void ServerRestorePosition(Vector3 position)
        {
            if (!IsServer || !ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            // 살아있는 칸의 갑판 판정 (M5 7차 A3 프레임 판정 재사용 — 이탈 오프셋 반영).
            if (!train.TryGetDeckSurface(position, out float deckHeight, out int carIndex))
            {
                return;
            }

            // 이탈 중(뒤로 밀려나는) 칸은 제외 — 복원 직후 후미 이탈 사망으로 이어질 자리다.
            if (train.GetEjectOffset(carIndex) > 0f)
            {
                return;
            }

            // 공중(점프 중) 캡처는 그대로 떨어뜨리되, 갑판 아래로는 들어가지 않게 받친다.
            position.y = Mathf.Max(position.y, deckHeight + 0.1f);
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
