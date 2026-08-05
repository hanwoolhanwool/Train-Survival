namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 무기·도구 아이템의 표시명 — 인벤토리·제작 UI가 공유한다.
    /// 자원(<see cref="HotbarItemType.Resource"/>)의 표시명은 <see cref="ResourceCatalog"/> 소관.
    /// </summary>
    public static class HotbarItemLabels
    {
        public static string GetLabel(HotbarItemType type)
        {
            switch (type)
            {
                case HotbarItemType.Harpoon:
                    return "집게";
                case HotbarItemType.Revolver:
                    return "리볼버";
                case HotbarItemType.Hammer:
                    return "망치";
                case HotbarItemType.Shotgun:
                    return "샷건";
                case HotbarItemType.Rifle:
                    return "볼트액션";
                case HotbarItemType.Melee:
                    return "마체테";
                default:
                    return string.Empty;
            }
        }
    }
}
