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
        [Tooltip("변종이 지정되지 않았을 때 쓰는 기본 설정.")]
        [SerializeField] private MonsterSettings _settings;

        [Tooltip("변종 목록 — 복제된 인덱스를 각 피어가 여기서 조회한다.")]
        [SerializeField] private MonsterVariantCatalog _variantCatalog;

        [SerializeField] private TrainLayoutSettings _trainLayout;

        private const float Gravity = 25f;

        private readonly NetworkVariable<Vector3> _syncedPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<float> _syncedYaw = new NetworkVariable<float>();

        /// <summary>이 개체의 변종 인덱스 (−1 = 기본 설정). 스폰 시 1회 확정되고 이후 바뀌지 않는다.</summary>
        private readonly NetworkVariable<int> _variantIndex = new NetworkVariable<int>(-1);

        private readonly MotionSnapshotBuffer _snapshotBuffer = new MotionSnapshotBuffer();

        private float _verticalSpeed;
        private float _syncTimer;
        private float _attackCooldown;
        private Vector3 _lastHorizontalVelocity;
        private int _pendingVariantIndex = -1;

        // M5 5차 — 집게 견인/무력화 중 시뮬레이션 정지 (서버 전용 상태. 표시는 복제된 위치로 따라온다).
        private bool _towed;
        private bool _stunned;

        /// <summary>
        /// 이 개체에 실제로 적용되는 설정 — 변종이 지정됐으면 카탈로그에서, 아니면 기본값.
        /// 클라이언트도 보간 지연·이동 파라미터가 필요하므로 인덱스를 복제해 같은 값을 조회한다.
        /// </summary>
        private MonsterSettings Settings
        {
            get
            {
                if (_variantCatalog != null)
                {
                    MonsterSettings variant = _variantCatalog.GetVariant(_variantIndex.Value);
                    if (variant != null)
                    {
                        return variant;
                    }
                }

                return _settings;
            }
        }

        /// <summary>
        /// 이 개체에 적용되는 설정 (변종 반영) — 나란한 관심사(체력·그랩)가 같은 값을 읽는다.
        /// 인덱스가 복제되므로 클라이언트에서도 유효하다.
        /// </summary>
        public MonsterSettings ActiveSettings => Settings;

        /// <summary>
        /// 열차 레이아웃 — 그랩 관심사의 즉사 존 판정(M5 6차)이 같은 에셋을 읽는다
        /// (프리팹에 새 참조를 배선하지 않기 위한 노출).
        /// </summary>
        public TrainLayoutSettings TrainLayout => _trainLayout;

        /// <summary>
        /// 스폰할 변종을 지정한다 (호스트 전용). <see cref="NetworkVariable{T}"/>는 스폰 전에 쓸 수 없으므로
        /// 대기 값으로 받아 <see cref="OnNetworkSpawn"/>에서 확정한다.
        /// </summary>
        public void ServerSetVariant(int variantIndex)
        {
            _pendingVariantIndex = variantIndex;
        }

        /// <summary>
        /// 견인 상태 전환 (M5 5차 — 서버 전용). 켜면 조향·중력·공격이 전부 멈추고 위치는
        /// 집게(<see cref="MonsterGrabTarget.UpdateTowPosition"/>)가 대입한다. 끌려오는 동안은 때리지 못한다.
        /// </summary>
        public void ServerSetTowed(bool towed)
        {
            if (!IsServer || _towed == towed)
            {
                return;
            }

            _towed = towed;
            _verticalSpeed = 0f;
            _lastHorizontalVelocity = Vector3.zero;

            // 상태 전환 프레임에 스냅샷을 즉시 한 번 보내 표시가 늦게 따라붙지 않게 한다.
            _syncTimer = float.MaxValue;
        }

        /// <summary>
        /// 무력화(그로기) 전환 (M5 5차 — 서버 전용). 조향·공격만 멈추고 중력·지지면 클램프는 유지해
        /// 그 자리에 쓰러진 채 남는다.
        /// </summary>
        public void ServerSetStunned(bool stunned)
        {
            if (!IsServer)
            {
                return;
            }

            _stunned = stunned;
            if (stunned)
            {
                _lastHorizontalVelocity = Vector3.zero;
            }
        }

        public override void OnNetworkSpawn()
        {
            _verticalSpeed = 0f;
            _syncTimer = 0f;
            _attackCooldown = 0f;
            _lastHorizontalVelocity = Vector3.zero;
            _towed = false;
            _stunned = false;

            if (IsServer)
            {
                _variantIndex.Value = _pendingVariantIndex;

                // 풀에서 재사용될 때 이전 변종이 새지 않도록 즉시 되돌린다.
                _pendingVariantIndex = -1;

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
            if (!IsSpawned || Settings == null)
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
            // 견인 중 (M5 5차): 위치는 집게가 대입한다 — 조향·중력·공격·회수 판정을 전부 멈추고
            // 기존 스냅샷 채널의 주기만 올려 끌려오는 모습을 매끄럽게 보여준다 (별도 위치 채널 없음).
            if (_towed)
            {
                ServerSync(Settings.TowSyncHz);
                return;
            }

            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;

            // 그로기 중에는 표적을 잡지 않는다 — 조향·공격이 함께 멈춘다 (중력·지지면은 그대로).
            Transform target = _stunned ? null : FindNearestAliveTarget();

            bool onDeck = IsOnDeck(transform.position);
            bool grounded = onDeck || transform.position.y <= 0.01f;

            // 기절 중 지상 컨베이어는 그랩 관심사(MonsterGrabTarget)가 재바인딩 앵커로 직접
            // 위치를 구동한다 (M5 6차) — 여기서 속도를 더하면 이중 이동이 된다.
            Vector3 horizontalVelocity = _stunned ? Vector3.zero : _lastHorizontalVelocity;
            if (target != null && grounded)
            {
                float chaseSpeed = MonsterSteering.EnforceChaseSpeed(
                    Settings.MoveSpeed, scrollSpeed, Settings.ChaseSpeedMargin);

                bool blocked = ProbeObstacle(out Vector3 obstacleNormal);

                if (onDeck)
                {
                    horizontalVelocity = MonsterSteering.ComputeDeckVelocity(
                        transform.position, target.position, blocked, obstacleNormal, Settings.MoveSpeed);
                }
                else
                {
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
            ServerSync(Settings.SyncHz);
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
                    Settings.AvoidProbeDistance, ~0, QueryTriggerInteraction.Ignore) &&
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

            // 목표가 갑판 위에 있거나 열차 부위(칸·연결부, 열차 발판 내부)면 도약해 승차한다 —
            // 열차를 노릴 때 몸통을 관통해 걸어 들어가지 않고 지붕에 올라 공격하게 한다(§M3).
            bool targetOnDeck = IsOnDeck(target.position)
                || target.position.y >= _trainLayout.DeckHeight - 0.5f
                || IsWithinTrainFootprint(target.position);
            float sideDistance = Mathf.Abs(transform.position.x) - _trainLayout.CarWidth * 0.5f;
            bool alongTrain = transform.position.z > _trainLayout.RearZ - 2f &&
                transform.position.z < _trainLayout.FrontZ + 2f;

            if (targetOnDeck && alongTrain && sideDistance <= Settings.LeapHorizontalRange)
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

            if ((target.position - transform.position).sqrMagnitude <= Settings.AttackRange * Settings.AttackRange)
            {
                IDamageable damageable = target.GetComponent<IDamageable>();
                if (damageable != null && damageable.IsAlive)
                {
                    damageable.ApplyDamage(Settings.AttackDamage, NetworkManager.ServerClientId);
                    _attackCooldown = Settings.AttackInterval;
                }
            }
        }

        private void ServerCheckFellBehind()
        {
            if (_trainLayout != null &&
                transform.position.z < _trainLayout.RearZ - Settings.DespawnBehindMeters)
            {
                // 추격 실패 — 도주 처리 (사망 아님, 이벤트 없음). 풀로 회수한다.
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>스냅샷 송신 — 주기는 상황이 정한다 (평시 <see cref="MonsterSettings.SyncHz"/>, 견인 중 고주기).</summary>
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

        // ── 클라이언트: 스냅샷 보간 (§6.2) ─────────────────────────────────

        private void OnSyncedPositionChanged(Vector3 previous, Vector3 current)
        {
            // 위치 변경 수신 시점에 (위치, 방향) 스냅샷을 쌓는다 — 방향은 같은 틱에 함께 갱신된다.
            _snapshotBuffer.AddSnapshot(current, _syncedYaw.Value, Time.timeAsDouble);
        }

        private void ClientInterpolate()
        {
            double renderTime = Time.timeAsDouble - Settings.InterpolationDelaySeconds;
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

            // 풀 재사용 시 이전 개체의 견인·그로기가 새지 않도록 되돌린다.
            _towed = false;
            _stunned = false;
        }
    }
}
