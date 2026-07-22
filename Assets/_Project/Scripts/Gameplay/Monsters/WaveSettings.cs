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
    }
}
