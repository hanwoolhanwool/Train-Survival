using UnityEngine;

namespace Game.UI.Ready
{
    /// <summary>
    /// 준비 화면 두 패널의 자리 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §0 · §4.2.
    ///
    /// <para><b>여기 적힌 픽셀 값은 전부 실측이다.</b> 소스 그림
    /// <c>T_Ready_Roster</c>(1012×1292)·<c>T_Ready_Controls</c>(904×1388)에서 슬롯과 버튼의
    /// 경계를 재서 정규화했고, 화면 안 배치는 시안 <c>게임 준비.png</c>(1672×941)에 두 패널을
    /// 그래디언트 NCC로 맞춰 얻었다(§14 ③).</para>
    ///
    /// <para><see cref="MenuPlateLayout"/>과 같은 방식이되 <b>출처 그림이 달라 클래스를 나눴다.</b>
    /// 배너는 3/4 뷰 원근이라 명판 높이가 아래로 갈수록 줄었지만, 이 패널들은 정면 뷰라
    /// 슬롯이 사실상 균등하다. 그래도 <c>VerticalLayoutGroup</c>을 쓰지 않는다 — 프레임 그림 위에
    /// 슬롯을 겹치는 구조라, 5 px만 어긋나도 테두리가 이중으로 보인다.</para>
    ///
    /// <para>좌표계는 두 겹이다. <b>패널 안</b>은 패널 좌상단 원점 0~1이고, <b>화면 안</b>은
    /// 높이 기준 배율 + 좌·우변으로부터의 중심 거리다. 폭 기준으로 잡으면 21:9에서 패널이
    /// 화면을 삼킨다(<c>MenuPlateLayout.BannerSize</c>가 이미 이 규칙을 쓴다).</para>
    /// </summary>
    internal static class ReadyPanelLayout
    {
        // ── 로스터 패널 (왼쪽) ──────────────────────────────────────────

        /// <summary>로스터 소스 그림의 폭 (px) — 아래 정규화 값들의 출처.</summary>
        public const int RosterSourceWidth = 1012;

        /// <summary>로스터 소스 그림의 높이 (px).</summary>
        public const int RosterSourceHeight = 1292;

        /// <summary>로스터 높이 ÷ 화면 높이. 1 미만이라 위아래가 잘리지 않는다.</summary>
        public const float RosterHeightScale = 0.89267f;

        /// <summary>로스터 그림의 가로÷세로. <b>이 비를 지켜야 21:9에서 패널이 늘어나지 않는다.</b></summary>
        public const float RosterAspect = (float)RosterSourceWidth / RosterSourceHeight;

        /// <summary>로스터 중심의 가로 위치 — <b>화면 폭이 아니라 패널 폭</b>에 비례한다(화면 좌변 기준).</summary>
        public const float RosterCenterXInWidths = 0.55623f;

        /// <summary>로스터 중심의 세로 위치 (화면 높이 대비, 아래 원점).</summary>
        public const float RosterCenterY = 0.51647f;

        // ── 슬롯 (로스터 정규화, 위 원점) ──────────────────────────────

        /// <summary>슬롯 개수 — 패널 그림이 4장 고정이고 Steam 로비도 친구 전용 4인이다.</summary>
        public const int SlotCount = 4;

        /// <summary>슬롯 좌변 (원본 x=100 px) — 프레임 기둥 안쪽, 슬롯 테두리가 시작하는 자리다.</summary>
        public const float SlotLeft = 100f / RosterSourceWidth;

        /// <summary>슬롯 우변 (원본 x=922 px).</summary>
        public const float SlotRight = 922f / RosterSourceWidth;

        private static readonly float[] SlotTopPx = { 324f, 548f, 784f, 1016f };
        private static readonly float[] SlotBottomPx = { 529f, 765f, 996f, 1226f };

        // ── 슬롯 안 요소 (슬롯 정규화) ─────────────────────────────────
        //
        // 슬롯 1(금테)과 슬롯 2(빈 판)에서 각각 재고 평균했다. 슬롯 높이가 205~217 px로
        // 5 % 흔들리는데, 그 차이는 정규화하면 묻힌다.
        //
        // 아래 인자는 실측 그대로 **위 원점**이고, SlotRect가 앵커(아래 원점)로 뒤집는다.

        /// <summary>아이콘 자리 — 왕관(호스트) 또는 사람+(빈자리).</summary>
        public static Rect SlotIcon => SlotRect(0.043f, 0.185f, 0.206f, 0.755f);

        /// <summary>"HOST" 자리 — 참가자가 있는 슬롯에서만 쓴다.</summary>
        public static Rect SlotRole => SlotRect(0.215f, 0.195f, 0.480f, 0.410f);

        /// <summary>
        /// 표시 이름 자리 — 역할 줄 아래.
        ///
        /// <para>접속 표시를 지우면서 <b>오른쪽으로 넓어졌다</b>(0.750 → 0.940). 예전에는 점과
        /// "접속 중"이 오른쪽 4분의 1을 차지하고 있었다.</para>
        /// </summary>
        public static Rect SlotName => SlotRect(0.226f, 0.498f, 0.940f, 0.800f);

        /// <summary>빈자리 안내 문구 자리 — 역할 줄이 없으므로 세로 중앙에 온다.</summary>
        public static Rect SlotEmptyLabel => SlotRect(0.286f, 0.378f, 0.760f, 0.636f);

        // 접속 표시(녹색 점 + "접속 중")는 **삭제됐다** (2026-08-22 사용자 지시).
        // 칸에 이름이 있다는 것이 곧 접속 중이라는 뜻이라, 같은 사실을 두 번 말하고 있었다.
        // 이름 칸이 그만큼 넓어져 긴 이름도 잘리지 않는다.

        /// <summary>
        /// 타이틀 자리 — 그림의 타이틀 판 안쪽 전체다.
        ///
        /// <para>계획 §5.1-5는 구워진 "게임 준비" 각인을 <b>남기고</b> 그 위에 TMP를 겹치기로 했지만,
        /// 1차 렌더 검증에서 두 글꼴이 달라 <b>이중으로 보였다.</b> 각인을 지우고 TMP만 남기는 쪽으로
        /// 뒤집었고(§14 ③), 그래서 이 사각형은 각인 크기가 아니라 <b>판 안쪽</b>을 가리킨다.</para>
        ///
        /// <para><b>2026-08-22에 다시 쟀다.</b> 원본 세로 밝기 프로파일에서 황동 테두리 안쪽이
        /// y 136~280이었는데 예전 값(152~278)은 그보다 <b>위아래로 좁았고</b>, 그 좁은 칸에
        /// 95 px 글자를 넣어 두어 <b>글자가 판 테두리를 넘어 나갔다</b>(사용자 지적).
        /// 지금은 판 안쪽에 맞추고 글자는 자동 크기로 담는다 — 칸을 넓히고 글자를 줄인 셈이다.</para>
        /// </summary>
        public static Rect RosterTitle => RosterRect(130f, 142f, 882f, 276f);

        // ── 조작 패널 (오른쪽) ──────────────────────────────────────────

        /// <summary>조작 패널 소스 그림의 폭 (px).</summary>
        public const int ControlsSourceWidth = 904;

        /// <summary>조작 패널 소스 그림의 높이 (px).</summary>
        public const int ControlsSourceHeight = 1388;

        /// <summary>조작 패널 높이 ÷ 화면 높이.</summary>
        public const float ControlsHeightScale = 0.58236f;

        /// <summary>조작 패널 그림의 가로÷세로.</summary>
        public const float ControlsAspect = (float)ControlsSourceWidth / ControlsSourceHeight;

        /// <summary>조작 패널 중심의 가로 위치 — 패널 폭에 비례한다(화면 <b>우변</b> 기준).</summary>
        public const float ControlsCenterXInWidths = 0.62045f;

        /// <summary>조작 패널 중심의 세로 위치 (화면 높이 대비, 아래 원점).</summary>
        public const float ControlsCenterY = 0.37726f;

        // ── 조작 패널 안 요소 (조작 패널 정규화) ───────────────────────
        //
        // 아래 값들은 크롭한 스프라이트의 원본 자리와 같다 — 프레임 위 같은 자리에 겹쳐야
        // 잘라낸 흔적이 드러나지 않는다(§5.1-4의 방식을 버튼에도 그대로 쓴다).

        /// <summary>"난이도" 라벨 자리 — 각인은 지웠다(§14 ③). 스테퍼 위 빈 띠 전체를 쓴다.</summary>
        public static Rect DifficultyLabel => ControlsRect(300f, 88f, 610f, 192f);

        /// <summary>난이도 감소 버튼 (◀).</summary>
        public static Rect DifficultyPrev => ControlsRect(92f, 197f, 240f, 369f);

        /// <summary>난이도 값 박스 — 이 안에 현재 단계 이름이 온다.</summary>
        public static Rect DifficultyValue => ControlsRect(241f, 197f, 665f, 369f);

        /// <summary>난이도 증가 버튼 (▶).</summary>
        public static Rect DifficultyNext => ControlsRect(668f, 197f, 808f, 369f);

        /// <summary>게임 시작 — 화면에서 시각적 최상위다.</summary>
        public static Rect StartButton => ControlsRect(84f, 410f, 816f, 786f);

        /// <summary>초대 하기.</summary>
        public static Rect InviteButton => ControlsRect(84f, 840f, 816f, 996f);

        /// <summary>나가기.</summary>
        public static Rect LeaveButton => ControlsRect(84f, 1049f, 816f, 1257f);

        // ── 슬롯 좌표 ─────────────────────────────────────────────────

        /// <summary>슬롯의 윗변 (로스터 정규화, 위 원점).</summary>
        public static float SlotTop(int slot)
        {
            return SlotTopPx[Mathf.Clamp(slot, 0, SlotCount - 1)] / RosterSourceHeight;
        }

        /// <summary>슬롯의 밑변 (로스터 정규화, 위 원점).</summary>
        public static float SlotBottom(int slot)
        {
            return SlotBottomPx[Mathf.Clamp(slot, 0, SlotCount - 1)] / RosterSourceHeight;
        }

        /// <summary>슬롯 높이 (로스터 정규화).</summary>
        public static float SlotHeight(int slot)
        {
            return SlotBottom(slot) - SlotTop(slot);
        }

        /// <summary>
        /// 슬롯의 <c>anchorMin</c> — 유니티는 <b>아래가 원점</b>이라 y를 뒤집는다.
        /// 로스터 RectTransform을 부모로 두고 이 값을 넣으면 패널이 어떤 크기여도 그림 위에 얹힌다.
        /// </summary>
        public static Vector2 SlotAnchorMin(int slot)
        {
            return new Vector2(SlotLeft, 1f - SlotBottom(slot));
        }

        /// <summary>슬롯의 <c>anchorMax</c> (아래 원점).</summary>
        public static Vector2 SlotAnchorMax(int slot)
        {
            return new Vector2(SlotRight, 1f - SlotTop(slot));
        }

        // ── 화면 안 배치 ──────────────────────────────────────────────

        /// <summary>
        /// 캔버스 rect에서 로스터 RectTransform의 크기를 낸다 — <b>높이로 폭을 정한다.</b>
        /// </summary>
        public static Vector2 RosterSize(Vector2 canvasSize)
        {
            return PanelSize(canvasSize, RosterHeightScale, RosterAspect);
        }

        /// <summary>로스터 앵커 — 화면 <b>좌변</b>에 고정한다.</summary>
        public static Vector2 RosterAnchor()
        {
            return new Vector2(0f, RosterCenterY);
        }

        /// <summary>로스터의 <c>anchoredPosition</c> — 앵커가 좌변에 있는 전제다.</summary>
        public static Vector2 RosterPosition(Vector2 canvasSize)
        {
            return new Vector2(RosterSize(canvasSize).x * RosterCenterXInWidths, 0f);
        }

        /// <summary>조작 패널 크기.</summary>
        public static Vector2 ControlsSize(Vector2 canvasSize)
        {
            return PanelSize(canvasSize, ControlsHeightScale, ControlsAspect);
        }

        /// <summary>조작 패널 앵커 — 화면 <b>우변</b>에 고정한다.</summary>
        public static Vector2 ControlsAnchor()
        {
            return new Vector2(1f, ControlsCenterY);
        }

        /// <summary>조작 패널의 <c>anchoredPosition</c> — 우변에서 왼쪽으로 물러난다(음수).</summary>
        public static Vector2 ControlsPosition(Vector2 canvasSize)
        {
            return new Vector2(-ControlsSize(canvasSize).x * ControlsCenterXInWidths, 0f);
        }

        private static Vector2 PanelSize(Vector2 canvasSize, float heightScale, float aspect)
        {
            if (canvasSize.y <= 0f)
            {
                return Vector2.zero;
            }

            float height = canvasSize.y * heightScale;
            return new Vector2(height * aspect, height);
        }

        /// <summary>슬롯 안 실측(0~1, 위 원점)을 앵커 사각형(아래 원점)으로 옮긴다.</summary>
        private static Rect SlotRect(float left, float top, float right, float bottom)
        {
            return Rect.MinMaxRect(left, 1f - bottom, right, 1f - top);
        }

        private static Rect RosterRect(float leftPx, float topPx, float rightPx, float bottomPx)
        {
            return ToAnchorRect(leftPx, topPx, rightPx, bottomPx, RosterSourceWidth, RosterSourceHeight);
        }

        private static Rect ControlsRect(float leftPx, float topPx, float rightPx, float bottomPx)
        {
            return ToAnchorRect(leftPx, topPx, rightPx, bottomPx, ControlsSourceWidth, ControlsSourceHeight);
        }

        /// <summary>실측 픽셀(위 원점)을 앵커 사각형(아래 원점)으로 옮긴다.</summary>
        private static Rect ToAnchorRect(float leftPx, float topPx, float rightPx, float bottomPx, int width, int height)
        {
            return Rect.MinMaxRect(leftPx / width, 1f - bottomPx / height, rightPx / width, 1f - topPx / height);
        }
    }
}
