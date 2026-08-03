using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 리볼버 밸런스 데이터 (기획서 §6.2 — 기본 지급 개인 화기).
    /// 예비 탄약은 인벤토리의 탄약 스택(M5 제작품) — 재장전이 호스트 확정으로 차감한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RevolverSettings", menuName = "Game/Revolver Settings")]
    public sealed class RevolverSettings : ScriptableObject
    {
        [Header("판정")]
        [SerializeField, Min(1f)] private float _damage = 34f;
        [SerializeField, Min(1f)] private float _maxRange = 45f;
        [SerializeField, Min(0f)] private float _rangeTolerance = 5f;

        [Header("실린더")]
        [SerializeField, Min(1)] private int _cylinderCapacity = 6;
        [SerializeField, Min(0.05f)] private float _fireInterval = 0.4f;
        [SerializeField, Min(0.1f)] private float _reloadDuration = 2.2f;

        [Header("탄약 (기획서 §6.2 — 탄약 3종 이내)")]
        [Tooltip("재장전이 소모하는 예비 탄약 종류 — 무기 확장 차수의 샷건·라이플도 이 필드로 같은 파이프라인을 탄다.")]
        [SerializeField] private ResourceType _ammoType = ResourceType.RevolverAmmo;

        public float Damage => _damage;

        public float MaxRange => _maxRange;

        /// <summary>호스트 명중 검증의 거리 허용 오차 (지연 중 이동 보상).</summary>
        public float RangeTolerance => _rangeTolerance;

        public int CylinderCapacity => _cylinderCapacity;

        public float FireInterval => _fireInterval;

        public float ReloadDuration => _reloadDuration;

        public ResourceType AmmoType => _ammoType;
    }
}
