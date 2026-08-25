using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 타이포 크기와 해상도 배율 — [비주얼·UI/UX 가이드](docs/design/Train-Survival-비주얼-UIUX-가이드.md) §13.2.
    ///
    /// <para>가이드의 크기표는 <b>2560×1440 기준</b>이고 1080p를 최소 지원으로 본다. 이 클래스는
    /// 그 표의 대표값을 상수로 들고, 실제 화면 높이에 맞춰 배율을 곱한다.</para>
    ///
    /// <para><b>하한 14 px이 있는 이유</b>: 이 게임은 1인칭이고 주행 중 갑판이 계속 흔들린다.
    /// 정지 화면에서 읽히는 크기가 여기서는 읽히지 않으므로, 배율을 곱한 뒤에도
    /// <see cref="MinFontPx"/> 아래로는 내려가지 않는다.</para>
    ///
    /// <para><b>배치 치수는 여기 없다</b> — 창·슬롯·여백은 <see cref="HudLayout"/>이 담당한다.
    /// 이 클래스는 "글자가 몇 픽셀인가"만 안다.</para>
    /// </summary>
    internal static class UiMetrics
    {
        /// <summary>가이드 §13.2 크기표의 기준 화면 높이 (2560×1440).</summary>
        public const float ReferenceHeight = 1440f;

        /// <summary>흔들리는 화면에서의 판독 하한 (px). 배율을 곱한 뒤에도 이 아래로 내려가지 않는다.</summary>
        public const int MinFontPx = 14;

        /// <summary>배율 하한 — 720p(0.5)보다 작은 창에서 UI가 무한히 작아지지 않게 한다.</summary>
        public const float MinScale = 0.5f;

        /// <summary>배율 상한 — 4K(1.5)를 넘는 화면에서 글자가 과도하게 커지지 않게 한다.</summary>
        public const float MaxScale = 2f;

        // ── §13.2 대표값 (1440p 기준) ────────────────────────────────────
        //
        // 가이드는 범위로 적혀 있고(예: 26–32), 여기서는 그 중앙값을 대표값으로 고정한다.
        // 범위 안에서 조정할 일이 생기면 이 상수만 바꾼다 — 호출부는 이름으로만 참조한다.

        /// <summary>로고·타이틀 (가이드 160–240).</summary>
        public const int DisplayLogo = 200;

        /// <summary>메인 메뉴 버튼 (40–56).</summary>
        public const int MenuButton = 48;

        /// <summary>설정 화면 제목 (44–56).</summary>
        public const int SettingsTitle = 50;

        /// <summary>탭 (28–36).</summary>
        public const int Tab = 32;

        /// <summary>설정 항목 라벨 (26–32).</summary>
        public const int SettingsLabel = 28;

        /// <summary>드롭다운 값 (22–28).</summary>
        public const int DropdownValue = 24;

        /// <summary>HUD 숫자 (20–28).</summary>
        public const int HudNumber = 24;

        /// <summary>상호작용 프롬프트 (26–36).</summary>
        public const int ContextPrompt = 30;

        /// <summary>플레이어 이름표 (18–24) — 대표값 중 가장 작아 하한 판정의 기준이 된다.</summary>
        public const int Nameplate = 20;

        /// <summary>
        /// <b>IMGUI HUD가 실제로 요구하는</b> 1440p 기준 크기 목록 — 글리프 워밍업의 대상이다
        /// ([인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §5.4).
        ///
        /// <para><b>크기마다 아틀라스가 따로다.</b> 하나를 빠뜨리면 그 크기에서만 첫 그리기가
        /// 여전히 튄다 — 조용한 실패라 화면을 보고는 못 찾는다. 그래서 목록을 상수로 두고
        /// <c>UiWarmupTextTests</c>가 이 배열을 덮는지 검사한다.</para>
        ///
        /// <para><b>메뉴 크기는 여기 없다.</b> 로고·명판·설정은 TMP(uGUI)라 IMGUI 아틀라스를
        /// 건드리지 않는다. 200 px 글리프를 300자 굽는 것은 순수한 낭비다.</para>
        /// </summary>
        public static readonly int[] HudSizes1440 = { HudNumber, ContextPrompt, Nameplate };

        // ── 조회 ─────────────────────────────────────────────────────────

        /// <summary>현재 화면 기준 배율.</summary>
        public static float Scale => ScaleFor(Screen.height);

        /// <summary>현재 화면에서의 실제 글자 크기 (px).</summary>
        public static int Font(int size1440) => FontFor(size1440, Screen.height);

        /// <summary>
        /// 화면 높이 → 배율. <see cref="Screen"/>을 읽지 않는 순수 함수라 테스트에서 임의 해상도를 넣어볼 수 있다.
        /// </summary>
        public static float ScaleFor(float screenHeight)
        {
            if (screenHeight <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp(screenHeight / ReferenceHeight, MinScale, MaxScale);
        }

        /// <summary>1440p 기준 크기 → 해당 화면에서의 실제 크기 (px). 하한 <see cref="MinFontPx"/>가 적용된다.</summary>
        public static int FontFor(int size1440, float screenHeight)
        {
            int scaled = Mathf.RoundToInt(size1440 * ScaleFor(screenHeight));
            return Mathf.Max(MinFontPx, scaled);
        }
    }
}
