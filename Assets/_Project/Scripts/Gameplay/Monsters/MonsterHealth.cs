using Game.Core.Events;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 몬스터 체력 — 호스트 권위 (권위 분담표: 데미지 적용·사망 확정 = 호스트).
    /// 피해는 상태와 무관하게 전부 일반 데미지다 (M5 6차 — 처형 배율 제거. 기절은
    /// 그랩 관심사의 내부 상태이고, 체력이 기절을 알아야 할 이유가 없다).
    /// 사망 확정 시 권위 이벤트 <see cref="MonsterDiedEvent"/>를 전 피어에 전파한 뒤 풀로 회수한다.
    /// 사망 연출(M5 8차)은 즉시 디스폰 앞의 짧은 파티클 버스트 — 사망 위치·분쇄 여부를
    /// RPC에 실어 각 피어가 로컬 재생한다 (판정·킬 카운트 경로 무변).
    /// </summary>
    public sealed class MonsterHealth : NetworkBehaviour, IDamageable
    {
        [SerializeField] private MonsterSettings _settings;

        [Header("사망 연출 (M5 8차 — 표시 전용)")]
        [Tooltip("일반 사망 버스트 프리팹 — 풀링 비네트워크. 비면 연출 없음.")]
        [SerializeField] private ImpactEffectView _deathEffectPrefab;

        [Tooltip("바퀴 분쇄 강조 버스트 프리팹 — 부피 큰 변형. 비면 일반 사망 프리팹을 쓴다.")]
        [SerializeField] private ImpactEffectView _crushEffectPrefab;

        private readonly NetworkVariable<float> _health = new NetworkVariable<float>();

        private float _pendingHealthMultiplier = 1f;
        private MonsterSettings _pendingVariant;

        // 서버 전용 — 이 개체에 실제로 적용되는 설정 (변종 반영).
        private MonsterSettings _effectiveSettings;

        // 서버 전용 — 처치 시 지상 드랍 (M7 1차 — 스탬피드 개체 한정 주입. 일반 웨이브는 드랍 없음 유지).
        private Inventory.ResourceType _pendingDropType = Inventory.ResourceType.None;
        private int _pendingDropCount;
        private Inventory.ResourceType _dropType = Inventory.ResourceType.None;
        private int _dropCount;

        public bool IsAlive => IsSpawned && _health.Value > 0f;

        /// <summary>
        /// 이 개체의 변종 설정을 스폰 직전에 주입한다 (호스트 전용). 체력은 서버만 확정하므로
        /// 인덱스 복제 없이 참조를 직접 받는다 — 클라이언트는 복제된 체력 값만 쓴다.
        /// </summary>
        public void ServerSetVariant(MonsterSettings variant)
        {
            _pendingVariant = variant;
        }

        /// <summary>
        /// 이 개체에 적용할 체력 배율을 스폰 직전에 주입한다 (호스트 전용 — Day·지역 난이도, 기획서 §5).
        /// <see cref="NetworkObject"/>.Spawn() 호출 전에 설정해야 <see cref="OnNetworkSpawn"/>이 반영한다.
        /// </summary>
        public void ServerSetHealthMultiplier(float multiplier)
        {
            _pendingHealthMultiplier = Mathf.Max(0.01f, multiplier);
        }

        /// <summary>
        /// 처치 시 지상 드랍을 스폰 직전에 주입한다 (호스트 전용, M7 1차 — 결정 ③).
        /// 스탬피드 개체만 주입한다 — 미주입 = 드랍 없음이 기본이라 일반 웨이브의 탄약 경제가 지켜진다.
        /// </summary>
        public void ServerSetDeathDrop(Inventory.ResourceType type, int count)
        {
            _pendingDropType = type;
            _pendingDropCount = count;
        }

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            _effectiveSettings = _pendingVariant != null ? _pendingVariant : _settings;
            if (_effectiveSettings != null)
            {
                _health.Value = _effectiveSettings.MaxHealth * _pendingHealthMultiplier;
            }

            _dropType = _pendingDropType;
            _dropCount = _pendingDropCount;

            // 풀에서 재사용될 때 이전 밤의 배율·변종·드랍이 새지 않도록 즉시 되돌린다.
            _pendingHealthMultiplier = 1f;
            _pendingVariant = null;
            _pendingDropType = Inventory.ResourceType.None;
            _pendingDropCount = 0;
        }

        public void ApplyDamage(float amount, ulong instigatorClientId)
        {
            if (!IsServer || !IsAlive || amount <= 0f)
            {
                return;
            }

            _health.Value = Mathf.Max(0f, _health.Value - amount);

            if (_health.Value <= 0f)
            {
                // 처치 드랍 (M7 1차 — 스탬피드 개체 한정): 버리기 낙하와 같은 스포너 경로로
                // 지상 스폰돼 컨베이어·집게 회수에 자동 편입된다 (결정 ③).
                if (_dropType != Inventory.ResourceType.None && _dropCount > 0
                    && ServiceLocator.TryGet(out World.IResourceDropper dropper))
                {
                    dropper.ServerSpawnDropped(_dropType, _dropCount, transform.position);
                }

                NotifyDiedRpc(instigatorClientId, transform.position, _crushKill);
                // destroy: true여야 PooledNetworkPrefabHandler를 거쳐 풀로 반환된다.
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>
        /// 바퀴 분쇄 처치 (M5 8차) — 서버 전용 별도 진입점. 판정은 일반 사망 경로에 그대로
        /// 합류하고(<see cref="IDamageable"/> 계약 무변), 사망 RPC의 분쇄 표시만 달라진다.
        /// </summary>
        public void ServerKillByCrush(ulong instigatorClientId)
        {
            _crushKill = true;
            ApplyDamage(float.MaxValue, instigatorClientId);
            _crushKill = false;
        }

        // 서버 전용 — ServerKillByCrush 경유 사망인가 (RPC 발신 순간에만 참).
        private bool _crushKill;

        /// <summary>
        /// 권위 이벤트 전파 — 호스트 확정 후 전 피어에서 발행된다. 사망 위치·분쇄 여부를 실어
        /// 각 피어가 사망 연출을 로컬 재생한다 (M5 8차 — 디스폰과 무관한 풀링 코스메틱).
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void NotifyDiedRpc(ulong killerClientId, Vector3 deathPosition, bool crushed)
        {
            EventBus<MonsterDiedEvent>.Publish(new MonsterDiedEvent(killerClientId));

            ImpactEffectView prefab = crushed && _crushEffectPrefab != null
                ? _crushEffectPrefab
                : _deathEffectPrefab;
            if (prefab != null)
            {
                ImpactEffectView effect = PoolManager.Spawn(prefab, deathPosition, Quaternion.identity);
                effect.Play(deathPosition, Vector3.up);
            }
        }
    }
}
