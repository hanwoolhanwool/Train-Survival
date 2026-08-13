using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 밤 웨이브 밸런스 데이터 (개발 가이드 M2, 네트워크 문서 §6.2).
    /// 웨이브는 "한 번에 대량"이 아닌 "지속 유입으로 체감 물량" 전제로 설계한다.
    /// 동시 존재 상한(<see cref="MaxAliveCap"/>)은 M2 릴레이 대역폭 계측 후 확정하는 미결 수치다.
    /// </summary>
    [CreateAssetMenu(fileName = "WaveSettings", menuName = "Game/Wave Settings")]
    public sealed class WaveSettings : ScriptableObject
    {
        [Header("밤당 총량 (Day 비례 증가 — 기획서 §5 '수 → 체력 → 패턴' 중 1단계)")]
        [SerializeField, Min(1)] private int _baseCountPerNight = 6;
        [SerializeField, Min(0)] private int _countGrowthPerDay = 3;
        [SerializeField, Min(1)] private int _totalCountCap = 24;

        [Header("유입 간격 (초)")]
        [SerializeField, Min(0.5f)] private float _baseSpawnInterval = 5f;
        [SerializeField, Min(0f)] private float _intervalReductionPerDay = 0.4f;
        [SerializeField, Min(0.5f)] private float _minSpawnInterval = 1.5f;

        [Header("동시 존재 상한 (미결 — 대역폭 계측 후 확정)")]
        [SerializeField, Min(1)] private int _baseMaxAlive = 5;
        [SerializeField, Min(0)] private int _maxAliveGrowthPerDay = 1;
        [SerializeField, Min(1)] private int _maxAliveCap = 12;

        [Header("몬스터 체력 (Day 비례 증가 — 기획서 §5 '수 → 체력' 중 2단계)")]
        [Tooltip("Day 1일당 최대 체력에 가산되는 배율 (0.08 = 하루에 8 % 증가).")]
        [SerializeField, Min(0f)] private float _healthGrowthPerDay = 0.08f;

        [Tooltip("Day 성장분 체력 배율의 상한. 지역 배율은 이 상한과 별개로 곱해진다.")]
        [SerializeField, Min(1f)] private float _healthMultiplierCap = 3f;

        [Header("지역 마지막 밤 대형 웨이브 (기획서 §5 — '지역 졸업 시험')")]
        [Tooltip("마지막 밤의 총량·동시 상한 배율. 유입 간격도 이 배율만큼 짧아진다.")]
        [SerializeField, Min(1f)] private float _finalNightCountMultiplier = 2f;

        [Tooltip("마지막 밤의 몬스터 체력 배율.")]
        [SerializeField, Min(1f)] private float _finalNightHealthMultiplier = 1.5f;

        [Header("지역 중간 강화 밤 (M7 2차 결정 ⑥ — 마지막 밤보다 낮은 가중)")]
        [Tooltip("지역 중앙일 밤의 총량·동시 상한 배율. 마지막 밤(기본 2)보다 낮게 둔다.")]
        [SerializeField, Min(1f)] private float _reinforcedNightCountMultiplier = 1.4f;

        [Tooltip("지역 중앙일 밤의 몬스터 체력 배율. 마지막 밤(기본 1.5)보다 낮게 둔다.")]
        [SerializeField, Min(1f)] private float _reinforcedNightHealthMultiplier = 1.2f;

        [Header("스폰 배치 (지상 선로변)")]
        [SerializeField, Min(1f)] private float _minLateralOffset = 14f;
        [SerializeField, Min(1f)] private float _maxLateralOffset = 24f;
        [SerializeField] private float _spawnZMin = -15f;
        [SerializeField] private float _spawnZMax = 25f;

        public int BaseCountPerNight => _baseCountPerNight;

        public int CountGrowthPerDay => _countGrowthPerDay;

        public int TotalCountCap => _totalCountCap;

        public float BaseSpawnInterval => _baseSpawnInterval;

        public float IntervalReductionPerDay => _intervalReductionPerDay;

        public float MinSpawnInterval => _minSpawnInterval;

        public int BaseMaxAlive => _baseMaxAlive;

        public int MaxAliveGrowthPerDay => _maxAliveGrowthPerDay;

        public int MaxAliveCap => _maxAliveCap;

        public float MinLateralOffset => _minLateralOffset;

        public float MaxLateralOffset => _maxLateralOffset;

        public float SpawnZMin => _spawnZMin;

        public float SpawnZMax => _spawnZMax;

        public float HealthGrowthPerDay => _healthGrowthPerDay;

        public float HealthMultiplierCap => _healthMultiplierCap;

        public float FinalNightCountMultiplier => _finalNightCountMultiplier;

        public float FinalNightHealthMultiplier => _finalNightHealthMultiplier;

        public float ReinforcedNightCountMultiplier => _reinforcedNightCountMultiplier;

        public float ReinforcedNightHealthMultiplier => _reinforcedNightHealthMultiplier;

        /// <summary>순수 로직(<see cref="WaveMath"/>)에 넘길 밸런스 곡선으로 변환한다.</summary>
        public WaveCurve ToCurve()
        {
            return new WaveCurve(
                _baseCountPerNight, _countGrowthPerDay, _totalCountCap,
                _baseSpawnInterval, _intervalReductionPerDay, _minSpawnInterval,
                _baseMaxAlive, _maxAliveGrowthPerDay, _maxAliveCap,
                _healthGrowthPerDay, _healthMultiplierCap,
                _finalNightCountMultiplier, _finalNightHealthMultiplier,
                _reinforcedNightCountMultiplier, _reinforcedNightHealthMultiplier);
        }
    }
}
