using Game.Core.Events;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 요리 버프 — 호스트 권위 시간제 상태 2종 (기획서 §7.3, M5 4차).
    /// 재생은 <see cref="PlayerHealth"/>의 회복 경로를 초당 틱으로 돌리고,
    /// 보온은 <see cref="PlayerTemperature"/>가 장비 단열 합산에 가산해 같은 클램프를 통과시킨다.
    /// 같은 버프를 다시 받으면 세기·지속을 덮어쓴다 (중첩 없음 — 단순 갱신).
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerBuffs : NetworkBehaviour
    {
        /// <summary>남은 시간은 초 단위 표시가 목적이라 이만큼 벌어졌을 때만 복제한다.</summary>
        private const float SyncThreshold = 0.5f;

        private readonly NetworkVariable<float> _regenRemaining = new NetworkVariable<float>();
        private readonly NetworkVariable<float> _warmthRemaining = new NetworkVariable<float>();

        private PlayerHealth _health;
        private float _serverRegenRemaining;
        private float _serverWarmthRemaining;
        private float _regenPerSecond;
        private float _warmthColdBonus;
        private float _pendingHeal;

        /// <summary>보온 버프의 추위 단열 가산 — 서버 체온 계산 전용 (만료면 0).</summary>
        public float ServerColdInsulationBonus => _serverWarmthRemaining > 0f ? _warmthColdBonus : 0f;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                ServerClear();
            }

            _regenRemaining.OnValueChanged += OnRemainingChanged;
            _warmthRemaining.OnValueChanged += OnRemainingChanged;
            EventBus<PlayerDiedEvent>.Subscribe(OnPlayerDied);
            PublishChanged();
        }

        public override void OnNetworkDespawn()
        {
            _regenRemaining.OnValueChanged -= OnRemainingChanged;
            _warmthRemaining.OnValueChanged -= OnRemainingChanged;
            EventBus<PlayerDiedEvent>.Unsubscribe(OnPlayerDied);
        }

        /// <summary>
        /// 사망 확정 즉시 버프를 끝낸다 (M5 4차 D10) — <see cref="Update"/>의 생존 검사에만 기대면
        /// 사망 프레임과 갱신 순서에 따라 한 틱이 새어 나갈 수 있어, 권위 이벤트에서도 정리한다.
        /// </summary>
        private void OnPlayerDied(PlayerDiedEvent evt)
        {
            if (IsServer && evt.ClientId == OwnerClientId)
            {
                ServerClear();
            }
        }

        /// <summary>음식의 버프를 적용한다 — 서버 전용. 지속 0인 축은 건드리지 않는다.</summary>
        public void ServerApplyFood(
            float regenPerSecond, float regenDuration, float coldBonus, float warmthDuration)
        {
            // 사망 중 부여를 여기서도 막는다 — 호출부(섭취 RPC)의 검사에만 기대지 않는 심층 방어.
            if (!IsServer || !_health.IsAlive)
            {
                return;
            }

            if (regenDuration > 0f && regenPerSecond > 0f)
            {
                _regenPerSecond = regenPerSecond;
                _serverRegenRemaining = regenDuration;
                _regenRemaining.Value = regenDuration;
            }

            if (warmthDuration > 0f && coldBonus > 0f)
            {
                _warmthColdBonus = coldBonus;
                _serverWarmthRemaining = warmthDuration;
                _warmthRemaining.Value = warmthDuration;
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer)
            {
                return;
            }

            // 사망하면 버프도 끝난다 — 부활 후 이전 식사의 효과가 남지 않는다.
            if (!_health.IsAlive)
            {
                ServerClear();
                return;
            }

            TickRegen();
            TickWarmth();
        }

        private void TickRegen()
        {
            if (_serverRegenRemaining <= 0f)
            {
                return;
            }

            _serverRegenRemaining = Mathf.Max(0f, _serverRegenRemaining - Time.deltaTime);

            // 소수점 회복을 매 프레임 넣으면 체력 복제가 폭주한다 — 1 이상 쌓였을 때만 적용 (피해 누산과 대칭).
            _pendingHeal += _regenPerSecond * Time.deltaTime;
            if (_pendingHeal >= 1f)
            {
                float amount = Mathf.Floor(_pendingHeal);
                _pendingHeal -= amount;
                _health.ServerHeal(amount);
            }

            SyncRemaining(_regenRemaining, _serverRegenRemaining);
        }

        private void TickWarmth()
        {
            if (_serverWarmthRemaining <= 0f)
            {
                return;
            }

            _serverWarmthRemaining = Mathf.Max(0f, _serverWarmthRemaining - Time.deltaTime);
            SyncRemaining(_warmthRemaining, _serverWarmthRemaining);
        }

        private static void SyncRemaining(NetworkVariable<float> variable, float serverValue)
        {
            // 만료(0 도달)는 임계 미만이라도 즉시 복제한다 — HUD에 잔여 표시가 남지 않게.
            if (Mathf.Abs(serverValue - variable.Value) >= SyncThreshold
                || (serverValue <= 0f) != (variable.Value <= 0f))
            {
                variable.Value = serverValue;
            }
        }

        private void ServerClear()
        {
            _serverRegenRemaining = 0f;
            _serverWarmthRemaining = 0f;
            _pendingHeal = 0f;
            if (_regenRemaining.Value != 0f)
            {
                _regenRemaining.Value = 0f;
            }

            if (_warmthRemaining.Value != 0f)
            {
                _warmthRemaining.Value = 0f;
            }
        }

        private void OnRemainingChanged(float previous, float current)
        {
            PublishChanged();
        }

        private void PublishChanged()
        {
            EventBus<PlayerBuffsChangedEvent>.Publish(new PlayerBuffsChangedEvent(
                OwnerClientId, IsOwner, _regenRemaining.Value, _warmthRemaining.Value));
        }
    }
}
