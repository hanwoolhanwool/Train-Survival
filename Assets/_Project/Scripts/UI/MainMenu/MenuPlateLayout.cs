using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 배너 스프라이트(<c>T_Menu_Banner</c>) 안에서 명판 4장이 차지하는 자리 —
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §3.2 · §4.2.
    ///
    /// <para><b>이 값들은 계산이 아니라 실측이다.</b> 원본 컨셉 아트 `Ver2.png`(941×1672)에서
    /// 명판의 위·아래 경계를 픽셀로 재서 정규화한 것이고, 여기 적힌 픽셀 값이 그 출처다.
    /// 배너 그림 자체가 원근이 걸린 3/4 뷰라 <b>명판 높이가 아래로 갈수록 줄어든다</b> —
    /// 균등 배치(<c>VerticalLayoutGroup</c>)로 만들면 그림과 어긋난다.</para>
    ///
    /// <para>좌표계는 <b>배너 좌상단 원점 · 0~1</b>이다. 화면 해상도도 배너 크기도 모르는 값이라
    /// 배너 RectTransform이 어떻게 놓이든 그대로 쓸 수 있고, 앵커로 바로 환산된다
    /// (<see cref="ToAnchorMin"/> · <see cref="ToAnchorMax"/>).</para>
    /// </summary>
    internal static class MenuPlateLayout
    {
        /// <summary>실측 원본의 크기 (px) — 아래 정규화 값들의 출처.</summary>
        public const int SourceWidth = 940;

        /// <summary>실측 원본의 높이 (px).</summary>
        public const int SourceHeight = 1672;

        /// <summary>명판 좌변 (원본 x=276 px) — 왼쪽 기둥 바깥이 아니라 <b>금색 테두리</b>가 시작하는 자리다.</summary>
        public const float PlateLeft = 276f / SourceWidth;

        /// <summary>명판 우변 (원본 x=716 px). 그 오른쪽은 화살표 자리라 명판에 포함하지 않는다.</summary>
        public const float PlateRight = 716f / SourceWidth;

        /// <summary>명판 개수 — 시안의 게임 시작·업적·설정·종료.</summary>
        public const int SlotCount = 4;

        /// <summary>슬롯별 위·아래 경계 (원본 px, 위 원점). 인덱스 0이 맨 위다.</summary>
        private static readonly float[] TopPx = { 661f, 808f, 946f, 1090f };
        private static readonly float[] BottomPx = { 777f, 920f, 1058f, 1200f };

        /// <summary>화살표 스프라이트의 원본 자리 (px) — 선택된 슬롯의 오른쪽에 붙는다.</summary>
        public const float ArrowLeft = 712f / SourceWidth;
        public const float ArrowRight = 812f / SourceWidth;
        private const float ArrowTopPx = 660f;
        private const float ArrowBottomPx = 776f;

        // ── 배너 전체 배치 ──────────────────────────────────────────────
        //
        // 시안(1672×941)의 타이틀판 "TRAIN SURVIVAL" 각인을 Ver2의 같은 각인에 겹쳐
        // 균등 배율 0.8694를 얻었고, 아래 세 값이 그 결과다. 배너는 화면보다 커서
        // 위·아래가 잘린다 — 시안이 의도한 모습이다(§3.2).

        /// <summary>배너 높이 ÷ 화면 높이. 1을 넘으므로 위아래가 잘린다.</summary>
        public const float BannerHeightScale = 1.54478f;

        /// <summary>배너 스프라이트의 가로÷세로. <b>이 비를 지켜야 21:9에서 배너가 늘어나지 않는다.</b></summary>
        public const float BannerAspect = (float)SourceWidth / SourceHeight;

        /// <summary>배너 중심의 가로 위치 — <b>화면 폭이 아니라 배너 폭</b>에 비례한다. 왼쪽 가장자리에 붙어 잘린다.</summary>
        public const float BannerCenterXInWidths = 0.41618f;

        /// <summary>배너 중심의 세로 위치 (화면 높이 대비, 아래 원점).</summary>
        public const float BannerCenterY = 0.45067f;

        /// <summary>
        /// 캔버스 rect에서 배너 RectTransform의 크기를 낸다 — <b>높이로 폭을 정한다.</b>
        /// 폭 기준으로 잡으면 화면이 넓어질 때 배너가 같이 커져 명판이 화면을 삼킨다.
        /// </summary>
        public static Vector2 BannerSize(Vector2 canvasSize)
        {
            if (canvasSize.y <= 0f)
            {
                return Vector2.zero;
            }

            float height = canvasSize.y * BannerHeightScale;
            return new Vector2(height * BannerAspect, height);
        }

        /// <summary>배너의 <c>anchoredPosition</c> — 앵커를 캔버스 좌변 <see cref="BannerCenterY"/> 지점에 둔 전제다.</summary>
        public static Vector2 BannerPosition(Vector2 canvasSize)
        {
            return new Vector2(BannerSize(canvasSize).x * BannerCenterXInWidths, 0f);
        }

        /// <summary>배너 앵커 (좌변에 고정 · 세로는 화면 비율). min과 max가 같은 점 앵커다.</summary>
        public static Vector2 BannerAnchor()
        {
            return new Vector2(0f, BannerCenterY);
        }

        /// <summary>슬롯의 윗변 (배너 정규화, 위 원점).</summary>
        public static float Top(int slot)
        {
            return TopPx[Mathf.Clamp(slot, 0, SlotCount - 1)] / SourceHeight;
        }

        /// <summary>슬롯의 밑변 (배너 정규화, 위 원점).</summary>
        public static float Bottom(int slot)
        {
            return BottomPx[Mathf.Clamp(slot, 0, SlotCount - 1)] / SourceHeight;
        }

        /// <summary>슬롯 높이 (배너 정규화). 아래 슬롯일수록 작다 — 원근이 걸려 있다.</summary>
        public static float Height(int slot)
        {
            return Bottom(slot) - Top(slot);
        }

        /// <summary>슬롯 세로 중심 (배너 정규화, 위 원점).</summary>
        public static float Center(int slot)
        {
            return (Top(slot) + Bottom(slot)) * 0.5f;
        }

        /// <summary>
        /// RectTransform <c>anchorMin</c> — 유니티는 <b>아래가 원점</b>이라 y를 뒤집는다.
        /// 배너 RectTransform을 부모로 두고 이 값을 넣으면 배너가 어떤 크기여도 명판 위에 얹힌다.
        /// </summary>
        public static Vector2 ToAnchorMin(int slot)
        {
            return new Vector2(PlateLeft, 1f - Bottom(slot));
        }

        /// <summary>RectTransform <c>anchorMax</c> (아래 원점).</summary>
        public static Vector2 ToAnchorMax(int slot)
        {
            return new Vector2(PlateRight, 1f - Top(slot));
        }

        /// <summary>화살표의 <c>anchorMin</c> — 세로는 지정한 슬롯에 맞추고 가로는 명판 오른쪽에 고정된다.</summary>
        public static Vector2 ArrowAnchorMin(int slot)
        {
            float half = (ArrowBottomPx - ArrowTopPx) / SourceHeight * 0.5f;
            return new Vector2(ArrowLeft, 1f - (Center(slot) + half));
        }

        /// <summary>화살표의 <c>anchorMax</c>.</summary>
        public static Vector2 ArrowAnchorMax(int slot)
        {
            float half = (ArrowBottomPx - ArrowTopPx) / SourceHeight * 0.5f;
            return new Vector2(ArrowRight, 1f - (Center(slot) - half));
        }
    }
}
