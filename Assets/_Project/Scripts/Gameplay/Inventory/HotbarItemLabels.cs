namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 무기·도구 아이템의 표시명 — 인벤토리·제작 UI가 공유한다.
    /// 자원(<see cref="HotbarItemType.Resource"/>)의 표시명은 <see cref="ResourceCatalog"/> 소관.
    /// </summary>
    public static class HotbarItemLabels
    {
        /// <summary>
        /// 집게의 등급별 표시명 (M5 5차 승급) — 1단계는 기존과 같은 "집게", 상위는 "집게(2단계)".
        /// 핫바·제작 창이 이 하나를 공유해 표기가 갈리지 않는다.
        /// </summary>
        public static string GetHarpoonLabel(int tier)
        {
            return tier <= 1 ? "집게" : $"집게({tier}단계)";
        }

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
                case HotbarItemType.FishingRod:
                    return "낚싯대";

                case HotbarItemType.Melee:
                    return "마체테";
                case HotbarItemType.LeatherCoat:
                    return "가죽 옷";
                case HotbarItemType.DesertRobe:
                    return "사막 로브";
                case HotbarItemType.ScrapHelmet:
                    return "고철 투구";
                case HotbarItemType.PaddedPants:
                    return "누비 바지";
                case HotbarItemType.DesertBoots:
                    return "사막 장화";
                case HotbarItemType.FurHood:
                    return "모피 후드";
                case HotbarItemType.WinterParka:
                    return "방한 파카";
                case HotbarItemType.WinterPants:
                    return "방한 바지";
                case HotbarItemType.WinterBoots:
                    return "방한 부츠";
                case HotbarItemType.Bundle:
                    return "보따리\n[좌클릭 풀기]";
                default:
                    return string.Empty;
            }
        }
    }
}
