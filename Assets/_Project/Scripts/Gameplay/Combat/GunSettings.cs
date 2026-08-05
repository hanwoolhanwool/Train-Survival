using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 총기 공통 밸런스 데이터 (기획서 §6.2 무기 3축 — 리볼버·샷건·볼트액션이 이 한 종류의 에셋으로 성립).
    /// 무기 차이는 전부 데이터: 펠릿 수·산탄 각(샷건), 발사 간격(볼트액션 노리쇠), 탄약 종류.
    /// 예비 탄약은 인벤토리의 탄약 스택(제작품) — 재장전이 호스트 확정으로 차감한다.
    /// </summary>
    [CreateAssetMenu(fileName = "GunSettings", menuName = "Game/Gun Settings")]
    public sealed class GunSettings : ScriptableObject
    {
        [Header("무기 정체")]
        [Tooltip("이 총이 차지하는 핫바 아이템 종류 — 핫바 게이트·HUD 표시의 식별자.")]
        [SerializeField] private HotbarItemType _weaponItem = HotbarItemType.Revolver;

        [Tooltip("HUD 탄약 줄에 쓰는 표시명.")]
        [SerializeField] private string _displayName = "리볼버";

        [Header("판정")]
        [Tooltip("펠릿 1발당 데미지 — 총 데미지 = 펠릿당 데미지 × 명중 펠릿 수.")]
        [SerializeField, Min(1f)] private float _damage = 34f;
        [SerializeField, Min(1f)] private float _maxRange = 45f;
        [SerializeField, Min(0f)] private float _rangeTolerance = 5f;

        [Header("산탄 (샷건 — 리볼버·볼트액션은 1펠릿·0도)")]
        [Tooltip("한 발사가 쏘는 펠릿 수. 1이면 단발 정조준.")]
        [SerializeField, Min(1)] private int _pelletCount = 1;
        [Tooltip("산탄 원뿔 반각(도) — 펠릿이 이 각 안에 균일 분포한다.")]
        [SerializeField, Min(0f)] private float _spreadAngle;

        [Header("장탄")]
        [SerializeField, Min(1)] private int _magazineCapacity = 6;
        [SerializeField, Min(0.05f)] private float _fireInterval = 0.4f;
        [SerializeField, Min(0.1f)] private float _reloadDuration = 2.2f;

        [Header("탄약 (기획서 §6.2 — 탄약 3종 이내)")]
        [Tooltip("재장전이 소모하는 예비 탄약 종류.")]
        [SerializeField] private ResourceType _ammoType = ResourceType.RevolverAmmo;

        public HotbarItemType WeaponItem => _weaponItem;

        public string DisplayName => _displayName;

        /// <summary>펠릿 1발당 데미지.</summary>
        public float Damage => _damage;

        public float MaxRange => _maxRange;

        /// <summary>호스트 명중 검증의 거리 허용 오차 (지연 중 이동 보상).</summary>
        public float RangeTolerance => _rangeTolerance;

        public int PelletCount => _pelletCount;

        public float SpreadAngle => _spreadAngle;

        public int MagazineCapacity => _magazineCapacity;

        public float FireInterval => _fireInterval;

        public float ReloadDuration => _reloadDuration;

        public ResourceType AmmoType => _ammoType;
    }
}
