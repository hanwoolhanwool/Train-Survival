using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>1단계 기본 집게 밸런스 데이터 (슬라이스 스펙 §2.2~§2.4 초기값).</summary>
    [CreateAssetMenu(fileName = "HarpoonSettings", menuName = "Game/Harpoon Settings")]
    public sealed class HarpoonSettings : ScriptableObject
    {
        [Header("발사 (§2.2)")]
        [SerializeField, Min(1f)] private float _maxRange = 20f;
        [SerializeField, Min(1f)] private float _projectileSpeed = 40f;
        [SerializeField, Min(0.01f)] private float _projectileRadius = 0.15f;

        [Header("릴 (§2.2~§2.3)")]
        [SerializeField, Min(0.1f)] private float _reelSpeed = 8f;
        [SerializeField, Min(0f)] private float _fireCooldown = 0.5f;
        [SerializeField, Min(0f)] private float _missRecoveryDuration = 2.5f;
        [SerializeField, Min(0.1f)] private float _arriveRadius = 1.2f;

        [Header("호스트 검증 (§2.4)")]
        [SerializeField, Min(0f)] private float _rangeTolerance = 2f;

        [Header("견인 표시 보간 (§2.4 — 30 Hz 스냅샷 사이를 짧은 버퍼로 메움)")]
        [SerializeField, Min(1f)] private float _towInterpolationRate = 20f;

        [Header("실패 연출 — 빗나감·거부 시 총구로 되돌아옴")]
        [SerializeField, Min(0.1f)] private float _retractSpeed = 14f;
        [SerializeField, Min(0f)] private float _impactPauseDuration = 0.12f;
        [SerializeField, Min(0.1f)] private float _waitingForServerTimeout = 1.5f;

        public float MaxRange => _maxRange;

        public float ProjectileSpeed => _projectileSpeed;

        public float ProjectileRadius => _projectileRadius;

        public float ReelSpeed => _reelSpeed;

        public float FireCooldown => _fireCooldown;

        public float MissRecoveryDuration => _missRecoveryDuration;

        /// <summary>이 거리 안으로 끌려오면 획득 완료로 처리한다.</summary>
        public float ArriveRadius => _arriveRadius;

        public float RangeTolerance => _rangeTolerance;

        public float TowInterpolationRate => _towInterpolationRate;

        /// <summary>실패 연출 시 총구로 되돌아가는 속도 (m/s).</summary>
        public float RetractSpeed => _retractSpeed;

        /// <summary>빗나감 직후 되감기 전 잠깐 정지하는 시간 (피격 연출).</summary>
        public float ImpactPauseDuration => _impactPauseDuration;

        /// <summary>호스트 확정 대기 안전 타임아웃 — 응답이 끝내 오지 않으면 되감기로 폴백.</summary>
        public float WaitingForServerTimeout => _waitingForServerTimeout;
    }
}
