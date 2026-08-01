using Game.Core.Services;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 월드 스크롤 속도·누적 주행 거리의 호스트 권위 소유자 (네트워크 문서 §4.1, §8 해소 항목 ①안).
    /// 호스트가 두 NetworkVariable을 갱신하고, 클라이언트는 수신 값 사이를 속도로 외삽 + 스무딩해
    /// <see cref="IWorldScrollService"/>로 노출한다. Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class WorldScrollController : NetworkBehaviour, IWorldScrollService, IWorldScrollSpeedControl
    {
        [SerializeField] private WorldScrollSettings _settings;

        private readonly NetworkVariable<float> _scrollSpeed = new NetworkVariable<float>();
        private readonly NetworkVariable<float> _traveledDistance = new NetworkVariable<float>();

        private float _displayDistance;

        /// <summary>연료 상태가 정하는 기본 속도 — 환경 배율과 별개 레이어로 보관한다.</summary>
        private float _baseSpeed;

        /// <summary>날씨 등 일시적 환경 개입의 배율.</summary>
        private float _environmentMultiplier = 1f;

        public float ScrollSpeed => _scrollSpeed.Value;

        public float TraveledDistance => IsServer ? _traveledDistance.Value : _displayDistance;

        public override void OnNetworkSpawn()
        {
            if (IsServer && _settings != null)
            {
                _baseSpeed = _settings.BaseScrollSpeed;
                _environmentMultiplier = 1f;
                _scrollSpeed.Value = _baseSpeed;
            }

            _displayDistance = _traveledDistance.Value;

            if (!ServiceLocator.IsRegistered<IWorldScrollService>())
            {
                ServiceLocator.Register<IWorldScrollService>(this);
            }

            if (!ServiceLocator.IsRegistered<IWorldScrollSpeedControl>())
            {
                ServiceLocator.Register<IWorldScrollSpeedControl>(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (ServiceLocator.TryGet(out IWorldScrollService service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<IWorldScrollService>();
            }

            if (ServiceLocator.TryGet(out IWorldScrollSpeedControl control) && ReferenceEquals(control, this))
            {
                ServiceLocator.Unregister<IWorldScrollSpeedControl>();
            }
        }

        /// <summary>기본 스크롤 속도를 변경한다 (연료 감속·초가속 연출). 호스트 전용.</summary>
        public void SetScrollSpeed(float speed)
        {
            if (!IsServer)
            {
                return;
            }

            _baseSpeed = Mathf.Max(0f, speed);
            ApplyEffectiveSpeed();
        }

        /// <summary>환경 배율을 변경한다 (날씨 감속 등). 호스트 전용.</summary>
        public void SetEnvironmentSpeedMultiplier(float multiplier)
        {
            if (!IsServer)
            {
                return;
            }

            _environmentMultiplier = Mathf.Max(0f, multiplier);
            ApplyEffectiveSpeed();
        }

        private void ApplyEffectiveSpeed()
        {
            _scrollSpeed.Value = Mathf.Max(0f, _baseSpeed * _environmentMultiplier);
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (IsServer)
            {
                _traveledDistance.Value += _scrollSpeed.Value * Time.deltaTime;
            }
            else
            {
                float correctionRate = _settings != null ? _settings.CorrectionRate : 5f;
                _displayDistance = WorldScrollMath.SmoothToward(
                    _displayDistance, _traveledDistance.Value, _scrollSpeed.Value, Time.deltaTime, correctionRate);
            }
        }
    }
}
