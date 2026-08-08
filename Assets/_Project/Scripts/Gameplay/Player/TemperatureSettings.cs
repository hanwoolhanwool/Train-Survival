using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 체온 밸런스 데이터 (기획서 §4.2 — 온도·생필품 관리, 개발 가이드 M4).
    /// 지역별 환경 온도는 `RegionDefinition`이 갖고, 몸이 그에 어떻게 반응하는지는 이 에셋이 정한다.
    /// </summary>
    [CreateAssetMenu(fileName = "TemperatureSettings", menuName = "Game/Temperature Settings")]
    public sealed class TemperatureSettings : ScriptableObject
    {
        [Header("체온 범위 (℃)")]
        [SerializeField] private float _normalBodyTemperature = 36.5f;
        [SerializeField] private float _minBodyTemperature = 30f;
        [SerializeField] private float _maxBodyTemperature = 42f;

        [Header("쾌적 환경 온도대 (이 밖이면 체온이 표류한다)")]
        [SerializeField] private float _comfortMin = 10f;
        [SerializeField] private float _comfortMax = 32f;

        [Header("변화 속도")]
        [Tooltip("쾌적대를 1℃ 벗어날 때마다 초당 체온이 이만큼 표류한다.")]
        [SerializeField, Min(0f)] private float _driftRatePerDegree = 0.012f;

        [Tooltip("쾌적대 안에서 정상 체온으로 회복하는 초당 속도.")]
        [SerializeField, Min(0f)] private float _recoveryRate = 0.35f;

        [Header("경고·피해 임계 (℃)")]
        [SerializeField] private float _heatWarnThreshold = 38f;
        [SerializeField] private float _heatDamageThreshold = 39f;
        [SerializeField] private float _coldWarnThreshold = 35f;
        [SerializeField] private float _coldDamageThreshold = 34f;

        [Tooltip("피해 임계를 1℃ 벗어날 때마다 초당 이만큼 체력이 깎인다.")]
        [SerializeField, Min(0f)] private float _damagePerDegreePerSecond = 3f;

        [Header("건축물 완화 (M5 3차 — 그늘은 더위만, 난방은 추위만)")]
        [Tooltip("그늘 건축물(돔)이 있는 칸 위에서 더운 환경 온도가 쾌적대 중심으로 당겨지는 정도 (0~1).")]
        [SerializeField, Range(0f, 1f)] private float _shelterFactor = 0.8f;

        [Tooltip("난방 건축물이 있는 칸 위에서 추운 환경 온도가 쾌적대 중심으로 당겨지는 정도 (0~1).")]
        [SerializeField, Range(0f, 1f)] private float _heaterFactor = 0.8f;

        [Header("즉시 체온 상승 (M5 5차 — 따뜻한 음식 섭취)")]
        [Tooltip("음식의 즉시 체온 상승이 넘지 못하는 체온 상한(℃). 기본 = 고온 경고 임계 — " +
            "사막 낮에 스튜를 먹어 더위 피해를 입는 역효과를 만들지 않는다.")]
        [SerializeField] private float _instantWarmthCeiling = 38f;

        public float NormalBodyTemperature => _normalBodyTemperature;

        public float MinBodyTemperature => _minBodyTemperature;

        public float MaxBodyTemperature => _maxBodyTemperature;

        public float HeatDamageThreshold => _heatDamageThreshold;

        public float ColdDamageThreshold => _coldDamageThreshold;

        /// <summary>즉시 체온 상승이 넘지 못하는 상한 (℃) — 음식으로 더위 피해를 입지 않게 하는 안전선.</summary>
        public float InstantWarmthCeiling => _instantWarmthCeiling;

        /// <summary>순수 로직(<see cref="TemperatureMath"/>)에 넘길 곡선으로 변환한다.</summary>
        public TemperatureCurve ToCurve()
        {
            return new TemperatureCurve(
                _normalBodyTemperature, _minBodyTemperature, _maxBodyTemperature,
                _comfortMin, _comfortMax,
                _driftRatePerDegree, _recoveryRate,
                _heatWarnThreshold, _heatDamageThreshold,
                _coldWarnThreshold, _coldDamageThreshold,
                _damagePerDegreePerSecond, _shelterFactor, _heaterFactor);
        }
    }
}
