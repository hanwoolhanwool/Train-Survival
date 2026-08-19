using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 상태 4단계 — [비주얼·UI/UX 가이드](docs/design/Train-Survival-비주얼-UIUX-가이드.md) §7.2.
    /// <b>이 순서는 게임 전체에서 뒤집히지 않는다.</b> 같은 색이 어디서나 같은 뜻이어야
    /// 플레이어가 주변 시야만으로 상태를 읽을 수 있다.
    ///
    /// <para>단계를 판정하는 <b>임계값은 여기 없다</b> — 허기·갈증·체온·탄약은 각자 다른 기준으로
    /// 위험해진다. 판정은 각 도메인이 하고, 이 열거는 "그래서 무슨 색인가"만 담당한다.</para>
    /// </summary>
    internal enum UiStatusLevel
    {
        /// <summary>정상 범위. 가이드 §9.2의 A계층은 이 구간에서 화면 뒤로 물러난다.</summary>
        Safe = 0,

        /// <summary>임계 접근 — 곧 행동이 필요하다.</summary>
        Caution = 1,

        /// <summary>행동 필요.</summary>
        Alert = 2,

        /// <summary>즉시 대응.</summary>
        Critical = 3,
    }

    /// <summary>
    /// UI 색 토큰 — 가이드 §7.1(코어) · §7.2(상태 4단계). 화면마다 색을 새로 고르지 않기 위한 단일 출처다.
    ///
    /// <para><b>면(fill)과 텍스트 색을 분리하는 이유</b>: 어두운 패널 위에서 위험 적색
    /// <c>#B23A2E</c>는 바 채움으로는 강렬하지만 텍스트로는 대비 <b>2.9:1</b>로 읽히지 않는다
    /// (<see cref="Game.Tests.EditMode"/>의 <c>UiPaletteTests</c>가 이 수치를 고정한다).
    /// 따라서 <see cref="StatusFill"/>과 <see cref="StatusTextColor"/>는 같은 단계라도 다른 색을 준다.</para>
    ///
    /// <para><b>여기 없는 것</b>: 별씨·별들림·화실 불빛의 발광색(가이드 §7.3)은 <b>머티리얼 규격</b>이라
    /// UI 어셈블리가 아니라 셰이더·머티리얼이 소유한다. 지역 팔레트(§7.4)도 마찬가지다.</para>
    ///
    /// <para><b>값의 색 공간</b>: 전부 sRGB(감마) 기준이다. IMGUI의 <c>GUI.color</c>·리치텍스트가
    /// 그대로 받는 값이며, uGUI로 전환해도 같은 값을 쓴다.</para>
    ///
    /// <para>튜닝 대상이 되면(플레이 검증에서 색을 자주 바꾸게 되면) ScriptableObject로 승격한다.
    /// 지금은 상수인 편이 참조가 단순하고, 값이 바뀔 근거도 아직 없다.</para>
    /// </summary>
    internal static class UiPalette
    {
        // ── §7.1 코어 토큰 ────────────────────────────────────────────────

        /// <summary>패널·오버레이 바탕 (그을린 무쇠). 모든 대비 계산의 기준 배경이다.</summary>
        public static readonly Color PanelSoot = FromRgb(0x1F1B1A);

        /// <summary>경계선·구분선. 대비 1.7:1 — <b>선 전용</b>이며 텍스트로 쓰지 않는다.</summary>
        public static readonly Color PanelLine = FromRgb(0x4A423C);

        /// <summary>본문·컨트롤 텍스트. 바탕 위 14.3:1.</summary>
        public static readonly Color TextSteam = FromRgb(0xF2EAE0);

        /// <summary>보조 정보·비활성 텍스트. 바탕 위 5.5:1.</summary>
        public static readonly Color TextMuted = FromRgb(0x9A9089);

        /// <summary>선택·포커스·강조 (황동). 바탕 위 6.7:1.</summary>
        public static readonly Color FocusBrass = FromRgb(0xC89B4A);

        /// <summary>비활성 컨트롤·슬롯 배경. 대비 3.0:1 — <b>면 전용</b>.</summary>
        public static readonly Color IronGray = FromRgb(0x6B6660);

        // ── §9.1 · §12.1 배경 층 ─────────────────────────────────────────

        /// <summary>
        /// HUD 패널 배경 — 가이드 §9.1. 배경이 계속 흐르는 화면이라 반투명 패널은 명도가 요동친다.
        /// <b>불투명에 가깝게(88%) 두거나 아예 배경 없이 외곽선만</b> 쓴다. 중간값이 가장 나쁘다.
        /// </summary>
        public static readonly Color PanelBackdrop = WithAlpha(PanelSoot, 0.88f);

        /// <summary>설정 화면의 어두운 오버레이 — 가이드 §12.1의 65~80% 중간값.</summary>
        public static readonly Color SettingsOverlay = WithAlpha(PanelSoot, 0.72f);

        // ── §7.2 상태 4단계 ──────────────────────────────────────────────

        /// <summary>안전 — 면.</summary>
        public static readonly Color SafeFill = FromRgb(0x7FA653);

        /// <summary>안전 — 텍스트 (면색보다 밝은 변형).</summary>
        public static readonly Color SafeText = FromRgb(0x9FC46E);

        /// <summary>주의 — 면·텍스트 공용 (8.7:1로 이미 충분히 밝다).</summary>
        public static readonly Color CautionFill = FromRgb(0xE3B23C);

        /// <summary>경고 — 면·텍스트 공용 (5.6:1).</summary>
        public static readonly Color AlertFill = FromRgb(0xDD7A2E);

        /// <summary>위험 — 면. <b>텍스트로 쓰지 않는다</b> (2.9:1).</summary>
        public static readonly Color CriticalFill = FromRgb(0xB23A2E);

        /// <summary>위험 — 텍스트 변형 (5.8:1).</summary>
        public static readonly Color CriticalText = FromRgb(0xF0705F);

        // ── 리치텍스트용 16진 문자열 ──────────────────────────────────────
        //
        // IMGUI 리치텍스트(<color=#RRGGBB>)는 문자열을 받는다. 매 프레임 ColorUtility로
        // 변환하면 OnGUI에서 GC를 만들므로 상수로 둔다. 위 Color 값과 어긋나면
        // UiPaletteTests가 실패한다 — 두 표현의 동기화는 테스트가 보증한다.

        /// <summary><see cref="TextSteam"/>의 리치텍스트 표기.</summary>
        public const string HexTextSteam = "#F2EAE0";

        /// <summary><see cref="TextMuted"/>의 리치텍스트 표기.</summary>
        public const string HexTextMuted = "#9A9089";

        /// <summary><see cref="FocusBrass"/>의 리치텍스트 표기.</summary>
        public const string HexFocusBrass = "#C89B4A";

        /// <summary><see cref="SafeText"/>의 리치텍스트 표기.</summary>
        public const string HexSafeText = "#9FC46E";

        /// <summary><see cref="CautionFill"/>의 리치텍스트 표기.</summary>
        public const string HexCautionText = "#E3B23C";

        /// <summary><see cref="AlertFill"/>의 리치텍스트 표기.</summary>
        public const string HexAlertText = "#DD7A2E";

        /// <summary><see cref="CriticalText"/>의 리치텍스트 표기.</summary>
        public const string HexCriticalText = "#F0705F";

        // ── 조회 ─────────────────────────────────────────────────────────

        /// <summary>단계에 대응하는 <b>면</b> 색 — 바 채움·슬롯 테두리처럼 면적이 있는 표현에 쓴다.</summary>
        public static Color StatusFill(UiStatusLevel level)
        {
            switch (level)
            {
                case UiStatusLevel.Caution: return CautionFill;
                case UiStatusLevel.Alert: return AlertFill;
                case UiStatusLevel.Critical: return CriticalFill;
                default: return SafeFill;
            }
        }

        /// <summary>단계에 대응하는 <b>텍스트</b> 색 — 어두운 배경 위 4.5:1을 보장하는 변형이다.</summary>
        public static Color StatusTextColor(UiStatusLevel level)
        {
            switch (level)
            {
                case UiStatusLevel.Caution: return CautionFill;
                case UiStatusLevel.Alert: return AlertFill;
                case UiStatusLevel.Critical: return CriticalText;
                default: return SafeText;
            }
        }

        /// <summary>
        /// 단계에 대응하는 리치텍스트 16진 표기 — <c>$"&lt;color={UiPalette.StatusHex(level)}&gt;…"</c>.
        /// 문자열 상수를 돌려주므로 호출당 할당이 없다.
        /// </summary>
        public static string StatusHex(UiStatusLevel level)
        {
            switch (level)
            {
                case UiStatusLevel.Caution: return HexCautionText;
                case UiStatusLevel.Alert: return HexAlertText;
                case UiStatusLevel.Critical: return HexCriticalText;
                default: return HexSafeText;
            }
        }

        // ── 변환 유틸 ────────────────────────────────────────────────────

        /// <summary>0xRRGGBB 정수를 불투명 <see cref="Color"/>로. 문서의 16진 표기를 그대로 옮기기 위한 것이다.</summary>
        private static Color FromRgb(int rgb)
        {
            const float Inv255 = 1f / 255f;
            return new Color(
                ((rgb >> 16) & 0xFF) * Inv255,
                ((rgb >> 8) & 0xFF) * Inv255,
                (rgb & 0xFF) * Inv255,
                1f);
        }

        /// <summary>색상은 유지하고 알파만 바꾼 사본.</summary>
        private static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }
    }
}
