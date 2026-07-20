using Game.Core.Events;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.World;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 집게(하푼) 파이프라인 — 로컬 선반영 계층과 권위 계층의 분리 (개발 가이드 §6.1):
    /// 입력 → (즉시) 로컬 연출·투사체 → 로컬 명중 판정 → 호스트 그랩 요청 → 호스트 확정
    /// → 견인(호스트 시뮬레이션, 30 Hz 동기화) → 도착 시 획득 확정.
    /// 조작·수치·검증 규칙은 슬라이스 스펙 §2. 상태 게이트는 <see cref="HarpoonStateMachine"/>이 담당한다.
    ///
    /// 발사·명중 대기·견인·실패는 <see cref="_activeProjectile"/> 하나가 로프의 시각적 종점을 대표한다
    /// (Flying/WaitingForServer/Attached/Retracting). 소유자는 이 훅으로 실제 판정을 수행하고,
    /// 다른 클라이언트는 동일 컴포넌트를 연출 전용 사본(<see cref="HarpoonProjectile.LaunchCosmetic"/>)으로
    /// 재생해 발사·견인 모습을 함께 볼 수 있다 (NotOwner 브로드캐스트 — 서버가 중계).
    /// </summary>
    public sealed class HarpoonController : NetworkBehaviour
    {
        [SerializeField] private HarpoonSettings _settings;
        [SerializeField] private Transform _aimSource;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private HarpoonProjectile _projectilePrefab;
        [SerializeField] private HarpoonRopeRenderer _rope;

        private HarpoonStateMachine _stateMachine;
        private HarpoonProjectile _activeProjectile;
        private Vector3 _lastFirePosition;
        private double _localHitTime;

        // 서버 전용 — 이 플레이어가 견인 중인 대상.
        private IGrabbable _serverTowTarget;

        public HarpoonState State => _stateMachine != null ? _stateMachine.State : HarpoonState.Ready;

        public override void OnNetworkSpawn()
        {
            _stateMachine = new HarpoonStateMachine(
                _settings != null ? _settings.MissRecoveryDuration : 2.5f,
                _settings != null ? _settings.FireCooldown : 0.5f);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                ServerReleaseTow();
            }

            DiscardActiveProjectile();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                ServerUpdateTow();
            }

            if (IsOwner && _settings != null)
            {
                _stateMachine.Tick(Time.deltaTime);
                UpdateOwnerInput();
            }

            UpdateRopeVisual();
        }

        // ── 소유자: 입력·로컬 선반영 계층 ──────────────────────────────────

        private void UpdateOwnerInput()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && _stateMachine.TryFire())
            {
                Fire();
            }

            if (mouse.rightButton.wasPressedThisFrame && _stateMachine.TryCancel())
            {
                // 취소: 로프 절단, 대상은 그 자리에 낙하. 미스 페널티 없음 — 쿨다운만 (§2.1).
                DiscardActiveProjectile();
                CancelGrabServerRpc();
            }
        }

        private void Fire()
        {
            // 발사 시점 플레이어 위치 — 호스트 거리 검증의 기준점으로 보고에 포함한다 (§2.4).
            _lastFirePosition = transform.position;

            // Q1: 발사 연출은 입력 즉시 로컬 발행 (지연 0).
            EventBus<HarpoonFiredLocalEvent>.Publish(new HarpoonFiredLocalEvent(OwnerClientId));

            Vector3 origin = _muzzle != null ? _muzzle.position : _lastFirePosition;
            Vector3 direction = _aimSource != null ? _aimSource.forward : transform.forward;

            SpawnAuthoritativeProjectile(origin, direction);

            // 다른 클라이언트에게도 발사 모습을 보여준다 (연출 전용, 판정에는 영향 없음).
            ReportFireServerRpc(origin, direction);
        }

        private void SpawnAuthoritativeProjectile(Vector3 origin, Vector3 direction)
        {
            DiscardActiveProjectile();
            _activeProjectile = PoolManager.Spawn(_projectilePrefab, origin, Quaternion.LookRotation(direction));
            _activeProjectile.Launch(
                origin, direction,
                _settings.ProjectileSpeed, _settings.ProjectileRadius, _settings.MaxRange,
                _muzzle, _settings.RetractSpeed, _settings.ImpactPauseDuration, _settings.WaitingForServerTimeout,
                OnProjectileHit, OnProjectileMiss);
        }

        private void OnProjectileHit(IGrabbable grabbable, Vector3 hitPoint)
        {
            _localHitTime = Time.realtimeSinceStartupAsDouble;
            _stateMachine.NotifyLocalHit();

            RequestGrabServerRpc(grabbable.NetworkObject, _lastFirePosition, hitPoint);
        }

        private void OnProjectileMiss()
        {
            _stateMachine.NotifyMiss();
            EventBus<HarpoonMissLocalEvent>.Publish(new HarpoonMissLocalEvent(false));
        }

        private void UpdateRopeVisual()
        {
            if (_rope == null)
            {
                return;
            }

            if (_activeProjectile == null || !_activeProjectile.IsAlive)
            {
                _activeProjectile = null;
                _rope.Hide();
                return;
            }

            Vector3 start = _muzzle != null ? _muzzle.position : transform.position;
            float slack = _activeProjectile.IsWaitingForServer ? 1f : (_activeProjectile.IsFailing ? 0.5f : 0.15f);
            _rope.Show(start, _activeProjectile.transform.position, slack, _activeProjectile.IsFailing);
        }

        private void DiscardActiveProjectile()
        {
            if (_activeProjectile != null)
            {
                _activeProjectile.Cancel();
                _activeProjectile = null;
            }
        }

        // ── 호스트: 권위 계층 (그랩 검증·견인·획득 확정) ────────────────────

        [Rpc(SendTo.Server)]
        private void RequestGrabServerRpc(
            NetworkObjectReference targetRef, Vector3 firePosition, Vector3 hitPoint,
            RpcParams rpcParams = default)
        {
            ulong senderClientId = rpcParams.Receive.SenderClientId;

            IGrabbable grabbable = null;
            if (targetRef.TryGet(out NetworkObject targetObject))
            {
                grabbable = targetObject.GetComponent<IGrabbable>();
            }

            bool targetExists = grabbable != null && grabbable.NetworkObject.IsSpawned && grabbable.IsAvailableForGrab;
            bool claimedByOther = grabbable != null && grabbable.IsClaimed;

            GrabVerdict verdict = GrabValidation.Validate(
                targetExists, claimedByOther, firePosition, hitPoint,
                _settings.MaxRange, _settings.RangeTolerance);

            if (verdict == GrabVerdict.Approved && grabbable.TryClaimGrab(senderClientId))
            {
                _serverTowTarget = grabbable;
                GrabApprovedOwnerRpc(targetRef);
                GrabApprovedNotOwnerRpc(targetRef);
            }
            else
            {
                if (verdict == GrabVerdict.Approved)
                {
                    verdict = GrabVerdict.TargetClaimed;
                }

                Debug.Log($"[HarpoonController] 그랩 거부: client={senderClientId} verdict={verdict}");
                GrabRejectedOwnerRpc(verdict);
                GrabRejectedNotOwnerRpc();
            }
        }

        private void ServerUpdateTow()
        {
            if (_serverTowTarget == null)
            {
                return;
            }

            if (_serverTowTarget.NetworkObject == null || !_serverTowTarget.NetworkObject.IsSpawned)
            {
                _serverTowTarget = null;
                ForceReleaseOwnerRpc();
                ForceReleaseNotOwnerRpc();
                return;
            }

            Vector3 anchor = transform.position + Vector3.up * 0.5f;
            Vector3 current = _serverTowTarget.NetworkObject.transform.position;
            Vector3 next = Vector3.MoveTowards(current, anchor, _settings.ReelSpeed * Time.deltaTime);
            _serverTowTarget.UpdateTowPosition(next);

            if ((next - anchor).sqrMagnitude <= _settings.ArriveRadius * _settings.ArriveRadius)
            {
                // 획득 확정 — 공유 카운터 증가(권위 이벤트는 카운터가 발행) 후 대상 소멸.
                if (ServiceLocator.TryGet(out ISharedResourceCounter counter))
                {
                    counter.AddResource();
                }

                _serverTowTarget.CompleteGrab();
                _serverTowTarget = null;
                TargetArrivedOwnerRpc();
                TargetArrivedNotOwnerRpc();
            }
        }

        private void ServerReleaseTow()
        {
            if (_serverTowTarget != null)
            {
                _serverTowTarget.ReleaseGrab();
                _serverTowTarget = null;
            }
        }

        [Rpc(SendTo.Server)]
        private void CancelGrabServerRpc()
        {
            ServerReleaseTow();
            CancelledNotOwnerRpc();
        }

        // ── 소유자: 호스트 확정 수신 ───────────────────────────────────────

        [Rpc(SendTo.Owner)]
        private void GrabApprovedOwnerRpc(NetworkObjectReference targetRef)
        {
            double latencyMs = (Time.realtimeSinceStartupAsDouble - _localHitTime) * 1000.0;
            Debug.Log($"[HarpoonController] Q2 계측 — 로컬 명중 → 그랩 승인 수신: {latencyMs:F0} ms");

            if (targetRef.TryGet(out NetworkObject targetObject) && _activeProjectile != null)
            {
                _activeProjectile.AttachTo(targetObject.transform);
            }

            _stateMachine.NotifyGrabApproved();
        }

        [Rpc(SendTo.Owner)]
        private void GrabRejectedOwnerRpc(GrabVerdict verdict)
        {
            // 판정 불일치 (Q4): 로프가 미끄러져 빠지는 연출로 미스 전환 → 미스 페널티 (§2.4).
            Debug.Log($"[HarpoonController] Q4 계측 — 호스트 거부: {verdict}");
            _activeProjectile?.BeginRetract();
            _stateMachine.NotifyGrabRejected();
            EventBus<HarpoonMissLocalEvent>.Publish(new HarpoonMissLocalEvent(true));
        }

        [Rpc(SendTo.Owner)]
        private void TargetArrivedOwnerRpc()
        {
            DiscardActiveProjectile();
            _stateMachine.NotifyTargetArrived();
        }

        [Rpc(SendTo.Owner)]
        private void ForceReleaseOwnerRpc()
        {
            _activeProjectile?.BeginRetract();
            _stateMachine.NotifyForcedRelease();
        }

        // ── 비소유 클라이언트: 발사·견인 연출 브로드캐스트 (판정에 영향 없음) ────

        [Rpc(SendTo.Server)]
        private void ReportFireServerRpc(Vector3 origin, Vector3 direction)
        {
            PlayRemoteFireRpc(origin, direction);
        }

        [Rpc(SendTo.NotOwner)]
        private void PlayRemoteFireRpc(Vector3 origin, Vector3 direction)
        {
            DiscardActiveProjectile();
            _activeProjectile = PoolManager.Spawn(_projectilePrefab, origin, Quaternion.LookRotation(direction));
            _activeProjectile.LaunchCosmetic(
                origin, direction, _settings.ProjectileSpeed, _settings.ProjectileRadius, _settings.MaxRange,
                _muzzle, _settings.RetractSpeed, _settings.ImpactPauseDuration, _settings.WaitingForServerTimeout);
        }

        [Rpc(SendTo.NotOwner)]
        private void GrabApprovedNotOwnerRpc(NetworkObjectReference targetRef)
        {
            if (targetRef.TryGet(out NetworkObject targetObject) && _activeProjectile != null)
            {
                _activeProjectile.AttachTo(targetObject.transform);
            }
        }

        [Rpc(SendTo.NotOwner)]
        private void GrabRejectedNotOwnerRpc()
        {
            _activeProjectile?.BeginRetract();
        }

        [Rpc(SendTo.NotOwner)]
        private void TargetArrivedNotOwnerRpc()
        {
            DiscardActiveProjectile();
        }

        [Rpc(SendTo.NotOwner)]
        private void ForceReleaseNotOwnerRpc()
        {
            _activeProjectile?.BeginRetract();
        }

        [Rpc(SendTo.NotOwner)]
        private void CancelledNotOwnerRpc()
        {
            DiscardActiveProjectile();
        }
    }
}
