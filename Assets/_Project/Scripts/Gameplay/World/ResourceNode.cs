using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Harpoon;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지상 소형 자원 (돌·나뭇가지 더미) — 집게 그랩 대상.
    /// 위치 동기화 (네트워크 문서 §8 해소 항목 ①안):
    /// 평소에는 스폰 시점 (누적 거리, 오프셋)만 동기화하고 각 피어가 위치를 로컬 유도한다 (컨베이어).
    /// 그랩 확정 시 열차 프레임 소속으로 전환되어 견인 위치를 NetworkVariable(틱 30 Hz)로 동기화하고,
    /// 클라이언트는 짧은 보간으로 표시한다 (슬라이스 스펙 §2.4).
    /// </summary>
    public sealed class ResourceNode : NetworkBehaviour, IGrabbable, IPoolable
    {
        private const ulong NoGrabber = ulong.MaxValue;

        [SerializeField, Min(1f)] private float _towInterpolationRate = 20f;

        [Tooltip("서버가 종류를 주입하지 않았을 때의 기본 자원 종류.")]
        [SerializeField] private Inventory.ResourceType _defaultResourceType = Inventory.ResourceType.Wood;

        [Tooltip("종류 식별 색·표시명 조회용 카탈로그 — 전 종류가 한 프리팹을 공유하므로 색이 외형 구분이다.")]
        [SerializeField] private Inventory.ResourceCatalog _catalog;

        [Tooltip("종류 색을 칠할 렌더러 (Visual).")]
        [SerializeField] private Renderer[] _tintRenderers;

        private readonly NetworkVariable<Vector3> _spawnPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<float> _spawnDistance = new NetworkVariable<float>();
        private readonly NetworkVariable<bool> _isTowed = new NetworkVariable<bool>();
        private readonly NetworkVariable<Vector3> _towPosition = new NetworkVariable<Vector3>();
        private readonly NetworkVariable<ulong> _grabberClientId = new NetworkVariable<ulong>(NoGrabber);

        // 자원 종류 — 몬스터 변종과 같은 규약: 프리팹을 늘리지 않고 인덱스(byte)를 복제해 각 피어가 카탈로그를 조회한다.
        private readonly NetworkVariable<byte> _syncedResourceType = new NetworkVariable<byte>();

        private static MaterialPropertyBlock _tintBlock;

        private Vector3 _pendingSpawnPosition;
        private float _pendingSpawnDistance;
        private bool _hasPendingBinding;
        private Inventory.ResourceType _pendingResourceType;
        private bool _hasPendingResourceType;
        private bool _acquired;

        // 클라이언트 로컬 — 쏜 클라이언트의 예측 고정 상태 (동기화되지 않는다).
        private bool _predictedTow;

        public GrabKind Kind => GrabKind.Reel;

        /// <summary>채집 시 수납되는 자원 종류 — 스폰 동기화 후에는 복제 값, 그 외에는 프리팹 기본값.</summary>
        public Inventory.ResourceType ResourceType => IsSpawned
            ? (Inventory.ResourceType)_syncedResourceType.Value
            : _defaultResourceType;

        /// <summary>무게 등급은 종류가 정한다 (M5 5차) — 미등재 종류는 1이라 기존 5종의 채집 경로가 유지된다.</summary>
        public int GrabWeight => _catalog != null ? _catalog.GetGrabWeight(ResourceType) : 1;

        /// <summary>서버 전용 — 스폰 직전에 자원 종류를 예약한다. OnNetworkSpawn에서 동기화된다.</summary>
        public void ServerSetResourceType(Inventory.ResourceType type)
        {
            _pendingResourceType = type;
            _hasPendingResourceType = true;
        }

        public bool IsAvailableForGrab => IsSpawned && !_acquired && !_isTowed.Value;

        public bool IsClaimed => _isTowed.Value;

        /// <summary>서버 전용 — 스폰 직전에 (위치, 누적 거리) 바인딩을 예약한다. OnNetworkSpawn에서 동기화된다.</summary>
        public void ServerSetSpawnBinding(Vector3 spawnPosition, float spawnDistance)
        {
            _pendingSpawnPosition = spawnPosition;
            _pendingSpawnDistance = spawnDistance;
            _hasPendingBinding = true;
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
                _grabberClientId.Value = NoGrabber;
                _hasPendingBinding = false;
            }

            if (IsServer)
            {
                _syncedResourceType.Value = (byte)(_hasPendingResourceType ? _pendingResourceType : _defaultResourceType);
                _hasPendingResourceType = false;
            }

            _acquired = false;
            _predictedTow = false;
            _syncedResourceType.OnValueChanged += OnResourceTypeChanged;
            ApplyTint();
            ApplyScrolledPosition();
        }

        public override void OnNetworkDespawn()
        {
            _syncedResourceType.OnValueChanged -= OnResourceTypeChanged;
        }

        private void OnResourceTypeChanged(byte previous, byte current)
        {
            ApplyTint();
        }

        /// <summary>종류 색을 렌더러에 칠한다 — 전 종류 공유 프리팹의 외형 구분 (URP Lit _BaseColor).</summary>
        private void ApplyTint()
        {
            if (_catalog == null || _tintRenderers == null)
            {
                return;
            }

            Color color = _catalog.GetColor(ResourceType, Color.white);
            _tintBlock ??= new MaterialPropertyBlock();
            _tintBlock.SetColor("_BaseColor", color);
            for (int i = 0; i < _tintRenderers.Length; i++)
            {
                if (_tintRenderers[i] != null)
                {
                    _tintRenderers[i].SetPropertyBlock(_tintBlock);
                }
            }
        }

        /// <summary>
        /// 클라이언트 예측 고정 (§11 게스트 그랩 순간이동 — 수정안 A): 로컬 명중 시점에 컨베이어 유도를
        /// 멈추고 현재 표시 위치에 고정한다. 서버 확정(_isTowed) 도착까지 계속 스크롤에 밀리면
        /// 확정 순간 서버 고정 위치로 되돌아가는 스냅이 생기던 것을 막는다.
        /// </summary>
        public void BeginPredictedTow()
        {
            if (IsServer || !IsSpawned || _acquired || _isTowed.Value)
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

            // 그랩 확정 = 컨베이어 제외, 열차 프레임 소속 전환 (§2.4).
            _towPosition.Value = transform.position;
            _isTowed.Value = true;
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

            // 낙하 지점을 새 (위치, 누적 거리)로 재바인딩해 월드 소속으로 복귀.
            float currentDistance = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.TraveledDistance : 0f;
            Vector3 dropPosition = transform.position;
            dropPosition.y = _spawnPosition.Value.y;
            _spawnPosition.Value = dropPosition;
            _spawnDistance.Value = currentDistance;
            _isTowed.Value = false;
            _grabberClientId.Value = NoGrabber;
        }

        /// <summary>
        /// 획득 확정 (M5 5차 — 집게에서 이관): <b>자원이 스스로</b> 그래버 인벤토리에 수납하고
        /// 팀 카운터를 올린 뒤 소멸한다. 집게는 "무엇이 자원인지"를 알 필요가 없어진다 (OCP).
        /// 수납 실패(가득)는 false — 집게가 그 자리 낙하(강제 해제)로 처리한다 (기획서 §3.4).
        /// </summary>
        public GrabCompletionResult TryCompleteGrab(in GrabCompletion completion)
        {
            if (!IsServer || completion.Grabber == null)
            {
                return GrabCompletionResult.Rejected;
            }

            var inventory = completion.Grabber.GetComponent<Inventory.IResourceInventory>();
            if (inventory == null || !inventory.ServerTryAdd(ResourceType, 1))
            {
                return GrabCompletionResult.Rejected;
            }

            // 팀 누적 채집 통계 (권위 이벤트는 카운터가 발행). 카운터는 같은 World 도메인의 서비스다.
            if (ServiceLocator.TryGet(out ISharedResourceCounter counter))
            {
                counter.AddResource();
            }

            _acquired = true;
            // destroy: true여야 PooledNetworkPrefabHandler를 거쳐 풀로 반환된다.
            NetworkObject.Despawn(true);
            return GrabCompletionResult.Consumed;
        }

        private void Update()
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

            if (_predictedTow)
            {
                // 예측 고정 — 서버 확정/거부 수신까지 현재 위치를 유지한다.
                return;
            }

            ApplyScrolledPosition();
        }

        private void ApplyScrolledPosition()
        {
            if (ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                transform.position = WorldScrollMath.GetScrolledPosition(
                    _spawnPosition.Value, _spawnDistance.Value, scroll.TraveledDistance);
            }
        }

        public void OnSpawned()
        {
        }

        public void OnDespawned()
        {
            _hasPendingBinding = false;
            _hasPendingResourceType = false;
            _acquired = false;
            _predictedTow = false;
        }
    }
}
