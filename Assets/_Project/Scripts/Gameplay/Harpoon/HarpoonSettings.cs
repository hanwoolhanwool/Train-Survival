using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 집게 밸런스 데이터 (슬라이스 스펙 §2.2~§2.4 초기값 + M5 5차 티어).
    /// 등급마다 달라지는 수치(사거리·릴 속도·미스 페널티·쿨다운·무게 상한)만 <see cref="Tier"/> 배열로
    /// 나누고, 등급과 무관한 공통 수치(투사체·보간·되감기·타임아웃)는 이 에셋 상단에 그대로 둔다.
    /// </summary>
    [CreateAssetMenu(fileName = "HarpoonSettings", menuName = "Game/Harpoon Settings")]
    public sealed class HarpoonSettings : ScriptableObject
    {
        /// <summary>집게 등급 상한 (기획서 §3.2 — 1~3단계).</summary>
        public const int MaxTier = 3;

        /// <summary>등급 하나의 수치 묶음 — 배열 인덱스 0 = 1단계.</summary>
        [System.Serializable]
        public sealed class Tier
        {
            [SerializeField, Min(1f)] private float _maxRange = 20f;
            [SerializeField, Min(0.1f)] private float _reelSpeed = 8f;
            [SerializeField, Min(0f)] private float _fireCooldown = 0.5f;
            [SerializeField, Min(0f)] private float _missRecoveryDuration = 2.5f;

            [Tooltip("이 등급으로 낚아챌 수 있는 최대 무게 등급 (대상의 GrabWeight 상한).")]
            [SerializeField, Range(1, MaxTier)] private int _grabWeightLimit = 1;

            public float MaxRange => _maxRange;

            public float ReelSpeed => _reelSpeed;

            public float FireCooldown => _fireCooldown;

            public float MissRecoveryDuration => _missRecoveryDuration;

            /// <summary>낚아챌 수 있는 대상 무게 상한 — 그랩 검증의 "집게 등급" 인자로 쓰인다.</summary>
            public int GrabWeightLimit => _grabWeightLimit;
        }

        [Header("등급 (§M5 5차 — 인덱스 0 = 1단계)")]
        [SerializeField] private Tier[] _tiers;

        [Header("발사 (§2.2 — 등급 공통)")]
        [SerializeField, Min(1f)] private float _projectileSpeed = 40f;
        [SerializeField, Min(0.01f)] private float _projectileRadius = 0.15f;

        [Header("릴 (§2.2~§2.3 — 등급 공통)")]
        [SerializeField, Min(0.1f)] private float _arriveRadius = 1.2f;

        [Header("호스트 검증 (§2.4)")]
        [SerializeField, Min(0f)] private float _rangeTolerance = 2f;

        [Header("견인 표시 보간 (§2.4 — 30 Hz 스냅샷 사이를 짧은 버퍼로 메움)")]
        [SerializeField, Min(1f)] private float _towInterpolationRate = 20f;

        [Header("실패 연출 — 빗나감·거부 시 총구로 되돌아옴")]
        [SerializeField, Min(0.1f)] private float _retractSpeed = 14f;
        [SerializeField, Min(0f)] private float _impactPauseDuration = 0.12f;
        [SerializeField, Min(0.1f)] private float _waitingForServerTimeout = 1.5f;

        private Tier _fallbackTier;

        /// <summary>등재된 등급 수 — 승급 상한 판정에 쓴다 (에셋이 비면 1단계만 있는 것으로 본다).</summary>
        public int TierCount => _tiers == null || _tiers.Length == 0 ? 1 : Mathf.Min(_tiers.Length, MaxTier);

        public float ProjectileSpeed => _projectileSpeed;

        public float ProjectileRadius => _projectileRadius;

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

        /// <summary>
        /// 등급(1~3)의 수치 묶음. 미등재 등급은 등재된 마지막 등급으로, 배열이 비면 기본값으로 폴백한다
        /// (에셋 미설정 상태에서도 1단계가 그대로 동작해야 한다).
        /// </summary>
        public Tier GetTier(int tier)
        {
            if (_tiers == null || _tiers.Length == 0)
            {
                return _fallbackTier ??= new Tier();
            }

            int index = Mathf.Clamp(tier - 1, 0, _tiers.Length - 1);
            return _tiers[index] ?? (_fallbackTier ??= new Tier());
        }
    }
}
