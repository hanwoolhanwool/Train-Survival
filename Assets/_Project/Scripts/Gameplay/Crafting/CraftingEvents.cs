namespace Game.Gameplay.Crafting
{
    /// <summary>로컬 표현 이벤트 — 제작대 상호작용 안내("E — 제작") 표시 여부.</summary>
    public readonly struct CraftingPromptLocalEvent
    {
        public readonly bool InRange;

        public CraftingPromptLocalEvent(bool inRange)
        {
            InRange = inRange;
        }
    }

    /// <summary>
    /// 로컬 표현 이벤트 — 제작 창 토글. I 창과 동일하게 열려 있는 동안
    /// 시점 회전·무기 입력이 정지되고 커서가 표시된다.
    /// </summary>
    public readonly struct CraftingPanelToggledLocalEvent
    {
        public readonly bool IsOpen;

        public CraftingPanelToggledLocalEvent(bool isOpen)
        {
            IsOpen = isOpen;
        }
    }
}
