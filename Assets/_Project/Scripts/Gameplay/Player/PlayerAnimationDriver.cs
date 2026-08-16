using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.World;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// Animator 파라미터 구동 — 판정·상태 소유 없음, 읽기 전용 표현 (플레이어 확장 계획 §2.1).
    /// 하이브리드 동기화 (확장 계획 결정 ④):
    /// - <b>Speed</b>: 채널 없음 — 소유자는 <see cref="CharacterController.velocity"/>, 원격은
    ///   보간 위치 델타에서 월드 스크롤 성분을 제거해 각 피어가 로컬 유도한다 (대역폭 0).
    /// - <b>Jump</b>: 소유자 → 서버 → <c>SendTo.NotOwner</c> 중계 1발 — 위치 델타로는
    ///   점프/낙하 구분이 안 되는 단발 동작이라 승격했다 (연출 RPC 규약).
    /// - <b>Dead</b>: 기존 권위 확정(<see cref="PlayerDiedEvent"/> + 복제 체력) 재사용 — 증분 0.
    /// - <b>Grabbed</b>: 파라미터만 선배치 (확장 계획 B축) — 이 차수에서는 구동하지 않는다.
    /// <see cref="NetworkPlayerController"/>는 무수정 — 공개 프로퍼티만 읽는다 (M8 1차 §2.6 계약).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class PlayerAnimationDriver : NetworkBehaviour
    {
        private static readonly int SpeedParam = Animator.StringToHash("Speed");
        private static readonly int JumpParam = Animator.StringToHash("Jump");
        private static readonly int DeadParam = Animator.StringToHash("Dead");

        // 원격 지면 프로브 — 발밑 지지면이 지상(월드 프레임)인지만 본다 (소유자 ProbeGround의 축소판).
        private static readonly RaycastHit[] ProbeHits = new RaycastHit[4];
        private const float ProbeOriginHeight = 0.5f;
        private const float ProbeDistance = 0.9f;

        [SerializeField] private PlayerAnimationSettings _settings;
        [SerializeField] private PlayerMovementSettings _movement;
        [SerializeField] private PlayerCharacterView _view;

        private CharacterController _characterController;
        private NetworkPlayerController _controller;
        private PlayerHealth _health;

        private LocomotionTier _tier;
        private float _smoothedSpeed;
        private Vector3 _remoteLastPosition;
        private bool _hasRemoteLastPosition;
        private bool _remoteOnWorldFrame;
        private JumpDetectState _jumpDetect;

        // 이음새 스파이크(1프레임)와 실제 점프를 가르는 연속 프레임 수 — 감지 지연 2프레임(~30 ms).
        private const int JumpConfirmFrames = 2;

        // 서버 전용 — 점프 중계 상한 창 (확장 계획 §2.2, 4회/s).
        private double _jumpWindowStartTime;
        private int _jumpsInWindow;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
            _controller = GetComponent<NetworkPlayerController>();
            _health = GetComponent<PlayerHealth>();
        }

        public override void OnNetworkSpawn()
        {
            // 권위 이벤트 구독 — 사망 확정 즉시 반응한다. 복제 상태 폴링(UpdateDead)이
            // 부활·늦은 접속(이벤트를 못 받은 피어) 복원을 담당하는 이중 안전망이다.
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
        }

        public override void OnNetworkDespawn()
        {
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
        }

        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (evt.ClientId != OwnerClientId)
            {
                return;
            }

            Animator animator = _view != null ? _view.ActiveAnimator : null;
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.SetBool(DeadParam, true);
            }
        }

        private void Update()
        {
            if (!IsSpawned || _settings == null || _movement == null)
            {
                return;
            }

            Animator animator = _view != null ? _view.ActiveAnimator : null;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            float deltaTime = Time.deltaTime;
            if (deltaTime <= 0f)
            {
                return;
            }

            UpdateDead(animator);

            // 스무딩 → 단계 판정 순서다 — 원값으로 판정하면 충돌·이음새의 1~2프레임 속도 저하가
            // 히스테리시스를 뚫고 Walk로 떨어져 달리기 블렌드가 튄다 (1회차 검증 버벅임 원인 ③).
            float rawSpeed = IsOwner ? OwnerRawSpeed() : RemoteRawSpeed(deltaTime);
            float runBoundary = (_movement.WalkSpeed + _movement.RunSpeed) * 0.5f;
            _smoothedSpeed = PlayerAnimationMath.SmoothTowards(
                _smoothedSpeed, rawSpeed, _settings.SpeedSmoothingHalfLifeSeconds, deltaTime);
            _tier = PlayerAnimationMath.StepTier(
                _tier, _smoothedSpeed,
                _settings.WalkEnterSpeed, _settings.IdleEnterSpeed,
                runBoundary, _settings.RunHysteresisBand);
            animator.SetFloat(SpeedParam,
                PlayerAnimationMath.TierTargetSpeed(_tier, _smoothedSpeed, runBoundary));

            if (IsOwner)
            {
                DetectOwnerJump(animator);
            }
        }

        /// <summary>사망·부활 복원 — 두 사망 경로(전투·이탈)를 통합한 복제 판정을 그대로 따른다.</summary>
        private void UpdateDead(Animator animator)
        {
            bool alive = _health == null || _health.IsAlive;
            animator.SetBool(DeadParam, !alive);
        }

        /// <summary>소유자 — 실제 이동 속도에서 지상 스크롤 밀림을 뺀 수평 속력 (추정 불요).</summary>
        private float OwnerRawSpeed()
        {
            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;
            Vector3 velocity = PlayerAnimationMath.RemoveWorldScroll(
                _characterController.velocity, scrollSpeed, _controller.StandingOnWorldFrame);
            return PlayerAnimationMath.HorizontalSpeed(velocity);
        }

        /// <summary>원격 — 보간 위치 델타 기반 추정 (확장 계획 §2.1). 접지면은 프레임마다 프로브한다.</summary>
        private float RemoteRawSpeed(float deltaTime)
        {
            Vector3 position = transform.position;
            if (!_hasRemoteLastPosition)
            {
                _remoteLastPosition = position;
                _hasRemoteLastPosition = true;
                return 0f;
            }

            Vector3 delta = position - _remoteLastPosition;
            _remoteLastPosition = position;

            ProbeRemoteWorldFrame(position);
            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;
            return PlayerAnimationMath.EstimateHorizontalSpeed(
                delta, deltaTime, scrollSpeed, _remoteOnWorldFrame, _settings.TeleportSpeedThreshold);
        }

        /// <summary>
        /// 원격 접지면 판정 — 공중이면(히트 0) 이륙 당시 값을 유지한다
        /// (소유자 <see cref="NetworkPlayerController.StandingOnWorldFrame"/>과 같은 래치 규약).
        /// </summary>
        private void ProbeRemoteWorldFrame(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * ProbeOriginHeight;
            int count = Physics.RaycastNonAlloc(
                origin, Vector3.down, ProbeHits, ProbeDistance, ~0, QueryTriggerInteraction.Ignore);
            if (count <= 0)
            {
                return;
            }

            float closest = float.PositiveInfinity;
            Collider closestCollider = null;
            for (int i = 0; i < count; i++)
            {
                if (ProbeHits[i].distance < closest)
                {
                    closest = ProbeHits[i].distance;
                    closestCollider = ProbeHits[i].collider;
                }
            }

            _remoteOnWorldFrame = closestCollider != null
                && closestCollider.GetComponentInParent<WorldFrameSurface>() != null;
        }

        /// <summary>
        /// 소유자 점프 감지 — 공중 + 상승 속도 연속 유지로 판정한다. 입력을 다시 읽지 않으므로
        /// UI 게이트·구속 상태와 자동으로 정합하고, 낙하(상승 없음)·칸 이음새 스파이크
        /// (1프레임)는 걸러진다 (1회차 검증 버벅임 원인 ① — 이음새 점프 오발).
        /// </summary>
        private void DetectOwnerJump(Animator animator)
        {
            bool jumped = PlayerAnimationMath.StepJumpDetect(
                ref _jumpDetect, _characterController.isGrounded, _characterController.velocity.y,
                _settings.JumpDetectSpeed, JumpConfirmFrames);
            if (jumped)
            {
                animator.SetTrigger(JumpParam);
                ReportJumpServerRpc();
            }
        }

        // ── 연출 전용 서버 중계 (판정에 영향 없음 — GunController.PlayRemoteFireRpc와 동일 규약) ──

        /// <summary>
        /// 점프 연출 보고 — 게임 상태를 바꾸지 않으므로 서버 재검증 없이 중계만 한다.
        /// 홍수 방지로 초당 상한만 건다 (확장 계획 §2.2).
        /// </summary>
        [Rpc(SendTo.Server)]
        private void ReportJumpServerRpc()
        {
            double now = NetworkManager.ServerTime.Time;
            if (!PlayerAnimationMath.TryConsumeJumpBudget(
                    ref _jumpWindowStartTime, ref _jumpsInWindow, now, _settings != null ? _settings.MaxJumpRpcPerSecond : 4))
            {
                return;
            }

            PlayRemoteJumpRpc();
        }

        [Rpc(SendTo.NotOwner)]
        private void PlayRemoteJumpRpc()
        {
            Animator animator = _view != null ? _view.ActiveAnimator : null;
            if (animator != null && animator.isActiveAndEnabled)
            {
                animator.SetTrigger(JumpParam);
            }
        }
    }
}
