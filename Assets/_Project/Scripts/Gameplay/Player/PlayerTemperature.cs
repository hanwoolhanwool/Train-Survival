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
    /// (기획서 §4.2 — 사막 낮 열사병 / 밤 급랭). 완화 수단은 <b>건축물이 있는 칸 위</b> —
    /// 종류에 따라 그늘(돔 = 더위)·난방(난방기 = 추위)이 갈린다 (M5 3차 종류화, 카탈로그 데이터).
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class PlayerTemperature : NetworkBehaviour
    {
        [SerializeField] private TemperatureSettings _settings;
        [SerializeField] private TrainLayoutSettings _trainLayout;
        [SerializeField] private StructureCatalog _structureCatalog;

        /// <summary>체온은 초당 1℃ 미만으로 천천히 변하므로, 이만큼 벌어졌을 때만 복제해 대역폭을 아낀다.</summary>
        private const float SyncThreshold = 0.05f;

        private readonly NetworkVariable<float> _temperature = new NetworkVariable<float>();

        private PlayerHealth _health;
        private Inventory.PlayerInventory _inventory;
        private float _serverTemperature;
        private float _pendingDamage;

        /// <summary>현재 체온 (℃).</summary>
        public float Temperature => _temperature.Value;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _inventory = GetComponent<Inventory.PlayerInventory>();
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
            ResolveShelter(out bool hasShade, out bool hasHeat);
            float ambient = TemperatureMath.ResolveAmbient(GetRegionAmbient(curve), hasShade, hasHeat, curve);

            // 장비 단열 (기획서 §6.3, M5 3차) — 건축물 다음 층으로 적용한다. 음수 계수 = 역효과.
            if (_inventory != null)
            {
                _inventory.GetEquippedInsulation(out float cold, out float heat);
                ambient = TemperatureMath.ApplyInsulation(ambient, cold, heat, curve);
            }

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

        /// <summary>
        /// 살아 있는 건축물이 있는 칸 위에 서 있는가 — 건축물 종류에 따라 그늘(더위 완화)과
        /// 난방(추위 완화)을 구분해 알려준다 (M5 3차 — 건축물 종류화, 효과는 카탈로그 데이터).
        /// 판단 기준은 <b>건축물의 존재</b>이므로 칸이 편성에서 이탈했는지는 보지 않는다
        /// (이탈 칸에 고립된 플레이어가 체온으로 이중 처벌받지 않게 한다). 파괴된 건축물은 제외.
        /// </summary>
        private void ResolveShelter(out bool hasShade, out bool hasHeat)
        {
            hasShade = false;
            hasHeat = false;

            if (_trainLayout == null || _structureCatalog == null
                || !ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            Vector3 position = transform.position;

            // 갑판 위에 올라와 있고 열차 폭 안이어야 한다 — 지상에서 칸 옆을 지나는 경우를 배제.
            if (position.y < _trainLayout.DeckHeight - 0.5f)
            {
                return;
            }

            if (Mathf.Abs(position.x) > _trainLayout.CarWidth * 0.5f + 0.5f)
            {
                return;
            }

            int carIndex = ResolveCarIndexAt(position.z, train);
            if (carIndex < 0)
            {
                return;
            }

            // 부서진 건축물은 효과가 없다 — Present만으로는 파괴된 자리도 통과하므로 체력까지 본다.
            if (!train.TryGetStructure(carIndex, out StructureState structure)
                || !structure.Present || structure.Health <= 0f)
            {
                return;
            }

            hasShade = _structureCatalog.ProvidesShade(structure.Kind);
            hasHeat = _structureCatalog.ProvidesHeat(structure.Kind);
        }

        /// <summary>
        /// Z 위에 있는 칸 — 이탈 칸은 슬롯에서 뒤로 밀려나 있어 슬롯 기준 역산이 성립하지 않는다.
        /// 칸 수가 적으므로(기본 3) 실제 중심이 가장 가까운 칸을 순회로 고른다. 없으면 -1.
        /// </summary>
        private int ResolveCarIndexAt(float z, ITrainState train)
        {
            int best = -1;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < train.CarCount; i++)
            {
                float offset = train.GetEjectOffset(i);
                if (!_trainLayout.IsZOnCar(z, i, offset))
                {
                    continue;
                }

                // 앞 칸이 pitch 가까이 밀리면 뒤 칸 슬롯과 겹칠 수 있다 — 첫 매치는 편성 순서에
                // 좌우되므로 최근접으로 고른다 (연쇄 이탈 규칙상 실제로는 겹치지 않지만 규칙 의존을 없앤다).
                float distance = Mathf.Abs(z - _trainLayout.CarCenterZ(i, offset));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
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

            // 환경 피해 — 물리 경로와 분리된 전용 진입점 (장비 피해 감소의 영향을 받지 않는다).
            _health.ApplyEnvironmentalDamage(amount);
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
