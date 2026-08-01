using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 지역(계절) 1종의 밸런스·비주얼 정의 (기획서 §4 — 지역당 3~5일 주기).
    /// 지역별 일수·난이도·자원 밀도·지형 프리팹을 전부 데이터로 분리해,
    /// 주기 축소 밸런싱(기획서 §2)이 코드 수정 없이 에셋 수정만으로 가능하게 한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RegionDefinition", menuName = "Game/Region Definition")]
    public sealed class RegionDefinition : ScriptableObject
    {
        [Header("표시")]
        [Tooltip("HUD에 표시할 지역 이름 (예: 숲, 사막).")]
        [SerializeField] private string _displayName = "숲";

        [Tooltip("이 지역에 머무는 일수. 기획서 §4 기준안 — 숲 5일 / 사막 4일 (3~5일 범위).")]
        [SerializeField, Min(1)] private int _dayCount = 5;

        [Header("난이도 배율 (기획서 §4 지역 표 — 숲 1 / 사막 4)")]
        [Tooltip("밤 웨이브 총량·동시 상한에 곱하는 배율.")]
        [SerializeField, Min(0.1f)] private float _waveCountMultiplier = 1f;

        [Tooltip("몬스터 최대 체력에 곱하는 배율.")]
        [SerializeField, Min(0.1f)] private float _monsterHealthMultiplier = 1f;

        [Header("환경 온도 (기획서 §4.2 — 사막은 낮 고온·밤 급랭)")]
        [Tooltip("낮 국면의 환경 온도(℃). 쾌적대를 벗어나면 플레이어 체온이 그 방향으로 끌려간다.")]
        [SerializeField] private float _dayAmbientTemperature = 22f;

        [Tooltip("밤 국면의 환경 온도(℃).")]
        [SerializeField] private float _nightAmbientTemperature = 15f;

        [Header("지형·자원")]
        [Tooltip("이 지역에서 스트리밍할 지형 타일 프리팹. 비우면 이전 지역 타일을 유지한다.")]
        [SerializeField] private GameObject _terrainTilePrefab;

        [Tooltip("이 지역의 지상 자원 프리팹. 비우면 스포너 기본 프리팹을 쓴다.")]
        [SerializeField] private GameObject _resourcePrefab;

        [Tooltip("자원 스폰 간격 배율 — 클수록 자원이 희소하다 (기획서 §4 자원 등급: 숲 3 / 사막 1).")]
        [SerializeField, Min(0.1f)] private float _resourceSpawnIntervalMultiplier = 1f;

        public string DisplayName => _displayName;

        public int DayCount => _dayCount;

        public float WaveCountMultiplier => _waveCountMultiplier;

        public float MonsterHealthMultiplier => _monsterHealthMultiplier;

        public float DayAmbientTemperature => _dayAmbientTemperature;

        public float NightAmbientTemperature => _nightAmbientTemperature;

        public GameObject TerrainTilePrefab => _terrainTilePrefab;

        public GameObject ResourcePrefab => _resourcePrefab;

        public float ResourceSpawnIntervalMultiplier => _resourceSpawnIntervalMultiplier;
    }
}
