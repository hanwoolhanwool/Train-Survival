using Game.Core.Events;
using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 날씨의 입자 연출 (사막 지역 구현 계획 4차 §8).
    ///
    /// <para><b>왜 fog 만으로 부족한가.</b> 모래폭풍은 스크롤을 ×0.65로 늦추고 fog 를 0.035로
    /// 올린다 — 몸은 느려진 것을 느끼는데 <b>눈에는 갈색 안개가 낀 것</b>뿐이다.
    /// "안개"와 "폭풍"을 가르는 것은 밀도가 아니라 <b>흐르는 알갱이</b>다.</para>
    ///
    /// <para><b>순수 로컬 표현이다</b> — <c>NetworkBehaviour</c>가 아니고 네트워크 상태를 만들지 않는다.
    /// 날씨 전환은 이미 전 피어가 같은 값을 받는다(<see cref="WeatherChangedEvent"/>).</para>
    ///
    /// <para>자식의 <see cref="ParticleSystem"/>을 전부 켜고 끈다 — 어느 것을 켤지 배열로 배선하면
    /// 조각을 하나 더 넣을 때마다 배선이 새어 나간다.</para>
    ///
    /// Game 씬에 1개 배치한다. 담당 날씨가 지정되지 않으면 <b>아무 날씨에나</b> 켜진다.
    /// </summary>
    public sealed class WeatherParticleView : MonoBehaviour
    {
        [Tooltip("이 날씨에만 켠다 (예: 모래폭풍). 비우면 어떤 날씨든 켜진다 — " +
                 "지역마다 다른 입자를 쓰려면 각각 배선한다.")]
        [SerializeField] private WeatherDefinition _weather;

        private ParticleSystem[] _systems;
        private bool _playing;

        /// <summary>이 연출이 담당하는 날씨. null = 전 날씨.</summary>
        public WeatherDefinition Weather => _weather;

        /// <summary>지금 입자가 돌고 있는가 — 검수·디버깅용.</summary>
        public bool IsPlaying => _playing;

        /// <summary>이 날씨에 입자를 켜야 하는가 — 순수 판정이라 EditMode 가 고정한다.</summary>
        public static bool ShouldPlay(WeatherDefinition active, WeatherDefinition assigned)
        {
            if (active == null)
            {
                return false;
            }

            return assigned == null || ReferenceEquals(active, assigned);
        }

        private void Awake()
        {
            _systems = GetComponentsInChildren<ParticleSystem>(true);
        }

        private void OnEnable()
        {
            EventBus<WeatherChangedEvent>.Subscribe(OnWeatherChanged);

            // 늦게 켜졌어도 진행 중인 날씨를 반영한다 (WeatherVisualController 와 같은 규약).
            Apply(ServiceLocator.TryGet(out IWeatherService weather) ? weather.ActiveWeather : null);
        }

        private void OnDisable()
        {
            EventBus<WeatherChangedEvent>.Unsubscribe(OnWeatherChanged);
            Apply(null);
        }

        private void OnWeatherChanged(WeatherChangedEvent evt)
        {
            Apply(evt.Weather);
        }

        private void Apply(WeatherDefinition active)
        {
            bool play = ShouldPlay(active, _weather);
            if (play == _playing || _systems == null)
            {
                return;
            }

            _playing = play;
            for (int i = 0; i < _systems.Length; i++)
            {
                if (_systems[i] == null)
                {
                    continue;
                }

                if (play)
                {
                    _systems[i].Play(true);
                }
                else
                {
                    // 걷힐 때는 남은 알갱이가 자기 수명만큼 흘러 나가게 둔다 — 뚝 끊기면 연출이 아니라 버그로 보인다.
                    _systems[i].Stop(true, ParticleSystemStopBehavior.StopEmitting);
                }
            }
        }
    }
}
