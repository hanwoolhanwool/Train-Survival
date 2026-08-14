using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 창 배치의 공유 치수 (M7 3차 검증 W3-b) — 인벤토리 창과 제작 창이 <b>나란히</b> 떠야 하므로
    /// 두 뷰가 같은 계산을 봐야 한다. 각자 상수를 들고 있으면 한쪽만 바뀌었을 때 겹친다.
    ///
    /// <para>인벤토리는 화면 중앙, 제작 창은 <b>그 왼쪽에 붙는다</b>. 좁은 화면에서 제작 창이
    /// 화면 밖으로 나가지 않도록 왼쪽 여백에서 멈춘다(겹치더라도 잘려 보이지는 않게).</para>
    /// </summary>
    internal static class HudLayout
    {
        /// <summary>인벤토리 슬롯 한 변 (px).</summary>
        public const float SlotSize = 64f;

        /// <summary>슬롯 사이 간격 (px).</summary>
        public const float SlotGap = 6f;

        /// <summary>인벤토리 격자 열 수 — 핫바 5칸과 같은 폭을 유지한다.</summary>
        public const int Columns = 5;

        /// <summary>창과 창 사이 간격 (px).</summary>
        public const float PanelGap = 12f;

        /// <summary>화면 가장자리 최소 여백 (px).</summary>
        public const float ScreenMargin = 12f;

        /// <summary>슬롯 격자 폭.</summary>
        public static float GridWidth => Columns * SlotSize + (Columns - 1) * SlotGap;

        /// <summary>인벤토리 창 폭 (격자 + 좌우 여백).</summary>
        public static float InventoryPanelWidth => GridWidth + 40f;

        /// <summary>인벤토리 창의 좌측 x — 화면 중앙 정렬.</summary>
        public static float InventoryPanelX => (Screen.width - InventoryPanelWidth) * 0.5f;

        /// <summary>제작 창의 좌측 x — 인벤토리 왼쪽에 붙되 화면 밖으로 나가지 않는다.</summary>
        public static float CraftingPanelX(float craftingWidth)
        {
            return Mathf.Max(ScreenMargin, InventoryPanelX - craftingWidth - PanelGap);
        }

        /// <summary>창의 상단 y — 화면 세로 중앙 정렬 (인벤토리와 제작 창이 같은 눈높이에 온다).</summary>
        public static float CenteredY(float panelHeight)
        {
            return Mathf.Max(ScreenMargin, (Screen.height - panelHeight) * 0.5f);
        }
    }
}
