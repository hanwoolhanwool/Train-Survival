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

        [Tooltip("쾌적대 안에서 수렴점 위의 체온이 내려오는 초당 속도 (M5 7차 2차) — " +
            "스튜 온기가 돔 등 쾌적 공간에서 순식간에 증발하지 않게 상향 회복과 분리한다.")]
        [SerializeField, Min(0f)] private float _cooldownRate = 0.05f;

        [Header("경고·피해 임계 (℃)")]
        [SerializeField] private float _heatWarnThreshold = 38f;
        [SerializeField] private float _heatDamageThreshold = 39f;
        [SerializeField] private float _coldWarnThreshold = 35f;
        [SerializeField] private float _coldDamageThreshold = 34f;

        [Tooltip("피해 임계를 1℃ 벗어날 때마다 초당 이만큼 체력이 깎인다.")]
        [SerializeField, Min(0f)] private float _damagePerDegreePerSecond = 3f;

        [Header("건축물 (M5 3차 그늘 · M5 7차 2차 난방 재설계)")]
        [Tooltip("그늘 건축물(돔)이 있는 칸 위에서 더운 환경 온도가 쾌적대 중심으로 당겨지는 정도 (0~1).")]
        [SerializeField, Range(0f, 1f)] private float _shelterFactor = 0.8f;

        [Tooltip("밤에 난방기 위에서 수렴하는 체온 (℃) — 정상 체온보다 살짝 낮아 난방기만으로는 " +
            "완전하지 않다 (옷·요리와 조합해야 완전. M5 7차 2차 — 사용자 요청 밤 36).")]
        [SerializeField] private float _heaterTargetNight = 36f;

        [Tooltip("낮에 난방기 위에서 수렴하는 체온 (℃) — 정상 체온 위까지 데워 낮에도 가치가 있다 " +
            "(M5 7차 2차 — 사용자 요청 낮 37).")]
        [SerializeField] private float _heaterTargetDay = 37f;

        [Tooltip("환경 온도가 쾌적 하단을 1℃ 밑돌 때마다 난방기 수렴 목표가 내려가는 양 (℃, M7 3차 결정 ③). " +
            "합산 단열이 이 페널티를 완화하고, 강화 난방로는 연료를 태우는 동안 페널티를 지운다. " +
            "0.04 = 북극 밤(−32 ℃) 맨몸에서 목표 34.3 ℃ — 경고대에는 들어가되 피해 임계는 면한다.")]
        [SerializeField, Min(0f)] private float _heaterColdPenaltyPerDegree = 0.04f;

        [Header("부위별 동상 (M7 3차 — 기획서 §4.4 북극)")]
        [Tooltip("맨 부위·완화 없음일 때의 초당 진행도. 0.02 = 저온 경고대에서 50초에 중증.")]
        [SerializeField, Min(0f)] private float _frostbiteProgressPerSecond = 0.02f;

        [Tooltip("체온이 저온 경고 임계 위로 돌아왔을 때의 초당 회복량.")]
        [SerializeField, Min(0f)] private float _frostbiteRecoveryPerSecond = 0.05f;

        [Tooltip("그 부위 장비의 추위 단열 1당 진행 저항 가산 — 클수록 '부위를 갖췄는가'가 크게 갈린다. " +
            "4 = 방한 파카(0.4) 착용 부위가 맨 부위의 2.6배 느리다.")]
        [SerializeField, Min(0f)] private float _frostbiteInsulationWeight = 4f;

        [Tooltip("전신 완화(요리 보온 버프 + 난방칸) 1당 진행 저항 가산 — 네 부위 모두에 적용된다.")]
        [SerializeField, Min(0f)] private float _frostbiteMitigationWeight = 3f;

        [Tooltip("난방칸 위에 있을 때의 전신 완화량 — 요리 보온 버프와 같은 축에 합산된다.")]
        [SerializeField, Min(0f)] private float _frostbiteHeaterMitigation = 0.5f;

        [Tooltip("경증 진입 진행도.")]
        [SerializeField, Min(0f)] private float _frostbiteMildThreshold = 0.4f;

        [Tooltip("중증 진입 진행도 (= 진행도 상한).")]
        [SerializeField, Min(0f)] private float _frostbiteSevereThreshold = 1f;

        [Tooltip("네 부위 단계 합계 1당 이동속도 배율 감소량 (합계 최대 8).")]
        [SerializeField, Min(0f)] private float _frostbiteMoveSpeedPenaltyPerStage = 0.05f;

        [Tooltip("이동속도 배율 하한 — 전 부위 중증이어도 이 밑으로는 내려가지 않는다(이동 불가 방지).")]
        [SerializeField, Range(0.1f, 1f)] private float _frostbiteMinMoveSpeedMultiplier = 0.6f;

        [Header("즉시 체온 상승 (M5 5차 — 따뜻한 음식 섭취)")]
        [Tooltip("음식의 즉시 체온 상승이 넘지 못하는 체온 상한(℃). 기본 = 고온 경고 임계 — " +
            "사막 낮에 스튜를 먹어 더위 피해를 입는 역효과를 만들지 않는다.")]
        [SerializeField] private float _instantWarmthCeiling = 38f;

        [Tooltip("더위 국면(환경이 쾌적 상한 초과)의 즉시 체온 상한(℃) — 고온 피해 임계 직전. " +
            "경고 구간까지는 허용하되 음식으로 피해 구간에 스스로 들어가지는 않는다 (M5 6차 — G3 국면화).")]
        [SerializeField] private float _instantWarmthCeilingHot = 39f;

        public float NormalBodyTemperature => _normalBodyTemperature;

        public float MinBodyTemperature => _minBodyTemperature;

        public float MaxBodyTemperature => _maxBodyTemperature;

        public float HeatDamageThreshold => _heatDamageThreshold;

        public float ColdDamageThreshold => _coldDamageThreshold;

        /// <summary>즉시 체온 상승이 넘지 못하는 상한 (℃) — 음식으로 더위 피해를 입지 않게 하는 안전선.</summary>
        public float InstantWarmthCeiling => _instantWarmthCeiling;

        /// <summary>더위 국면의 즉시 체온 상한 (℃) — 국면 판정은 <see cref="TemperatureMath.ResolveWarmthCeiling"/>.</summary>
        public float InstantWarmthCeilingHot => _instantWarmthCeilingHot;

        /// <summary>밤 난방기 수렴 체온 (℃) — 수렴은 <see cref="TemperatureMath.StepOnHeater"/>.</summary>
        public float HeaterTargetNight => _heaterTargetNight;

        /// <summary>낮 난방기 수렴 체온 (℃).</summary>
        public float HeaterTargetDay => _heaterTargetDay;

        /// <summary>난방기 한파 페널티 계수 (℃/℃, M7 3차) — 적용은 <see cref="TemperatureMath.ResolveHeaterTarget"/>.</summary>
        public float HeaterColdPenaltyPerDegree => _heaterColdPenaltyPerDegree;

        /// <summary>난방칸 위의 전신 동상 완화량 (M7 3차) — 요리 보온 버프와 같은 축에 합산된다.</summary>
        public float FrostbiteHeaterMitigation => _frostbiteHeaterMitigation;

        /// <summary>
        /// 순수 로직(<see cref="FrostbiteMath"/>)에 넘길 동상 곡선 — 진행 임계는 체온 곡선의
        /// 저온 경고 임계를 그대로 쓴다 (HUD 경고와 동상 진행이 같은 선에서 시작한다).
        /// </summary>
        public FrostbiteCurve ToFrostbiteCurve()
        {
            return new FrostbiteCurve(
                _coldWarnThreshold,
                _frostbiteProgressPerSecond, _frostbiteRecoveryPerSecond,
                _frostbiteInsulationWeight, _frostbiteMitigationWeight,
                _frostbiteMildThreshold, _frostbiteSevereThreshold,
                _frostbiteMoveSpeedPenaltyPerStage, _frostbiteMinMoveSpeedMultiplier);
        }

        /// <summary>순수 로직(<see cref="TemperatureMath"/>)에 넘길 곡선으로 변환한다.</summary>
        public TemperatureCurve ToCurve()
        {
            return ToCurve(0f);
        }

        /// <summary>
        /// 보온 장비의 평상 체온 상향(M5 7차)을 얹은 곡선 — 쾌적 환경의 수렴점(NormalBody)만
        /// 올라간다. 돔(그늘)이 환경을 쾌적대로 당겨도 "높아진 값까지만 내려간다"가 수식으로 성립한다.
        /// </summary>
        public TemperatureCurve ToCurve(float bodyWarmthBonus)
        {
            return new TemperatureCurve(
                _normalBodyTemperature + Mathf.Max(0f, bodyWarmthBonus),
                _minBodyTemperature, _maxBodyTemperature,
                _comfortMin, _comfortMax,
                _driftRatePerDegree, _recoveryRate, _cooldownRate,
                _heatWarnThreshold, _heatDamageThreshold,
                _coldWarnThreshold, _coldDamageThreshold,
                _damagePerDegreePerSecond, _shelterFactor);
        }
    }
}
