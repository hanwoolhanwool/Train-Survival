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

        public bool IsAlive => IsSpawned && _health.Value > 0f;

        public override void OnNetworkSpawn()
        {
            if (IsServer && _settings != null)
            {
                _health.Value = _settings.MaxHealth;
            }
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
