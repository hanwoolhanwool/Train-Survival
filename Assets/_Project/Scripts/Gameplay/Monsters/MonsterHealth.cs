using Game.Core.Events;
using Game.Gameplay.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 몬스터 체력 — 호스트 권위 (권위 분담표: 데미지 적용·사망 확정 = 호스트).
    /// 사망 확정 시 권위 이벤트 <see cref="MonsterDiedEvent"/>를 전 피어에 전파한 뒤 풀로 회수한다.
    /// </summary>
    public sealed class MonsterHealth : NetworkBehaviour, IDamageable
    {
        [SerializeField] private MonsterSettings _settings;

        private readonly NetworkVariable<float> _health = new NetworkVariable<float>();

        private float _pendingHealthMultiplier = 1f;
        private MonsterSettings _pendingVariant;

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

        public override void OnNetworkSpawn()
        {
            if (!IsServer)
            {
                return;
            }

            MonsterSettings effective = _pendingVariant != null ? _pendingVariant : _settings;
            if (effective != null)
            {
                _health.Value = effective.MaxHealth * _pendingHealthMultiplier;
            }

            // 풀에서 재사용될 때 이전 밤의 배율·변종이 새지 않도록 즉시 되돌린다.
            _pendingHealthMultiplier = 1f;
            _pendingVariant = null;
        }

        public void ApplyDamage(float amount, ulong instigatorClientId)
        {
            if (!IsServer || !IsAlive)
            {
                return;
            }

            _health.Value = Mathf.Max(0f, _health.Value - amount);

            if (_health.Value <= 0f)
            {
                NotifyDiedRpc(instigatorClientId);
                // destroy: true여야 PooledNetworkPrefabHandler를 거쳐 풀로 반환된다.
                NetworkObject.Despawn(true);
            }
        }

        /// <summary>권위 이벤트 전파 — 호스트 확정 후 전 피어에서 발행된다.</summary>
        [Rpc(SendTo.Everyone)]
        private void NotifyDiedRpc(ulong killerClientId)
        {
            EventBus<MonsterDiedEvent>.Publish(new MonsterDiedEvent(killerClientId));
        }
    }
}
