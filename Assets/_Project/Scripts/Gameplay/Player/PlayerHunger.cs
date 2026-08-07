using Game.Core.Events;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 플레이어 허기 — 호스트 권위 (권위 분담표: 상태 변경·피해 적용 = 호스트, M5 4차).
    /// 자연 감소로 내려가고 음식 섭취(<see cref="ServerRestore"/>)로만 회복한다. 0에 닿으면
    /// 기아 지속 피해 — 환경 피해 경로라 장비 피해 감소의 영향을 받지 않는다 (체온과 같은 계약).
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerHunger : NetworkBehaviour
    {
        [SerializeField] private HungerSettings _settings;

        /// <summary>허기는 초당 1 미만으로 천천히 변하므로, 이만큼 벌어졌을 때만 복제해 대역폭을 아낀다.</summary>
        private const float SyncThreshold = 0.5f;

        private readonly NetworkVariable<float> _hunger = new NetworkVariable<float>();

        private PlayerHealth _health;
        private float _serverHunger;
        private float _pendingDamage;

        /// <summary>현재 허기.</summary>
        public float Hunger => _hunger.Value;

        public float MaxHunger => _settings != null ? _settings.MaxHunger : 0f;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _settings != null)
            {
                _serverHunger = _settings.MaxHunger;
                _hunger.Value = _serverHunger;
                _pendingDamage = 0f;
            }

            _hunger.OnValueChanged += OnHungerChanged;

            // 첫 표시를 위해 현재 값으로 한 번 발행한다 (복제 콜백은 변경이 있어야 오므로).
            PublishChanged(_hunger.Value);
        }

        public override void OnNetworkDespawn()
        {
            _hunger.OnValueChanged -= OnHungerChanged;
        }

        /// <summary>
        /// 음식 섭취 회복 — 서버 전용. 섭취 확정(차감 + 회복 + 버프)의 회복 축을 담당한다.
        /// 이미 가득이면 false를 돌려줘 호출자가 낭비 섭취를 거부할 수 있게 한다.
        /// </summary>
        public bool ServerRestore(float amount)
        {
            if (!IsServer || _settings == null || !_health.IsAlive)
            {
                return false;
            }

            HungerCurve curve = _settings.ToCurve();
            if (_serverHunger >= curve.MaxHunger)
            {
                return false;
            }

            _serverHunger = HungerMath.Restore(_serverHunger, amount, curve);
            _hunger.Value = _serverHunger;
            return true;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _settings == null)
            {
                return;
            }

            // 사망~부활 사이에는 허기를 정상으로 되돌려 둔다 — 부활 직후 즉사 루프를 막는다 (체온과 동일).
            if (!_health.IsAlive)
            {
                _serverHunger = _settings.MaxHunger;
                _hunger.Value = _serverHunger;
                _pendingDamage = 0f;
                return;
            }

            HungerCurve curve = _settings.ToCurve();
            _serverHunger = HungerMath.Step(_serverHunger, curve, Time.deltaTime);

            // 0 도달(기아 시작·해제)은 임계 미만 변화라도 즉시 복제한다 — HUD 압박 단계가 지연되지 않게.
            if (Mathf.Abs(_serverHunger - _hunger.Value) >= SyncThreshold
                || (_serverHunger <= 0f) != (_hunger.Value <= 0f))
            {
                _hunger.Value = _serverHunger;
            }

            ApplyStarveDamage(curve);
        }

        /// <summary>
        /// 기아 지속 피해 — 매 프레임 소수점 피해를 넣으면 체력 복제·이벤트가 폭주하므로
        /// 1 이상 쌓였을 때만 정수 단위로 적용한다 (체온의 피해 누산과 동일).
        /// </summary>
        private void ApplyStarveDamage(in HungerCurve curve)
        {
            float damagePerSecond = HungerMath.GetDamagePerSecond(_serverHunger, curve);
            if (damagePerSecond <= 0f)
            {
                _pendingDamage = 0f;
                return;
            }

            _pendingDamage += damagePerSecond * Time.deltaTime;
            if (_pendingDamage < 1f)
            {
                return;
            }

            float amount = Mathf.Floor(_pendingDamage);
            _pendingDamage -= amount;

            // 환경 피해 — 물리 경로와 분리된 전용 진입점 (장비 피해 감소의 영향을 받지 않는다).
            _health.ApplyEnvironmentalDamage(amount);
        }

        private void OnHungerChanged(float previous, float current)
        {
            PublishChanged(current);
        }

        private void PublishChanged(float hunger)
        {
            HungerStress stress = _settings == null
                ? HungerStress.None
                : HungerMath.GetStress(hunger, _settings.ToCurve());

            EventBus<PlayerHungerChangedEvent>.Publish(
                new PlayerHungerChangedEvent(OwnerClientId, IsOwner, hunger, MaxHunger, stress));
        }
    }
}
