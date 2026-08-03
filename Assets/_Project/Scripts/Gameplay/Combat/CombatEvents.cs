namespace Game.Gameplay.Combat
{
    /// <summary>로컬 표현 이벤트 — 리볼버 발사 연출 (발사음·총구 화염). 입력 즉시 발행 (지연 0).</summary>
    public readonly struct RevolverFiredLocalEvent
    {
        /// <summary>로컬 레이캐스트가 피격 대상을 맞혔는가 (히트 마커용, 판정 확정 아님).</summary>
        public readonly bool HitDamageable;

        public RevolverFiredLocalEvent(bool hitDamageable)
        {
            HitDamageable = hitDamageable;
        }
    }

    /// <summary>로컬 표현 이벤트 — 자기 리볼버 장탄 상태 변경. HUD 탄약 표시용.</summary>
    public readonly struct RevolverAmmoChangedLocalEvent
    {
        public readonly int RoundsLoaded;

        public readonly int Capacity;

        public readonly bool IsReloading;

        /// <summary>인벤토리의 예비 탄약 수 (M5 — 탄약 스택).</summary>
        public readonly int ReserveRounds;

        public RevolverAmmoChangedLocalEvent(int roundsLoaded, int capacity, bool isReloading, int reserveRounds)
        {
            RoundsLoaded = roundsLoaded;
            Capacity = capacity;
            IsReloading = isReloading;
            ReserveRounds = reserveRounds;
        }
    }
}
