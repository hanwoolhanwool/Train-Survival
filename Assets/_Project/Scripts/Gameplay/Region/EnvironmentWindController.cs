using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Cycle;
using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 환경 바람을 날씨·국면에 묶어 <b>전역 셰이더 값 하나</b>로 내보낸다 (천막 계획 3차).
    ///
    /// 천막 천이 이 값을 곱해 흔들린다 — 모래폭풍이 오면 크게 펄럭이고 밤에는 잦아든다.
    /// 플레이어 위치를 보지 않으므로 <b>네 사람이 같은 천을 본다</b>. 전역 값 하나라
    /// 천막이 몇 채든 배칭이 깨지지 않고, 머티리얼 에셋을 런타임에 고치지도 않는다
    /// (공유 머티리얼을 만지면 에디터에서 에셋이 더럽혀진다).
    ///
    /// 로컬 표현 전용 — 상태를 소유하지 않고 이벤트·서비스 조회로만 그린다. 인게임 씬에 1개.
    /// </summary>
    public sealed class EnvironmentWindController : MonoBehaviour
    {
        /// <summary>천 셰이더가 읽는 전역 배율 이름 — 깃발·풀 같은 다음 소비자도 이 값을 쓴다.</summary>
        public const string GlobalWindScaleProperty = "_GlobalWindScale";

        private static readonly int WindScaleId = Shader.PropertyToID(GlobalWindScaleProperty);

        [Tooltip("밤에 곱하는 배율 — 1이면 밤낮이 같다. 낮보다 잦아들어야 밤이 조용해진다.")]
        [SerializeField, Range(0f, 1f)] private float _nightScale = 0.45f;

        [Tooltip("목표로 다가가는 초당 속도 — 날씨가 바뀌는 순간 천이 튀지 않게 한다.")]
        [SerializeField, Min(0.05f)] private float _changeRatePerSecond = 0.6f;

        private float _current = EnvironmentWindMath.CalmScale;
        private float _target = EnvironmentWindMath.CalmScale;

        private void OnEnable()
        {
            EventBus<WeatherChangedEvent>.Subscribe(OnWeatherChanged);
            EventBus<DayPhaseChangedEvent>.Subscribe(OnPhaseChanged);

            // 늦게 켜진 경우를 위해 현재 상태로 맞추고 시작한다 — 첫 프레임부터 값이 맞아야
            // 천이 한 번 크게 튀었다가 제자리를 찾는 일이 없다.
            _target = ResolveTarget();
            _current = _target;
            Apply();
        }

        private void OnDisable()
        {
            EventBus<WeatherChangedEvent>.Unsubscribe(OnWeatherChanged);
            EventBus<DayPhaseChangedEvent>.Unsubscribe(OnPhaseChanged);

            // 씬을 떠날 때 기준값으로 되돌린다 — 다음 씬이 이 컨트롤러 없이 천을 그릴 수 있다.
            Shader.SetGlobalFloat(WindScaleId, EnvironmentWindMath.CalmScale);
        }

        private void Update()
        {
            if (Mathf.Approximately(_current, _target))
            {
                return;
            }

            _current = EnvironmentWindMath.Step(_current, _target, _changeRatePerSecond, Time.deltaTime);
            Apply();
        }

        private void OnWeatherChanged(WeatherChangedEvent evt)
        {
            _target = ResolveTarget();
        }

        private void OnPhaseChanged(DayPhaseChangedEvent evt)
        {
            _target = ResolveTarget();
        }

        /// <summary>지금 날씨·국면이 요구하는 배율 — 서비스가 없으면 맑은 낮으로 본다.</summary>
        private float ResolveTarget()
        {
            float weatherScale = EnvironmentWindMath.CalmScale;
            if (ServiceLocator.TryGet(out IWeatherService weather) && weather.ActiveWeather != null)
            {
                weatherScale = weather.ActiveWeather.WindScale;
            }

            bool isNight = ServiceLocator.TryGet(out IDayCycleService cycle)
                && cycle.Phase == DayPhase.Night;

            return EnvironmentWindMath.ResolveTargetScale(weatherScale, isNight, _nightScale);
        }

        private void Apply()
        {
            Shader.SetGlobalFloat(WindScaleId, _current);
        }
    }
}
