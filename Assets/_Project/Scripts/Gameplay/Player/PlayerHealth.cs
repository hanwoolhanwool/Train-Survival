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
        private Inventory.PlayerInventory _inventory;
        private bool _serverDead;

        /// <summary>
        /// 살아서 활동 가능한 상태인가 — <b>사망 경로가 둘</b>이므로 둘 다 본다 (M5 4차 D5·D10):
        /// 전투·환경 피해로 체력이 0이 된 사망(<see cref="_serverDead"/>)과,
        /// 체력을 거치지 않는 이탈 사망 후 부활 대기(<see cref="NetworkPlayerController.IsRespawnPending"/>).
        /// 이탈 사망을 빠뜨리면 "죽어서 부활을 기다리는 동안" 섭취·버프·설치가 그대로 살아 있게 된다.
        /// </summary>
        public bool IsAlive => IsSpawned && !_serverDead
            && !_controller.IsRespawnPending && _health.Value > 0f;

        public float Health => _health.Value;

        public float MaxHealth => _settings != null ? _settings.MaxHealth : 0f;

        // ── 재접속 복원 (M6 1차) — 사망 상태·시각 노출 ─────────────────────

        /// <summary>전투 사망으로 죽어 있는가 — 서버 전용. 이탈 사망(<see cref="NetworkPlayerController.IsRespawnPending"/>)과
        /// 함께 스냅샷의 "부활 대기 여부" 캡처 근거가 된다.</summary>
        public bool ServerIsDead => _serverDead;

        /// <summary>사망 확정 시각 (서버 시계, 초) — 잔여 대기 계산용 (결정 ⑦). 두 사망 경로 모두 기록한다.</summary>
        public double ServerDeathTime { get; private set; }

        /// <summary>사망 확정 시 계산된 부활 대기 시간 (초).</summary>
        public float ServerRespawnDelaySeconds { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<NetworkPlayerController>();
            _inventory = GetComponent<Inventory.PlayerInventory>();
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
            // 물리 피해만 장비 감산을 통과한다 (기획서 §6.3, M5 3차) — 체온 피해는
            // ApplyEnvironmentalDamage 경로라 장비의 체온 축이 이미 완화를 담당한다.
            float multiplier = _inventory != null ? _inventory.GetEquippedDamageMultiplier() : 1f;
            ServerApplyDamageInternal(amount * multiplier);
        }

        /// <summary>
        /// 환경 피해(체온 등) 전용 진입점 — 서버 전용. 물리 피해(<see cref="ApplyDamage"/>)와
        /// 진입점을 분리해 장비 피해 감소(M5)가 물리 경로에만 적용되게 한다
        /// (체온 피해는 장비의 체온 축이 이미 완화를 담당한다).
        /// </summary>
        public void ApplyEnvironmentalDamage(float amount)
        {
            ServerApplyDamageInternal(amount);
        }

        /// <summary>
        /// 체력 회복 — 서버 전용 (M5 4차 요리 재생 버프). 최대치에서 잘리고 사망 중에는 무효다.
        /// </summary>
        public void ServerHeal(float amount)
        {
            if (!IsServer || !IsAlive || _settings == null || amount <= 0f)
            {
                return;
            }

            _health.Value = Mathf.Min(_settings.MaxHealth, _health.Value + amount);
        }

        private void ServerApplyDamageInternal(float amount)
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

            ServerRecordDeath(delay);
            NotifyDiedRpc(OwnerClientId);
            BeginRespawnOwnerRpc(respawnPosition, delay);
        }

        /// <summary>
        /// 사망 확정 시각·대기 시간 기록 — 서버 전용 (M6 1차 결정 ⑦). 현행 카운트다운은 소유자
        /// 코루틴에만 존재해 서버가 잔여를 모르므로, 확정 순간을 서버 시계로 남겨 재접속 시
        /// "사망 시각 + 대기 시간 − 현재 시각"으로 잔여를 계산한다. 이탈 사망 경로
        /// (<see cref="NetworkPlayerController"/>)도 이 API로 기록한다.
        /// </summary>
        public void ServerRecordDeath(float respawnDelaySeconds)
        {
            if (!IsServer)
            {
                return;
            }

            ServerDeathTime = NetworkManager.ServerTime.Time;
            ServerRespawnDelaySeconds = respawnDelaySeconds;
        }

        /// <summary>
        /// 체력 절대값 설정 — 서버 전용, 재접속 복원(M6 1차) 전용.
        /// <see cref="ServerHeal"/>은 증가 전용 + IsAlive 게이트라 쓸 수 없다.
        /// </summary>
        public void ServerSetHealth(float value)
        {
            if (!IsServer || _settings == null)
            {
                return;
            }

            _health.Value = Mathf.Clamp(value, 0f, _settings.MaxHealth);
        }

        /// <summary>
        /// 부활 대기 재개 — 서버 전용, 재접속 복원(M6 1차 결정 ②⑦) 전용. 사망 상태로 되돌린 뒤
        /// 잔여 시간으로 소유자 카운트다운을 다시 지시한다 (<see cref="NetworkPlayerController.BeginOwnerRespawn"/>은
        /// IsOwner 가드 + 기존 재개 RPC는 private이라 서버에서 직접 부를 수 없어 신설).
        /// 잔여 ≤ 0이면 대기 0초 = 즉시 부활. 부활 완료 시 스탯 초기화는 기존 부활 경로 그대로다.
        /// </summary>
        public void ServerResumeRespawn(float remainingSeconds)
        {
            if (!IsServer)
            {
                return;
            }

            _serverDead = true;
            _health.Value = 0f;

            Vector3 respawnPosition = _trainLayout != null
                ? _trainLayout.RespawnPosition
                : new Vector3(0f, 4f, 0f);
            BeginRespawnOwnerRpc(respawnPosition, Mathf.Max(0f, remainingSeconds));
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
