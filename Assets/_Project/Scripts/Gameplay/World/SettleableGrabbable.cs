using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Harpoon;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 컨베이어 세계에 놓이는 집게(릴) 대상의 공용 안착 파이프라인 (M5 8차 — 7차 축의 공용화).
    /// 자원 노드·창고 보따리가 함께 쓴다. 위치 동기화 (네트워크 문서 §8 해소 항목 ①안):
    /// 평소에는 스폰 시점 (누적 거리, 오프셋)만 동기화하고 각 피어가 위치를 로컬 유도한다 (컨베이어).
    /// 그랩 확정 시 열차 프레임 소속으로 전환되어 견인 위치를 NetworkVariable(틱 30 Hz)로 동기화하고,
    /// 클라이언트는 짧은 보간으로 표시한다 (슬라이스 스펙 §2.4).
    /// 갑판 위에서 해제되면 <b>갑판 휴지</b>(M5 7차 A3) — 스크롤 유도를 끄고 그 자리에 남아
    /// 재그랩을 기다리며, 휴지한 칸이 이탈로 밀려나면 함께 따라간다 (7차 2차 D9).
    /// 해제 낙하(A6)는 최종 안착 값을 한 번에 재바인딩하고 <b>각 피어가 로컬로 하강을 재생</b>한다
    /// (컨베이어와 같은 규약 — 프레임 단위 동기화가 없어 떨림이 없다. 7차 2차 C3).
    /// </summary>
    public abstract class SettleableGrabbable : NetworkBehaviour, IGrabbable, IPoolable
    {
        private const ulong NoGrabber = ulong.MaxValue;

        [SerializeField, Min(1f)] private float _towInterpolationRate = 20f;

        [Tooltip("해제 낙하의 하강 속도 (m/s) — 중력이 없어 스폰 Y를 이 속도로 내린다 (M5 7차 A6).")]
        [SerializeField, Min(0.1f)] private float _fallSpeed = 6f;

        private readonly NetworkVariable<Vector3> _spawnPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<float> _spawnDistance = new NetworkVariable<float>();
        private readonly NetworkVariable<bool> _isTowed = new NetworkVariable<bool>();
        private readonly NetworkVariable<Vector3> _towPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<ulong> _grabberClientId = new NetworkVariable<ulong>(NoGrabber);

        // 갑판 휴지 (M5 7차 A3) — true면 스크롤 유도를 끄고 재바인딩 위치에 고정된다 (열차 프레임 소속).
        private readonly NetworkVariable<bool> _isDeckResting = new NetworkVariable<bool>();

        // 휴지한 칸 (7차 2차 D9 — 이탈 추종): 휴지 이후 칸이 밀려난 만큼 z를 따라간다. -1 = 없음.
        private readonly NetworkVariable<int> _deckCarIndex = new NetworkVariable<int>(-1);
        private readonly NetworkVariable<float> _deckRestEjectOffset = new NetworkVariable<float>();

        private Vector3 _pendingSpawnPosition;
        private float _pendingSpawnDistance;
        private bool _hasPendingBinding;

        // 갑판 휴지 상태로 곧바로 스폰 (M5 8차 — 보따리 건축물 파괴 지점). -1 = 예약 없음(월드 스폰).
        private int _pendingDeckCarIndex = -1;
        private float _pendingDeckEjectOffset;

        // 로컬 하강 재생 (M5 7차 A6, 2차 C3) — 해제 순간의 표시 높이에서 안착 높이까지 각 피어가 내린다.
        private bool _falling;
        private float _fallDisplayY;

        // 최초 스폰 시 지면 위 안착 오프셋 — 갑판 휴지 후 재해제돼도 안착 높이의 기준이 유지된다.
        private float _serverRestOffsetY;

        // 클라이언트 로컬 — 쏜 클라이언트의 예측 고정 상태 (동기화되지 않는다).
        private bool _predictedTow;

        public GrabKind Kind => GrabKind.Reel;

        /// <summary>무게 등급 — 파생이 정한다 (자원 = 종류 카탈로그, 보따리 = 고정값).</summary>
        public abstract int GrabWeight { get; }

        public virtual bool IsAvailableForGrab => IsSpawned && !_isTowed.Value;

        public bool IsClaimed => _isTowed.Value;

        /// <summary>갑판 휴지 중인가 (M5 7차 A3) — 열차 프레임 소속이라 후방 회수 대상이 아니다.</summary>
        public bool IsDeckResting => _isDeckResting.Value;

        /// <summary>휴지한 칸의 편성 인덱스 (휴지 중이 아니면 -1) — 칸 소실 시 회수 판정용 (7차 2차).</summary>
        public int DeckCarIndex => _deckCarIndex.Value;

        /// <summary>서버 전용 — 스폰 직전에 (위치, 누적 거리) 바인딩을 예약한다. OnNetworkSpawn에서 동기화된다.</summary>
        public void ServerSetSpawnBinding(Vector3 spawnPosition, float spawnDistance)
        {
            _pendingSpawnPosition = spawnPosition;
            _pendingSpawnDistance = spawnDistance;
            _hasPendingBinding = true;
            _pendingDeckCarIndex = -1;

            // 스폰 Y = 지면 위 안착 오프셋 (지면은 y 0 평면) — 낙하 안착 높이의 기준으로 보관한다.
            _serverRestOffsetY = spawnPosition.y;
        }

        /// <summary>
        /// 서버 전용 — 갑판 휴지 상태로 곧바로 스폰하는 바인딩 예약 (M5 8차 — 보따리 건축물 파괴).
        /// restOffsetY는 갑판/지면 위 안착 오프셋 — 이후 그랩·해제 낙하의 안착 높이 기준이 된다.
        /// </summary>
        public void ServerSetDeckRestBinding(Vector3 position, int carIndex, float ejectOffset, float restOffsetY)
        {
            _pendingSpawnPosition = position;
            _pendingSpawnDistance = 0f;
            _hasPendingBinding = true;
            _pendingDeckCarIndex = carIndex;
            _pendingDeckEjectOffset = ejectOffset;
            _serverRestOffsetY = restOffsetY;
        }

        /// <summary>스폰 지점이 현재 누적 거리 대비 얼마나 뒤로 밀려났는가 (서버 회수 판단용).</summary>
        public float GetMetersBehindSpawn(float currentDistance)
        {
            return currentDistance - _spawnDistance.Value;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _hasPendingBinding)
            {
                _spawnPosition.Value = _pendingSpawnPosition;
                _spawnDistance.Value = _pendingSpawnDistance;
                _isTowed.Value = false;
                _isDeckResting.Value = _pendingDeckCarIndex >= 0;
                _deckCarIndex.Value = _pendingDeckCarIndex;
                _deckRestEjectOffset.Value = _pendingDeckCarIndex >= 0 ? _pendingDeckEjectOffset : 0f;
                _grabberClientId.Value = NoGrabber;
                _hasPendingBinding = false;
                _pendingDeckCarIndex = -1;
            }

            // 해제(견인 → 비견인)를 전 피어가 감지해 자기 표시 높이에서 로컬 하강을 시작한다 (7차 2차 C3).
            _isTowed.OnValueChanged += OnTowedChanged;

            _predictedTow = false;
            ApplyScrolledPosition();
        }

        public override void OnNetworkDespawn()
        {
            _isTowed.OnValueChanged -= OnTowedChanged;
        }

        /// <summary>견인이 풀리는 순간 — 현재 표시 높이에서 안착 위치까지의 하강을 로컬로 시작한다.</summary>
        private void OnTowedChanged(bool previous, bool current)
        {
            if (previous && !current)
            {
                _falling = true;
                _fallDisplayY = transform.position.y;
            }
        }

        /// <summary>
        /// 클라이언트 예측 고정 (§11 게스트 그랩 순간이동 — 수정안 A): 로컬 명중 시점에 컨베이어 유도를
        /// 멈추고 현재 표시 위치에 고정한다. 서버 확정(_isTowed) 도착까지 계속 스크롤에 밀리면
        /// 확정 순간 서버 고정 위치로 되돌아가는 스냅이 생기던 것을 막는다.
        /// </summary>
        public void BeginPredictedTow()
        {
            if (IsServer || !IsAvailableForGrab)
            {
                return;
            }

            _predictedTow = true;
        }

        public void CancelPredictedTow()
        {
            _predictedTow = false;
        }

        public bool TryClaimGrab(ulong grabberClientId)
        {
            if (!IsServer || !IsAvailableForGrab)
            {
                return false;
            }

            // 그랩 확정 = 컨베이어 제외, 열차 프레임 소속 전환 (§2.4). 갑판 휴지·하강 중이었다면 해제한다.
            _towPosition.Value = transform.position;
            _isTowed.Value = true;
            _isDeckResting.Value = false;
            _deckCarIndex.Value = -1;
            _falling = false;
            _grabberClientId.Value = grabberClientId;
            return true;
        }

        public void UpdateTowPosition(Vector3 position)
        {
            if (!IsServer || !_isTowed.Value)
            {
                return;
            }

            _towPosition.Value = position;
            transform.position = position;
        }

        public void ReleaseGrab()
        {
            if (!IsServer || !_isTowed.Value)
            {
                return;
            }

            float currentDistance = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.TraveledDistance : 0f;
            Vector3 dropPosition = transform.position;

            // 프레임 판정 (M5 7차 A3) — 갑판 위 해제는 열차 프레임 휴지(스크롤 제외), 그 외는 월드 재바인딩.
            // 재바인딩 Y는 곧바로 최종 안착 값이다 — 하강 표현은 전 피어가 로컬로 재생하므로 (7차 2차 C3)
            // 프레임 단위 동기화가 없다 (_isTowed 해제를 각 피어가 감지해 자기 표시 높이에서 내려온다).
            float deckHeight = 0f;
            int deckCarIndex = -1;
            bool onDeck = ServiceLocator.TryGet(out Train.ITrainState train)
                && train.TryGetDeckSurface(dropPosition, out deckHeight, out deckCarIndex);

            dropPosition.y = (onDeck ? deckHeight : 0f) + _serverRestOffsetY;

            _spawnPosition.Value = dropPosition;
            _spawnDistance.Value = currentDistance;
            _isDeckResting.Value = onDeck;
            _deckCarIndex.Value = onDeck ? deckCarIndex : -1;
            _deckRestEjectOffset.Value = onDeck && train != null ? train.GetEjectOffset(deckCarIndex) : 0f;
            _isTowed.Value = false;
            _grabberClientId.Value = NoGrabber;
        }

        /// <summary>획득·이관 확정 — 파생이 정한다 (자원 = 수납 후 소멸, 보따리 = 항상 Rejected(운반 전용)).</summary>
        public abstract GrabCompletionResult TryCompleteGrab(in GrabCompletion completion);

        protected virtual void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (_isTowed.Value)
            {
                // 서버 확정 도착 — 예측 고정을 자동 해제하고 견인 보간으로 수렴한다.
                _predictedTow = false;

                if (!IsServer)
                {
                    // 30 Hz 스냅샷 사이를 짧은 지수 보간으로 메운다.
                    float t = 1f - Mathf.Exp(-_towInterpolationRate * Time.deltaTime);
                    transform.position = Vector3.Lerp(transform.position, _towPosition.Value, t);
                }

                return;
            }

            // 안착 목표 — 갑판 휴지면 재바인딩 위치(+ 이탈 추종), 아니면 컨베이어 유도 위치.
            Vector3 rest = GetRestPosition();

            if (_falling)
            {
                // 하강 재생 (M5 7차 A6, 2차 C3) — 각 피어가 해제 순간의 표시 높이에서 안착 높이까지
                // 로컬로 내린다. 동기화는 최종 안착 값 한 번뿐이라 프레임 스텝 떨림이 없다.
                _fallDisplayY = Mathf.MoveTowards(_fallDisplayY, rest.y, _fallSpeed * Time.deltaTime);
                if (Mathf.Approximately(_fallDisplayY, rest.y))
                {
                    _falling = false;
                }

                transform.position = new Vector3(rest.x, _fallDisplayY, rest.z);
                return;
            }

            if (_isDeckResting.Value)
            {
                // 갑판 휴지 (M5 7차 A3) — 열차 프레임 소속: 스크롤 유도 없이 고정된다.
                transform.position = rest;
                return;
            }

            if (_predictedTow)
            {
                // 예측 고정 — 서버 확정/거부 수신까지 현재 위치를 유지한다.
                return;
            }

            transform.position = rest;
        }

        /// <summary>
        /// 비견인 상태의 안착 위치 — 갑판 휴지면 재바인딩 위치에 이탈 추종(7차 2차 D9)을 더하고,
        /// 아니면 컨베이어 유도 위치다. 전 피어가 같은 수식을 로컬 계산한다 (동기화 없는 표시 규약).
        /// </summary>
        private Vector3 GetRestPosition()
        {
            if (_isDeckResting.Value)
            {
                Vector3 rest = _spawnPosition.Value;
                if (_deckCarIndex.Value >= 0 && ServiceLocator.TryGet(out Train.ITrainState train))
                {
                    // 휴지 이후 칸이 이탈로 더 밀려난(재결합으로 돌아온) 만큼 함께 움직인다 (칸은 -z로 밀린다).
                    rest.z -= train.GetEjectOffset(_deckCarIndex.Value) - _deckRestEjectOffset.Value;
                }

                return rest;
            }

            return ServiceLocator.TryGet(out IWorldScrollService scroll)
                ? WorldScrollMath.GetScrolledPosition(_spawnPosition.Value, _spawnDistance.Value, scroll.TraveledDistance)
                : _spawnPosition.Value;
        }

        protected void ApplyScrolledPosition()
        {
            // 늦게 접속한 피어의 첫 표시도 같은 안착 수식을 쓴다 — 휴지 노드는 하강 없이 곧바로 제자리.
            transform.position = GetRestPosition();
        }

        public virtual void OnSpawned()
        {
        }

        public virtual void OnDespawned()
        {
            _hasPendingBinding = false;
            _pendingDeckCarIndex = -1;
            _predictedTow = false;
            _falling = false;
            _serverRestOffsetY = 0f;
        }
    }
}
