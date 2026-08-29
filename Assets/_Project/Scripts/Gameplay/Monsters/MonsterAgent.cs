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

        // 편성 상태 — 갑판 반폭(판자 증축 반영) 조회가 매 프레임 여러 번 도는 경로라 캐시한다.
        private ITrainState _trainState;

        private const float Gravity = 25f;

        // 갑판 위 건축물 관통 금지 판정의 몸 반경 여유(m) — 건축 개편 1차 §2.10 최소 구현.
        // 이동 AI 재설계(회피·경로)는 이월이라 상수로 둔다.
        private const float StructureBlockPadding = 0.3f;

        // 표적이 물면에서 이만큼 위에 있어야 "물 밖"으로 본다 (TryBeginSurfaceLeap).
        // 수영 중인 플레이어는 수면에 걸쳐 있으므로 여유를 둬야 물속 표적에 튀어오르지 않는다.
        private const float SurfaceLeapEmergeMargin = 1f;

        // 급강하가 표적 위 이 높이까지 내려온다 (ServerSimulateAerial). 머리 위에 서야
        // 공격 사거리 안에 들면서도 몸에 박히지 않는다.
        private const float AerialStrikeClearance = 1.6f;

        private readonly NetworkVariable<Vector3> _syncedPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<float> _syncedYaw = new NetworkVariable<float>();

        /// <summary>이 개체의 변종 인덱스 (−1 = 기본 설정). 스폰 시 1회 확정되고 이후 바뀌지 않는다.</summary>
        private readonly NetworkVariable<int> _variantIndex = new NetworkVariable<int>(-1);

        private readonly MotionSnapshotBuffer _snapshotBuffer = new MotionSnapshotBuffer();

        private float _verticalSpeed;

        // 이 개체가 서는 바닥 — 물 지역이면 물면이다 (ResolveSurfaceY 주석). 스폰 시 확정.
        private float _surfaceY;

        // 하늘 위협의 왕복 국면과 남은 체공 시간 (ServerSimulateAerial). 서버 전용 —
        // 클라는 위치 스냅샷만 받으므로 국면을 복제할 필요가 없다.
        private AerialPhase _aerialPhase;
        private float _hoverTimer;
        private float _syncTimer;
        private float _attackCooldown;
        private Vector3 _lastHorizontalVelocity;
        private int _pendingVariantIndex = -1;

        // M5 5차 — 집게 견인/무력화 중 시뮬레이션 정지 (서버 전용 상태. 표시는 복제된 위치로 따라온다).
        private bool _towed;
        private bool _stunned;

        // M7 1차 — 스탬피드 통과 모드 (서버 전용 상태). 추격·공격 조향을 끄고 열차와 평행한
        // 직선 주행만 한다 (접촉 피해는 유지). 클라이언트는 평소처럼 스냅샷 보간만 한다.
        private bool _passThrough;

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
        /// 통과 모드 전환 (M7 1차 — 서버 전용, 스탬피드). 켜면 추격·공격·도약 조향이 꺼지고
        /// 열차와 평행한 -Z 직선 주행(스크롤 상대 속도)만 한다. 접촉 피해는 유지된다.
        /// 스폰 직후 호출한다 — 풀 재사용 시 <see cref="OnDespawned"/>가 되돌린다.
        /// </summary>
        public void ServerSetPassThrough(bool passThrough)
        {
            if (IsServer)
            {
                _passThrough = passThrough;
            }
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
            _surfaceY = ResolveSurfaceY();

            if (IsServer)
            {
                _variantIndex.Value = _pendingVariantIndex;

                // 풀에서 재사용될 때 이전 변종이 새지 않도록 즉시 되돌린다.
                _pendingVariantIndex = -1;

                // 하늘 위협은 순항 고도에서 시작한다 — 변종이 확정된 **뒤에** 읽어야 한다.
                // 스포너가 놓은 자리(물면·지면)에서 올라오게 두면 물에서 솟는 것처럼 보인다.
                _aerialPhase = AerialPhase.Cruise;
                _hoverTimer = 0f;
                if (Settings.AerialDiver)
                {
                    Vector3 spawn = transform.position;
                    spawn.y = Settings.CruiseAltitudeY;
                    transform.position = spawn;
                }

                _syncedPosition.Value = transform.position;
            }
            else
            {
                _snapshotBuffer.Clear();

                // 스폰 위치를 첫 스냅샷으로 심는다 — 없으면 첫 동기화(최대 1/SyncHz초)까지
                // 보간 표본이 0개라 개체가 스폰 지점에 붙었다가 튄다. 연속 유입되는
                // 스탬피드 무리에서 이 공백이 "생성 시 떨림"으로 누적돼 보였다.
                _snapshotBuffer.AddSnapshot(_syncedPosition.Value, _syncedYaw.Value, Time.timeAsDouble);

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

            // 통과 모드 (M7 1차 — 스탬피드): 추격·도약 없이 직선 주행 + 접촉 피해만.
            if (_passThrough)
            {
                ServerSimulatePassThrough();
                return;
            }

            if (Settings.AerialDiver)
            {
                ServerSimulateAerial();
                return;
            }

            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;

            // 그로기 중에는 표적을 잡지 않는다 — 조향·공격이 함께 멈춘다 (중력·지지면은 그대로).
            Transform target = _stunned ? null : FindNearestAliveTarget();

            bool onDeck = IsOnDeck(transform.position);

            // 지지면은 지역이 정한다 — 물 지역이면 수면이 바닥이다. 0으로 두면 도약 중
            // 수면과 0 사이 구간이 통째로 "접지"로 잡혀 공중에서 추격 속도가 되살아난다.
            bool grounded = onDeck || transform.position.y <= _surfaceY + 0.01f;

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

                    if (Settings.AquaticLeaper)
                    {
                        TryBeginSurfaceLeap(target);
                    }
                    else
                    {
                        TryBeginDeckLeap(target);
                    }
                }
            }

            // 관통 금지 (건축 개편 1차 — 계획서 §2.10 최소 구현): 갑판 위 이동 후 위치가 건축물 점유
            // 셀과 겹치면 수평 이동을 취소한다. 판정은 물리 쿼리가 아니라 그리드 점유 조회(복제 데이터)다.
            // 막힌 몬스터는 별도 AI 없이도 성립한다 — 건축물이 타깃 등록소에 있으므로 길을 막은
            // 건축물이 곧 최근접 타깃이 되어 "막히면 부순다"가 자연 발생한다.
            if (onDeck && horizontalVelocity.sqrMagnitude > 0f
                && ServiceLocator.TryGet(out ITrainState trainState)
                && trainState.IsStructureBlockingAt(
                    transform.position + horizontalVelocity * Time.deltaTime, StructureBlockPadding))
            {
                horizontalVelocity = Vector3.zero;
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
        /// 통과 모드 시뮬레이션 (M7 1차 — 스탬피드): 열차와 평행한 -Z 직선 주행(자체 주행 + 스크롤
        /// 가산). 열차·플레이어를 추격하지 않고, 접촉 범위 안의 플레이어에게만 피해를 준다
        /// (열차 무관심 — 방심 방지의 축은 "치이면 아프다"). 후방 이탈 회수는 일반 경로와 같다.
        /// </summary>
        private void ServerSimulatePassThrough()
        {
            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;

            // 그로기(파지 해제 직후)에는 주행이 잠시 멈춘다 — 일반 모드와 같은 이탈 틈.
            Vector3 horizontalVelocity = _stunned
                ? Vector3.zero
                : StampedeMath.ComputePassVelocity(Settings.MoveSpeed, scrollSpeed);

            ApplyVerticalMotion(false);

            Vector3 motion = (horizontalVelocity + Vector3.up * _verticalSpeed) * Time.deltaTime;
            transform.position += motion;
            ClampToSupport();

            _lastHorizontalVelocity = horizontalVelocity;
            FaceVelocity(horizontalVelocity, null);

            // 접촉 피해 — 기존 공격 판정(사거리 + 쿨다운)을 플레이어 한정으로 재사용한다.
            ServerTryAttack(_stunned ? null : FindNearestPlayer(out _));

            ServerCheckFellBehind();
            ServerSync(Settings.SyncHz);
        }

        /// <summary>
        /// 하늘 위협 시뮬레이션 (바다 계획 §13 — ㄷ 급강하 왕복).
        ///
        /// <para><b>지상 경로와 겹치지 않는다.</b> 중력·지지면 클램프·장애물 회피를 쓰지 않고
        /// 고도를 국면이 직접 몬다 — 하늘에는 밟을 것도 막을 것도 없다. 대신 <b>월드 스크롤</b>은
        /// 그대로 받는다(안 받으면 열차만 앞서가고 새는 뒤로 흘러간다).</para>
        ///
        /// <para>왕복은 <b>순항 → 강하 → 체공 → 상승</b> 한 방향으로만 돈다. 표적을 잃으면
        /// 상승으로 빠져나가므로, 표적이 잠깐 사라져도 공중에서 굳지 않는다.</para>
        /// </summary>
        private void ServerSimulateAerial()
        {
            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;
            Transform target = _stunned ? null : FindNearestAliveTarget();

            Vector3 position = transform.position;
            float cruiseY = Settings.CruiseAltitudeY;

            // 칠 수 있는 높이는 표적이 정한다 — 갑판이든 상판이든 그 바로 위로 내려온다.
            float strikeY = (target != null ? target.position.y : 0f) + AerialStrikeClearance;

            float horizontal = target == null
                ? float.PositiveInfinity
                : Vector2.Distance(
                    new Vector2(position.x, position.z),
                    new Vector2(target.position.x, target.position.z));

            AerialPhase next = AerialDiveMath.ResolvePhase(
                _aerialPhase, position.y, strikeY, cruiseY,
                horizontal, Settings.LeapHorizontalRange, _hoverTimer, target != null);

            if (next == AerialPhase.Hover && _aerialPhase != AerialPhase.Hover)
            {
                _hoverTimer = Settings.HoverSeconds;
            }

            _aerialPhase = next;
            if (_aerialPhase == AerialPhase.Hover)
            {
                _hoverTimer -= Time.deltaTime;
            }

            float altitudeTarget = AerialDiveMath.TargetAltitude(_aerialPhase, cruiseY, strikeY);
            float speed = _aerialPhase == AerialPhase.Climb ? Settings.ClimbSpeed : Settings.DiveSpeed;
            position.y = AerialDiveMath.StepAltitude(position.y, altitudeTarget, speed, Time.deltaTime);

            // 그로기 중에는 따라가지 않는다 — 지상 경로와 같은 이탈 틈이다. 고도는 계속 몬다
            // (공중에서 멈춰 서면 떨어지지도 오르지도 않는 채 굳는다).
            Vector3 horizontalVelocity = _stunned || target == null
                ? new Vector3(0f, 0f, -scrollSpeed)
                : AerialDiveMath.ComputeApproachVelocity(
                    position, target.position, Settings.MoveSpeed, scrollSpeed);

            position += new Vector3(horizontalVelocity.x, 0f, horizontalVelocity.z) * Time.deltaTime;
            transform.position = position;

            _lastHorizontalVelocity = horizontalVelocity;
            FaceVelocity(horizontalVelocity, target);

            // 공격은 기존 판정을 그대로 쓴다 — 사거리 안에 들어오는 것은 체공 국면뿐이다.
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
            float sideDistance = Mathf.Abs(transform.position.x) - DeckHalfWidth(transform.position);
            bool alongTrain = transform.position.z > _trainLayout.RearZ - 2f &&
                transform.position.z < _trainLayout.FrontZ + 2f;

            if (targetOnDeck && alongTrain && sideDistance <= Settings.LeapHorizontalRange)
            {
                // 갑판 높이 + 여유를 넘는 포물선 도약 초기 속도.
                //
                // **어디서 뛰는지를 넣는다.** 예전에는 지면 y 0을 전제로 갑판 높이만 봤는데,
                // 바다는 물면이 −4라 그대로 두면 정점이 0.57에 그쳐 갑판에 4 m 모자란다 —
                // 물에서 올라오는 몬스터가 열차에 영영 닿지 못한다 (바다 계획 §12.1).
                _verticalSpeed = MonsterLeapMath.LeapSpeed(
                    _surfaceY, _trainLayout.DeckHeight + 1f, Gravity);
            }
        }

        /// <summary>
        /// 물에서 <b>튀어오른다</b> (ㄴ 물고기 점프 — 바다 계획 §8.2).
        ///
        /// <para>갑판 도약과 두 가지가 다르다. ① 목표가 <b>열차가 아니라 정점 높이</b>다 —
        /// 결정 ⑨의 y +1.5라 상판 위 플레이어는 닿고 <b>갑판 위는 안전하다.</b>
        /// ② 물 밖 표적에게만 뛴다 — 잠수 중인 표적에게는 도약 없이 그대로 추격한다.</para>
        ///
        /// <para>착지는 <see cref="ClampToSupport"/>가 막는다 — 이 변종은 어디에도 올라서지 않고
        /// 물로 되떨어진다.</para>
        /// </summary>
        private void TryBeginSurfaceLeap(Transform target)
        {
            if (_verticalSpeed > 0f)
            {
                return;
            }

            if (!MonsterLeapMath.ShouldSurfaceLeap(
                transform.position, target.position, _surfaceY,
                Settings.LeapHorizontalRange, SurfaceLeapEmergeMargin))
            {
                return;
            }

            _verticalSpeed = MonsterLeapMath.LeapSpeed(_surfaceY, Settings.LeapApexY, Gravity);
        }

        /// <summary>
        /// 이 개체가 서는 바닥 높이 — 물 지역이면 <b>물면</b>, 아니면 지면(0).
        ///
        /// <para><b>왜 필요한가.</b> 바다 1차는 <see cref="MonsterWaveSpawner"/>가 스폰 y만 물면으로
        /// 내렸다. 그런데 여기 클램프가 0으로 남아 있어 <b>다음 프레임에 도로 끌어올려졌다</b> —
        /// 몬스터가 수면 위를 걸어오는 것이 그것이다 (바다 계획 §12.1).</para>
        ///
        /// <para>스폰 시 한 번만 읽는다. 지역은 며칠에 한 번 바뀌고 웨이브는 새벽에 회수되므로
        /// 개체 수명 안에서 값이 달라질 일이 없다 — 매 프레임 서비스를 조회할 이유가 없다.
        /// 물이 없는 지역은 <c>SurfaceY</c>가 0이라 <b>동작이 그대로다.</b></para>
        /// </summary>
        private static float ResolveSurfaceY()
        {
            return WaterSurfaceQuery.SurfaceY();
        }

        private void ApplyVerticalMotion(bool onDeck)
        {
            if (_verticalSpeed > 0f || (!onDeck && transform.position.y > _surfaceY + 0.01f))
            {
                _verticalSpeed -= Gravity * Time.deltaTime;
            }
        }

        private void ClampToSupport()
        {
            Vector3 position = transform.position;

            if (_verticalSpeed <= 0f)
            {
                // 물고기 점프는 갑판에 <b>내려서지 않는다</b> — 튀어올랐다 물로 되떨어진다.
                // 올라서게 두면 물 위의 위협이 아니라 그냥 갑판에 오르는 몬스터가 된다.
                bool canLand = !Settings.AquaticLeaper;

                if (canLand && IsWithinTrainFootprint(position) && _trainLayout != null &&
                    position.y <= _trainLayout.DeckHeight && position.y > _trainLayout.DeckHeight - 1.5f)
                {
                    position.y = _trainLayout.DeckHeight;
                    _verticalSpeed = 0f;
                }
                else if (position.y < _surfaceY)
                {
                    position.y = _surfaceY;
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
            // Z 범위를 먼저 거른다 — 폭 조회는 편성을 훑으므로, 열차 근처가 아닌 개체가 매 프레임
            // 그 비용을 내지 않게 한다 (건축 개편 마무리 패스).
            return _trainLayout != null &&
                position.z >= _trainLayout.RearZ && position.z <= _trainLayout.FrontZ &&
                Mathf.Abs(position.x) <= DeckHalfWidth(position) + 0.5f;
        }

        /// <summary>
        /// 그 지점의 갑판 반폭 — 판자 증축이 넓힌 폭을 반영한다 (건축 개편 3차 §2.9 — 판자 위로도
        /// 기어오를 수 있어야 한다). 편성 상태를 못 읽으면 칸 실물 반폭으로 물러선다.
        /// </summary>
        private float DeckHalfWidth(Vector3 position)
        {
            if (_trainState == null && !ServiceLocator.TryGet(out _trainState))
            {
                return _trainLayout.CarWidth * 0.5f;
            }

            return _trainState.GetDeckHalfWidthAt(position);
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

            // 풀 재사용 시 이전 개체의 견인·그로기·통과 모드가 새지 않도록 되돌린다.
            _towed = false;
            _stunned = false;
            _passThrough = false;
        }
    }
}
