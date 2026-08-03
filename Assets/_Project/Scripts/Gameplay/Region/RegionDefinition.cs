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
        /// <summary>
        /// 지역의 자원 스폰 후보 1종 — 자원 종류와 추첨 가중치.
        /// 프리팹은 전 종류가 공유한다 (몬스터 변종과 같은 규약 — 네트워크 프리팹 목록을 늘리지 않는다).
        /// </summary>
        [System.Serializable]
        public sealed class ResourceSpawnEntry
        {
            [SerializeField] private Inventory.ResourceType _type = Inventory.ResourceType.Wood;

            [Tooltip("스폰 1회당 이 종류가 뽑힐 상대 가중치.")]
            [SerializeField, Min(0f)] private float _weight = 1f;

            public Inventory.ResourceType Type => _type;

            public float Weight => _weight;
        }

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

        [Header("날씨 (기획서 §7.4 — 지역 정체성 강화 요소)")]
        [Tooltip("이 지역에서 발생할 수 있는 날씨 목록. 비우면 항상 맑다.")]
        [SerializeField] private WeatherDefinition[] _weathers;

        [Tooltip("하루(낮 시작)마다 날씨가 발생할 확률 (0~1).")]
        [SerializeField, Range(0f, 1f)] private float _weatherChancePerDay = 0f;

        [Header("지형·자원")]
        [Tooltip("이 지역에서 스트리밍할 지형 타일 프리팹. 비우면 이전 지역 타일을 유지한다.")]
        [SerializeField] private GameObject _terrainTilePrefab;

        [Tooltip("이 지역의 자원 스폰 후보(종류 + 가중치). 비우면 스포너가 기본 종류로 심는다.")]
        [SerializeField] private ResourceSpawnEntry[] _resourceSpawns;

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

        public int WeatherCount => _weathers == null ? 0 : _weathers.Length;

        public float WeatherChancePerDay => _weatherChancePerDay;

        /// <summary>인덱스의 날씨 정의. 범위 밖이거나 비어 있으면 null.</summary>
        public WeatherDefinition GetWeather(int index)
        {
            if (_weathers == null || index < 0 || index >= _weathers.Length)
            {
                return null;
            }

            return _weathers[index];
        }

        public GameObject TerrainTilePrefab => _terrainTilePrefab;

        public GameObject ResourcePrefab => _resourcePrefab;

        public int ResourceSpawnCount => _resourceSpawns == null ? 0 : _resourceSpawns.Length;

        /// <summary>인덱스의 자원 스폰 후보. 범위 밖이면 null.</summary>
        public ResourceSpawnEntry GetResourceSpawn(int index)
        {
            if (_resourceSpawns == null || index < 0 || index >= _resourceSpawns.Length)
            {
                return null;
            }

            return _resourceSpawns[index];
        }

        public float ResourceSpawnIntervalMultiplier => _resourceSpawnIntervalMultiplier;
    }
}
