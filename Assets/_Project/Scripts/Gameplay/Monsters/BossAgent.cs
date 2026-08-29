using System.Collections.Generic;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Game.Gameplay.Train;
using Game.Gameplay.World;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 지역 보스의 이동·패턴 시뮬레이션 — 호스트 단독 (M7 2차, 권위 분담표: 몬스터 AI = 호스트).
    /// 조향(<see cref="MonsterSteering"/>)·스냅샷 보간(<see cref="MotionSnapshotBuffer"/>)은 웨이브
    /// 몬스터와 같은 계약을 재사용하고, 보스 고유분(돌진·페이즈·고유 패턴)만 이 클래스가 더한다.
    /// <b>갑판 도약은 없다</b> — 보스는 지상 개체로 열차 측면을 따라붙어 칸·연결부를 때린다
    /// (지붕 난전은 웨이브 몫). 패턴 상태 중 돌진 국면만 복제한다 (예고를 전 피어가 봐야 하므로).
    /// </summary>
    public sealed class BossAgent : NetworkBehaviour, IPoolable
    {
        /// <summary>낙하 판정을 기다리는 투사체 1발 (서버 전용).</summary>
        private struct PendingImpact
        {
            public Vector3 Point;
            public float RemainingSeconds;
            public float Radius;
            public float Damage;
        }

        [Tooltip("이 보스의 정의 — 프리팹과 1:1이다 (결정 ①).")]
        [SerializeField] private BossDefinition _definition;

        [SerializeField] private TrainLayoutSettings _trainLayout;

        [Tooltip("몸통 반경 — 열차 측면에 붙을 때의 최소 이격. 돌진이 이 선을 넘으면 충돌 경직이다.")]
        [SerializeField, Min(0.5f)] private float _bodyRadius = 1.5f;

        [Tooltip("열차 앞뒤로 이만큼까지만 벗어난다 — 보스가 시야 밖으로 도주해 소프트락이 되는 것을 막는다.")]
        [SerializeField, Min(5f)] private float _leashMeters = 30f;

        [Tooltip("몸통 색을 덧입힐 렌더러 — 임시 외형(결정 ⑤)에서 지역별 구분과 돌진 예고를 함께 담당한다.")]
        [SerializeField] private Renderer[] _tintRenderers;

        [Tooltip("평상시 몸통 색 — 머티리얼을 공유한 채 프리팹마다 다른 색을 낸다 (머티리얼 에셋 무증가).")]
        [SerializeField] private Color _bodyColor = new Color(0.3f, 0.35f, 0.2f, 1f);

        [SerializeField] private Color _telegraphColor = new Color(1f, 0.75f, 0.2f, 1f);

        [SerializeField] private Color _chargingColor = new Color(1f, 0.25f, 0.15f, 1f);

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly NetworkVariable<Vector3> _syncedPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<float> _syncedYaw = new NetworkVariable<float>();

        /// <summary>돌진 국면 — 예고를 전 피어가 봐야 하므로 이 하나만 복제한다 (나머지 패턴 상태는 서버 전용).</summary>
        private readonly NetworkVariable<byte> _syncedChargeState =
            new NetworkVariable<byte>((byte)BossChargeState.Ready);

        private readonly MotionSnapshotBuffer _snapshotBuffer = new MotionSnapshotBuffer();

        private readonly List<PendingImpact> _pendingImpacts = new List<PendingImpact>(4);

        private BossHealth _health;
        private MaterialPropertyBlock _propertyBlock;

        private float _syncTimer;
        private float _attackCooldown;
        private float _signatureTimer;
        private Vector3 _lastHorizontalVelocity;

        // 보스가 서는 바닥 — 물 지역이면 물면이다 (웨이브 개체와 같은 규약). 스폰 시 확정.
        private float _surfaceY;

        private BossChargeState _chargeState = BossChargeState.Ready;
        private float _chargeTimer;
        private Vector3 _chargeDirection;
        private float _chargeHitCooldown;

        private byte _lastAppliedChargeVisual = byte.MaxValue;

        public override void OnNetworkSpawn()
        {
            _health = GetComponent<BossHealth>();
            _syncTimer = 0f;
            _attackCooldown = 0f;
            _signatureTimer = 0f;
            _lastHorizontalVelocity = Vector3.zero;
            _chargeState = BossChargeState.Ready;
            _chargeTimer = 0f;
            _chargeHitCooldown = 0f;
            _pendingImpacts.Clear();

            // 물 지역이면 보스도 물면에 선다 — 안 하면 수면 위에 뜬 채 싸운다 (바다 계획 §12.1).
            _surfaceY = World.WaterSurfaceQuery.SurfaceY();

            if (IsServer)
            {
                _syncedChargeState.Value = (byte)BossChargeState.Ready;
                _syncedPosition.Value = transform.position;
            }
            else
            {
                _snapshotBuffer.Clear();

                // 스폰 위치를 첫 스냅샷으로 심는다 (웨이브 개체와 같은 이유) — 없으면 첫
                // 동기화가 올 때까지 보간 표본이 0개라 등장 순간 위치가 튄다.
                _snapshotBuffer.AddSnapshot(_syncedPosition.Value, _syncedYaw.Value, Time.timeAsDouble);

                _syncedPosition.OnValueChanged += OnSyncedPositionChanged;
            }

            ApplyChargeVisual(_syncedChargeState.Value);
        }

        public override void OnNetworkDespawn()
        {
            if (!IsServer)
            {
                _syncedPosition.OnValueChanged -= OnSyncedPositionChanged;
            }
        }

        private void Update()
        {
            if (!IsSpawned || _definition == null)
            {
                return;
            }

            if (IsServer)
            {
                ServerSimulate();
            }
            else
            {
                ClientInterpolate();
            }

            ApplyChargeVisual(_syncedChargeState.Value);
        }

        // ── 호스트: 시뮬레이션 ────────────────────────────────────────────────

        private void ServerSimulate()
        {
            int phase = _health == null ? 0 : _health.PhaseIndex;
            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;
            Transform target = FindNearestAliveTarget();

            ServerStepCharge(target, phase);

            Vector3 horizontalVelocity = _chargeState == BossChargeState.Charging
                ? BossChargeMath.ComputeChargeVelocity(_chargeDirection, _definition.ChargeSpeed)
                : ServerComputeChaseVelocity(target, phase, scrollSpeed);

            transform.position += horizontalVelocity * Time.deltaTime;
            ClampToGround();
            _lastHorizontalVelocity = horizontalVelocity;
            FaceVelocity(horizontalVelocity, target);

            if (_chargeState == BossChargeState.Charging)
            {
                ServerApplyChargeDamage();
            }
            else if (_chargeState != BossChargeState.Recover)
            {
                // 경직 중에는 때리지 못한다 — 돌진 뒤의 반격 틈이 그 대가다.
                ServerTryContactAttack(target);
            }

            ServerStepSignature(target, phase, scrollSpeed);
            ServerStepPendingImpacts();
            ServerSync(_definition.SyncHz);
        }

        private Vector3 ServerComputeChaseVelocity(Transform target, int phase, float scrollSpeed)
        {
            if (target == null || _chargeState == BossChargeState.Telegraph || _chargeState == BossChargeState.Recover)
            {
                // 예고·경직 중에는 제자리에 선다 — 예고를 보고 피할 여유가 패턴의 핵심이다.
                return Vector3.zero;
            }

            float speed = _definition.MoveSpeed *
                BossPhaseMath.SpeedMultiplier(phase, _definition.PhaseSpeedBonusPerPhase);
            float chaseSpeed = MonsterSteering.EnforceChaseSpeed(speed, scrollSpeed, _definition.ChaseSpeedMargin);
            bool blocked = ProbeObstacle(out Vector3 obstacleNormal);

            return MonsterSteering.ComputeGroundVelocity(
                transform.position, target.position, blocked, obstacleNormal, chaseSpeed, scrollSpeed);
        }

        private void ServerStepCharge(Transform target, int phase)
        {
            float cooldown = _definition.ChargeCooldownSeconds *
                BossPhaseMath.CooldownScale(phase, _definition.PhaseCooldownScalePerPhase);

            bool blocked = _chargeState == BossChargeState.Charging &&
                (WouldEnterTrainFootprint(transform.position + _chargeDirection * (_bodyRadius * 0.5f))
                    || ProbeObstacle(out _));

            BossChargeStep step = BossChargeMath.Step(
                _chargeState, _chargeTimer, Time.deltaTime,
                cooldown, _definition.ChargeTelegraphSeconds,
                _definition.ChargeDurationSeconds, _definition.ChargeRecoverSeconds,
                target != null, blocked);

            if (step.EnteredTelegraph)
            {
                // 방향은 예고 시작에 고정된다 — 예고를 읽고 옆으로 피하는 것이 회피 수단이다.
                Vector3 toTarget = target.position - transform.position;
                toTarget.y = 0f;
                _chargeDirection = toTarget.sqrMagnitude > 0.01f ? toTarget.normalized : transform.forward;
            }

            if (step.EnteredCharge)
            {
                _chargeHitCooldown = 0f;
            }

            _chargeState = step.State;
            _chargeTimer = step.Timer;

            byte replicated = (byte)_chargeState;
            if (_syncedChargeState.Value != replicated)
            {
                _syncedChargeState.Value = replicated;
            }
        }

        private void ServerStepSignature(Transform target, int phase, float scrollSpeed)
        {
            if (_definition.SignaturePattern == BossSignaturePattern.None
                || !BossPhaseMath.IsSignatureUnlocked(phase, _definition.SignatureUnlockPhaseIndex))
            {
                return;
            }

            float interval = _definition.SignatureIntervalSeconds *
                BossPhaseMath.CooldownScale(phase, _definition.PhaseCooldownScalePerPhase);

            _signatureTimer += Time.deltaTime;
            if (_signatureTimer < interval)
            {
                return;
            }

            _signatureTimer = 0f;

            switch (_definition.SignaturePattern)
            {
                case BossSignaturePattern.Summon:
                    ServerSpawnSignatureMinions(false);
                    break;

                case BossSignaturePattern.HerdCall:
                    ServerSpawnSignatureMinions(true);
                    break;

                case BossSignaturePattern.Projectile:
                    ServerLaunchProjectile(target, scrollSpeed);
                    break;
            }
        }

        private void ServerSpawnSignatureMinions(bool passThrough)
        {
            if (!ServiceLocator.TryGet(out IBossMinionSink sink))
            {
                return;
            }

            sink.ServerSpawnMinions(
                _definition.SignatureCount, transform.position,
                _definition.SignatureVariantIndex, _definition.SignatureMaxAlive, passThrough);
        }

        private void ServerLaunchProjectile(Transform target, float scrollSpeed)
        {
            if (target == null)
            {
                return;
            }

            // 갑판 위 표적은 열차와 함께 정지해 있고, 지상 표적은 컨베이어에 실려 -Z로 밀린다.
            bool onDeck = _trainLayout != null && target.position.y >= _trainLayout.DeckHeight - 0.5f;
            Vector3 drift = onDeck ? Vector3.zero : Vector3.back * scrollSpeed;

            Vector3 origin = transform.position + Vector3.up * 2f;
            Vector3 impact = BossProjectileMath.PredictImpactPoint(
                target.position, drift, _definition.ProjectileFlightSeconds);

            _pendingImpacts.Add(new PendingImpact
            {
                Point = impact,
                RemainingSeconds = _definition.ProjectileFlightSeconds,
                Radius = _definition.ProjectileImpactRadius,
                Damage = _definition.ProjectileDamage,
            });

            PlayProjectileRpc(origin, impact, _definition.ProjectileFlightSeconds, _definition.ProjectileImpactRadius);
        }

        private void ServerStepPendingImpacts()
        {
            for (int i = _pendingImpacts.Count - 1; i >= 0; i--)
            {
                PendingImpact pending = _pendingImpacts[i];
                pending.RemainingSeconds -= Time.deltaTime;

                if (pending.RemainingSeconds > 0f)
                {
                    _pendingImpacts[i] = pending;
                    continue;
                }

                _pendingImpacts.RemoveAt(i);
                ServerApplyImpactDamage(pending);
            }
        }

        private void ServerApplyImpactDamage(PendingImpact pending)
        {
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return;
            }

            foreach (NetworkClient client in manager.ConnectedClientsList)
            {
                NetworkObject playerObject = client.PlayerObject;
                if (playerObject == null)
                {
                    continue;
                }

                var damageable = playerObject.GetComponent<IDamageable>();
                if (damageable == null || !damageable.IsAlive
                    || !BossProjectileMath.IsWithinImpact(pending.Point, playerObject.transform.position, pending.Radius))
                {
                    continue;
                }

                damageable.ApplyDamage(pending.Damage, NetworkManager.ServerClientId);
            }
        }

        private void ServerApplyChargeDamage()
        {
            _chargeHitCooldown = Mathf.Max(0f, _chargeHitCooldown - Time.deltaTime);
            if (_chargeHitCooldown > 0f)
            {
                return;
            }

            Transform target = FindNearestAliveTarget();
            if (target == null || HorizontalSqrDistance(transform.position, target.position)
                > _definition.ChargeHitRadius * _definition.ChargeHitRadius)
            {
                return;
            }

            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                return;
            }

            damageable.ApplyDamage(_definition.ChargeDamage, NetworkManager.ServerClientId);

            // 같은 돌진에서 한 대상이 매 프레임 맞지 않게 하는 최소 간격.
            _chargeHitCooldown = 0.5f;
        }

        private void ServerTryContactAttack(Transform target)
        {
            _attackCooldown = Mathf.Max(0f, _attackCooldown - Time.deltaTime);
            if (target == null || _attackCooldown > 0f)
            {
                return;
            }

            // 보스는 지상에서 갑판 위 부위를 올려친다 — 높이 차이로 사거리가 깎이지 않게 수평 거리로 본다.
            if (HorizontalSqrDistance(transform.position, target.position)
                > _definition.AttackRange * _definition.AttackRange)
            {
                return;
            }

            var damageable = target.GetComponent<IDamageable>();
            if (damageable == null || !damageable.IsAlive)
            {
                return;
            }

            damageable.ApplyDamage(_definition.ContactDamage, NetworkManager.ServerClientId);
            _attackCooldown = _definition.AttackInterval;
        }

        /// <summary>가장 가까운 공격 대상 — 살아있는 플레이어와 열차 부위(칸·연결부) 중 최근접.</summary>
        private Transform FindNearestAliveTarget()
        {
            Transform nearest = null;
            float bestSqr = float.MaxValue;

            NetworkManager manager = NetworkManager.Singleton;
            if (manager != null)
            {
                foreach (NetworkClient client in manager.ConnectedClientsList)
                {
                    NetworkObject playerObject = client.PlayerObject;
                    if (playerObject == null)
                    {
                        continue;
                    }

                    var damageable = playerObject.GetComponent<IDamageable>();
                    if (damageable == null || !damageable.IsAlive)
                    {
                        continue;
                    }

                    float sqr = (playerObject.transform.position - transform.position).sqrMagnitude;
                    if (sqr < bestSqr)
                    {
                        bestSqr = sqr;
                        nearest = playerObject.transform;
                    }
                }
            }

            if (ServiceLocator.TryGet(out ITrainTargetRegistry registry)
                && registry.TryGetNearest(transform.position, out Transform trainTarget, out _))
            {
                float sqr = (trainTarget.position - transform.position).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    nearest = trainTarget;
                }
            }

            return nearest;
        }

        private bool ProbeObstacle(out Vector3 obstacleNormal)
        {
            obstacleNormal = Vector3.zero;
            Vector3 forward = _chargeState == BossChargeState.Charging ? _chargeDirection : _lastHorizontalVelocity;
            if (forward.sqrMagnitude < 0.01f)
            {
                return false;
            }

            Vector3 origin = transform.position + Vector3.up * 1.2f;
            if (Physics.Raycast(origin, forward.normalized, out RaycastHit hit,
                    _definition.AvoidProbeDistance, ~0, QueryTriggerInteraction.Ignore)
                && hit.transform.root != transform.root)
            {
                obstacleNormal = hit.normal;
                return true;
            }

            return false;
        }

        /// <summary>발밑 보정 간격(초) — 웨이브 개체와 같은 값(북극 3차 §3.1).</summary>
        private const float SupportProbeInterval = 0.25f;

        private float _regionSurfaceY;
        private float _supportProbeTimer;

        /// <summary>지상 고정 + 열차 관통 방지 + 이탈 리시(leash) — 보스가 무대를 벗어나지 않게 한다.</summary>
        private void ClampToGround()
        {
            // 얼음과 물이 교차하는 지역(북극)에서는 지역 물면 단일값이 보스를 얼음에 묻는다.
            // 물이 없는 지역은 SurfaceY 가 0이라 이 분기가 아예 돌지 않는다.
            _regionSurfaceY = World.WaterSurfaceQuery.SurfaceY();
            if (_regionSurfaceY >= 0f)
            {
                _surfaceY = _regionSurfaceY;
            }
            else
            {
                _supportProbeTimer -= Time.deltaTime;
                if (_supportProbeTimer <= 0f)
                {
                    _supportProbeTimer = SupportProbeInterval;
                    _surfaceY = GroundSupportProbe.Sample(transform.position, _regionSurfaceY);
                }
            }

            Vector3 position = transform.position;
            position.y = _surfaceY;

            if (_trainLayout != null)
            {
                float minLateral = _trainLayout.CarWidth * 0.5f + _bodyRadius;
                if (Mathf.Abs(position.x) < minLateral &&
                    position.z >= _trainLayout.RearZ && position.z <= _trainLayout.FrontZ)
                {
                    float side = position.x >= 0f ? 1f : -1f;
                    position.x = side * minLateral;
                }

                // 후방 이탈은 회수가 아니라 리시다 — 보스가 사라지면 밤이 끝나지 않는 규칙이 소프트락이 된다.
                position.z = Mathf.Clamp(
                    position.z, _trainLayout.RearZ - _leashMeters, _trainLayout.FrontZ + _leashMeters);
            }

            transform.position = position;
        }

        private bool WouldEnterTrainFootprint(Vector3 position)
        {
            return _trainLayout != null &&
                Mathf.Abs(position.x) < _trainLayout.CarWidth * 0.5f + _bodyRadius &&
                position.z >= _trainLayout.RearZ && position.z <= _trainLayout.FrontZ;
        }

        private static float HorizontalSqrDistance(Vector3 a, Vector3 b)
        {
            float dx = b.x - a.x;
            float dz = b.z - a.z;

            return dx * dx + dz * dz;
        }

        private void FaceVelocity(Vector3 horizontalVelocity, Transform target)
        {
            Vector3 facing = horizontalVelocity;
            if (facing.sqrMagnitude < 0.01f)
            {
                facing = _chargeState == BossChargeState.Telegraph
                    ? _chargeDirection
                    : (target == null ? Vector3.zero : target.position - transform.position);
            }

            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 0f);
            }
        }

        private void ServerSync(float hz)
        {
            _syncTimer += Time.deltaTime;
            if (hz > 0f && _syncTimer < 1f / hz)
            {
                return;
            }

            _syncTimer = 0f;
            _syncedYaw.Value = transform.eulerAngles.y;
            _syncedPosition.Value = transform.position;
        }

        // ── 클라이언트: 스냅샷 보간 + 예고 표시 ────────────────────────────────

        private void OnSyncedPositionChanged(Vector3 previous, Vector3 current)
        {
            _snapshotBuffer.AddSnapshot(current, _syncedYaw.Value, Time.timeAsDouble);
        }

        private void ClientInterpolate()
        {
            double renderTime = Time.timeAsDouble - _definition.InterpolationDelaySeconds;
            if (_snapshotBuffer.TrySample(renderTime, out Vector3 position, out float yaw))
            {
                transform.position = position;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        /// <summary>돌진 국면을 색으로 표시한다 — 임시 외형(결정 ⑤)에서도 예고가 읽히게 하는 최소 연출.</summary>
        private void ApplyChargeVisual(byte state)
        {
            if (_tintRenderers == null || _tintRenderers.Length == 0 || _lastAppliedChargeVisual == state)
            {
                return;
            }

            if (_propertyBlock == null)
            {
                _propertyBlock = new MaterialPropertyBlock();
            }

            _lastAppliedChargeVisual = state;

            Color color = _bodyColor;
            if (state == (byte)BossChargeState.Telegraph)
            {
                color = _telegraphColor;
            }
            else if (state == (byte)BossChargeState.Charging)
            {
                color = _chargingColor;
            }

            for (int i = 0; i < _tintRenderers.Length; i++)
            {
                Renderer renderer = _tintRenderers[i];
                if (renderer == null)
                {
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorId, color);
                renderer.SetPropertyBlock(_propertyBlock);
            }
        }

        /// <summary>발사 파라미터만 보내고 각 피어가 궤적을 로컬 재생한다 — 탄체 복제 없음(결정 ②-b).</summary>
        [Rpc(SendTo.Everyone)]
        private void PlayProjectileRpc(Vector3 origin, Vector3 impact, float flightSeconds, float radius)
        {
            if (_definition == null || _definition.ProjectileViewPrefab == null)
            {
                return;
            }

            BossProjectileView view = PoolManager.Spawn(
                _definition.ProjectileViewPrefab, origin, Quaternion.identity);
            view.Play(origin, impact, flightSeconds, radius);
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _snapshotBuffer.Clear();
            _pendingImpacts.Clear();
            _lastHorizontalVelocity = Vector3.zero;
            _chargeState = BossChargeState.Ready;
            _chargeTimer = 0f;
            _signatureTimer = 0f;
            _lastAppliedChargeVisual = byte.MaxValue;
        }
    }
}
