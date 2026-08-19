using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// UI 색 토큰 검증 (비주얼·UI/UX 가이드 §7.1·§7.2). 색이 "예쁜가"는 여기서 판정할 수 없다 —
    /// 이 테스트가 지키는 것은 <b>판독 가능성(대비)</b>과 <b>두 표현(Color·16진 문자열)의 동기화</b>다.
    ///
    /// <para>대비비는 WCAG 2.x 상대 휘도 공식을 그대로 쓴다. 본문 기준은 4.5:1이고,
    /// 흔들리는 화면이라는 사정을 감안해 <b>주 텍스트는 AAA(7:1)</b>를 요구한다.</para>
    /// </summary>
    public sealed class UiPaletteTests
    {
        /// <summary>WCAG 본문 대비 기준.</summary>
        private const float BodyMinimum = 4.5f;

        /// <summary>WCAG 강화 기준 — 주 텍스트에만 적용한다.</summary>
        private const float EnhancedMinimum = 7f;

        // ── 텍스트로 쓰는 토큰은 전부 본문 기준을 넘어야 한다 ──────────────

        [Test]
        public void 주_텍스트는_강화_대비를_만족한다()
        {
            float ratio = ContrastRatio(UiPalette.TextSteam, UiPalette.PanelSoot);

            Assert.That(ratio, Is.GreaterThanOrEqualTo(EnhancedMinimum),
                $"TextSteam 대비 {ratio:F2}:1 — 주 텍스트는 {EnhancedMinimum}:1 이상이어야 한다.");
        }

        [Test]
        public void 보조_텍스트와_강조색은_본문_대비를_만족한다()
        {
            AssertReadable(UiPalette.TextMuted, nameof(UiPalette.TextMuted));
            AssertReadable(UiPalette.FocusBrass, nameof(UiPalette.FocusBrass));
        }

        [Test]
        public void 상태_4단계의_텍스트_색은_모두_본문_대비를_만족한다()
        {
            foreach (UiStatusLevel level in System.Enum.GetValues(typeof(UiStatusLevel)))
            {
                AssertReadable(UiPalette.StatusTextColor(level), $"StatusTextColor({level})");
            }
        }

        // ── 면 전용 토큰은 텍스트로 쓸 수 없다 (분리의 근거를 고정한다) ────

        /// <summary>
        /// 위험 적색을 텍스트로 쓰면 읽히지 않는다 — 이것이 <see cref="UiPalette.CriticalText"/>가
        /// 따로 존재하는 이유다. 현재 코드의 <c>&lt;color=red&gt;</c>가 안고 있는 문제이기도 하다.
        /// </summary>
        [Test]
        public void 위험_면색은_본문_대비에_미달한다()
        {
            float fill = ContrastRatio(UiPalette.CriticalFill, UiPalette.PanelSoot);
            float text = ContrastRatio(UiPalette.CriticalText, UiPalette.PanelSoot);

            Assert.That(fill, Is.LessThan(BodyMinimum),
                $"CriticalFill 대비 {fill:F2}:1 — 이 값이 4.5를 넘게 되면 텍스트 변형을 둘 이유가 사라진다.");
            Assert.That(text, Is.GreaterThanOrEqualTo(BodyMinimum),
                $"CriticalText 대비 {text:F2}:1 — 텍스트 변형은 본문 기준을 넘어야 한다.");
            Assert.That(text, Is.GreaterThan(fill), "텍스트 변형은 면색보다 밝아야 한다.");
        }

        [Test]
        public void 선과_비활성_면은_텍스트로_쓰지_않는다()
        {
            Assert.That(ContrastRatio(UiPalette.PanelLine, UiPalette.PanelSoot), Is.LessThan(BodyMinimum));
            Assert.That(ContrastRatio(UiPalette.IronGray, UiPalette.PanelSoot), Is.LessThan(BodyMinimum));
        }

        // ── 단계 매핑 ────────────────────────────────────────────────────

        [Test]
        public void 상태_4단계의_면색은_서로_구분된다()
        {
            var colors = new[]
            {
                UiPalette.StatusFill(UiStatusLevel.Safe),
                UiPalette.StatusFill(UiStatusLevel.Caution),
                UiPalette.StatusFill(UiStatusLevel.Alert),
                UiPalette.StatusFill(UiStatusLevel.Critical),
            };

            for (int i = 0; i < colors.Length; i++)
            {
                for (int j = i + 1; j < colors.Length; j++)
                {
                    Assert.AreNotEqual(colors[i], colors[j], $"{(UiStatusLevel)i}와 {(UiStatusLevel)j}가 같은 색이다.");
                }
            }
        }

        /// <summary>
        /// 색각 이상 대응 — 가이드 §17 체크리스트. 색상이 아니라 <b>휘도</b>만으로도 단계가 구분돼야
        /// 흑백으로 바꿨을 때 살아남는다. 인접 단계끼리 최소한의 휘도 차를 요구한다.
        /// </summary>
        [Test]
        public void 상태_4단계는_휘도만으로도_구분된다()
        {
            float safe = RelativeLuminance(UiPalette.StatusFill(UiStatusLevel.Safe));
            float caution = RelativeLuminance(UiPalette.StatusFill(UiStatusLevel.Caution));
            float alert = RelativeLuminance(UiPalette.StatusFill(UiStatusLevel.Alert));
            float critical = RelativeLuminance(UiPalette.StatusFill(UiStatusLevel.Critical));

            const float MinGap = 0.03f;

            Assert.That(Mathf.Abs(caution - safe), Is.GreaterThan(MinGap), "안전↔주의의 휘도 차가 너무 작다.");
            Assert.That(Mathf.Abs(alert - caution), Is.GreaterThan(MinGap), "주의↔경고의 휘도 차가 너무 작다.");
            Assert.That(Mathf.Abs(critical - alert), Is.GreaterThan(MinGap), "경고↔위험의 휘도 차가 너무 작다.");
        }

        [Test]
        public void 알_수_없는_단계는_안전으로_떨어진다()
        {
            var outOfRange = (UiStatusLevel)99;

            Assert.AreEqual(UiPalette.SafeFill, UiPalette.StatusFill(outOfRange));
            Assert.AreEqual(UiPalette.SafeText, UiPalette.StatusTextColor(outOfRange));
            Assert.AreEqual(UiPalette.HexSafeText, UiPalette.StatusHex(outOfRange));
        }

        // ── Color와 16진 문자열의 동기화 ─────────────────────────────────

        /// <summary>
        /// 같은 색을 <see cref="Color"/>와 리치텍스트 문자열로 이중 정의하고 있다.
        /// 한쪽만 고치면 화면에서 색이 갈라지므로, 여기서 어긋남을 막는다.
        /// </summary>
        [Test]
        public void 리치텍스트_16진값은_Color_토큰과_일치한다()
        {
            AssertHexMatches(UiPalette.HexTextSteam, UiPalette.TextSteam, nameof(UiPalette.TextSteam));
            AssertHexMatches(UiPalette.HexTextMuted, UiPalette.TextMuted, nameof(UiPalette.TextMuted));
            AssertHexMatches(UiPalette.HexFocusBrass, UiPalette.FocusBrass, nameof(UiPalette.FocusBrass));
            AssertHexMatches(UiPalette.HexSafeText, UiPalette.SafeText, nameof(UiPalette.SafeText));
            AssertHexMatches(UiPalette.HexCautionText, UiPalette.CautionFill, nameof(UiPalette.CautionFill));
            AssertHexMatches(UiPalette.HexAlertText, UiPalette.AlertFill, nameof(UiPalette.AlertFill));
            AssertHexMatches(UiPalette.HexCriticalText, UiPalette.CriticalText, nameof(UiPalette.CriticalText));
        }

        [Test]
        public void 단계별_16진값은_텍스트_색과_일치한다()
        {
            foreach (UiStatusLevel level in System.Enum.GetValues(typeof(UiStatusLevel)))
            {
                AssertHexMatches(UiPalette.StatusHex(level), UiPalette.StatusTextColor(level), $"StatusHex({level})");
            }
        }

        // ── 배경 층 ──────────────────────────────────────────────────────

        /// <summary>
        /// 가이드 §9.1 — 흐르는 배경 위에서 반투명 패널은 명도가 요동친다.
        /// 중간 투명도(대략 0.3~0.7)가 가장 나쁘므로, 패널 배경은 충분히 불투명해야 한다.
        /// </summary>
        [Test]
        public void HUD_패널_배경은_충분히_불투명하다()
        {
            Assert.That(UiPalette.PanelBackdrop.a, Is.GreaterThanOrEqualTo(0.85f));
            Assert.That(UiPalette.SettingsOverlay.a, Is.InRange(0.65f, 0.8f), "설정 오버레이는 가이드 §12.1의 65~80% 범위다.");
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────────

        private static void AssertReadable(Color foreground, string label)
        {
            float ratio = ContrastRatio(foreground, UiPalette.PanelSoot);

            Assert.That(ratio, Is.GreaterThanOrEqualTo(BodyMinimum),
                $"{label} 대비 {ratio:F2}:1 — 본문 기준 {BodyMinimum}:1에 미달한다.");
        }

        private static void AssertHexMatches(string hex, Color expected, string label)
        {
            Assert.IsTrue(ColorUtility.TryParseHtmlString(hex, out Color parsed), $"{label}: '{hex}' 파싱 실패.");

            Assert.That(parsed.r, Is.EqualTo(expected.r).Within(0.002f), $"{label}: R 불일치 ({hex}).");
            Assert.That(parsed.g, Is.EqualTo(expected.g).Within(0.002f), $"{label}: G 불일치 ({hex}).");
            Assert.That(parsed.b, Is.EqualTo(expected.b).Within(0.002f), $"{label}: B 불일치 ({hex}).");
        }

        /// <summary>WCAG 2.x 대비비 — (밝은 쪽 + 0.05) / (어두운 쪽 + 0.05).</summary>
        private static float ContrastRatio(Color a, Color b)
        {
            float la = RelativeLuminance(a);
            float lb = RelativeLuminance(b);
            float high = Mathf.Max(la, lb);
            float low = Mathf.Min(la, lb);

            return (high + 0.05f) / (low + 0.05f);
        }

        /// <summary>WCAG 2.x 상대 휘도. 입력은 sRGB(감마) 값이다.</summary>
        private static float RelativeLuminance(Color color)
        {
            return 0.2126f * Linearize(color.r)
                 + 0.7152f * Linearize(color.g)
                 + 0.0722f * Linearize(color.b);
        }

        private static float Linearize(float channel)
        {
            return channel <= 0.03928f
                ? channel / 12.92f
                : Mathf.Pow((channel + 0.055f) / 1.055f, 2.4f);
        }
    }
}
