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

        private CharacterController _characterController;
        private Vector3 _horizontalVelocity;
        private float _verticalSpeed;
        private float _pitch;
        private bool _respawning;
        private bool _serverDeathPending;
        private bool _needsInitialPlacement;
        private bool _inventoryPanelOpen;
        private bool _standingOnWorldFrame;
        private CarView _ridingCar;
        private Vector3 _ridingCarLastPos;
        private bool _ridingCarTracked;
        private float _groundGraceTimer;

        public PlayerMovementState MovementState => _movementState.Value;

        /// <summary>
        /// 접지 프레임 기준 — 현재 지상(월드 프레임) 위에 서 있는지. 공중에서는 이륙 당시 값을 유지한다.
        /// 발사체가 발사 시점의 기준 프레임(정지=열차 위 / 스크롤=지상)을 이어받는 데 쓴다.
        /// </summary>
        public bool StandingOnWorldFrame => _standingOnWorldFrame;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        public override void OnNetworkSpawn()
        {
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
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsOwner)
            {
                EventBus<InventoryPanelToggledLocalEvent>.Unsubscribe(OnInventoryPanelToggled);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }

        /// <summary>I 창 토글 (기획서 §3.4) — 열려 있는 동안 시점 회전을 멈추고 커서를 드래그용으로 푼다.</summary>
        private void OnInventoryPanelToggled(InventoryPanelToggledLocalEvent evt)
        {
            _inventoryPanelOpen = evt.IsOpen;
            Cursor.lockState = evt.IsOpen ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = evt.IsOpen;
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
                TeleportTo(_trainLayout != null
                    ? _trainLayout.GetSpawnPosition((int)OwnerClientId)
                    : new Vector3(0f, 4f, 0f));
                _horizontalVelocity = Vector3.zero;
                _verticalSpeed = 0f;
            }

            // 구속형 상태(Grabbed/Carried)에서는 소유자 입력을 정지한다 — 호스트 구동 (§4.2).
            if (_movementState.Value != PlayerMovementState.Normal)
            {
                return;
            }

            if (!_inventoryPanelOpen)
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

        /// <summary>접지 표면을 한 번의 레이로 판정 — 지상(월드 프레임) 여부와 밟고 있는 칸(무빙 플랫폼)을 함께 갱신한다.</summary>
        private void ProbeGround()
        {
            _standingOnWorldFrame = false;
            CarView car = null;

            Vector3 origin = transform.position + Vector3.up * 0.1f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit,
                    _characterController.height * 0.5f + 0.4f, ~0, QueryTriggerInteraction.Ignore))
            {
                _standingOnWorldFrame = hit.collider.GetComponentInParent<WorldFrameSurface>() != null;
                car = hit.collider.GetComponentInParent<CarView>();
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
            if (_trainLayout == null || _serverDeathPending)
            {
                return;
            }

            if (transform.position.z < _trainLayout.DeathZ)
            {
                _serverDeathPending = true;
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
            _serverDeathPending = false;
        }

        // ── 상태 머신 전환 (호스트 확정, §4.2) ─────────────────────────────

        [Rpc(SendTo.Server)]
        private void DebugSetMovementStateServerRpc(PlayerMovementState state)
        {
            _movementState.Value = state;
            Debug.Log($"[NetworkPlayerController] 디버그 상태 전환 확정: client={OwnerClientId} state={state}");
        }

        private void TeleportTo(Vector3 position)
        {
            _characterController.enabled = false;
            transform.position = position;
            _characterController.enabled = true;
        }
    }
}
