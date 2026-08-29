using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 몬스터 밸런스·조향 데이터 (개발 가이드 M2, 네트워크 문서 §4.3).
    /// "이동속도 > 스크롤 속도" 제약은 <see cref="ChaseSpeedMargin"/>으로 데이터에서 강제한다 — 추격 성립 조건.
    /// </summary>
    [CreateAssetMenu(fileName = "MonsterSettings", menuName = "Game/Monster Settings")]
    public sealed class MonsterSettings : ScriptableObject
    {
        [Header("변종 (기획서 §5 — Day 비례 '패턴' 축)")]
        [Tooltip("디버그 로그·HUD에 쓰는 이름.")]
        [SerializeField] private string _displayName = "일반형";

        [Tooltip("이 Day부터 웨이브에 섞여 등장한다 — 새 행동은 Day가 지나며 추가된다.")]
        [SerializeField, Min(1)] private int _minDayToAppear = 1;

        [Tooltip("등장 가능한 변종 사이의 추첨 가중치. 클수록 자주 나온다.")]
        [SerializeField, Min(0f)] private float _spawnWeight = 1f;

        [Header("변종 시각 (M5 8차 — 시각 구분)")]
        [Tooltip("몸통 색 (_BaseColor 대체) — 전 변종이 한 프리팹을 공유하므로 색이 외형 구분이다.")]
        [SerializeField] private Color _variantColor = new Color(0.55f, 0.1f, 0.12f, 1f);

        [Tooltip("표현 스케일 배율 — 몸집 차이를 보이게 한다. 판정·충돌은 변하지 않는다.")]
        [SerializeField, Range(0.5f, 2f)] private float _visualScale = 1f;

        [Header("이동 (커스텀 조향 — NavMesh 불사용)")]
        [SerializeField, Min(0.5f)] private float _moveSpeed = 6.5f;
        [SerializeField, Min(0f)] private float _chaseSpeedMargin = 0.7f;
        [SerializeField, Min(0.5f)] private float _avoidProbeDistance = 3f;
        [SerializeField, Min(0.1f)] private float _leapHorizontalRange = 3f;

        [Header("물 위의 위협 (바다 계획 §8.2 — ㄴ 물고기 점프)")]
        [Tooltip("물에서만 살며 도약으로만 공격하는가. 켜면 갑판·상판에 올라서지 않고 " +
            "튀어올랐다 물로 되떨어진다. 물속 표적에게는 도약하지 않고 그대로 추격한다.")]
        [SerializeField] private bool _aquaticLeaper;

        [Tooltip("튀어오를 때 정점의 월드 높이(m). AquaticLeaper 일 때만 쓴다. " +
            "바다 결정 ⑨ = 1.5 — 상판(0) 위 플레이어는 닿고 갑판(3.566)은 닿지 않는다.")]
        [SerializeField] private float _leapApexY = 1.5f;

        [Header("하늘의 위협 (바다 계획 §13 — ㄷ 급강하 왕복)")]
        [Tooltip("하늘에서 급강하로 공격하는가. 켜면 지상 이동·중력을 쓰지 않고 " +
            "순항 고도와 표적 높이 사이를 왕복한다. 강하 개시 거리는 LeapHorizontalRange 를 쓴다.")]
        [SerializeField] private bool _aerialDiver;

        [Tooltip("순항 고도 (월드 y). 손 무기가 닿는 천장 위여야 순항 중에는 잡히지 않는다 — " +
            "산탄총 20 m + 갑판 3.566 = y 23.5 가 그 천장이다.")]
        [SerializeField] private float _cruiseAltitudeY = 34f;

        [Tooltip("급강하 속도 (m/s). 상승은 이 값의 65 %다 — 내려올 때보다 천천히 빠져나가야 " +
            "반격 창이 열린다.")]
        [SerializeField, Min(1f)] private float _diveSpeed = 18f;

        [Tooltip("표적 높이에서 머무르는 시간(초). 이 동안이 가장 확실한 반격 창이다.")]
        [SerializeField, Min(0f)] private float _hoverSeconds = 1.2f;

        [Header("전투")]
        [SerializeField, Min(1f)] private float _maxHealth = 100f;
        [SerializeField, Min(0f)] private float _attackDamage = 15f;
        [SerializeField, Min(0.5f)] private float _attackRange = 2.2f;
        [SerializeField, Min(0.1f)] private float _attackInterval = 1.4f;

        [Header("집게 그랩·기절 (M5 5차 — 6차에서 파지로 재정의)")]
        [Tooltip("요구 집게 등급 — 이 값 이상의 집게 등급이어야 낚아챌 수 있다 (3 = 향후 대형 변종 자리).")]
        [FormerlySerializedAs("_grabWeight")]
        [SerializeField, Range(1, 3)] private int _requiredHarpoonTier = 1;

        [Tooltip("파지에서 놓였을 때 잠깐 기절하는 시간(초) — 추격 재개 전의 이탈 틈. 0 = 기절 없음.")]
        [SerializeField, Min(0f)] private float _stunDurationSeconds = 2.5f;

        [Header("동기화 (§6.2 — 10~15Hz + 보간)")]
        [SerializeField, Range(5f, 15f)] private float _syncHz = 12f;

        [Tooltip("견인 중 위치 동기화 주기 — 별도 채널 없이 기존 스냅샷 채널의 주기만 올린다.")]
        [SerializeField, Range(10f, 60f)] private float _towSyncHz = 30f;

        [SerializeField, Min(0.05f)] private float _interpolationDelaySeconds = 0.18f;

        [Header("회수")]
        [SerializeField, Min(10f)] private float _despawnBehindMeters = 60f;

        public string DisplayName => _displayName;

        /// <summary>이 Day부터 등장 — 기획서 §5의 "Day가 지날수록 새로운 행동 추가".</summary>
        public int MinDayToAppear => _minDayToAppear;

        public float SpawnWeight => _spawnWeight;

        /// <summary>변종 몸통 색 (M5 8차) — 기절 노란색이 위를 덮고, 해제 시 이 색으로 복원된다.</summary>
        public Color VariantColor => _variantColor;

        /// <summary>표현 스케일 배율 (M5 8차) — 판정·충돌 무변, 몸집으로도 변종이 구분되게 한다.</summary>
        public float VisualScale => _visualScale;

        public float MoveSpeed => _moveSpeed;

        /// <summary>스크롤 속도 대비 최소 초과 속도 — 추격이 항상 성립하게 하는 하한 (네트워크 문서 §4.3).</summary>
        public float ChaseSpeedMargin => _chaseSpeedMargin;

        public float AvoidProbeDistance => _avoidProbeDistance;

        /// <summary>열차 측면에서 이 수평 거리 안이면 갑판 도약을 시작한다.</summary>
        public float LeapHorizontalRange => _leapHorizontalRange;

        /// <summary>물에서만 살며 도약으로만 공격하는가 (ㄴ 물고기 점프).</summary>
        public bool AquaticLeaper => _aquaticLeaper;

        /// <summary>튀어오를 때 정점의 월드 높이 (m). <see cref="AquaticLeaper"/>일 때만 쓴다.</summary>
        public float LeapApexY => _leapApexY;

        /// <summary>하늘에서 급강하로 공격하는가 (ㄷ 별들린 바닷새).</summary>
        public bool AerialDiver => _aerialDiver;

        /// <summary>순항 고도 (월드 y). <see cref="AerialDiver"/>일 때만 쓴다.</summary>
        public float CruiseAltitudeY => _cruiseAltitudeY;

        /// <summary>급강하 속도 (m/s).</summary>
        public float DiveSpeed => _diveSpeed;

        /// <summary>상승 속도 (m/s) — 강하보다 느려야 반격 창이 열린다.</summary>
        public float ClimbSpeed => _diveSpeed * 0.65f;

        /// <summary>표적 높이에서 머무르는 시간(초).</summary>
        public float HoverSeconds => _hoverSeconds;

        public float MaxHealth => _maxHealth;

        public float AttackDamage => _attackDamage;

        public float AttackRange => _attackRange;

        public float AttackInterval => _attackInterval;

        /// <summary>
        /// 요구 집게 등급 (M5 5차) — 그랩 검증의 대상 등급 인자.
        /// <b>기획서 §3.1은 몬스터를 2단계부터로 확정했지만 에셋 값은 아직 현행(일반형·돌진형 1)이다</b> —
        /// 변종 체계를 다시 설계할 예정이라 의도적으로 미룬 편차다 (집게 단계별 파지 계획 §1.2).
        /// 재설계 차수에는 변종별 값만 여기 대입하면 된다 — 코드 변경은 이미 끝나 있다.
        /// </summary>
        public int RequiredHarpoonTier => _requiredHarpoonTier;

        /// <summary>파지에서 놓였을 때의 기절 지속 시간 (초) — 추격 재개 전의 이탈 틈.</summary>
        public float StunDurationSeconds => _stunDurationSeconds;

        public float SyncHz => _syncHz;

        /// <summary>견인 중 위치 동기화 주기 (Hz) — 표시 매끄러움만 담당, 판정은 서버 값 그대로다.</summary>
        public float TowSyncHz => _towSyncHz;

        public float InterpolationDelaySeconds => _interpolationDelaySeconds;

        /// <summary>열차 후미에서 이만큼 뒤처지면 회수(도주 처리)한다.</summary>
        public float DespawnBehindMeters => _despawnBehindMeters;
    }
}
