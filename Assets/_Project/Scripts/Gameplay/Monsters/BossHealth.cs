using Game.Core.Events;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 보스 체력·페이즈·처치 보상 — 호스트 권위 (M7 2차, 권위 분담표: 데미지 적용·사망 확정 = 호스트).
    /// 체력을 복제하는 이유는 HUD(<see cref="BossSpawnedEvent"/> 계열)와 페이즈 판정이 전 피어에서
    /// 같은 값을 봐야 하기 때문이다 — 페이즈는 별도 복제 없이 체력 비율의 순수 함수로 유도된다
    /// (<see cref="BossPhaseMath"/>, 지역이 Day의 함수인 것과 같은 규약).
    /// 처치 시 드랍은 1차 지상 스폰 경로(<see cref="World.IResourceDropper"/>)를 그대로 탄다.
    /// </summary>
    public sealed class BossHealth : NetworkBehaviour, IDamageable
    {
        [Tooltip("이 보스의 정의 — 프리팹과 1:1이다 (결정 ①). 각 피어가 이름·페이즈 임계를 로컬 조회한다.")]
        [SerializeField] private BossDefinition _definition;

        [Tooltip("사망 버스트 프리팹 — 풀링 비네트워크. 비면 연출 없음.")]
        [SerializeField] private ImpactEffectView _deathEffectPrefab;

        private readonly NetworkVariable<float> _health = new NetworkVariable<float>();

        private readonly NetworkVariable<float> _maxHealth = new NetworkVariable<float>();

        private float _pendingHealthMultiplier = 1f;

        private int _phaseIndex;

        public bool IsAlive => IsSpawned && _health.Value > 0f;

        /// <summary>이 보스의 정의 (프리팹 참조) — 나란한 관심사(에이전트)가 같은 값을 읽는다.</summary>
        public BossDefinition Definition => _definition;

        /// <summary>현재 페이즈 인덱스 (0 = 1페이즈). 체력 비율에서 유도되므로 전 피어가 같은 값을 본다.</summary>
        public int PhaseIndex => _phaseIndex;

        public float CurrentHealth => _health.Value;

        public float MaxHealth => _maxHealth.Value;

        /// <summary>
        /// 이 보스에 적용할 체력 배율을 스폰 직전에 주입한다 (호스트 전용 — 지역·Day 배율).
        /// <see cref="NetworkObject"/>.Spawn() 호출 전에 설정해야 <see cref="OnNetworkSpawn"/>이 반영한다.
        /// </summary>
        public void ServerSetHealthMultiplier(float multiplier)
        {
            _pendingHealthMultiplier = Mathf.Max(0.01f, multiplier);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _definition != null)
            {
                _maxHealth.Value = _definition.MaxHealth * _pendingHealthMultiplier;
                _health.Value = _maxHealth.Value;

                // 풀에서 재사용될 때 이전 밤의 배율이 새지 않도록 즉시 되돌린다.
                _pendingHealthMultiplier = 1f;
            }

            _phaseIndex = EvaluatePhase();
            _health.OnValueChanged += OnHealthChanged;

            string displayName = _definition == null ? "보스" : _definition.DisplayName;
            int phaseCount = _definition == null ? 1 : _definition.PhaseCount;

            // 후발 접속도 스폰 시점에 같은 이벤트를 받으므로 HUD가 같은 상태로 열린다.
            EventBus<BossSpawnedEvent>.Publish(new BossSpawnedEvent(displayName, _maxHealth.Value, phaseCount));
            EventBus<BossHealthChangedEvent>.Publish(new BossHealthChangedEvent(_health.Value, _maxHealth.Value));
        }

        public override void OnNetworkDespawn()
        {
            _health.OnValueChanged -= OnHealthChanged;

            // 처치·회수(QA·Day 스킵) 공통 — 표시를 닫는 신호는 하나로 모은다.
            EventBus<BossDespawnedEvent>.Publish(default);
        }

        public void ApplyDamage(float amount, ulong instigatorClientId)
        {
            if (!IsServer || !IsAlive || amount <= 0f)
            {
                return;
            }

            _health.Value = Mathf.Max(0f, _health.Value - amount);

            if (_health.Value > 0f)
            {
                return;
            }

            ServerDropRewards();

            NotifyDiedRpc(instigatorClientId, transform.position);

            // destroy: true여야 PooledNetworkPrefabHandler를 거쳐 풀로 반환된다.
            NetworkObject.Despawn(true);
        }

        /// <summary>처치 보상 지상 드랍 (결정 ③) — 집게·컨베이어 회수에 자동 편입된다.</summary>
        private void ServerDropRewards()
        {
            if (_definition == null || _definition.DropCount == 0
                || !ServiceLocator.TryGet(out World.IResourceDropper dropper))
            {
                return;
            }

            for (int i = 0; i < _definition.DropCount; i++)
            {
                BossDefinition.DropEntry drop = _definition.GetDrop(i);
                if (drop == null || drop.Type == Inventory.ResourceType.None || drop.Count <= 0)
                {
                    continue;
                }

                dropper.ServerSpawnDropped(drop.Type, drop.Count, transform.position);
            }
        }

        private void OnHealthChanged(float previous, float current)
        {
            EventBus<BossHealthChangedEvent>.Publish(new BossHealthChangedEvent(current, _maxHealth.Value));

            int next = EvaluatePhase();
            if (next == _phaseIndex)
            {
                return;
            }

            _phaseIndex = next;
            EventBus<BossPhaseChangedEvent>.Publish(new BossPhaseChangedEvent(next));
        }

        private int EvaluatePhase()
        {
            if (_definition == null || _maxHealth.Value <= 0f)
            {
                return 0;
            }

            return BossPhaseMath.EvaluatePhase(_health.Value / _maxHealth.Value, _definition.PhaseHealthThresholds);
        }

        /// <summary>
        /// 권위 이벤트 전파 — 호스트 확정 후 전 피어에서 발행된다. 새벽 보류 해제와 같은 시점이다
        /// (해제 자체는 호스트의 <see cref="BossSpawner"/>가 생존 여부로 판정한다).
        /// </summary>
        [Rpc(SendTo.Everyone)]
        private void NotifyDiedRpc(ulong killerClientId, Vector3 deathPosition)
        {
            string displayName = _definition == null ? "보스" : _definition.DisplayName;
            EventBus<BossDiedEvent>.Publish(new BossDiedEvent(killerClientId, displayName));

            if (_deathEffectPrefab != null)
            {
                ImpactEffectView effect = PoolManager.Spawn(_deathEffectPrefab, deathPosition, Quaternion.identity);
                effect.Play(deathPosition, Vector3.up);
            }
        }
    }
}
