using Game.Gameplay.Inventory;

namespace Game.Gameplay.Combat
{
    // WeaponFiredLocalEvent는 M5 8차에서 제거 — 구독자가 끝내 생기지 않은 죽은 이벤트였고,
    // 발사·스윙 연출은 각 컨트롤러가 풀링 코스메틱을 직접 재생한다 (죽은 축을 남기지 않는다).

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
