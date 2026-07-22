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
        [Header("이동 (커스텀 조향 — NavMesh 불사용)")]
        [SerializeField, Min(0.5f)] private float _moveSpeed = 8.5f;
        [SerializeField, Min(0f)] private float _chaseSpeedMargin = 1.5f;
        [SerializeField, Min(0.5f)] private float _avoidProbeDistance = 3f;
        [SerializeField, Min(0.1f)] private float _leapHorizontalRange = 3f;

        [Header("전투")]
        [SerializeField, Min(1f)] private float _maxHealth = 100f;
        [SerializeField, Min(0f)] private float _attackDamage = 15f;
        [SerializeField, Min(0.5f)] private float _attackRange = 2.2f;
        [SerializeField, Min(0.1f)] private float _attackInterval = 1.4f;

        [Header("동기화 (§6.2 — 10~15Hz + 보간)")]
        [SerializeField, Range(5f, 15f)] private float _syncHz = 12f;
        [SerializeField, Min(0.05f)] private float _interpolationDelaySeconds = 0.18f;

        [Header("회수")]
        [SerializeField, Min(10f)] private float _despawnBehindMeters = 60f;

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

        public float SyncHz => _syncHz;

        public float InterpolationDelaySeconds => _interpolationDelaySeconds;

        /// <summary>열차 후미에서 이만큼 뒤처지면 회수(도주 처리)한다.</summary>
        public float DespawnBehindMeters => _despawnBehindMeters;
    }
}
