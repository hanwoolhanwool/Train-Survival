using Game.Core.Events;
using Game.Core.Pooling;
using Game.Gameplay.Combat;
using Game.Gameplay.Crafting;
using Game.Gameplay.Player;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 집게(하푼) 파이프라인 — 로컬 선반영 계층과 권위 계층의 분리 (개발 가이드 §6.1):
    /// 입력 → (즉시) 로컬 연출·투사체 → 로컬 명중 판정 → 호스트 그랩 요청 → 호스트 확정
    /// → 견인(호스트 시뮬레이션, 30 Hz 동기화) → 도착 시 <see cref="IGrabbable.TryCompleteGrab"/>.
    /// 조작·수치·검증 규칙은 슬라이스 스펙 §2. 상태 게이트는 <see cref="HarpoonStateMachine"/>이 담당한다.
    ///
    /// 도착 시 무슨 일이 일어나는지는 <b>대상이 결정한다</b> (M5 5차 → 6차) — 자원은 수납·소멸(Consumed),
    /// 몬스터는 파지 유지(Held): 놓을 때까지 매달린 채 들고 다닌다. 여기서는 결과 enum만 본다.
    /// 종류 분기가 없으므로 대상은 <see cref="IGrabbable"/> 구현 추가만으로 늘어난다.
    ///
    /// 발사·명중 대기·견인·실패는 <see cref="_activeProjectile"/> 하나가 로프의 시각적 종점을 대표한다
    /// (Flying/WaitingForServer/Attached/Retracting). 소유자는 이 훅으로 실제 판정을 수행하고,
    /// 다른 클라이언트는 동일 컴포넌트를 연출 전용 사본(<see cref="HarpoonProjectile.LaunchCosmetic"/>)으로
    /// 재생해 발사·견인 모습을 함께 볼 수 있다 (NotOwner 브로드캐스트 — 서버가 중계).
    /// </summary>
    // LateUpdate 실행 순서 고정 (M5 6차 2차) — 로프 그리기(여기, +10)는 파지 부착(몬스터, −10)과
    // 훅 추종(HarpoonProjectile, 0)이 끝난 뒤여야 로프 끝점이 한 프레임 늦게 끌리지 않는다.
    [DefaultExecutionOrder(10)]
    public sealed class HarpoonController : NetworkBehaviour, IHarpoonTierHolder
    {
        [SerializeField] private HarpoonSettings _settings;
        [SerializeField] private Transform _aimSource;
        [SerializeField] private Transform _muzzle;
        [SerializeField] private HarpoonProjectile _projectilePrefab;
        [SerializeField] private HarpoonRopeRenderer _rope;

        // 집게 등급 (M5 5차) — 게스트도 코스메틱 훅의 사거리·속도가 필요하므로 복제한다
        // (비소유 사본이 같은 궤적을 재생해야 한다 — 변종 인덱스 복제와 같은 규약).
        private readonly NetworkVariable<byte> _tier = new NetworkVariable<byte>(1);

        private HarpoonStateMachine _stateMachine;
        private HarpoonProjectile _activeProjectile;
        private NetworkPlayerController _player;
        private Vector3 _lastFirePosition;
        private double _localHitTime;

        // 소유자 클라이언트 전용 — 로컬 명중 후 서버 확정 대기 중 예측 고정한 대상 (§11 수정안 A).
        private IGrabbable _predictedTowTarget;

        // 서버 전용 — 이 플레이어가 견인 중인 대상.
        private IGrabbable _serverTowTarget;

        // 서버 전용 — 도착 후 파지 유지 중인가 (M5 6차). 견인의 연장이라 대상 참조·해제 경로를
        // 그대로 쓰고, 위치 대입만 릴 감기에서 파지 앵커 추종으로 바뀐다.
        private bool _serverHolding;

        // 서버 전용 — 든 몬스터 투척 비행 중 (M5 6차 2차). 충돌·사거리 끝에서 놓임(기절)으로 끝난다.
        private bool _serverThrowing;
        private Vector3 _throwDirection;
        private float _throwTraveled;

        public HarpoonState State => _stateMachine != null ? _stateMachine.State : HarpoonState.Ready;

        /// <summary>무기 슬롯 활성 여부 — 무기 전환 시스템이 제어한다. 소유자 입력 게이트 (M2).</summary>
        public bool InputEnabled { get; set; } = true;

        /// <summary>현재 집게 등급 (1~). 스폰 전에는 1단계로 본다.</summary>
        public int Tier => Mathf.Max(1, _tier.Value);

        /// <summary>승급 상한 — 데이터에 등재된 등급 수 (에셋이 비면 1단계).</summary>
        public int MaxTier => _settings != null ? _settings.TierCount : 1;

        /// <summary>현재 등급의 수치 묶음 — 사거리·릴 속도·페널티·무게 상한.</summary>
        private HarpoonSettings.Tier CurrentTier => _settings.GetTier(Tier);

        /// <summary>서버 전용 — 등급 확정 (제작대 승급). 범위 밖이면 무시한다.</summary>
        public void ServerSetTier(int tier)
        {
            if (!IsServer || tier < 1 || tier > MaxTier)
            {
                return;
            }

            _tier.Value = (byte)tier;
        }

        public override void OnNetworkSpawn()
        {
            _stateMachine = BuildStateMachine();
            _player = GetComponent<NetworkPlayerController>();
            _tier.OnValueChanged += OnTierChanged;

            if (IsOwner)
            {
                EventBus<HarpoonTierChangedLocalEvent>.Publish(new HarpoonTierChangedLocalEvent(Tier));
            }
        }

        public override void OnNetworkDespawn()
        {
            _tier.OnValueChanged -= OnTierChanged;

            if (IsServer)
            {
                ServerReleaseTow();
            }

            if (IsOwner)
            {
                HarpoonSliceMetrics.EndTowTracking("디스폰");
                CancelPredictedTow();
            }

            DiscardActiveProjectile();
        }

        /// <summary>
        /// 등급이 바뀌면 상태 머신을 새 페널티·쿨다운으로 다시 만든다 (승급은 제작대 앞에서만 일어나므로
        /// 잠금 중 재생성으로 잃을 진행이 사실상 없다 — 재생성 시 Ready로 복귀한다).
        /// </summary>
        private void OnTierChanged(byte previous, byte current)
        {
            _stateMachine = BuildStateMachine();

            if (IsOwner)
            {
                EventBus<HarpoonTierChangedLocalEvent>.Publish(new HarpoonTierChangedLocalEvent(Tier));
            }
        }

        private HarpoonStateMachine BuildStateMachine()
        {
            if (_settings == null)
            {
                return new HarpoonStateMachine(2.5f, 0.5f);
            }

            HarpoonSettings.Tier tier = CurrentTier;
            return new HarpoonStateMachine(tier.MissRecoveryDuration, tier.FireCooldown);
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

                // 예측 고정 안전장치 — 승인/거부가 끝내 오지 않아 훅이 타임아웃 되감기로
                // 넘어갔으면(WaitingForServerTimeout) 대상을 컨베이어 유도로 복귀시킨다.
                if (_predictedTowTarget != null &&
                    (_activeProjectile == null || !_activeProjectile.IsAlive || _activeProjectile.IsFailing))
                {
                    CancelPredictedTow();
                }

                // Q3 계측 — 견인 중 대상(=부착된 훅) 이동의 워프/역행 검출 (소유자 화면 기준, §3.2).
                if (_activeProjectile != null && _activeProjectile.IsAttached)
                {
                    Vector3 anchor = _muzzle != null ? _muzzle.position : transform.position;
                    HarpoonSliceMetrics.RecordTowSample(_activeProjectile.transform.position, anchor, Time.deltaTime);
                }
            }
        }

        private void LateUpdate()
        {
            if (IsSpawned)
            {
                // 로프는 이번 프레임의 최종 위치(파지 부착 → 훅 추종 이후)로 그린다 (M5 6차 2차).
                UpdateRopeVisual();
            }
        }

        // ── 소유자: 입력·로컬 선반영 계층 ──────────────────────────────────

        private void UpdateOwnerInput()
        {
            // 무기를 바꿔도 파지는 유지된다 (M5 6차 2차 — 사용자 결정으로 "놓기 ② 무기 교체" 철회).
            // 든 채 다른 무기로 쏠 수 있고, 놓기·투척은 집게로 돌아와서 한다.
            if (!InputEnabled)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (_stateMachine.State == HarpoonState.Holding)
                {
                    // 파지 중 좌클릭 = 든 몬스터 투척 (M5 6차 2차) — 재발사가 아니라 슬램이다.
                    // 상태는 Holding 그대로 두고, 서버가 비행을 끝내면 강제 해제 통지로 쿨다운에 들어간다.
                    Vector3 direction = _aimSource != null ? _aimSource.forward : transform.forward;
                    direction = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;

                    // 게스트 홀더 선반영 — 서버 왕복 + 보간 지연을 기다리지 않고 즉시 비행을 재생한다
                    // (발사 Fire의 로컬 훅과 같은 규약. 피해·기절 확정은 서버가 한다).
                    if (!IsServer && _activeProjectile != null && _activeProjectile.AttachTarget != null)
                    {
                        var attachable = _activeProjectile.AttachTarget.GetComponentInParent<IHoldAttachable>();
                        attachable?.BeginPredictedThrow(
                            direction, _settings.ThrowSpeed, _settings.ThrowRange, _settings.ThrowRadius);
                    }

                    ThrowHeldServerRpc(direction);
                }
                else if (_stateMachine.TryFire())
                {
                    Fire();
                }
            }

            if (mouse.rightButton.wasPressedThisFrame && _stateMachine.TryCancel())
            {
                // 취소: 로프 절단, 대상은 그 자리에 낙하. 미스 페널티 없음 — 쿨다운만 (§2.1).
                // 파지 중 우클릭 = 놓기 (M5 6차) — 같은 경로다. 놓인 몬스터는 대상 쪽에서 잠깐 기절한다.
                HarpoonSliceMetrics.EndTowTracking("취소");
                DiscardActiveProjectile();
                CancelGrabServerRpc();
            }
        }

        private void Fire()
        {
            int inputFrame = Time.frameCount;

            // 발사 시점 플레이어 위치 — 호스트 거리 검증의 기준점으로 보고에 포함한다 (§2.4).
            _lastFirePosition = transform.position;

            // Q1: 발사 연출은 입력 즉시 로컬 발행 (지연 0).
            EventBus<HarpoonFiredLocalEvent>.Publish(new HarpoonFiredLocalEvent(OwnerClientId));

            Vector3 origin = _muzzle != null ? _muzzle.position : _lastFirePosition;
            Vector3 direction = ComputeFireDirection(origin);

            // 발사 시점 사수의 기준 프레임을 훅에 물려준다 — 지상(월드 프레임) 발사분은 컨베이어를 따라
            // 함께 밀려 지면 위 대상과 어긋나지 않고, 열차 위 발사분은 밀림 없이 그대로 날아간다.
            bool scrollWithWorld = _player != null && _player.StandingOnWorldFrame;

            SpawnAuthoritativeProjectile(origin, direction, scrollWithWorld);

            // Q1 계측 — 로컬 연출(이벤트 발행 + 훅 스폰)까지 마친 시점의 프레임 차를 기록한다.
            HarpoonSliceMetrics.RecordFire(inputFrame);

            // 다른 클라이언트에게도 발사 모습을 보여준다 (연출 전용, 판정에는 영향 없음).
            ReportFireServerRpc(origin, direction, scrollWithWorld);
        }

        private void SpawnAuthoritativeProjectile(Vector3 origin, Vector3 direction, bool scrollWithWorld)
        {
            DiscardActiveProjectile();
            _activeProjectile = PoolManager.Spawn(_projectilePrefab, origin, Quaternion.LookRotation(direction));
            _activeProjectile.Launch(
                origin, direction,
                _settings.ProjectileSpeed, _settings.ProjectileRadius, CurrentTier.MaxRange,
                transform.root, _muzzle, _settings.RetractSpeed, _settings.ImpactPauseDuration, _settings.WaitingForServerTimeout, scrollWithWorld,
                OnProjectileHit, OnProjectileMiss);
        }

        /// <summary>
        /// 조준점 수렴 발사 방향 (§11 명중 판정 시차 해소): 총구는 카메라에서 어긋나 있으므로
        /// 카메라 평행 발사는 조준점과 상시 ~0.4 m 어긋난다. 카메라 중심 레이로 조준점을 구해
        /// 총구→조준점 방향으로 수렴시킨다. 판정은 기존대로 훅 경로의 SphereCast가 수행한다.
        /// </summary>
        private Vector3 ComputeFireDirection(Vector3 muzzleOrigin)
        {
            Vector3 aimOrigin = _aimSource != null ? _aimSource.position : transform.position;
            Vector3 aimForward = _aimSource != null ? _aimSource.forward : transform.forward;

            Vector3 aimPoint = WeaponRaycast.ResolveAimPoint(
                aimOrigin, aimForward, CurrentTier.MaxRange, transform.root);

            return HarpoonAimMath.ResolveFireDirection(muzzleOrigin, aimPoint, aimForward);
        }

        private void OnProjectileHit(IGrabbable grabbable, Vector3 hitPoint)
        {
            _localHitTime = Time.realtimeSinceStartupAsDouble;
            _stateMachine.NotifyLocalHit();
            HarpoonSliceMetrics.RecordLocalHit();

            // 예측 고정 (§11 수정안 A): 서버 확정 도착까지 대상이 컨베이어로 계속 밀리면 확정 순간
            // 서버 고정 위치로 스냅되므로, 로컬 명중 즉시 고정한다. 호스트는 확정이 즉시라 불필요.
            if (!IsServer)
            {
                grabbable.BeginPredictedTow();
                _predictedTowTarget = grabbable;
            }

            RequestGrabServerRpc(grabbable.NetworkObject, _lastFirePosition, hitPoint);
        }

        private void CancelPredictedTow()
        {
            _predictedTowTarget?.CancelPredictedTow();
            _predictedTowTarget = null;
        }

        private void OnProjectileMiss()
        {
            _stateMachine.NotifyMiss();
            HarpoonSliceMetrics.RecordMiss();
            EventBus<HarpoonMissLocalEvent>.Publish(new HarpoonMissLocalEvent(false));

            // 미스도 다른 클라이언트에 전파한다 — 비소유 코스메틱 훅은 충돌을 로컬 재시뮬레이션하므로
            // 경계 사례에서 "명중"으로 재현해 승인/거부 RPC를 기다릴 수 있다. 소유자 미스가 정답임을
            // 알려주지 않으면 WaitingForServerTimeout(1.5 s)까지 대상에 붙어 있는 화면 불일치가 생긴다.
            ReportMissServerRpc();
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

        /// <summary>
        /// QA 전용 — 동시 그랩 트리거 (M5 6차, 검증 I1·I2). 발사·명중을 건너뛰고 서버 그랩 요청만
        /// 발행한다 — 경합 판정(한쪽 승인 + 한쪽 "다른 사람이 잡고 있다")이 목적이라 훅 연출은 없다.
        /// 승인되면 견인·수납은 실제 경로 그대로 진행된다.
        /// </summary>
        public void QaRequestGrab(NetworkObject target)
        {
            if (!IsOwner || target == null)
            {
                return;
            }

            RequestGrabServerRpc(target, transform.position, target.transform.position);
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

            // 무게 등급 게이트 (M5 5차) — 집게 등급 상한과 대상 무게를 순수 함수가 비교한다.
            GrabVerdict verdict = GrabValidation.Validate(
                targetExists, claimedByOther, firePosition, hitPoint,
                CurrentTier.MaxRange, _settings.RangeTolerance,
                CurrentTier.GrabWeightLimit, targetExists ? grabbable.GrabWeight : 1);

            if (verdict == GrabVerdict.Approved && grabbable.TryClaimGrab(senderClientId))
            {
                // 승인도 남긴다 — 동시 그랩 QA(I1·I2)가 "한쪽 승인 + 한쪽 거부"를 콘솔로 확인한다.
                Debug.Log($"[HarpoonController] 그랩 승인: client={senderClientId} target={targetObject.name}");
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
                // 견인·파지 중 대상 사망·디스폰 — 놓기 ③ (팀원이 든 것을 쏴 죽인 경우 포함, 5차 E12).
                _serverTowTarget = null;
                _serverHolding = false;
                _serverThrowing = false;
                ForceReleaseOwnerRpc();
                ForceReleaseNotOwnerRpc();
                return;
            }

            // 앵커(손잡이): 릴 감지 않고 붙잡기만 한다 — 대상 이동·저항은 대상 측(호스트)이 담당하고, 여기서는
            // 로프만 유지한다. 해제는 우클릭 취소(CancelGrab)나 대상 측의 점유 해제로 일어난다.
            if (_serverTowTarget.Kind == GrabKind.Anchor)
            {
                // 손잡이 앵커는 despawn하지 않으므로(씬 정적 배치) 위 스폰 검사에 걸리지 않는다.
                // 재결합·소실로 대상이 스스로 잡기를 끊었으면 로프도 함께 끊는다.
                if (!_serverTowTarget.IsClaimed)
                {
                    _serverTowTarget = null;
                    ForceReleaseOwnerRpc();
                    ForceReleaseNotOwnerRpc();
                }

                return;
            }

            // 파지 중 (M5 6차): 릴은 끝났고, 매 프레임 홀더의 파지 앵커에 대상을 대입한다 —
            // 견인과 같은 경로·같은 30 Hz 채널이라 신규 동기화가 없다. 게스트 홀더의 위치·회전은
            // OwnerNetworkTransform으로 서버에 이미 있으므로 이 판정 계산은 전부 서버로 충분하다.
            // (표시는 각 피어가 같은 계산으로 로컬 부착한다 — MonsterGrabTarget.LateUpdate.)
            if (_serverHolding)
            {
                if (_serverThrowing)
                {
                    ServerUpdateThrow();
                    return;
                }

                _serverTowTarget.UpdateTowPosition(ComputeHoldAnchor());
                return;
            }

            Vector3 anchor = transform.position + Vector3.up * 0.5f;
            Vector3 current = _serverTowTarget.NetworkObject.transform.position;
            Vector3 next = Vector3.MoveTowards(current, anchor, CurrentTier.ReelSpeed * Time.deltaTime);
            _serverTowTarget.UpdateTowPosition(next);

            if ((next - anchor).sqrMagnitude <= _settings.ArriveRadius * _settings.ArriveRadius)
            {
                // 도착 확정 — 무슨 일이 일어나는지는 대상이 정한다 (자원 = 수납 후 소멸,
                // 몬스터 = 파지 유지). 실패(예: 인벤토리 가득)는 그 자리 낙하 = 기존 강제 해제 경로.
                var completion = new GrabCompletion(OwnerClientId, gameObject, Tier);
                switch (_serverTowTarget.TryCompleteGrab(completion))
                {
                    case GrabCompletionResult.Held:
                        // 파지 유지 — 로프·훅도 그대로다 (전 피어의 훅이 Attached 상태로 남는다).
                        _serverHolding = true;
                        HeldOwnerRpc();
                        break;

                    case GrabCompletionResult.Consumed:
                        _serverTowTarget = null;
                        TargetArrivedOwnerRpc();
                        TargetArrivedNotOwnerRpc();
                        break;

                    default:
                        ServerReleaseTow();
                        ForceReleaseOwnerRpc();
                        ForceReleaseNotOwnerRpc();
                        break;
                }
            }
        }

        private void ServerReleaseTow()
        {
            _serverHolding = false;
            _serverThrowing = false;

            if (_serverTowTarget != null)
            {
                _serverTowTarget.ReleaseGrab();
                _serverTowTarget = null;
            }
        }

        /// <summary>
        /// 파지 앵커 — 홀더 기준 전방·측면(좌측)·높이 오프셋 (M5 6차 2차).
        /// 서버 판정(<see cref="ServerUpdateTow"/>)과 전 피어 표시(몬스터 쪽 로컬 부착)가
        /// <b>같은 계산</b>을 쓴다 — 판정과 화면이 갈리지 않는다.
        /// </summary>
        public Vector3 ComputeHoldAnchor()
        {
            return transform.position
                + transform.forward * _settings.HoldDistance
                + transform.right * _settings.HoldSide
                + Vector3.up * _settings.HoldHeight;
        }

        /// <summary>파지 중 좌클릭 — 든 몬스터 투척 요청 (M5 6차 2차). 비행·충돌·해제는 전부 서버가 확정한다.</summary>
        [Rpc(SendTo.Server)]
        private void ThrowHeldServerRpc(Vector3 direction)
        {
            if (!_serverHolding || _serverThrowing || _serverTowTarget == null)
            {
                return;
            }

            _throwDirection = direction.sqrMagnitude > 0.01f ? direction.normalized : transform.forward;
            _throwTraveled = 0f;
            _serverThrowing = true;

            // 앵커 추종이 끊긴다 — 대상의 로컬 부착 표시를 풀어 비행이 동기화 표시로 보이게 한다.
            (_serverTowTarget as IHoldAttachable)?.ServerSetHoldAttached(false);
        }

        /// <summary>
        /// 투척 비행 (서버) — 든 몬스터가 조준 방향으로 짧게 날아가고, 부딪히면 큰 피해를 받은 뒤
        /// 놓인다(파지에서 놓임 = 기절). 사거리 끝까지 아무것도 안 맞으면 그 자리에 놓인다(기절만).
        /// 위치는 견인과 같은 채널로 나가므로 비행 모습도 신규 동기화 없이 보인다.
        /// </summary>
        private void ServerUpdateThrow()
        {
            float step = _settings.ThrowSpeed * Time.deltaTime;
            Vector3 current = _serverTowTarget.NetworkObject.transform.position;

            // 던진 사람·던져진 몬스터 자신은 충돌로 치지 않는다 (시작 겹침은 SphereCast가 무시한다).
            if (Physics.SphereCast(current, _settings.ThrowRadius, _throwDirection, out RaycastHit hit,
                    step, ~0, QueryTriggerInteraction.Ignore)
                && hit.transform.root != transform.root
                && hit.transform.root != _serverTowTarget.NetworkObject.transform.root)
            {
                // 슬램 — 충돌 지점에 놓고 큰 피해 + 기절. 피해 귀속 = 던진 사람.
                _serverTowTarget.UpdateTowPosition(hit.point + hit.normal * _settings.ThrowRadius);
                var damageable = _serverTowTarget.NetworkObject.GetComponent<IDamageable>();
                damageable?.ApplyDamage(_settings.ThrowDamage, OwnerClientId);
                ServerEndThrow();
                return;
            }

            _serverTowTarget.UpdateTowPosition(current + _throwDirection * step);
            _throwTraveled += step;

            if (_throwTraveled >= _settings.ThrowRange)
            {
                ServerEndThrow();
            }
        }

        private void ServerEndThrow()
        {
            _serverThrowing = false;

            // 슬램 피해로 죽어 이미 디스폰됐으면 해제할 것이 없다 — 참조만 정리한다.
            if (_serverTowTarget != null && _serverTowTarget.NetworkObject != null
                && _serverTowTarget.NetworkObject.IsSpawned)
            {
                ServerReleaseTow();
            }
            else
            {
                _serverTowTarget = null;
                _serverHolding = false;
            }

            // 파지 종료 통지 — 소유자는 Holding → Cooldown, 훅은 전 피어에서 되감긴다.
            ForceReleaseOwnerRpc();
            ForceReleaseNotOwnerRpc();
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
            HarpoonSliceMetrics.RecordGrabApproved(latencyMs);
            HarpoonSliceMetrics.BeginTowTracking(CurrentTier.ReelSpeed);

            if (targetRef.TryGet(out NetworkObject targetObject) && _activeProjectile != null)
            {
                _activeProjectile.AttachTo(targetObject.transform);
            }

            // 예측 고정은 대상이 _isTowed 스냅샷 수신 시 스스로 해제한다 — 여기서는 추적만 끝낸다.
            _predictedTowTarget = null;

            _stateMachine.NotifyGrabApproved();
        }

        [Rpc(SendTo.Owner)]
        private void GrabRejectedOwnerRpc(GrabVerdict verdict)
        {
            // 판정 불일치 (Q4): 로프가 미끄러져 빠지는 연출로 미스 전환 → 미스 페널티 (§2.4).
            HarpoonSliceMetrics.RecordGrabRejected(verdict);
            CancelPredictedTow();
            _activeProjectile?.BeginRetract();
            _stateMachine.NotifyGrabRejected();
            EventBus<HarpoonMissLocalEvent>.Publish(new HarpoonMissLocalEvent(true));

            // 사유 안내 (M5 5차) — 등급 부족은 외형만으로 알 수 없어 HUD가 대신 말해준다.
            EventBus<HarpoonGrabRejectedLocalEvent>.Publish(new HarpoonGrabRejectedLocalEvent(verdict));
        }

        [Rpc(SendTo.Owner)]
        private void TargetArrivedOwnerRpc()
        {
            HarpoonSliceMetrics.EndTowTracking("도착");
            DiscardActiveProjectile();
            _stateMachine.NotifyTargetArrived();
        }

        /// <summary>도착 = 파지 (M5 6차). 훅·로프는 회수하지 않는다 — 파지 중 로프가 계속 보인다.</summary>
        [Rpc(SendTo.Owner)]
        private void HeldOwnerRpc()
        {
            HarpoonSliceMetrics.EndTowTracking("파지 도착");
            _stateMachine.NotifyHeld();
        }

        [Rpc(SendTo.Owner)]
        private void ForceReleaseOwnerRpc()
        {
            HarpoonSliceMetrics.EndTowTracking("강제 해제");
            CancelPredictedTow();
            _activeProjectile?.BeginRetract();
            _stateMachine.NotifyForcedRelease();
        }

        // ── 비소유 클라이언트: 발사·견인 연출 브로드캐스트 (판정에 영향 없음) ────

        [Rpc(SendTo.Server)]
        private void ReportFireServerRpc(Vector3 origin, Vector3 direction, bool scrollWithWorld)
        {
            PlayRemoteFireRpc(origin, direction, scrollWithWorld);
        }

        [Rpc(SendTo.Server)]
        private void ReportMissServerRpc()
        {
            PlayRemoteMissRpc();
        }

        [Rpc(SendTo.NotOwner)]
        private void PlayRemoteMissRpc()
        {
            // 코스메틱 훅이 어느 단계에 있든 즉시 되감기로 수렴시킨다 (BeginRetract는 Idle 가드가 있어 안전).
            _activeProjectile?.BeginRetract();
        }

        [Rpc(SendTo.NotOwner)]
        private void PlayRemoteFireRpc(Vector3 origin, Vector3 direction, bool scrollWithWorld)
        {
            DiscardActiveProjectile();
            _activeProjectile = PoolManager.Spawn(_projectilePrefab, origin, Quaternion.LookRotation(direction));
            _activeProjectile.LaunchCosmetic(
                origin, direction, _settings.ProjectileSpeed, _settings.ProjectileRadius, CurrentTier.MaxRange,
                transform.root, _muzzle, _settings.RetractSpeed, _settings.ImpactPauseDuration, _settings.WaitingForServerTimeout, scrollWithWorld);
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
