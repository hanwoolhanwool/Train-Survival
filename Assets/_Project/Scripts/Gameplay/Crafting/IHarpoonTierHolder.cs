namespace Game.Gameplay.Crafting
{
    /// <summary>
    /// 집게 등급 보유자 계약 (M5 5차 — 제작대 승급). 제작 확정 경로가 "지금 몇 단계인가"를 묻고
    /// 승급을 확정할 때 쓰는 최소 표면이다. 제작이 <see cref="Game.Gameplay.Harpoon"/> 구현체를
    /// 직접 참조하지 않도록 계약을 제작 쪽에 두고, 집게가 이를 구현한다 (DIP).
    /// 확정은 호스트 권위 — <see cref="ServerSetTier"/>는 서버에서만 호출한다.
    /// </summary>
    public interface IHarpoonTierHolder
    {
        /// <summary>현재 집게 등급 (1부터). 복제 값이라 전 피어가 같은 값을 본다.</summary>
        int Tier { get; }

        /// <summary>승급 가능한 최대 등급 — 데이터(HarpoonSettings)에 등재된 등급 수.</summary>
        int MaxTier { get; }

        /// <summary>서버 전용 — 등급을 확정한다. 범위 밖이면 무시한다.</summary>
        void ServerSetTier(int tier);
    }
}
