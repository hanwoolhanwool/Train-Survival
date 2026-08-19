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

        // ── 상시·임계 상태 기둥 (비주얼·UI/UX 가이드 §9.2 A·B계층) ──────────
        //
        // 좌하단에 둔다. 1인칭이라 화면 하단 중앙은 손·무기가 차지하고, 상단 중앙은 배너 자리다.
        // 왼쪽 아래 모서리만이 주변 시야로 읽히면서 아무것도 가리지 않는 자리다 (§9.1).

        /// <summary>상태 기둥의 폭 (px).</summary>
        public const float StatusColumnWidth = 360f;

        /// <summary>상태 기둥의 최대 높이 — 실제 줄 수는 계층 판정에 따라 이보다 적다.</summary>
        public const float StatusColumnHeight = 260f;

        /// <summary>
        /// 상태 기둥 영역. 아래 모서리에 붙이고, 내용은 <c>GUILayout.FlexibleSpace()</c>로
        /// 바닥 정렬한다 — 줄이 늘고 줄어도 <b>맨 아래 줄의 위치가 변하지 않는다</b>.
        /// </summary>
        public static Rect StatusColumnRect()
        {
            return new Rect(
                ScreenMargin,
                Screen.height - StatusColumnHeight - ScreenMargin,
                StatusColumnWidth,
                StatusColumnHeight);
        }

        // ── 사건 배너 (§9.2 D계층) ────────────────────────────────────────

        /// <summary>배너 한 줄의 높이 (px).</summary>
        public const float BannerHeight = 30f;

        /// <summary>배너 줄 사이 간격 (px).</summary>
        public const float BannerGap = 6f;

        /// <summary>배너 폭 (px).</summary>
        public const float BannerWidth = 520f;

        /// <summary>첫 배너의 상단 y — 화면 높이 비율. 조준점보다 충분히 위에 둔다.</summary>
        public const float BannerTopRatio = 0.13f;

        /// <summary>
        /// <paramref name="index"/>번째 배너 자리 (0이 맨 위). 자리는 <b>종류가 아니라 순서</b>로
        /// 배분된다 — 하나만 떠도 첫 줄부터 채워져 화면 가운데가 비지 않는다
        /// (<see cref="HudBannerQueue"/> 참조).
        /// </summary>
        public static Rect BannerSlotRect(int index)
        {
            return new Rect(
                (Screen.width - BannerWidth) * 0.5f,
                Screen.height * BannerTopRatio + index * (BannerHeight + BannerGap),
                BannerWidth,
                BannerHeight);
        }
    }
}
