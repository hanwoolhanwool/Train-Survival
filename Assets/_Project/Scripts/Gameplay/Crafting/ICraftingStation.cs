namespace Game.Gameplay.Crafting
{
    /// <summary>
    /// 제작 지점 계약 — UI(제작 창)는 구체 타입이 아니라 이 인터페이스로 조회한다 (DIP).
    /// 제작 확정은 호스트 권위: RequestCraft는 요청일 뿐, 차감·지급은 서버가 원자적으로 확정한다.
    /// </summary>
    public interface ICraftingStation
    {
        /// <summary>제공하는 레시피 수.</summary>
        int RecipeCount { get; }

        /// <summary>로컬 플레이어가 상호작용 범위 안에 있는가.</summary>
        bool IsLocalPlayerInRange { get; }

        /// <summary>인덱스의 레시피. 범위 밖이면 null.</summary>
        CraftingRecipe GetRecipe(int index);

        /// <summary>제작 요청 — 소유자 로컬에서 호출하면 서버가 검증 후 확정한다.</summary>
        void RequestCraft(int recipeIndex);
    }
}
