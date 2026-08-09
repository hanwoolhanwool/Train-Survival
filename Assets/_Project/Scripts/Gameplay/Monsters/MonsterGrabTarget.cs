using Game.Core.Pooling;
using Game.Gameplay.Harpoon;
using Game.Gameplay.Train;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 몬스터의 집게 그랩 관심사 (M5 5차) — <see cref="MonsterAgent"/>(이동)·<see cref="MonsterHealth"/>(체력)와
    /// 나란한 세 번째 관심사로 분리한다 (SRP: 기존 두 파일의 책임은 그대로 둔다).
    ///
    /// M1 그랩 파이프라인을 그대로 재사용한다 — 권위 구조가 자원과 동일하고(그랩 확정·견인 = 호스트),
    /// 달라지는 것은 <b>도착했을 때 벌어지는 일</b>뿐이다: 자원은 수납 후 소멸, 몬스터는 소멸하지 않고
    /// <b>집게에 매달린 채 유지된다</b> (M5 6차 파지 — 든 채 이동해 아군 앞이나 열차 바퀴에 넘긴다).
    /// 파지에서 놓이면 잠깐 기절했다가 추격을 재개한다. 기절은 이 관심사의 내부 상태다 —
    /// 피해 배율 같은 외부 소비자는 없다 (처형 축 제거).
    ///
    /// 예측 고정은 no-op다 — 자원은 각 피어가 컨베이어로 로컬 유도하기 때문에 그랩 전환 순간의 스냅을
    /// 없앨 예측이 필요했지만, 몬스터는 원래부터 서버 스냅샷 보간만 하므로 그 간극이 존재하지 않는다.
    /// </summary>
    [RequireComponent(typeof(MonsterAgent))]
    [RequireComponent(typeof(MonsterHealth))]
    public sealed class MonsterGrabTarget : NetworkBehaviour, IGrabbable, IHoldAttachable, IPoolable
    {
        [Tooltip("그로기 표현을 칠할 렌더러 (Body).")]
        [SerializeField] private Renderer[] _tintRenderers;

        [Tooltip("그로기 자세로 기울일 표현 트랜스폼 (Body). 판정에는 영향이 없다.")]
        [SerializeField] private Transform _visual;

        [Tooltip("기절 중 덧칠할 색 — '지금 무력화 상태다'를 한눈에 보이게 한다.")]
        [SerializeField] private Color _stunnedColor = new Color(0.95f, 0.85f, 0.25f, 1f);

        [Tooltip("그로기 자세로 기울이는 각도 (도).")]
        [SerializeField, Range(0f, 90f)] private float _stunnedPitch = 60f;

        private static MaterialPropertyBlock _tintBlock;

        // 그로기 종료 서버 시각 — 잔여 시간을 매 프레임 복제하지 않고 <b>끝나는 시점</b>만 한 번 복제한다
        // (스폰 중인 몬스터마다 초당 수십 번 쓰는 것을 피한다). ServerTime은 전 피어에서 동기화된다.
        private readonly NetworkVariable<double> _stunEndTime = new NetworkVariable<double>();

        // 파지 홀더 (M5 6차 2차) — 전 피어가 홀더의 파지 앵커에 <b>로컬 부착</b>해 홀더와 한 몸처럼
        // 움직이게 한다. 스냅샷 보간(지연 0.18 s)으로는 "뒤따라오는" 느낌이 나기 때문이다.
        // 투척 비행 중에는 서버가 default로 되돌려 부착을 풀고 스냅샷 표시로 복귀시킨다.
        private readonly NetworkVariable<NetworkObjectReference> _holder =
            new NetworkVariable<NetworkObjectReference>();

        private MonsterAgent _agent;
        private MonsterHealth _health;
        private bool _claimed;

        // 서버 전용 — 마지막 그래버 (M5 6차 즉사 존의 처치 귀속: 바퀴에 넣은 사람의 킬).
        private ulong _lastGrabberClientId;

        // 서버 전용 — 도착 후 파지 유지 중인가 (M5 6차). 견인(도착 전)과 구분해
        // "파지에서 놓인 경우에만" 기절에 들어간다 — 견인 중 취소는 즉시 복귀다 (5차 E13 유지).
        private bool _held;

        private bool _presentationStunned;
        private Color[] _baseColors;
        private Quaternion _visualBaseRotation;

        public GrabKind Kind => GrabKind.Reel;

        /// <summary>무게 등급은 변종이 정한다 — 일반형·돌진형 1 / 돌격형·도약형 2 / 3은 대형 변종 예약.</summary>
        public int GrabWeight
        {
            get
            {
                MonsterSettings settings = _agent != null ? _agent.ActiveSettings : null;
                return settings != null ? settings.GrabWeight : 1;
            }
        }

        /// <summary>살아 있고, 아무도 잡고 있지 않고(견인·파지 포함), 기절이 아닐 때만 잡을 수 있다.</summary>
        public bool IsAvailableForGrab =>
            IsSpawned && _health != null && _health.IsAlive && !_claimed && !IsStunned;

        public bool IsClaimed => _claimed;

        public bool IsStunned => IsSpawned && NetworkManager != null
            && _stunEndTime.Value > NetworkManager.ServerTime.Time;

        private void Awake()
        {
            _agent = GetComponent<MonsterAgent>();
            _health = GetComponent<MonsterHealth>();

            if (_visual != null)
            {
                _visualBaseRotation = _visual.localRotation;
            }

            CacheBaseColors();
        }

        public override void OnNetworkSpawn()
        {
            _claimed = false;

            if (IsServer)
            {
                _stunEndTime.Value = 0d;
                _holder.Value = default;

                // 풀 재사용 시 이전 개체의 귀속이 새지 않게 환경 사망 기본값으로 되돌린다.
                _lastGrabberClientId = NetworkManager.ServerClientId;
            }

            ApplyStunPresentation(IsStunned);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                // 견인·파지·기절 중 사망·회수로 사라져도 다음 재사용에 상태가 새지 않게 한다.
                _claimed = false;
                _held = false;
                _agent?.ServerSetTowed(false);
                _agent?.ServerSetStunned(false);
            }

            ApplyStunPresentation(false);
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            // 표현은 전 피어가 각자 갱신한다 — 복제된 종료 시각이 같으므로 같은 순간에 켜지고 꺼진다.
            ApplyStunPresentation(IsStunned);

            // 기절 해제 확정은 서버만 — 정상 복귀하고 다시 그랩 대상이 된다.
            if (IsServer && _stunEndTime.Value > 0d && !IsStunned)
            {
                _stunEndTime.Value = 0d;
                _agent?.ServerSetStunned(false);
            }

            ServerCheckWheelKillZone();
        }

        /// <summary>
        /// 파지 표시 — 전 피어가 홀더의 파지 앵커에 <b>로컬 부착</b>한다 (M5 6차 2차).
        /// 스냅샷 보간(지연 0.18 s)은 홀더가 움직일 때 "뒤따라오는" 느낌을 만들므로, 각자 화면의
        /// 홀더 위치(소유자 = 즉시, 원격 = 보간)를 기준으로 매 프레임 같은 앵커 계산을 대입해
        /// 홀더와 몬스터가 한 몸처럼 움직이게 한다. 판정 위치는 서버가 같은 계산으로 확정한다.
        /// <see cref="MonsterAgent"/>의 보간 대입(Update)을 덮어써야 하므로 LateUpdate다.
        /// </summary>
        private void LateUpdate()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (_holder.Value.TryGet(out NetworkObject holderObject)
                && holderObject.TryGetComponent(out HarpoonController harpoon))
            {
                transform.position = harpoon.ComputeHoldAnchor();
            }
        }

        /// <summary>
        /// 열차 하부 즉사 존 (M5 6차) — <b>견인·파지·기절 상태만</b> 즉사한다. 자유 몬스터는 제외 —
        /// 추격 중 열차 밑을 스치기만 해도 죽으면 밤 웨이브가 자멸한다 (존이 밸런스를 삼키지 않는 안전선).
        /// 자기 상태를 이미 아는 이곳이 판정 주체라 조회가 없다. 처치 귀속 = 마지막 그래버.
        /// </summary>
        private void ServerCheckWheelKillZone()
        {
            if (!IsServer || (!_claimed && !IsStunned) || _health == null || !_health.IsAlive)
            {
                return;
            }

            TrainLayoutSettings layout = _agent != null ? _agent.TrainLayout : null;
            if (layout == null || !TrainLayoutMath.IsInWheelKillZone(
                    transform.position, layout.CarWidth * 0.5f, layout.RearZ, layout.FrontZ,
                    layout.WheelKillHeight))
            {
                return;
            }

            // 기존 사망 확정·킬 카운트·이벤트 경로 재사용 — 어떤 체력 배율에도 반드시 즉사한다.
            // 파지 중이었다면 디스폰을 집게의 스폰 검사가 감지해 강제 해제로 로프를 끊는다 (놓기 ③).
            _health.ApplyDamage(float.MaxValue, _lastGrabberClientId);
        }

        // ── IGrabbable — 그랩 파이프라인 (권위 = 호스트) ─────────────────────

        public bool TryClaimGrab(ulong grabberClientId)
        {
            if (!IsServer || !IsAvailableForGrab)
            {
                return false;
            }

            _claimed = true;
            _lastGrabberClientId = grabberClientId;

            // 끌려오는 동안은 조향·중력·공격이 멈춘다 — 견인 중에 때리지 못한다.
            _agent?.ServerSetTowed(true);
            return true;
        }

        /// <summary>
        /// 견인 위치 대입 — 자원과 달리 별도 위치 채널을 두지 않는다. 위치를 직접 옮기면
        /// <see cref="MonsterAgent"/>가 견인 주기(TowSyncHz)로 기존 스냅샷 채널에 실어 보낸다.
        /// </summary>
        public void UpdateTowPosition(Vector3 position)
        {
            if (!IsServer || !_claimed)
            {
                return;
            }

            transform.position = position;
        }

        public void ReleaseGrab()
        {
            if (!IsServer || !_claimed)
            {
                return;
            }

            bool wasHeld = _held;
            _claimed = false;
            _held = false;
            _holder.Value = default;
            _agent?.ServerSetTowed(false);

            // 파지에서 놓인 경우에만 기절 (M5 6차) — 추격 재개 전의 이탈 틈.
            // 견인 중(도착 전) 취소는 즉시 복귀다 — 기절 없음 (5차 E13 유지).
            if (!wasHeld)
            {
                return;
            }

            MonsterSettings settings = _agent != null ? _agent.ActiveSettings : null;
            float duration = settings != null ? settings.StunDurationSeconds : 0f;
            if (duration <= 0f)
            {
                // 지속 0 = 기절 없음 (에셋으로 끌 수 있는 축).
                return;
            }

            _stunEndTime.Value = NetworkManager.ServerTime.Time + duration;
            _agent?.ServerSetStunned(true);
        }

        /// <summary>
        /// 회수 도착 — 몬스터는 <b>소멸하지도 놓이지도 않는다</b> (M5 6차 파지).
        /// 점유·견인 정지를 유지한 채 집게에 매달린다 — 위치는 집게가 파지 앵커로 계속 대입한다.
        /// 놓기(<see cref="ReleaseGrab"/>)가 일어나면 그때 잠깐 기절한다.
        /// </summary>
        public GrabCompletionResult TryCompleteGrab(in GrabCompletion completion)
        {
            if (!IsServer || !_claimed)
            {
                return GrabCompletionResult.Rejected;
            }

            _held = true;

            // 홀더를 복제한다 — 전 피어가 홀더의 파지 앵커에 로컬 부착해 한 몸처럼 움직인다.
            if (completion.Grabber != null
                && completion.Grabber.TryGetComponent(out NetworkObject holderObject))
            {
                _holder.Value = holderObject;
            }

            return GrabCompletionResult.Held;
        }

        /// <summary>서버 전용 — 투척 비행 시작 등 앵커 추종이 끊기면 부착 표시를 푼다 (M5 6차 2차).</summary>
        public void ServerSetHoldAttached(bool attached)
        {
            if (IsServer && !attached)
            {
                _holder.Value = default;
            }
        }

        // 몬스터는 서버 스냅샷 보간만 하므로 예측 고정이 필요 없다 (§11 수정안 A의 RTT 간극이 없다).
        public void BeginPredictedTow()
        {
        }

        public void CancelPredictedTow()
        {
        }

        // ── 표현 — 그로기 색·자세 (판정에 영향 없음) ─────────────────────────

        private void ApplyStunPresentation(bool stunned)
        {
            if (_presentationStunned == stunned)
            {
                return;
            }

            _presentationStunned = stunned;

            if (_visual != null)
            {
                _visual.localRotation = stunned
                    ? _visualBaseRotation * Quaternion.Euler(_stunnedPitch, 0f, 0f)
                    : _visualBaseRotation;
            }

            if (_tintRenderers == null)
            {
                return;
            }

            _tintBlock ??= new MaterialPropertyBlock();
            for (int i = 0; i < _tintRenderers.Length; i++)
            {
                if (_tintRenderers[i] == null)
                {
                    continue;
                }

                _tintBlock.SetColor("_BaseColor",
                    stunned ? _stunnedColor : (_baseColors != null ? _baseColors[i] : Color.white));
                _tintRenderers[i].SetPropertyBlock(_tintBlock);
            }
        }

        /// <summary>그로기 해제 시 되돌릴 원래 색 — 프리팹 머티리얼 값을 스폰 전에 한 번만 읽는다.</summary>
        private void CacheBaseColors()
        {
            if (_tintRenderers == null)
            {
                return;
            }

            _baseColors = new Color[_tintRenderers.Length];
            for (int i = 0; i < _tintRenderers.Length; i++)
            {
                Material material = _tintRenderers[i] != null ? _tintRenderers[i].sharedMaterial : null;
                _baseColors[i] = material != null && material.HasProperty("_BaseColor")
                    ? material.GetColor("_BaseColor")
                    : Color.white;
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _claimed = false;
            _held = false;
            ApplyStunPresentation(false);
        }
    }
}
