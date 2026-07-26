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
    /// 몬스터 이동·공격 — 호스트 단독 시뮬레이션 (권위 분담표: 몬스터 AI = 호스트).
    /// 이동은 커스텀 조향 (네트워크 문서 §4.3 — NavMesh 불사용):
    /// 지상 = 목표 향 조향 + 국소 회피 + 컨베이어 변위 가산 → 열차 측면 도약 → 갑판 위 = 목표 추격.
    /// 동기화는 10~15Hz 스냅샷 + 클라이언트 보간 (§6.2). 체력·사망은 <see cref="MonsterHealth"/>가 담당한다.
    /// M2는 평탄 갑판 + 3칸 고정 구성이라 칸 웨이포인트 그래프 없이 직선 추격으로 충분하다 —
    /// 그래프는 열차 구성이 동적으로 변하는 M3에서 도입한다.
    /// </summary>
    public sealed class MonsterAgent : NetworkBehaviour, IPoolable
    {
        [SerializeField] private MonsterSettings _settings;
        [SerializeField] private TrainLayoutSettings _trainLayout;

        private const float Gravity = 25f;

        private readonly NetworkVariable<Vector3> _syncedPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<float> _syncedYaw = new NetworkVariable<float>();

        private readonly MotionSnapshotBuffer _snapshotBuffer = new MotionSnapshotBuffer();

        private float _verticalSpeed;
        private float _syncTimer;
        private float _attackCooldown;
        private Vector3 _lastHorizontalVelocity;

        public override void OnNetworkSpawn()
        {
            _verticalSpeed = 0f;
            _syncTimer = 0f;
            _attackCooldown = 0f;
            _lastHorizontalVelocity = Vector3.zero;

            if (IsServer)
            {
                _syncedPosition.Value = transform.position;
            }
            else
            {
                _snapshotBuffer.Clear();
                _syncedPosition.OnValueChanged += OnSyncedPositionChanged;
            }
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
            if (!IsSpawned || _settings == null)
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
        }

        // ── 호스트: 조향 시뮬레이션 (§4.3) ─────────────────────────────────

        private void ServerSimulate()
        {
            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;
            Transform target = FindNearestAliveTarget();

            bool onDeck = IsOnDeck(transform.position);
            bool grounded = onDeck || transform.position.y <= 0.01f;

            Vector3 horizontalVelocity = _lastHorizontalVelocity;
            if (target != null && grounded)
            {
                float chaseSpeed = MonsterSteering.EnforceChaseSpeed(
                    _settings.MoveSpeed, scrollSpeed, _settings.ChaseSpeedMargin);

                if (onDeck)
                {
                    horizontalVelocity = MonsterSteering.ComputeDeckVelocity(
                        transform.position, target.position, _settings.MoveSpeed);
                }
                else
                {
                    bool blocked = ProbeObstacle(out Vector3 obstacleNormal);
                    horizontalVelocity = MonsterSteering.ComputeGroundVelocity(
                        transform.position, target.position, blocked, obstacleNormal, chaseSpeed, scrollSpeed);

                    TryBeginDeckLeap(target);
                }
            }

            ApplyVerticalMotion(onDeck);

            Vector3 motion = (horizontalVelocity + Vector3.up * _verticalSpeed) * Time.deltaTime;
            transform.position += motion;
            ClampToSupport();

            _lastHorizontalVelocity = horizontalVelocity;
            FaceVelocity(horizontalVelocity, target);
            ServerTryAttack(target);
            ServerCheckFellBehind();
            ServerSync();
        }

        /// <summary>
        /// 가장 가까운 공격 대상 — 살아있는 플레이어와 공격 가능한 열차 부위(칸·연결부) 중 최근접(§M3 — 몬스터가 열차 공격).
        /// 열차 부위는 <see cref="ITrainTargetRegistry"/>로 조회한다(연결부가 밤 방어전의 핵심 방어 목표, 기획서 §9).
        /// </summary>
        private Transform FindNearestAliveTarget()
        {
            Transform nearest = FindNearestPlayer(out float bestSqr);

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

        private Transform FindNearestPlayer(out float bestSqr)
        {
            bestSqr = float.MaxValue;
            NetworkManager manager = NetworkManager.Singleton;
            if (manager == null)
            {
                return null;
            }

            Transform nearest = null;
            foreach (NetworkClient client in manager.ConnectedClientsList)
            {
                NetworkObject playerObject = client.PlayerObject;
                if (playerObject == null)
                {
                    continue;
                }

                IDamageable damageable = playerObject.GetComponent<IDamageable>();
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

            return nearest;
        }

        private bool ProbeObstacle(out Vector3 obstacleNormal)
        {
            obstacleNormal = Vector3.zero;
            if (_lastHorizontalVelocity.sqrMagnitude < 0.01f)
            {
                return false;
            }

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (Physics.Raycast(origin, _lastHorizontalVelocity.normalized, out RaycastHit hit,
                    _settings.AvoidProbeDistance, ~0, QueryTriggerInteraction.Ignore) &&
                hit.transform.root != transform.root)
            {
                obstacleNormal = hit.normal;
                return true;
            }

            return false;
        }

        private void TryBeginDeckLeap(Transform target)
        {
            if (_trainLayout == null || _verticalSpeed > 0f)
            {
                return;
            }

            // 목표가 갑판 위에 있고 열차 측면에 붙었으면 도약해 승차한다.
            bool targetOnDeck = IsOnDeck(target.position) || target.position.y >= _trainLayout.DeckHeight - 0.5f;
            float sideDistance = Mathf.Abs(transform.position.x) - _trainLayout.CarWidth * 0.5f;
            bool alongTrain = transform.position.z > _trainLayout.RearZ - 2f &&
                transform.position.z < _trainLayout.FrontZ + 2f;

            if (targetOnDeck && alongTrain && sideDistance <= _settings.LeapHorizontalRange)
            {
                // 갑판 높이 + 여유를 넘는 포물선 도약 초기 속도.
                _verticalSpeed = Mathf.Sqrt(2f * Gravity * (_trainLayout.DeckHeight + 1f));
            }
        }

        private void ApplyVerticalMotion(bool onDeck)
        {
            if (_verticalSpeed > 0f || (!onDeck && transform.position.y > 0.01f))
            {
                _verticalSpeed -= Gravity * Time.deltaTime;
            }
        }

        private void ClampToSupport()
        {
            Vector3 position = transform.position;

            if (_verticalSpeed <= 0f)
            {
                if (IsWithinTrainFootprint(position) && _trainLayout != null &&
                    position.y <= _trainLayout.DeckHeight && position.y > _trainLayout.DeckHeight - 1.5f)
                {
                    position.y = _trainLayout.DeckHeight;
                    _verticalSpeed = 0f;
                }
                else if (position.y < 0f)
                {
                    position.y = 0f;
                    _verticalSpeed = 0f;
                }
            }

            transform.position = position;
        }

        private bool IsOnDeck(Vector3 position)
        {
            return _trainLayout != null &&
                IsWithinTrainFootprint(position) &&
                position.y >= _trainLayout.DeckHeight - 0.5f;
        }

        private bool IsWithinTrainFootprint(Vector3 position)
        {
            return _trainLayout != null &&
                Mathf.Abs(position.x) <= _trainLayout.CarWidth * 0.5f + 0.5f &&
                position.z >= _trainLayout.RearZ && position.z <= _trainLayout.FrontZ;
        }

        private void FaceVelocity(Vector3 horizontalVelocity, Transform target)
        {
            Vector3 facing = horizontalVelocity;
            if (facing.sqrMagnitude < 0.01f && target != null)
            {
                facing = target.position - transform.position;
            }

            facing.y = 0f;
            if (facing.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.Euler(0f, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg, 0f);
            }
        }

        private void ServerTryAttack(Transform target)
        {
            _attackCooldown = Mathf.Max(0f, _attackCooldown - Time.deltaTime);
            if (target == null || _attackCooldown > 0f)
            {
                return;
            }

            if ((target.position - transform.position).sqrMagnitude <= _settings.AttackRange * _settings.AttackRange)
            {
                IDamageable damageable = target.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.ApplyDamage(_settings.AttackDamage, NetworkManager.ServerClientId);
                    _attackCooldown = _settings.AttackInterval;
                }
            }
        }

        private void ServerCheckFellBehind()
        {
            if (_trainLayout != null &&
                transform.position.z < _trainLayout.RearZ - _settings.DespawnBehindMeters)
            {
                // 추격 실패 — 도주 처리 (사망 아님, 이벤트 없음). 풀로 회수한다.
                NetworkObject.Despawn(true);
            }
        }

        private void ServerSync()
        {
            _syncTimer += Time.deltaTime;
            if (_syncTimer < 1f / _settings.SyncHz)
            {
                return;
            }

            _syncTimer = 0f;
            _syncedYaw.Value = transform.eulerAngles.y;
            _syncedPosition.Value = transform.position;
        }

        // ── 클라이언트: 스냅샷 보간 (§6.2) ─────────────────────────────────

        private void OnSyncedPositionChanged(Vector3 previous, Vector3 current)
        {
            // 위치 변경 수신 시점에 (위치, 방향) 스냅샷을 쌓는다 — 방향은 같은 틱에 함께 갱신된다.
            _snapshotBuffer.AddSnapshot(current, _syncedYaw.Value, Time.timeAsDouble);
        }

        private void ClientInterpolate()
        {
            double renderTime = Time.timeAsDouble - _settings.InterpolationDelaySeconds;
            if (_snapshotBuffer.TrySample(renderTime, out Vector3 position, out float yaw))
            {
                transform.position = position;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _snapshotBuffer.Clear();
            _verticalSpeed = 0f;
            _lastHorizontalVelocity = Vector3.zero;
        }
    }
}
