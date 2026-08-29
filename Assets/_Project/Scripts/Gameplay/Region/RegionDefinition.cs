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

        /// <summary>
        /// 이 지역이 덮어쓰는 몬스터 변종 가중치 1건 (바다 계획 §12.3 안 ㉢).
        ///
        /// <para>변종을 <b>참조로</b> 가리킨다 — 카탈로그는 배열 순서가 곧 복제 식별자라
        /// 인덱스를 지역 에셋에 적어 두면 순서를 바꾸는 순간 엉뚱한 변종을 가리킨다.</para>
        ///
        /// <para>가중치 <b>0 = 이 지역에는 나오지 않는다.</b> 반대로 카탈로그 기본이 0인 변종을
        /// 특정 지역에서만 등장시킬 수도 있다 — 겹치기가 곱이 아니라 치환인 이유다
        /// (<see cref="Monsters.RegionVariantWeights"/>).</para>
        /// </summary>
        [System.Serializable]
        public sealed class MonsterVariantWeightEntry
        {
            [Tooltip("가중치를 덮어쓸 변종. 카탈로그에 없는 변종을 가리키면 무시된다.")]
            [SerializeField] private Monsters.MonsterSettings _variant;

            [Tooltip("이 지역에서 쓸 추첨 가중치. 0 = 등장하지 않는다.")]
            [SerializeField, Min(0f)] private float _weight = 1f;

            public Monsters.MonsterSettings Variant => _variant;

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

        [Header("스탬피드 (M7 1차, 기획서 §4.3 — 지역 정체성 몬스터 이벤트)")]
        [Tooltip("하루(낮 시작)마다 스탬피드가 발생할 확률 (0~1). 0 = 이 지역에서 발생하지 않는다. 날씨와 같은 규약 — 호스트 추첨·지역 첫날 제외.")]
        [SerializeField, Range(0f, 1f)] private float _stampedeChancePerDay = 0f;

        [Header("지역 보스 (M7 2차, 기획서 §5 — '지역 마지막 밤 = 대형 웨이브 + 보스')")]
        [Tooltip("이 지역의 마지막 밤에 등장할 보스 정의. 비우면 보스 없음 — 기존 대형 웨이브만으로 마지막 밤이 성립한다 (스탬피드 확률과 같은 소급 규약).")]
        [SerializeField] private Monsters.BossDefinition _bossDefinition;

        [Tooltip("이 지역만의 몬스터 변종 구성 (바다 계획 §12.3). 비우면 카탈로그 기본 가중치 그대로. " +
            "적어 둔 변종만 가중치가 치환된다 — 0을 주면 이 지역에서는 등장하지 않는다.")]
        [SerializeField] private MonsterVariantWeightEntry[] _monsterVariantWeights;

        [Header("안개 (사막 계획 §4.2 결정 ⑥·⑦ — fog 색은 지역 × 국면이 소유한다)")]
        [Tooltip("이 지역이 fog를 소유하는가. 끄면 RegionFogController 가 씬 fog를 그대로 둔다 — " +
            "배선하지 않은 지역은 1픽셀도 바뀌지 않는다(하늘 슬롯과 같은 회귀 방어선).")]
        [SerializeField] private bool _overridesFog;

        [Tooltip("낮 국면의 fog 색. 사막 = #E8DCC0 백열 하늘.")]
        [SerializeField] private Color _dayFogColor = new Color(0.784f, 0.867f, 0.91f, 1f);

        [Tooltip("낮 국면의 fog 밀도(ExponentialSquared). 씬 기본 0.0062는 300 m에서 3 %라 원경을 지운다 — " +
            "사막은 0.0015로 500 m 유적이 57 %, 800 m 산이 24 %로 남는다.")]
        [SerializeField, Min(0f)] private float _dayFogDensity = 0.0062f;

        [Tooltip("밤 국면의 fog 색. 사막 = #2B3A63 밤 남색 — 하늘은 남색인데 안개만 크림색인 어긋남을 없앤다.")]
        [SerializeField] private Color _nightFogColor = new Color(0.784f, 0.867f, 0.91f, 1f);

        [Tooltip("밤 국면의 fog 밀도. 사막은 낮과 같은 0.0015다(밤에도 대자연을 유지한다). " +
            "밤에 짙게 하는 지역(북극 블리자드 등)을 위해 필드만 2벌로 둔다.")]
        [SerializeField, Min(0f)] private float _nightFogDensity = 0.0062f;

        [Header("지형·자원")]
        [Tooltip("이 지역의 지형 세그먼트 팔레트 (레벨 디자인 가이드 §4.6). 설정하면 타일마다 " +
            "인덱스에서 결정론적으로 추첨한다 — 아래 단일 프리팹보다 우선한다. 비우면 종전대로 단일 타일.")]
        [SerializeField] private World.TerrainSegmentPalette _segmentPalette;

        [Header("하늘")]
        [Tooltip("이 지역의 스카이박스 머티리얼 (레벨 3차 · 미결 ② B안 — 슬롯은 지역이 소유한다). " +
            "RegionSkyController 가 복제본을 걸고, 낮/밤 연출은 그 위에 색만 쓴다. " +
            "비우면 슬롯을 건드리지 않는다 — 종전 하늘 그대로.")]
        [SerializeField] private Material _skyboxMaterial;

        [Tooltip("이 지역에서 스트리밍할 지형 타일 프리팹. 비우면 이전 지역 타일을 유지한다.")]
        [SerializeField] private GameObject _terrainTilePrefab;

        [Header("물 (바다)")]
        [Tooltip("이 지역의 지면이 물인가. 켜면 지상 개체가 지면(y 0)이 아니라 물면에서 선다. " +
            "바다처럼 궤도 밖이 전부 물인 지역만 켠다.")]
        [SerializeField] private bool _hasWater;

        [Tooltip("물 표면 높이 (m). HasWater 가 켜졌을 때만 쓴다. 바다 = -4 (레일 바닥에서 4 m 아래).")]
        [SerializeField] private float _waterSurfaceY = -4f;

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

        /// <summary>하루(낮 시작)당 스탬피드 발생 확률 (M7 1차). 0 = 미발생 — 대초원만 0보다 크다.</summary>
        public float StampedeChancePerDay => _stampedeChancePerDay;

        /// <summary>이 지역의 마지막 밤 보스 (M7 2차). null = 보스 없음 (하위 호환).</summary>
        public Monsters.BossDefinition BossDefinition => _bossDefinition;

        /// <summary>지형 세그먼트 팔레트 (레벨 디자인). null이면 <see cref="TerrainTilePrefab"/>을 쓴다.</summary>
        public World.TerrainSegmentPalette SegmentPalette => _segmentPalette;

        public GameObject TerrainTilePrefab => _terrainTilePrefab;

        /// <summary>이 지역의 하늘. null 이면 하늘 슬롯을 건드리지 않는다.</summary>
        public Material SkyboxMaterial => _skyboxMaterial;

        /// <summary>이 지역이 fog를 소유하는가. false면 씬 fog 그대로 — 배선 전 지역의 회귀 방어선.</summary>
        public bool OverridesFog => _overridesFog;

        /// <summary>낮 국면의 fog 색.</summary>
        public Color DayFogColor => _dayFogColor;

        /// <summary>낮 국면의 fog 밀도 (ExponentialSquared).</summary>
        public float DayFogDensity => _dayFogDensity;

        /// <summary>밤 국면의 fog 색.</summary>
        public Color NightFogColor => _nightFogColor;

        /// <summary>밤 국면의 fog 밀도 (ExponentialSquared).</summary>
        public float NightFogDensity => _nightFogDensity;

        /// <summary>이 지역의 지면이 물인가 (바다).</summary>
        public bool HasWater => _hasWater;

        /// <summary>물 표면 높이 (m). <see cref="HasWater"/>가 꺼져 있으면 의미 없다.</summary>
        public float WaterSurfaceY => _waterSurfaceY;

        /// <summary>
        /// 지상 개체가 서는 높이 — 물이 있으면 물면, 없으면 지면(0).
        /// 물 지역에서 이걸 안 쓰면 몬스터가 <b>물 위에 뜬다.</b>
        /// </summary>
        public float SurfaceY => _hasWater ? _waterSurfaceY : 0f;

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

        /// <summary>이 지역이 덮어쓰는 변종 수. 0이면 카탈로그 기본 구성 그대로다.</summary>
        public int MonsterVariantWeightCount =>
            _monsterVariantWeights == null ? 0 : _monsterVariantWeights.Length;

        /// <summary>인덱스의 변종 가중치 오버라이드. 범위 밖이면 null.</summary>
        public MonsterVariantWeightEntry GetMonsterVariantWeight(int index)
        {
            if (_monsterVariantWeights == null || index < 0 || index >= _monsterVariantWeights.Length)
            {
                return null;
            }

            return _monsterVariantWeights[index];
        }
    }
}
