using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Game.Gameplay.Cycle;
using Game.Gameplay.Train;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 플레이어 체력 — 호스트 권위 (권위 분담표: 데미지 적용·사망 확정 = 호스트).
    /// 사망 확정 시 권위 이벤트를 전파하고, 부활 절차는 소유자에게 위임한다
    /// (이동 컴포넌트의 기존 부활 흐름 재사용). 사망~부활 완료 사이에는 무적이다.
    /// </summary>
    [RequireComponent(typeof(NetworkPlayerController))]
    public sealed class PlayerHealth : NetworkBehaviour, IDamageable
    {
        [SerializeField] private PlayerHealthSettings _settings;
        [SerializeField] private TrainLayoutSettings _trainLayout;

        private readonly NetworkVariable<float> _health = new NetworkVariable<float>();

        private NetworkPlayerController _controller;
        private bool _serverDead;

        public bool IsAlive => IsSpawned && !_serverDead && _health.Value > 0f;

        public float Health => _health.Value;

        public float MaxHealth => _settings != null ? _settings.MaxHealth : 0f;

        private void Awake()
        {
            _controller = GetComponent<NetworkPlayerController>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _settings != null)
            {
                _serverDead = false;
                _health.Value = _settings.MaxHealth;
            }

            _health.OnValueChanged += OnHealthChanged;
        }

        public override void OnNetworkDespawn()
        {
            _health.OnValueChanged -= OnHealthChanged;
        }

        public void ApplyDamage(float amount, ulong instigatorClientId)
        {
            if (!IsServer || !IsAlive || _settings == null)
            {
                return;
            }

            _health.Value = Mathf.Max(0f, _health.Value - amount);

            if (_health.Value <= 0f)
            {
                ServerConfirmDeath();
            }
        }

        // ── 호스트 권위: 사망 확정 → 소유자 부활 지시 ──────────────────────

        private void ServerConfirmDeath()
        {
            _serverDead = true;

            int dayNumber = ServiceLocator.TryGet(out IDayCycleService cycle) ? cycle.DayNumber : 1;
            float delay = _settings.GetRespawnDelaySeconds(dayNumber);
            Vector3 respawnPosition = _trainLayout != null
                ? _trainLayout.RespawnPosition
                : new Vector3(0f, 4f, 0f);

            NotifyDiedRpc(OwnerClientId);
            BeginRespawnOwnerRpc(respawnPosition, delay);
        }

        /// <summary>권위 이벤트 전파 — 호스트 확정 후 전 피어에서 발행된다.</summary>
        [Rpc(SendTo.Everyone)]
        private void NotifyDiedRpc(ulong clientId)
        {
            EventBus<PlayerDiedEvent>.Publish(new PlayerDiedEvent(clientId, IsOwner));
        }

        [Rpc(SendTo.Owner)]
        private void BeginRespawnOwnerRpc(Vector3 respawnPosition, float delaySeconds)
        {
            _controller.BeginOwnerRespawn(respawnPosition, delaySeconds, () => ReviveServerRpc());
        }

        [Rpc(SendTo.Server)]
        private void ReviveServerRpc()
        {
            if (_settings != null)
            {
                _health.Value = _settings.MaxHealth;
            }

            _serverDead = false;
        }

        private void OnHealthChanged(float previous, float current)
        {
            EventBus<PlayerHealthChangedEvent>.Publish(
                new PlayerHealthChangedEvent(OwnerClientId, IsOwner, current, MaxHealth));
        }
    }
}
