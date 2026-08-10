using UnityEngine;

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

        [Header("전투")]
        [SerializeField, Min(1f)] private float _maxHealth = 100f;
        [SerializeField, Min(0f)] private float _attackDamage = 15f;
        [SerializeField, Min(0.5f)] private float _attackRange = 2.2f;
        [SerializeField, Min(0.1f)] private float _attackInterval = 1.4f;

        [Header("집게 그랩·기절 (M5 5차 — 6차에서 파지로 재정의)")]
        [Tooltip("집게 무게 등급 — 이 값 이상의 집게 등급이어야 낚아챌 수 있다 (3 = 향후 대형 변종 자리).")]
        [SerializeField, Range(1, 3)] private int _grabWeight = 1;

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

        public float MaxHealth => _maxHealth;

        public float AttackDamage => _attackDamage;

        public float AttackRange => _attackRange;

        public float AttackInterval => _attackInterval;

        /// <summary>집게 무게 등급 (M5 5차) — 그랩 검증의 대상 무게 인자.</summary>
        public int GrabWeight => _grabWeight;

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
