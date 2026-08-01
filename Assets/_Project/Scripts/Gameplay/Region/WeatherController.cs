using System;
using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Cycle;
using Game.Gameplay.World;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 날씨 이벤트의 호스트 권위 소유자 (기획서 §7.4, 개발 가이드 M4).
    /// 지역과 달리 날씨는 무작위로 발생하므로 Day 번호에서 유도할 수 없다 — 호스트가 발생·종료를
    /// 확정하고 <see cref="NetworkVariable{T}"/>로 전파한다. M4에서는 낮 국면 한정 이벤트로 두고,
    /// 밤 진입 시 강제로 갠다. Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class WeatherController : NetworkBehaviour, IWeatherService
    {
        /// <summary>
        /// 지역 인덱스와 날씨 인덱스를 한 값으로 묶어 복제한다 — 둘을 따로 복제하면
        /// 도착 순서에 따라 "다른 지역의 날씨"로 해석되는 중간 상태가 생긴다.
        /// </summary>
        private struct WeatherSyncState : INetworkSerializable, IEquatable<WeatherSyncState>
        {
            public int RegionIndex;

            /// <summary>해당 지역의 날씨 배열 인덱스. −1 = 맑음.</summary>
            public int WeatherIndex;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref RegionIndex);
                serializer.SerializeValue(ref WeatherIndex);
            }

            public bool Equals(WeatherSyncState other)
            {
                return RegionIndex == other.RegionIndex && WeatherIndex == other.WeatherIndex;
            }

            public static WeatherSyncState Clear => new WeatherSyncState { RegionIndex = -1, WeatherIndex = -1 };
        }

        [SerializeField] private RegionTimelineSettings _regionSettings;

        private readonly NetworkVariable<WeatherSyncState> _state =
            new NetworkVariable<WeatherSyncState>(WeatherSyncState.Clear);

        private float _serverRemainingSeconds;

        public WeatherDefinition ActiveWeather => Resolve(_state.Value);

        public bool IsActive => ActiveWeather != null;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _state.Value = WeatherSyncState.Clear;
                _serverRemainingSeconds = 0f;
            }

            _state.OnValueChanged += OnStateChanged;
            EventBus<DayPhaseChangedEvent>.Subscribe(OnDayPhaseChanged);
            EventBus<RegionChangedEvent>.Subscribe(OnRegionChanged);

            if (!ServiceLocator.IsRegistered<IWeatherService>())
            {
                ServiceLocator.Register<IWeatherService>(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            _state.OnValueChanged -= OnStateChanged;
            EventBus<DayPhaseChangedEvent>.Unsubscribe(OnDayPhaseChanged);
            EventBus<RegionChangedEvent>.Unsubscribe(OnRegionChanged);

            if (ServiceLocator.TryGet(out IWeatherService service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<IWeatherService>();
            }

            // 씬을 떠날 때 감속을 남기지 않는다.
            if (IsServer)
            {
                ApplyScrollMultiplier(1f);
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _state.Value.WeatherIndex < 0)
            {
                return;
            }

            // 남은 시간은 복제하지 않는다 — 종료 시점은 호스트가 확정하고 상태 변경만 전파하면 된다.
            _serverRemainingSeconds -= Time.deltaTime;
            if (_serverRemainingSeconds <= 0f)
            {
                ServerClearWeather();
            }
        }

        private void OnDayPhaseChanged(DayPhaseChangedEvent evt)
        {
            if (!IsServer)
            {
                return;
            }

            if (evt.Phase == DayPhase.Night)
            {
                // M4의 날씨는 낮 이벤트다 — 밤 방어전과 겹치지 않게 갠다.
                ServerClearWeather();
                return;
            }

            ServerTryStartWeather();
        }

        private void OnRegionChanged(RegionChangedEvent evt)
        {
            // 지역이 바뀌면 이전 지역의 날씨는 그 자리에서 끝난다.
            if (IsServer)
            {
                ServerClearWeather();
            }
        }

        private void ServerTryStartWeather()
        {
            if (!ServiceLocator.TryGet(out IRegionService region) || region.CurrentRegion == null)
            {
                return;
            }

            RegionDefinition definition = region.CurrentRegion;
            if (definition.WeatherCount <= 0 || UnityEngine.Random.value > definition.WeatherChancePerDay)
            {
                return;
            }

            int weatherIndex = UnityEngine.Random.Range(0, definition.WeatherCount);
            WeatherDefinition weather = definition.GetWeather(weatherIndex);
            if (weather == null)
            {
                return;
            }

            _serverRemainingSeconds = weather.RollDurationSeconds();
            _state.Value = new WeatherSyncState
            {
                RegionIndex = region.RegionIndex,
                WeatherIndex = weatherIndex,
            };

            ApplyScrollMultiplier(weather.ScrollSpeedMultiplier);

            Debug.Log($"[WeatherController] 날씨 시작: {weather.DisplayName} " +
                $"({_serverRemainingSeconds:F0}s, 속도 ×{weather.ScrollSpeedMultiplier:F2})");
        }

        private void ServerClearWeather()
        {
            _serverRemainingSeconds = 0f;

            if (_state.Value.WeatherIndex < 0)
            {
                return;
            }

            _state.Value = WeatherSyncState.Clear;
            ApplyScrollMultiplier(1f);
        }

        private void ApplyScrollMultiplier(float multiplier)
        {
            if (ServiceLocator.TryGet(out IWorldScrollSpeedControl control))
            {
                control.SetEnvironmentSpeedMultiplier(multiplier);
            }
        }

        /// <summary>복제 상태 → 날씨 정의. 설정에서 직접 인덱싱해 지역 전환과의 처리 순서에 의존하지 않는다.</summary>
        private WeatherDefinition Resolve(WeatherSyncState state)
        {
            if (state.WeatherIndex < 0 || _regionSettings == null)
            {
                return null;
            }

            RegionDefinition region = _regionSettings.GetRegion(state.RegionIndex);

            return region == null ? null : region.GetWeather(state.WeatherIndex);
        }

        private void OnStateChanged(WeatherSyncState previous, WeatherSyncState current)
        {
            EventBus<WeatherChangedEvent>.Publish(new WeatherChangedEvent(Resolve(current)));
        }
    }
}
