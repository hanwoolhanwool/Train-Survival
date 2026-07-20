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
    public sealed class WorldScrollController : NetworkBehaviour, IWorldScrollService
    {
        [SerializeField] private WorldScrollSettings _settings;

        private readonly NetworkVariable<float> _scrollSpeed = new NetworkVariable<float>();
        private readonly NetworkVariable<float> _traveledDistance = new NetworkVariable<float>();

        private float _displayDistance;

        public float ScrollSpeed => _scrollSpeed.Value;

        public float TraveledDistance => IsServer ? _traveledDistance.Value : _displayDistance;

        public override void OnNetworkSpawn()
        {
            if (IsServer && _settings != null)
            {
                _scrollSpeed.Value = _settings.BaseScrollSpeed;
            }

            _displayDistance = _traveledDistance.Value;

            if (!ServiceLocator.IsRegistered<IWorldScrollService>())
            {
                ServiceLocator.Register<IWorldScrollService>(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (ServiceLocator.TryGet(out IWorldScrollService service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<IWorldScrollService>();
            }
        }

        /// <summary>스크롤 속도를 변경한다 (연료 감속·초가속 연출의 유일한 제어점). 호스트 전용.</summary>
        public void SetScrollSpeed(float speed)
        {
            if (IsServer)
            {
                _scrollSpeed.Value = Mathf.Max(0f, speed);
            }
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
