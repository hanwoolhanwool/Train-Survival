using Game.Core.Services;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Game.Gameplay.Cycle
{
    /// <summary>
    /// 낮/밤 시각 연출의 적용자 (M8 2차). <see cref="IDayCycleService"/>의 국면 진행도를 읽어
    /// <see cref="DayVisualMath"/>가 낸 값을 <see cref="RenderSettings"/>와 방향광에 대입한다.
    /// <para>
    /// 역할이 셋으로 갈려 있다 — <b>수식</b>은 <see cref="DayVisualMath"/>, <b>원상 복구</b>는
    /// <see cref="RenderEnvironmentSnapshot"/>, <b>수치</b>는 <see cref="DayVisualSettings"/>가 갖는다.
    /// 이 클래스에 남은 것은 <b>언제 무엇을 소유하고 무엇을 대입할지</b>뿐이다.
    /// </para>
    /// <para>
    /// <b>순수 로컬 표현이다</b> — <c>NetworkBehaviour</c>가 아니고 네트워크 상태를 만들지 않으며,
    /// 피어마다 모드가 달라도 게임플레이가 갈라지지 않는다. 게임플레이 계약(<see cref="DayPhase"/>·
    /// 이벤트·서비스 시그니처)은 읽기만 한다.
    /// </para>
    /// <para>
    /// <b>fog는 건드리지 않는다</b> (M8 착수 준비 결정 ② ㉮) — <c>RenderSettings.fog*</c>는
    /// <c>WeatherVisualController</c> 단독 소유다. 이 컴포넌트가 쓰는 것은 ambient·skybox·sun과
    /// 방향광 하나뿐이라 두 컨트롤러의 소유 집합이 겹치지 않는다.
    /// </para>
    /// Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class DayCycleVisualController : MonoBehaviour
    {
        private static readonly int SkyTintId = Shader.PropertyToID("_SkyTint");
        private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
        private static readonly int AtmosphereThicknessId = Shader.PropertyToID("_AtmosphereThickness");
        private static readonly int ExposureId = Shader.PropertyToID("_Exposure");

        [SerializeField] private DayVisualSettings _settings;

        [Tooltip("연출이 회전·착색할 방향광. 비우면 태양 연출(B안)만 건너뛰고 환경광은 그대로 동작한다.")]
        [SerializeField] private Light _sunLight;

        [Tooltip("B안 하늘의 원본 머티리얼(Skybox/Procedural). 런타임에는 이 에셋의 복제본만 쓴다 — " +
                 "원본에 직접 쓰면 에디터 세션 내내 값이 남는다.")]
        [SerializeField] private Material _skyboxSource;

        [Header("모드 (M8 착수 준비 결정 ⑥ — 릴리스에서는 토글 키를 끈다)")]
        [Tooltip("Off = 아무것도 쓰지 않는다(회귀 기준선) / A = 국면 전환 크로스페이드 / B = 태양·하늘 상시 보간.")]
        [SerializeField] private DayVisualMode _mode = DayVisualMode.A;

        [Tooltip("켜면 F7로 Off → A → B → Off 순환. 같은 장면에서 즉시 비교하는 것이 이 차수의 검증 방식이다.")]
        [SerializeField] private bool _enableModeToggleKey = true;

        /// <summary>지금 화면에 실제로 반영돼 있는 모드 — <see cref="_mode"/>가 바뀌면 그 차이만큼만 원복·적용한다.</summary>
        private DayVisualMode _appliedMode = DayVisualMode.Off;

        private RenderEnvironmentSnapshot _backup;
        private bool _hasBackup;

        private Material _skyboxInstance;

        /// <summary>현재 모드 — 검증·디버그 표시용.</summary>
        public DayVisualMode Mode => _mode;

        private void OnEnable()
        {
            if (_hasBackup)
            {
                return;
            }

            _backup = RenderEnvironmentSnapshot.Capture(_sunLight);
            _hasBackup = true;
        }

        private void OnDisable()
        {
            ReleaseTo(DayVisualMode.Off);
            _appliedMode = DayVisualMode.Off;
        }

        private void Update()
        {
            HandleModeToggleInput();

            if (_mode != _appliedMode)
            {
                // 낮추는 쪽(B→A, A→Off)만 원복이 필요하다. 올리는 쪽은 아래 적용에서 자연히 덮인다.
                ReleaseTo(_mode);
                _appliedMode = _mode;
            }

            if (_mode == DayVisualMode.Off || _settings == null)
            {
                return;
            }

            if (!ServiceLocator.TryGet(out IDayCycleService cycle))
            {
                return;
            }

            float phaseElapsed = Mathf.Max(0f, cycle.PhaseDuration - cycle.PhaseRemaining);
            DayVisualProfile profile = _settings.ToProfile();

            if (_mode == DayVisualMode.A)
            {
                ApplyAmbient(DayVisualMath.EvaluateAmbientCrossfade(cycle.Phase, phaseElapsed, profile));
                return;
            }

            DayVisualState state = DayVisualMath.EvaluateContinuous(
                cycle.Phase, phaseElapsed, cycle.PhaseDuration, profile);

            ApplyAmbient(state.Ambient);
            ApplySun(state);
            ApplySky(state.Sky);
        }

        // ── 적용 ────────────────────────────────────────────────────────────

        /// <summary>
        /// 환경광 대입. 이 메서드에는 <b>실측으로 확인한 Unity 제약 두 가지</b>가 반영돼 있다.
        /// <list type="number">
        /// <item>씬 기본값인 <see cref="AmbientMode.Skybox"/>에서는 3색이 아예 읽히지 않는다 —
        /// 색을 아무리 보간해도 화면이 변하지 않으므로, 소유를 잡는 순간
        /// <see cref="AmbientMode.Trilight"/>로 바꾸고 놓을 때 되돌린다.</item>
        /// <item>그 <see cref="AmbientMode.Trilight"/>에서는 이번엔 <see cref="RenderSettings.ambientIntensity"/>가
        /// <b>무시된다</b> (강도를 1 → 0.1로 낮춰도 <c>ambientProbe</c>가 그대로였다). 그래서 강도를
        /// 프로퍼티로 넘기지 않고 <b>색에 곱해</b> 넣는다 — 밤에 강도를 내려도 어두워지지 않는 함정을 피한다.</item>
        /// </list>
        /// 어느 쪽도 씬 에셋(<c>m_AmbientMode</c>)은 손대지 않는다.
        /// </summary>
        private void ApplyAmbient(in AmbientTone tone)
        {
            if (RenderSettings.ambientMode != AmbientMode.Trilight)
            {
                RenderSettings.ambientMode = AmbientMode.Trilight;
            }

            RenderSettings.ambientSkyColor = Scale(tone.Sky, tone.Intensity);
            RenderSettings.ambientEquatorColor = Scale(tone.Equator, tone.Intensity);
            RenderSettings.ambientGroundColor = Scale(tone.Ground, tone.Intensity);

            // 무시되는 값이지만 모드를 오갈 때 남은 값이 섞이지 않도록 중립으로 고정한다.
            RenderSettings.ambientIntensity = 1f;
        }

        /// <summary>
        /// 강도를 색에 곱한다. 인스펙터에서 보는 색과 같은 공간(감마)에서 곱하므로,
        /// 하한 판정(<see cref="DayVisualMath.ApplyLuminanceGuard"/>)이 쓰는 휘도와 기준이 일치한다.
        /// </summary>
        private static Color Scale(Color color, float intensity)
        {
            return new Color(color.r * intensity, color.g * intensity, color.b * intensity, 1f);
        }

        private void ApplySun(in DayVisualState state)
        {
            if (_sunLight == null)
            {
                return;
            }

            _sunLight.color = state.Sun.Color;
            _sunLight.intensity = state.Sun.Intensity;
            _sunLight.transform.rotation = state.SunRotation;

            // 프로시저럴 하늘의 태양 원반이 실제 광원을 따라가게 한다 (씬 기본값은 미지정).
            if (RenderSettings.sun != _sunLight)
            {
                RenderSettings.sun = _sunLight;
            }
        }

        private void ApplySky(in SkyTone tone)
        {
            if (!EnsureSkyboxInstance())
            {
                return;
            }

            _skyboxInstance.SetColor(SkyTintId, tone.Tint);
            _skyboxInstance.SetColor(GroundColorId, tone.Ground);
            _skyboxInstance.SetFloat(AtmosphereThicknessId, tone.AtmosphereThickness);
            _skyboxInstance.SetFloat(ExposureId, tone.Exposure);
        }

        /// <summary>
        /// 하늘 복제본을 만들어 <see cref="RenderSettings.skybox"/>에 걸어 둔다. 원본 에셋에 직접 쓰면
        /// 에디터에서 값이 그대로 남아 다른 씬까지 물들기 때문에 <b>복제본에만</b> 쓴다.
        /// (풀링 규약의 대상은 GameObject 스폰이며, 머티리얼 인스턴스는 여기서 직접 관리한다.)
        /// </summary>
        private bool EnsureSkyboxInstance()
        {
            if (_skyboxInstance != null)
            {
                return true;
            }

            if (_skyboxSource == null)
            {
                return false;
            }

            _skyboxInstance = new Material(_skyboxSource);
            RenderSettings.skybox = _skyboxInstance;

            return true;
        }

        // ── 소유 해제 ───────────────────────────────────────────────────────

        /// <summary>
        /// 새 모드가 더 이상 소유하지 않는 것만 원래대로 돌린다. 되돌리는 방법은
        /// <see cref="RenderEnvironmentSnapshot"/>이 알고, 여기서는 <b>무엇을 언제 놓을지만</b> 정한다.
        /// </summary>
        private void ReleaseTo(DayVisualMode next)
        {
            if (!_hasBackup)
            {
                return;
            }

            // 태양·하늘은 B안에서만 소유한다.
            if (next != DayVisualMode.B)
            {
                _backup.RestoreSkyAndSun();
                DestroySkyboxInstance();
            }

            // 환경광은 A·B 공통이므로 Off로 갈 때만 놓는다.
            if (next == DayVisualMode.Off)
            {
                _backup.RestoreAmbient();
            }
        }

        private void DestroySkyboxInstance()
        {
            if (_skyboxInstance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_skyboxInstance);
            }
            else
            {
                DestroyImmediate(_skyboxInstance);
            }

            _skyboxInstance = null;
        }

        // ── 디버그: 모드 순환 ───────────────────────────────────────────────

        /// <summary>
        /// F7 = Off → A → B → Off. 숫자패드는 국면 점프(1·2·3)와 QA 핫키 12종이 전부 점유하고 있어
        /// 남는 키가 없다.
        /// </summary>
        private void HandleModeToggleInput()
        {
            if (!_enableModeToggleKey)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.f7Key.wasPressedThisFrame)
            {
                return;
            }

            _mode = _mode == DayVisualMode.Off
                ? DayVisualMode.A
                : _mode == DayVisualMode.A ? DayVisualMode.B : DayVisualMode.Off;

            Debug.Log($"[DayVisual] 모드 → {_mode}");
        }
    }
}
