using Game.Core.Events;
using Game.Core.Services;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 공유 연료 탱크 — 호스트 권위 (권위 분담표: 공유 자원 = 호스트).
    /// 호스트가 상시 소모를 시뮬레이션하고, 잔량에 따라 목표 스크롤 속도를
    /// <see cref="IWorldScrollSpeedControl"/>로 수렴시킨다 (연료 소모→감속, 개발 가이드 M2).
    /// 충전은 자동이 아니라 엔진 투입(기획서 §3.4) — EngineFuelPort가 <see cref="AddFuel"/>을 호출한다.
    /// Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class FuelTank : NetworkBehaviour, IFuelService
    {
        [SerializeField] private FuelSettings _settings;
        [SerializeField] private WorldScrollSettings _scrollSettings;

        [Tooltip("연료를 태우는 건축물(M7 3차 강화 난방로)의 소모율을 읽을 카탈로그. " +
            "비워 두면 건축물 소모 없이 기존 주행 소모만 계산한다.")]
        [SerializeField] private Train.StructureCatalog _structureCatalog;

        private readonly NetworkVariable<float> _fuel = new NetworkVariable<float>();

        private float _currentSpeed;

        public float Fuel => _fuel.Value;

        public float Capacity => _settings != null ? _settings.Capacity : 0f;

        public override void OnNetworkSpawn()
        {
            if (IsServer && _settings != null)
            {
                _fuel.Value = Mathf.Min(_settings.InitialFuel, _settings.Capacity);
                _currentSpeed = _scrollSettings != null ? _scrollSettings.BaseScrollSpeed : 0f;
            }

            _fuel.OnValueChanged += OnFuelChanged;

            if (!ServiceLocator.IsRegistered<IFuelService>())
            {
                ServiceLocator.Register<IFuelService>(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            _fuel.OnValueChanged -= OnFuelChanged;

            if (ServiceLocator.TryGet(out IFuelService service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<IFuelService>();
            }
        }

        public void AddFuel(float amount)
        {
            if (IsServer && _settings != null)
            {
                _fuel.Value = FuelMath.AddFuel(_fuel.Value, amount, _settings.Capacity);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _settings == null || _scrollSettings == null)
            {
                return;
            }

            float perSecond = ResolveConsumptionPerSecond();
            _fuel.Value = FuelMath.ConsumeFuel(_fuel.Value, perSecond, Time.deltaTime);

            float target = FuelMath.ComputeTargetScrollSpeed(
                _scrollSettings.BaseScrollSpeed, _fuel.Value, _settings.DepletedSpeedRatio);
            float next = FuelMath.StepScrollSpeed(_currentSpeed, target, _settings.SpeedChangeRate, Time.deltaTime);

            if (!Mathf.Approximately(next, _currentSpeed) && ServiceLocator.TryGet(out IWorldScrollSpeedControl control))
            {
                control.SetScrollSpeed(next);
            }

            _currentSpeed = next;
        }

        private void OnFuelChanged(float previous, float current)
        {
            // 소모율은 각 피어가 같은 입력(설정 SO + 복제 편성)으로 재계산한다 — 클라이언트 HUD에도 정확히 뜬다.
            EventBus<FuelChangedEvent>.Publish(new FuelChangedEvent(current, Capacity, ResolveConsumptionPerSecond()));
        }

        /// <summary>
        /// 초당 소모율 = 기본 + 끌고 있는 칸(기획서 §7.1) + <b>연료를 태우는 건축물</b>(M7 3차 강화 난방로).
        /// 소모와 HUD 표시가 이 하나를 공유해 "표시된 소모율"과 "실제 소모"가 갈리지 않는다.
        /// </summary>
        private float ResolveConsumptionPerSecond()
        {
            if (_settings == null)
            {
                return 0f;
            }

            // 열차 상태가 아직 없으면 기본 소모만.
            int attachedCars = ServiceLocator.TryGet(out IFuelLoadProvider load) ? load.AttachedCarCount : 0;
            float perSecond = FuelMath.ComputeConsumptionPerSecond(
                _settings.ConsumptionPerSecond, _settings.ConsumptionPerCar, attachedCars);

            return AddStructureDraw(perSecond);
        }

        /// <summary>
        /// 연료를 태우는 건축물의 몫. 카탈로그를 훑어 <b>소모율이 0보다 큰 종류</b>만 세므로
        /// 여기서 특정 건축물(강화 난방로)을 이름으로 알 필요가 없다 — 새 소모형 건축물은
        /// 카탈로그 등재만으로 합산에 들어온다.
        /// </summary>
        private float AddStructureDraw(float perSecond)
        {
            if (_structureCatalog == null || !ServiceLocator.TryGet(out Train.ITrainState train))
            {
                return perSecond;
            }

            for (int i = 0; i < _structureCatalog.EntryCount; i++)
            {
                if (!_structureCatalog.TryGetKindAt(i, out Train.StructureKind kind))
                {
                    continue;
                }

                float perStructure = _structureCatalog.GetHeaterFuelPerSecond(kind);
                if (perStructure <= 0f)
                {
                    continue;
                }

                perSecond = FuelMath.AddStructureConsumption(
                    perSecond, perStructure, train.CountStructures(kind));
            }

            return perSecond;
        }
    }
}
