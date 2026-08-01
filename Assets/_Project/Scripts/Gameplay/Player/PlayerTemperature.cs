using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Cycle;
using Game.Gameplay.Region;
using Game.Gameplay.Train;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 플레이어 체온 — 호스트 권위 (권위 분담표: 상태 변경·피해 적용 = 호스트).
    /// 지역·국면이 정하는 환경 온도에 따라 체온이 표류하고, 임계를 벗어나면 지속 피해를 준다
    /// (기획서 §4.2 — 사막 낮 열사병 / 밤 급랭). M4의 유일한 완화 수단은 <b>건축물이 있는 칸 위</b>로,
    /// M3 건축물 시스템을 그대로 재사용한다. M5 장비(사막 로브·방한 세트)가 이 위에 얹힌다.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerTemperature : NetworkBehaviour
    {
        [SerializeField] private TemperatureSettings _settings;
        [SerializeField] private TrainLayoutSettings _trainLayout;

        /// <summary>체온은 초당 1℃ 미만으로 천천히 변하므로, 이만큼 벌어졌을 때만 복제해 대역폭을 아낀다.</summary>
        private const float SyncThreshold = 0.05f;

        private readonly NetworkVariable<float> _temperature = new NetworkVariable<float>();

        private PlayerHealth _health;
        private float _serverTemperature;
        private float _pendingDamage;

        /// <summary>현재 체온 (℃).</summary>
        public float Temperature => _temperature.Value;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _settings != null)
            {
                _serverTemperature = _settings.NormalBodyTemperature;
                _temperature.Value = _serverTemperature;
                _pendingDamage = 0f;
            }

            _temperature.OnValueChanged += OnTemperatureChanged;

            // 첫 표시를 위해 현재 값으로 한 번 발행한다 (복제 콜백은 변경이 있어야 오므로).
            PublishChanged(_temperature.Value);
        }

        public override void OnNetworkDespawn()
        {
            _temperature.OnValueChanged -= OnTemperatureChanged;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _settings == null)
            {
                return;
            }

            // 사망~부활 사이에는 체온을 정상으로 되돌려 둔다 — 부활 직후 즉사 루프를 막는다.
            if (!_health.IsAlive)
            {
                _serverTemperature = _settings.NormalBodyTemperature;
                _temperature.Value = _serverTemperature;
                _pendingDamage = 0f;
                return;
            }

            TemperatureCurve curve = _settings.ToCurve();
            float ambient = TemperatureMath.ResolveAmbient(GetRegionAmbient(curve), IsSheltered(), curve);

            _serverTemperature = TemperatureMath.Step(_serverTemperature, ambient, curve, Time.deltaTime);

            if (Mathf.Abs(_serverTemperature - _temperature.Value) >= SyncThreshold)
            {
                _temperature.Value = _serverTemperature;
            }

            ApplyStressDamage(curve);
        }

        /// <summary>현재 지역·국면의 환경 온도. 지역 데이터가 없으면 쾌적대 중심으로 둔다(무해).</summary>
        private float GetRegionAmbient(in TemperatureCurve curve)
        {
            if (!ServiceLocator.TryGet(out IRegionService region) || region.CurrentRegion == null)
            {
                return curve.ComfortCenter;
            }

            bool isNight = ServiceLocator.TryGet(out IDayCycleService cycle) && cycle.Phase == DayPhase.Night;

            return isNight
                ? region.CurrentRegion.NightAmbientTemperature
                : region.CurrentRegion.DayAmbientTemperature;
        }

        /// <summary>건축물이 있는 칸 위에 서 있는가 — 그늘·실내로 취급해 환경 온도를 완화한다.</summary>
        private bool IsSheltered()
        {
            if (_trainLayout == null || !ServiceLocator.TryGet(out ITrainState train))
            {
                return false;
            }

            Vector3 position = transform.position;

            // 갑판 위에 올라와 있고 열차 폭 안이어야 한다 — 지상에서 칸 옆을 지나는 경우를 배제.
            if (position.y < _trainLayout.DeckHeight - 0.5f)
            {
                return false;
            }

            if (Mathf.Abs(position.x) > _trainLayout.CarWidth * 0.5f + 0.5f)
            {
                return false;
            }

            if (!_trainLayout.TryGetCarIndexAtZ(position.z, train.CarCount, out int carIndex))
            {
                return false;
            }

            // 파괴·이탈한 칸의 건축물은 차폐가 되지 않는다.
            if (!train.TryGetCar(carIndex, out CarState car) || !car.Attached || car.Health <= 0f)
            {
                return false;
            }

            return train.TryGetStructure(carIndex, out StructureState structure) && structure.Present;
        }

        /// <summary>
        /// 임계를 벗어난 만큼 지속 피해를 준다. 매 프레임 소수점 피해를 넣으면 체력 복제·이벤트가
        /// 폭주하므로 1 이상 쌓였을 때만 정수 단위로 적용한다.
        /// </summary>
        private void ApplyStressDamage(in TemperatureCurve curve)
        {
            float damagePerSecond = TemperatureMath.GetDamagePerSecond(_serverTemperature, curve);
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

            // 환경 피해 — 가해자는 서버 자신으로 기록한다.
            _health.ApplyDamage(amount, NetworkManager.ServerClientId);
        }

        private void OnTemperatureChanged(float previous, float current)
        {
            PublishChanged(current);
        }

        private void PublishChanged(float temperature)
        {
            TemperatureStress stress = _settings == null
                ? TemperatureStress.None
                : TemperatureMath.GetStress(temperature, _settings.ToCurve());

            EventBus<PlayerTemperatureChangedEvent>.Publish(
                new PlayerTemperatureChangedEvent(OwnerClientId, IsOwner, temperature, stress));
        }
    }
}
