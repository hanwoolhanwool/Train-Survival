using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 근접 무기 밸런스 데이터 (기획서 §6.2 — 마체테: 탄약 0일 때의 최후 수단, 무한 사용이되 위험).
    /// 탄약·재장전이 없어 총기(<see cref="GunSettings"/>)와 데이터가 다르다 — 스윙 간격·리치·판정 굵기.
    /// </summary>
    [CreateAssetMenu(fileName = "MeleeSettings", menuName = "Game/Melee Settings")]
    public sealed class MeleeSettings : ScriptableObject
    {
        [Header("표시")]
        [SerializeField] private string _displayName = "마체테";

        [Header("판정")]
        [SerializeField, Min(1f)] private float _damage = 45f;

        [Tooltip("스윙 리치 — 근접 무기이므로 짧다.")]
        [SerializeField, Min(0.5f)] private float _maxRange = 2.6f;

        [Tooltip("스피어캐스트 반지름 — 근접 스윙은 정밀 조준을 요구하지 않는다.")]
        [SerializeField, Min(0.05f)] private float _hitRadius = 0.6f;

        [SerializeField, Min(0f)] private float _rangeTolerance = 3f;

        [Header("템포")]
        [SerializeField, Min(0.1f)] private float _swingInterval = 0.7f;

        [Header("연출 (M5 8차 — 표시 전용, 판정 무변)")]
        [Tooltip("타격 이펙트 프리팹 — 풀링 비네트워크. 비면 타격을 그리지 않는다.")]
        [SerializeField] private ImpactEffectView _impactEffectPrefab;

        public string DisplayName => _displayName;

        public float Damage => _damage;

        public float MaxRange => _maxRange;

        public float HitRadius => _hitRadius;

        /// <summary>호스트 명중 검증의 거리 허용 오차 (지연 중 이동 보상).</summary>
        public float RangeTolerance => _rangeTolerance;

        public float SwingInterval => _swingInterval;

        /// <summary>타격 이펙트 프리팹 (M5 8차 — 표시 전용).</summary>
        public ImpactEffectView ImpactEffectPrefab => _impactEffectPrefab;
    }
}
