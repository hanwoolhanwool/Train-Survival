using Game.Core.Events;
using Game.Core.Logging;
using Game.Core.Services;
using Game.Gameplay.Cycle;
using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 지역 × 국면 fog의 소유자 (사막 지역 구현 계획 §4.2 · 결정 ⑥·⑦).
    ///
    /// <para><b>왜 신설했나.</b> 종전에는 <c>RenderSettings.fog*</c>가
    /// <see cref="WeatherVisualController"/> 단독 소유였고 낮/밤 연출은 fog를 건드리지 않았다
    /// (M8 착수 준비 결정 ② ㉮). 그래서 fog 색이 24시간 고정이었고, 사막 낮의 백열 하늘색
    /// <c>#E8DCC0</c>이 <b>밤에도 크림색 헤이즈</b>로 남는다 — 밤은 하루의 38.5 %다.</para>
    ///
    /// <para><b>소유권은 3단이다.</b> ① 지역 × 국면 기본값(이 컴포넌트, 상시) →
    /// ② 날씨 덮어쓰기(<see cref="WeatherVisualController"/>, 날씨 지속 중) →
    /// ③ 복원은 <b>"씬 값"이 아니라 "현재 지역 × 국면 값"으로</b>(<see cref="IRegionFogProvider"/>).
    /// §10.12가 스카이박스에서 세운 규약 — <b>슬롯은 지역, 프로퍼티는 국면</b> — 과 같은 모양이다.</para>
    ///
    /// <para><b>회귀 방어선</b> — 지역이 fog를 소유하지 않으면(<see cref="RegionDefinition.OverridesFog"/>가
    /// 꺼져 있으면) 씬 값을 그대로 두고 놓는다. 하늘 슬롯과 같은 규약이라, 배선하지 않은 지역·씬은
    /// <b>1픽셀도 바뀌지 않는다.</b></para>
    ///
    /// <para><b>순수 로컬 표현이다</b> — <c>NetworkBehaviour</c>가 아니고 네트워크 상태를 만들지 않는다.
    /// 지역·국면은 이미 전 피어가 같은 값을 갖는다.</para>
    ///
    /// Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class RegionFogController : MonoBehaviour, IRegionFogProvider
    {
        [Tooltip("국면 전환 크로스페이드 시간(초). 낮/밤 연출의 DayVisualSettings._fadeSeconds 와 같은 값을 쓴다 — " +
                 "안개만 다른 속도로 바뀌면 전환이 두 번 일어난 것처럼 보인다.")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 6f;

        private bool _hasBackup;
        private bool _sceneFogEnabled;
        private FogMode _sceneFogMode;
        private Color _sceneFogColor;
        private float _sceneFogDensity;

        /// <summary>지금 fog를 쥐고 있는가 — 놓을 때 씬 값으로 되돌릴지를 이 값이 정한다.</summary>
        private bool _owns;

        private Color _fromColor;
        private float _fromDensity;
        private Color _toColor;
        private float _toDensity;
        private float _fadeElapsed;

        private RegionDefinition _appliedRegion;
        private DayPhase _appliedPhase;
        private bool _hasApplied;

        /// <inheritdoc />
        public bool OwnsFog => _owns;

        /// <summary>
        /// 크로스페이드 진행도 (0 = 출발, 1 = 목표). 페이드 시간이 0 이하면 즉시 목표다 —
        /// 순수 함수라 EditMode가 고정한다.
        /// </summary>
        public static float EvaluateFadeProgress(float elapsedSeconds, float fadeSeconds)
        {
            if (fadeSeconds <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(elapsedSeconds / fadeSeconds);
        }

        /// <summary>
        /// 국면에 맞는 fog 한 벌을 고른다. 밀도까지 국면별로 갖는 이유는 다른 지역
        /// (북극 블리자드 등)이 밤에 짙게 할 여지를 남기기 위해서다 — 사막은 낮·밤 동일 0.0015다.
        /// </summary>
        public static void ResolvePhaseFog(
            DayPhase phase,
            Color dayColor, float dayDensity,
            Color nightColor, float nightDensity,
            out Color color, out float density)
        {
            bool isNight = phase == DayPhase.Night;
            color = isNight ? nightColor : dayColor;
            density = isNight ? nightDensity : dayDensity;
        }

        /// <inheritdoc />
        public bool TryApplyCurrentFog()
        {
            if (!TryResolveTarget(out Color color, out float density))
            {
                return false;
            }

            // 날씨가 걷힌 직후다 — 크로스페이드 없이 그 시각의 국면 색으로 곧장 돌아간다.
            _fromColor = color;
            _fromDensity = density;
            _toColor = color;
            _toDensity = density;
            _fadeElapsed = _fadeSeconds;
            Write(color, density);
            return true;
        }

        private void OnEnable()
        {
            if (!_hasBackup)
            {
                _sceneFogEnabled = RenderSettings.fog;
                _sceneFogMode = RenderSettings.fogMode;
                _sceneFogColor = RenderSettings.fogColor;
                _sceneFogDensity = RenderSettings.fogDensity;
                _hasBackup = true;
            }

            if (!ServiceLocator.IsRegistered<IRegionFogProvider>())
            {
                ServiceLocator.Register<IRegionFogProvider>(this);
            }

            EventBus<RegionChangedEvent>.Subscribe(OnRegionChanged);
            EventBus<DayPhaseChangedEvent>.Subscribe(OnPhaseChanged);
        }

        private void OnDisable()
        {
            EventBus<RegionChangedEvent>.Unsubscribe(OnRegionChanged);
            EventBus<DayPhaseChangedEvent>.Unsubscribe(OnPhaseChanged);

            if (ServiceLocator.TryGet(out IRegionFogProvider provider) && ReferenceEquals(provider, this))
            {
                ServiceLocator.Unregister<IRegionFogProvider>();
            }

            Release();
        }

        // 지역·국면 전환은 이벤트로 오지만, 늦게 켜진 경우와 이벤트를 놓친 경우가 있어
        // 목표는 매 프레임 조회한다 (조회 두 번이라 비용이 없다 — SeaSurfaceView 와 같은 규약).
        private void LateUpdate()
        {
            bool weatherActive = ServiceLocator.TryGet(out IWeatherService weather) && weather.IsActive;

            if (!TryResolveTarget(out Color color, out float density))
            {
                // 날씨가 덮어쓰고 있는 동안 놓으면 폭풍 안개를 씬 값으로 지워 버린다.
                if (!weatherActive)
                {
                    Release();
                }

                return;
            }

            if (!_hasApplied)
            {
                // 첫 적용 — 씬 값에서 출발해 크로스페이드하면 켜지는 순간 색이 튄다.
                _fromColor = color;
                _fromDensity = density;
                _toColor = color;
                _toDensity = density;
                _fadeElapsed = _fadeSeconds;
                _hasApplied = true;
            }
            else if (_toColor != color || !Mathf.Approximately(_toDensity, density))
            {
                // 국면·지역이 바뀌었다 — 지금 화면에 있는 색에서 출발해야 이어져 보인다.
                _fromColor = RenderSettings.fogColor;
                _fromDensity = RenderSettings.fogDensity;
                _toColor = color;
                _toDensity = density;
                _fadeElapsed = 0f;
            }

            _fadeElapsed += Time.deltaTime;

            // 날씨가 켜져 있는 동안은 2층(WeatherVisualController)의 것이다 — 쓰지 않고 진행만 시킨다.
            if (weatherActive)
            {
                return;
            }

            float t = EvaluateFadeProgress(_fadeElapsed, _fadeSeconds);
            Write(Color.Lerp(_fromColor, _toColor, t), Mathf.Lerp(_fromDensity, _toDensity, t));
        }

        private void OnRegionChanged(RegionChangedEvent evt)
        {
            if (evt.Region == _appliedRegion)
            {
                return;
            }

            GameLog.Info(LogCategory.Cycle, evt.Region != null && evt.Region.OverridesFog
                ? $"지역 안개 적용 — {evt.Region.DisplayName}"
                : "지역 안개 없음 — 씬 값 유지");
        }

        private void OnPhaseChanged(DayPhaseChangedEvent evt)
        {
            // 목표 조회는 LateUpdate 가 하므로 여기서는 아무것도 하지 않는다.
            // 구독을 유지하는 이유는 국면 전환이 이벤트로 통지된다는 계약을 이 컴포넌트도 따르기 때문이다.
            _appliedPhase = evt.Phase;
        }

        /// <summary>현재 지역 × 국면의 fog 한 벌. 지역이 fog를 소유하지 않으면 false.</summary>
        private bool TryResolveTarget(out Color color, out float density)
        {
            color = _sceneFogColor;
            density = _sceneFogDensity;

            if (!ServiceLocator.TryGet(out IRegionService region))
            {
                return false;
            }

            RegionDefinition definition = region.CurrentRegion;
            if (definition == null || !definition.OverridesFog)
            {
                return false;
            }

            DayPhase phase = ServiceLocator.TryGet(out IDayCycleService cycle) ? cycle.Phase : _appliedPhase;
            ResolvePhaseFog(
                phase,
                definition.DayFogColor, definition.DayFogDensity,
                definition.NightFogColor, definition.NightFogDensity,
                out color, out density);

            _appliedRegion = definition;
            return true;
        }

        private void Write(Color color, float density)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = color;
            RenderSettings.fogDensity = density;
            _owns = true;
        }

        /// <summary>fog를 놓는다 — <b>내가 쥐고 있었을 때만</b> 씬 값으로 되돌린다.</summary>
        private void Release()
        {
            _hasApplied = false;
            _appliedRegion = null;

            if (!_owns)
            {
                return;
            }

            _owns = false;

            if (!_hasBackup)
            {
                return;
            }

            RenderSettings.fog = _sceneFogEnabled;
            RenderSettings.fogMode = _sceneFogMode;
            RenderSettings.fogColor = _sceneFogColor;
            RenderSettings.fogDensity = _sceneFogDensity;
        }
    }
}
