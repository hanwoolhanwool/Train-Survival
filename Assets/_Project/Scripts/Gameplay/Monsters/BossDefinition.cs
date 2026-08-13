using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 보스의 지역 고유 패턴 종류 (M7 2차 결정 ② — 보스마다 패턴 셋이 다르다).
    /// 공통 골격(추격·돌진·페이즈) 위에 하나씩 배정된다.
    /// </summary>
    public enum BossSignaturePattern : byte
    {
        /// <summary>고유 패턴 없음 — 공통 골격만.</summary>
        None = 0,

        /// <summary>부하 소환 (숲) — 기존 몬스터 개체를 보스 소속으로 주기 소환한다.</summary>
        Summon = 1,

        /// <summary>원거리 투사체 (사막) — 포물선 탄도 + 낙점 범위 피해.</summary>
        Projectile = 2,

        /// <summary>무리 호출 (대초원) — 스탬피드 통과 무리 1열을 유입시킨다.</summary>
        HerdCall = 3,
    }

    /// <summary>
    /// 지역 보스 1종의 정의 (M7 2차 결정 ① — 정의가 자기 프리팹을 갖는다).
    /// 밸런스·패턴 파라미터를 전부 데이터로 분리해 수치 조정이 에셋 수정만으로 닫히게 한다
    /// (M4 규약). 프리팹은 이 정의를 역참조해 각 피어가 같은 값을 로컬 조회한다 —
    /// 몬스터 변종(<see cref="MonsterSettings"/>)과 같은 규약이라 복제 대상이 늘지 않는다.
    /// </summary>
    [CreateAssetMenu(fileName = "BossDefinition", menuName = "Game/Boss Definition")]
    public sealed class BossDefinition : ScriptableObject
    {
        /// <summary>처치 보상 1종 — 자원 종류와 수량 (M7 2차 결정 ③).</summary>
        [System.Serializable]
        public sealed class DropEntry
        {
            [SerializeField] private Inventory.ResourceType _type = Inventory.ResourceType.BossCore;

            [SerializeField, Min(1)] private int _count = 1;

            public Inventory.ResourceType Type => _type;

            public int Count => _count;
        }

        [Header("표시")]
        [Tooltip("HUD·로그에 표시할 보스 이름 (예: 거수, 모래 포식자).")]
        [SerializeField] private string _displayName = "거수";

        [Tooltip("이 보스의 전용 프리팹 (결정 ① — 지역별 3종). 풀·NetworkPrefabs 목록에 등재돼 있어야 한다.")]
        [SerializeField] private GameObject _prefab;

        [Header("전투 — 기본")]
        [Tooltip("기본 최대 체력. 지역 체력 배율·Day 성장 배율이 웨이브와 같은 규약으로 곱해진다.")]
        [SerializeField, Min(1f)] private float _maxHealth = 800f;

        [SerializeField, Min(0f)] private float _contactDamage = 15f;
        [SerializeField, Min(0.5f)] private float _attackRange = 3.5f;
        [SerializeField, Min(0.1f)] private float _attackInterval = 1.6f;

        [Header("이동 (지상 — 갑판 도약 없음)")]
        [SerializeField, Min(0.5f)] private float _moveSpeed = 5f;

        [Tooltip("스크롤 속도 대비 최소 초과 속도 — 추격이 항상 성립하게 하는 하한.")]
        [SerializeField, Min(0f)] private float _chaseSpeedMargin = 0.6f;

        [SerializeField, Min(0.5f)] private float _avoidProbeDistance = 4f;

        [Header("돌진 (공통 패턴)")]
        [Tooltip("돌진 사이의 대기 시간(초). 페이즈가 오를수록 짧아진다.")]
        [SerializeField, Min(1f)] private float _chargeCooldownSeconds = 9f;

        [Tooltip("예고 시간(초) — 전 피어 연출이 뜨고 방향이 고정된다. 회피 여유.")]
        [SerializeField, Min(0.2f)] private float _chargeTelegraphSeconds = 1.2f;

        [Tooltip("돌진 지속 시간(초).")]
        [SerializeField, Min(0.2f)] private float _chargeDurationSeconds = 1.6f;

        [Tooltip("벽 충돌·돌진 종료 후의 경직 시간(초) — 반격 틈.")]
        [SerializeField, Min(0f)] private float _chargeRecoverSeconds = 2f;

        [SerializeField, Min(1f)] private float _chargeSpeed = 18f;

        [SerializeField, Min(0f)] private float _chargeDamage = 25f;

        [Tooltip("돌진 경로 피해 반경 — 이 안의 플레이어·열차 부위가 맞는다.")]
        [SerializeField, Min(0.5f)] private float _chargeHitRadius = 3f;

        [Header("페이즈 (공통 패턴)")]
        [Tooltip("페이즈 전환 체력 비율 (내림차순, 0~1). 예: [0.7, 0.35] = 3페이즈 보스.")]
        [SerializeField] private float[] _phaseHealthThresholds = { 0.5f };

        [Tooltip("페이즈 1단계당 이동 속도 가산 배율 (0.12 = 단계마다 12 % 증가).")]
        [SerializeField, Min(0f)] private float _phaseSpeedBonusPerPhase = 0.12f;

        [Tooltip("페이즈 1단계당 패턴 쿨다운 배율 (0.85 = 단계마다 15 % 단축).")]
        [SerializeField, Range(0.1f, 1f)] private float _phaseCooldownScalePerPhase = 0.85f;

        [Header("고유 패턴 (결정 ② — 지역별 1개)")]
        [SerializeField] private BossSignaturePattern _signaturePattern = BossSignaturePattern.None;

        [Tooltip("이 페이즈 인덱스부터 고유 패턴이 해금된다 (0 = 처음부터, 1 = 페이즈 2부터).")]
        [SerializeField, Min(0)] private int _signatureUnlockPhaseIndex = 1;

        [Tooltip("고유 패턴 발동 간격(초). 페이즈 쿨다운 배율이 함께 곱해진다.")]
        [SerializeField, Min(0.5f)] private float _signatureIntervalSeconds = 8f;

        [Tooltip("1회 발동당 개체 수 (소환·무리 호출). 투사체는 연속 발사 수.")]
        [SerializeField, Min(1)] private int _signatureCount = 2;

        [Tooltip("소환·무리 개체의 몬스터 변종 인덱스 (MonsterVariantCatalog).")]
        [SerializeField] private int _signatureVariantIndex = -1;

        [Tooltip("이 보스가 동시에 거느릴 수 있는 소환·무리 개체 상한.")]
        [SerializeField, Min(1)] private int _signatureMaxAlive = 6;

        [Header("고유 패턴 — 투사체 (사막)")]
        [Tooltip("발사에서 낙하까지의 비행 시간(초) — 낙점 예고를 보고 피할 여유가 된다.")]
        [SerializeField, Min(0.2f)] private float _projectileFlightSeconds = 1.8f;

        [Tooltip("낙점 범위 반경(m, 수평 거리). 이 안의 플레이어가 피해를 받는다.")]
        [SerializeField, Min(0.5f)] private float _projectileImpactRadius = 3.5f;

        [SerializeField, Min(0f)] private float _projectileDamage = 20f;

        [Tooltip("탄체 표시 프리팹 (비네트워크·풀링). 비면 연출 없이 판정만 한다.")]
        [SerializeField] private BossProjectileView _projectileViewPrefab;

        [Header("처치 보상 (결정 ③)")]
        [Tooltip("처치 시 지상 드랍 목록 — 1차 드랍 경로(IResourceDropper)를 그대로 탄다.")]
        [SerializeField] private DropEntry[] _drops;

        [Header("동기화 (§6.2 — 스냅샷 + 보간)")]
        [SerializeField, Range(5f, 20f)] private float _syncHz = 15f;

        [SerializeField, Min(0.05f)] private float _interpolationDelaySeconds = 0.15f;

        public string DisplayName => _displayName;

        /// <summary>이 보스의 전용 프리팹 (결정 ①). 없으면 스폰되지 않는다.</summary>
        public GameObject Prefab => _prefab;

        public float MaxHealth => _maxHealth;

        public float ContactDamage => _contactDamage;

        public float AttackRange => _attackRange;

        public float AttackInterval => _attackInterval;

        public float MoveSpeed => _moveSpeed;

        public float ChaseSpeedMargin => _chaseSpeedMargin;

        public float AvoidProbeDistance => _avoidProbeDistance;

        public float ChargeCooldownSeconds => _chargeCooldownSeconds;

        public float ChargeTelegraphSeconds => _chargeTelegraphSeconds;

        public float ChargeDurationSeconds => _chargeDurationSeconds;

        public float ChargeRecoverSeconds => _chargeRecoverSeconds;

        public float ChargeSpeed => _chargeSpeed;

        public float ChargeDamage => _chargeDamage;

        public float ChargeHitRadius => _chargeHitRadius;

        /// <summary>페이즈 전환 체력 비율 배열 (내림차순). 길이 + 1 = 총 페이즈 수.</summary>
        public float[] PhaseHealthThresholds => _phaseHealthThresholds;

        /// <summary>총 페이즈 수 (1 = 페이즈 전환 없음).</summary>
        public int PhaseCount => (_phaseHealthThresholds == null ? 0 : _phaseHealthThresholds.Length) + 1;

        public float PhaseSpeedBonusPerPhase => _phaseSpeedBonusPerPhase;

        public float PhaseCooldownScalePerPhase => _phaseCooldownScalePerPhase;

        public BossSignaturePattern SignaturePattern => _signaturePattern;

        public int SignatureUnlockPhaseIndex => _signatureUnlockPhaseIndex;

        public float SignatureIntervalSeconds => _signatureIntervalSeconds;

        public int SignatureCount => _signatureCount;

        public int SignatureVariantIndex => _signatureVariantIndex;

        public int SignatureMaxAlive => _signatureMaxAlive;

        public float ProjectileFlightSeconds => _projectileFlightSeconds;

        public float ProjectileImpactRadius => _projectileImpactRadius;

        public float ProjectileDamage => _projectileDamage;

        public BossProjectileView ProjectileViewPrefab => _projectileViewPrefab;

        public int DropCount => _drops == null ? 0 : _drops.Length;

        /// <summary>인덱스의 드랍 항목. 범위 밖이면 null.</summary>
        public DropEntry GetDrop(int index)
        {
            if (_drops == null || index < 0 || index >= _drops.Length)
            {
                return null;
            }

            return _drops[index];
        }

        public float SyncHz => _syncHz;

        public float InterpolationDelaySeconds => _interpolationDelaySeconds;
    }
}
