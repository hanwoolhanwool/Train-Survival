using Game.Core.Services;
using Game.Gameplay.Harpoon;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지상 소형 자원 (돌·나뭇가지 더미) — 집게 그랩 대상.
    /// 위치·안착 파이프라인(컨베이어 유도·견인·갑판 휴지·하강 로컬 재생·이탈 추종)은
    /// <see cref="SettleableGrabbable"/> 공용 (M5 8차 공용화) — 여기에는 자원 종류 축
    /// (색 구분·수납·무게 등급)만 남는다.
    /// </summary>
    public sealed class ResourceNode : SettleableGrabbable
    {
        [Tooltip("서버가 종류를 주입하지 않았을 때의 기본 자원 종류.")]
        [SerializeField] private Inventory.ResourceType _defaultResourceType = Inventory.ResourceType.Wood;

        [Tooltip("종류 식별 색·표시명 조회용 카탈로그 — 전 종류가 한 프리팹을 공유하므로 색이 외형 구분이다.")]
        [SerializeField] private Inventory.ResourceCatalog _catalog;

        [Tooltip("종류 색을 칠할 렌더러 (Visual).")]
        [SerializeField] private Renderer[] _tintRenderers;

        // 자원 종류 — 몬스터 변종과 같은 규약: 프리팹을 늘리지 않고 인덱스(byte)를 복제해 각 피어가 카탈로그를 조회한다.
        private readonly NetworkVariable<byte> _syncedResourceType = new NetworkVariable<byte>();

        private static MaterialPropertyBlock _tintBlock;

        private Inventory.ResourceType _pendingResourceType;
        private bool _hasPendingResourceType;
        private bool _acquired;

        /// <summary>채집 시 수납되는 자원 종류 — 스폰 동기화 후에는 복제 값, 그 외에는 프리팹 기본값.</summary>
        public Inventory.ResourceType ResourceType => IsSpawned
            ? (Inventory.ResourceType)_syncedResourceType.Value
            : _defaultResourceType;

        /// <summary>무게 등급은 종류가 정한다 (M5 5차) — 미등재 종류는 1이라 기존 5종의 채집 경로가 유지된다.</summary>
        public override int GrabWeight => _catalog != null ? _catalog.GetGrabWeight(ResourceType) : 1;

        public override bool IsAvailableForGrab => !_acquired && base.IsAvailableForGrab;

        /// <summary>서버 전용 — 스폰 직전에 자원 종류를 예약한다. OnNetworkSpawn에서 동기화된다.</summary>
        public void ServerSetResourceType(Inventory.ResourceType type)
        {
            _pendingResourceType = type;
            _hasPendingResourceType = true;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (IsServer)
            {
                _syncedResourceType.Value = (byte)(_hasPendingResourceType ? _pendingResourceType : _defaultResourceType);
                _hasPendingResourceType = false;
            }

            _acquired = false;
            _syncedResourceType.OnValueChanged += OnResourceTypeChanged;
            ApplyTint();
        }

        public override void OnNetworkDespawn()
        {
            _syncedResourceType.OnValueChanged -= OnResourceTypeChanged;
            base.OnNetworkDespawn();
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
        /// 획득 확정 (M5 5차 — 집게에서 이관): <b>자원이 스스로</b> 그래버 인벤토리에 수납하고
        /// 팀 카운터를 올린 뒤 소멸한다. 집게는 "무엇이 자원인지"를 알 필요가 없어진다 (OCP).
        /// 수납 실패(가득)는 Rejected — 집게가 그 자리 낙하(강제 해제)로 처리한다 (기획서 §3.4).
        /// </summary>
        public override GrabCompletionResult TryCompleteGrab(in GrabCompletion completion)
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

        public override void OnDespawned()
        {
            base.OnDespawned();
            _hasPendingResourceType = false;
            _acquired = false;
        }
    }
}
