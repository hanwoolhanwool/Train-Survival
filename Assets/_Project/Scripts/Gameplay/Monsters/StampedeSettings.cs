using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 스탬피드 이벤트 밸런스 데이터 (M7 1차, 기획서 §4.3 — "사냥감 풍부 + 방심 방지").
    /// 발생 확률은 지역 데이터(<see cref="Region.RegionDefinition.StampedeChancePerDay"/>)가 갖고,
    /// 무리의 규모·배치·드랍은 이 에셋이 갖는다 — 수치 하드코딩 금지 (개발 가이드 §3).
    /// </summary>
    [CreateAssetMenu(fileName = "StampedeSettings", menuName = "Game/Stampede Settings")]
    public sealed class StampedeSettings : ScriptableObject
    {
        [Header("변종 (스탬피드 전용 — 웨이브 추첨에서 제외되도록 가중치 0으로 등재)")]
        [Tooltip("MonsterVariantCatalog에서 이 인덱스의 변종을 스폰한다. 인덱스 = 복제 식별자 (append-only).")]
        [SerializeField, Min(0)] private int _variantIndex = 4;

        [Header("유입 계획 (연속 유입 열 — 동시 수 억제)")]
        [Tooltip("이벤트 1회의 총 유입 마릿수.")]
        [SerializeField, Min(1)] private int _totalCount = 30;

        [Tooltip("유입 간격 (초).")]
        [SerializeField, Min(0.1f)] private float _spawnIntervalSeconds = 1.2f;

        [Tooltip("동시 존재 상한 — 밤 웨이브 상한과 독립인 별도 대역폭 방어선.")]
        [SerializeField, Min(1)] private int _maxAlive = 12;

        [Header("스폰 배치 (전방 지상 — 열차 옆을 스쳐 후방으로)")]
        [Tooltip("무리 열의 측면 대역 최소 거리 (m). 열차 폭 밖이되 웨이브 대역(14~24)보다 가깝게 스친다.")]
        [SerializeField, Min(1f)] private float _minLateralOffset = 4f;

        [SerializeField, Min(1f)] private float _maxLateralOffset = 9f;

        [Tooltip("유입 z 위치 (전방). 개체는 여기서 나타나 -Z로 직선 주행한다.")]
        [SerializeField] private float _spawnZ = 45f;

        [Tooltip("유입 z 지터 (±m) — 열이 한 점에서 겹쳐 나오지 않게 한다.")]
        [SerializeField, Min(0f)] private float _spawnZJitter = 3f;

        [Header("드랍 (결정 ③ — 처치 시 식재료. 스탬피드 개체 한정)")]
        [SerializeField] private Inventory.ResourceType _dropType = Inventory.ResourceType.RawFood;

        [SerializeField, Min(1)] private int _dropCount = 1;

        public int VariantIndex => _variantIndex;

        public int TotalCount => _totalCount;

        public float SpawnIntervalSeconds => _spawnIntervalSeconds;

        public int MaxAlive => _maxAlive;

        public float MinLateralOffset => _minLateralOffset;

        public float MaxLateralOffset => _maxLateralOffset;

        public float SpawnZ => _spawnZ;

        public float SpawnZJitter => _spawnZJitter;

        /// <summary>처치 드랍 자원 종류 — 일반 웨이브 몬스터는 드랍이 없다 (탄약 경제 보호).</summary>
        public Inventory.ResourceType DropType => _dropType;

        public int DropCount => _dropCount;
    }
}
