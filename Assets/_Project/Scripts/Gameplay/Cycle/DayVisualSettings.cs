using UnityEngine;

namespace Game.Gameplay.Cycle
{
    /// <summary>
    /// 낮/밤 시각 연출의 밸런스 데이터 (M8 2차). 색·각도·시간·하한을 전부 여기로 빼서
    /// 연출 튜닝이 코드 수정 없이 에셋 수정만으로 끝나게 한다 (M8 착수 준비 불변 지침).
    /// 게임플레이 수치는 하나도 들어 있지 않다 — 이 에셋을 어떻게 고쳐도 판정은 변하지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "DayVisualSettings", menuName = "Game/Day Visual Settings")]
    public sealed class DayVisualSettings : ScriptableObject
    {
        [Header("A안 — 국면 전환 크로스페이드")]
        [Tooltip("국면이 바뀐 뒤 목표 색에 도달하기까지의 시간(초). 이 시간이 지나면 국면이 끝날 때까지 정적이다.")]
        [SerializeField, Min(0f)] private float _fadeSeconds = 6f;

        [Header("환경광 — 낮")]
        [SerializeField] private Color _dayAmbientSky = new Color(0.55f, 0.68f, 0.85f, 1f);
        [SerializeField] private Color _dayAmbientEquator = new Color(0.62f, 0.60f, 0.55f, 1f);
        [SerializeField] private Color _dayAmbientGround = new Color(0.30f, 0.27f, 0.23f, 1f);
        [SerializeField, Min(0f)] private float _dayAmbientIntensity = 1f;

        [Header("환경광 — 밤")]
        [SerializeField] private Color _nightAmbientSky = new Color(0.17f, 0.21f, 0.33f, 1f);
        [SerializeField] private Color _nightAmbientEquator = new Color(0.11f, 0.13f, 0.20f, 1f);
        [SerializeField] private Color _nightAmbientGround = new Color(0.05f, 0.05f, 0.07f, 1f);

        [Tooltip("밤 환경광 강도. 밝기 하한(§ 아래)에 걸리면 이 값보다 높게 끌어올려진다.")]
        [SerializeField, Min(0f)] private float _nightAmbientIntensity = 0.45f;

        [Header("B안 — 태양 고도 (도, 양수가 지평선 위)")]
        [Tooltip("국면 경계(일출·일몰)의 고도. 낮과 밤이 이 값에서 만나므로 전환이 이어진다.")]
        [SerializeField, Range(-30f, 60f)] private float _riseElevation = 10f;

        [Tooltip("정오의 고도.")]
        [SerializeField, Range(0f, 90f)] private float _noonElevation = 80f;

        [Tooltip("한밤의 고도. 일출각보다 낮게 두면 '밤은 광원이 더 낮다'가 성립한다. " +
                 "0 이하로 내리면 빛이 아래에서 위로 향해 지면 그림자가 사라지고 명암이 뒤집힌다 — 양수를 유지할 것.")]
        [SerializeField, Range(-30f, 30f)] private float _nightElevation = 8f;

        [Header("B안 — 태양 방위 (도)")]
        [Tooltip("하루 시작(일출)의 방위. 하루 동안 이 값에서 끝 값까지 돌면서 그림자 방향이 바뀐다.")]
        [SerializeField] private float _yawStart = -30f;

        [Tooltip("하루 끝(다음 일출 직전)의 방위. **시작 + 360이어야 한다** — 그렇지 않으면 하루가 " +
                 "한 바퀴를 채우지 못하고, Day가 바뀌는 순간 태양이 모자란 각도만큼 반대편으로 순간이동한다.")]
        [SerializeField] private float _yawEnd = 330f;

        [Header("B안 — 태양 색·강도")]
        [Tooltip("정오의 태양.")]
        [SerializeField] private Color _daySunColor = new Color(1f, 0.957f, 0.839f, 1f);
        [SerializeField, Min(0f)] private float _daySunIntensity = 1f;

        [Tooltip("국면 경계(일출·일몰)의 태양 — 낮과 밤이 이 값에서 만난다.")]
        [SerializeField] private Color _duskSunColor = new Color(1f, 0.65f, 0.36f, 1f);
        [SerializeField, Min(0f)] private float _duskSunIntensity = 0.55f;

        [Tooltip("한밤의 달빛. 너무 낮추면 열차·몬스터가 통째로 실루엣이 된다(점광원은 M8 비범위).")]
        [SerializeField] private Color _nightSunColor = new Color(0.55f, 0.62f, 0.85f, 1f);
        [SerializeField, Min(0f)] private float _nightSunIntensity = 0.3f;

        [Header("B안 — 하늘 (Skybox/Procedural)")]
        [SerializeField] private Color _daySkyTint = new Color(0.5f, 0.5f, 0.5f, 1f);
        [SerializeField] private Color _daySkyGround = new Color(0.369f, 0.349f, 0.341f, 1f);
        [SerializeField, Range(0f, 5f)] private float _daySkyAtmosphereThickness = 1f;
        [SerializeField, Range(0f, 8f)] private float _daySkyExposure = 1.3f;

        [SerializeField] private Color _nightSkyTint = new Color(0.13f, 0.16f, 0.28f, 1f);
        [SerializeField] private Color _nightSkyGround = new Color(0.05f, 0.05f, 0.07f, 1f);
        [SerializeField, Range(0f, 5f)] private float _nightSkyAtmosphereThickness = 0.55f;
        [SerializeField, Range(0f, 8f)] private float _nightSkyExposure = 0.5f;

        [Header("밝기 하한 (M8 착수 준비 리스크 2)")]
        [Tooltip("실효 환경 밝기 = 하늘색 휘도 × 강도. 이 값 밑으로 내려가면 색은 두고 강도만 끌어올린다. " +
                 "0이면 하한 없음 — '예쁜데 아무것도 안 보이는' 색 조합을 데이터 선에서 막는 장치다.")]
        [SerializeField, Min(0f)] private float _minLuminanceGuard = 0.08f;

        public float FadeSeconds => _fadeSeconds;

        public float MinLuminanceGuard => _minLuminanceGuard;

        /// <summary>순수 로직에 넘길 수치 묶음 — ScriptableObject 참조가 <see cref="DayVisualMath"/>로 새지 않게 한다.</summary>
        public DayVisualProfile ToProfile()
        {
            return new DayVisualProfile(
                new AmbientTone(_dayAmbientSky, _dayAmbientEquator, _dayAmbientGround, _dayAmbientIntensity),
                new AmbientTone(_nightAmbientSky, _nightAmbientEquator, _nightAmbientGround, _nightAmbientIntensity),
                new SunTone(_daySunColor, _daySunIntensity),
                new SunTone(_duskSunColor, _duskSunIntensity),
                new SunTone(_nightSunColor, _nightSunIntensity),
                new SkyTone(_daySkyTint, _daySkyGround, _daySkyAtmosphereThickness, _daySkyExposure),
                new SkyTone(_nightSkyTint, _nightSkyGround, _nightSkyAtmosphereThickness, _nightSkyExposure),
                _fadeSeconds,
                _riseElevation, _noonElevation, _nightElevation,
                _yawStart, _yawEnd,
                _minLuminanceGuard);
        }
    }
}
