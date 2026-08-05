using Game.Gameplay.Inventory;

namespace Game.Gameplay.Combat
{
    /// <summary>로컬 표현 이벤트 — 무기 발사·스윙 연출 (발사음·총구 화염). 입력 즉시 발행 (지연 0).</summary>
    public readonly struct WeaponFiredLocalEvent
    {
        /// <summary>발사한 무기의 핫바 아이템 종류.</summary>
        public readonly HotbarItemType Weapon;

        /// <summary>로컬 판정이 피격 대상을 맞혔는가 (히트 마커용, 판정 확정 아님).</summary>
        public readonly bool HitDamageable;

        public WeaponFiredLocalEvent(HotbarItemType weapon, bool hitDamageable)
        {
            Weapon = weapon;
            HitDamageable = hitDamageable;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 자기가 든 총의 장탄 상태 변경. HUD 탄약 표시용.
    /// 든(활성) 총만 발행한다 — HUD는 마지막 이벤트의 무기와 핫바 선택이 일치할 때만 그린다.
    /// </summary>
    public readonly struct WeaponAmmoChangedLocalEvent
    {
        /// <summary>이 탄약 상태의 주인 무기.</summary>
        public readonly HotbarItemType Weapon;

        /// <summary>HUD 표시명 (세팅 에셋의 DisplayName).</summary>
        public readonly string WeaponName;

        public readonly int RoundsLoaded;

        public readonly int Capacity;

        public readonly bool IsReloading;

        /// <summary>인벤토리의 예비 탄약 수 (탄약 스택).</summary>
        public readonly int ReserveRounds;

        public WeaponAmmoChangedLocalEvent(
            HotbarItemType weapon, string weaponName,
            int roundsLoaded, int capacity, bool isReloading, int reserveRounds)
        {
            Weapon = weapon;
            WeaponName = weaponName;
            RoundsLoaded = roundsLoaded;
            Capacity = capacity;
            IsReloading = isReloading;
            ReserveRounds = reserveRounds;
        }
    }
}
