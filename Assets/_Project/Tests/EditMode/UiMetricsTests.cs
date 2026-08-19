using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 타이포 배율·하한 검증 (비주얼·UI/UX 가이드 §13.2). 크기표의 대표값이 실제 해상도에서
    /// <b>읽을 수 있는 픽셀 수로 떨어지는지</b>만 본다.
    ///
    /// <para><see cref="UiMetrics.ScaleFor"/>·<see cref="UiMetrics.FontFor"/>는 <c>Screen</c>을 읽지 않는
    /// 순수 함수라 임의 해상도를 넣어 검증할 수 있다 — EditMode에서 실제 화면 크기는 에디터 창에 좌우된다.</para>
    /// </summary>
    public sealed class UiMetricsTests
    {
        private const float Height720 = 720f;
        private const float Height1080 = 1080f;
        private const float Height1440 = 1440f;
        private const float Height2160 = 2160f;

        /// <summary>가이드 §13.2의 대표값 전부 — 하한 검사는 이 목록을 통째로 돈다.</summary>
        private static readonly int[] AllSizes =
        {
            UiMetrics.DisplayLogo,
            UiMetrics.MenuButton,
            UiMetrics.SettingsTitle,
            UiMetrics.Tab,
            UiMetrics.SettingsLabel,
            UiMetrics.DropdownValue,
            UiMetrics.HudNumber,
            UiMetrics.ContextPrompt,
            UiMetrics.Nameplate,
        };

        [Test]
        public void 기준_해상도에서는_크기표_값이_그대로_나온다()
        {
            Assert.AreEqual(1f, UiMetrics.ScaleFor(Height1440), 0.0001f);

            foreach (int size in AllSizes)
            {
                Assert.AreEqual(size, UiMetrics.FontFor(size, Height1440), $"1440p에서 {size}가 변형됐다.");
            }
        }

        [Test]
        public void 최소_지원_해상도에서_배율은_0_75다()
        {
            Assert.AreEqual(0.75f, UiMetrics.ScaleFor(Height1080), 0.0001f);
            Assert.AreEqual(15, UiMetrics.FontFor(UiMetrics.Nameplate, Height1080));
        }

        /// <summary>
        /// 가이드 §13.2의 하한 — 흔들리는 화면에서 14 px 미만은 읽히지 않는다.
        /// 최소 지원 해상도에서는 <b>하한에 걸리지 않고도</b> 전부 통과해야 한다(크기표가 그렇게 잡혀 있다).
        /// </summary>
        [Test]
        public void 최소_지원_해상도에서_모든_크기가_하한_이상이다()
        {
            foreach (int size in AllSizes)
            {
                int actual = UiMetrics.FontFor(size, Height1080);

                Assert.That(actual, Is.GreaterThanOrEqualTo(UiMetrics.MinFontPx),
                    $"1080p에서 {size} → {actual} px, 하한 {UiMetrics.MinFontPx} 미만.");
                Assert.AreEqual(UnityEngine.Mathf.RoundToInt(size * 0.75f), actual,
                    $"1080p에서 {size}가 하한에 걸렸다 — 크기표 대표값이 너무 작다는 뜻이다.");
            }
        }

        [Test]
        public void 더_작은_화면에서는_하한이_크기를_떠받친다()
        {
            Assert.AreEqual(0.5f, UiMetrics.ScaleFor(Height720), 0.0001f);

            // 20 * 0.5 = 10 → 하한 14로 올라간다.
            Assert.AreEqual(UiMetrics.MinFontPx, UiMetrics.FontFor(UiMetrics.Nameplate, Height720));

            foreach (int size in AllSizes)
            {
                Assert.That(UiMetrics.FontFor(size, Height720), Is.GreaterThanOrEqualTo(UiMetrics.MinFontPx));
            }
        }

        [Test]
        public void 고해상도에서는_비례해서_커진다()
        {
            Assert.AreEqual(1.5f, UiMetrics.ScaleFor(Height2160), 0.0001f);
            Assert.AreEqual(36, UiMetrics.FontFor(UiMetrics.HudNumber, Height2160));
        }

        [Test]
        public void 배율은_상한과_하한_사이로_잘린다()
        {
            Assert.AreEqual(UiMetrics.MaxScale, UiMetrics.ScaleFor(Height1440 * 10f), 0.0001f);
            Assert.AreEqual(UiMetrics.MinScale, UiMetrics.ScaleFor(1f), 0.0001f);
        }

        /// <summary>화면 크기를 아직 모르는 시점(초기화 전)에 0이 들어와도 배율이 무너지지 않아야 한다.</summary>
        [Test]
        public void 화면_높이가_비정상이면_기준_배율로_돌아간다()
        {
            Assert.AreEqual(1f, UiMetrics.ScaleFor(0f), 0.0001f);
            Assert.AreEqual(1f, UiMetrics.ScaleFor(-100f), 0.0001f);
            Assert.AreEqual(UiMetrics.HudNumber, UiMetrics.FontFor(UiMetrics.HudNumber, 0f));
        }
    }
}
