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

            _fuel.Value = FuelMath.ConsumeFuel(_fuel.Value, _settings.ConsumptionPerSecond, Time.deltaTime);

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
            EventBus<FuelChangedEvent>.Publish(new FuelChangedEvent(current, Capacity));
        }
    }
}
