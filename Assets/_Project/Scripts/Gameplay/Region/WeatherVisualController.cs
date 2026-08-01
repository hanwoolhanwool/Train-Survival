using Game.Core.Events;
using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 날씨의 시야 차단 연출 (기획서 §7.4 — 모래폭풍 = 시야 차단). 순수 로컬 표현이므로
    /// 네트워크 상태를 갖지 않고 <see cref="WeatherChangedEvent"/>만 구독해 각 피어가 스스로 적용한다.
    /// Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class WeatherVisualController : MonoBehaviour
    {
        private bool _originalFogEnabled;
        private Color _originalFogColor;
        private float _originalFogDensity;
        private FogMode _originalFogMode;
        private bool _hasBackup;

        private void OnEnable()
        {
            BackupFogSettings();

            EventBus<WeatherChangedEvent>.Subscribe(OnWeatherChanged);

            // 늦게 켜졌어도 진행 중인 날씨를 반영한다.
            if (ServiceLocator.TryGet(out IWeatherService weather))
            {
                Apply(weather.ActiveWeather);
            }
        }

        private void OnDisable()
        {
            EventBus<WeatherChangedEvent>.Unsubscribe(OnWeatherChanged);

            RestoreFogSettings();
        }

        private void OnWeatherChanged(WeatherChangedEvent evt)
        {
            Apply(evt.Weather);
        }

        private void Apply(WeatherDefinition weather)
        {
            if (weather == null)
            {
                RestoreFogSettings();
                return;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = weather.FogColor;
            RenderSettings.fogDensity = weather.FogDensity;
        }

        private void BackupFogSettings()
        {
            if (_hasBackup)
            {
                return;
            }

            _originalFogEnabled = RenderSettings.fog;
            _originalFogColor = RenderSettings.fogColor;
            _originalFogDensity = RenderSettings.fogDensity;
            _originalFogMode = RenderSettings.fogMode;
            _hasBackup = true;
        }

        private void RestoreFogSettings()
        {
            if (!_hasBackup)
            {
                return;
            }

            RenderSettings.fog = _originalFogEnabled;
            RenderSettings.fogColor = _originalFogColor;
            RenderSettings.fogDensity = _originalFogDensity;
            RenderSettings.fogMode = _originalFogMode;
        }
    }
}
