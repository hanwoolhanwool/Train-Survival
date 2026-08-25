using System.Collections.Generic;
using Game.UI;
using Game.UI.Loading;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// UI 워밍업 대상 검증 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §5.4 · §10.
    ///
    /// <para><b>크기 목록이 여기서 가장 중요하다.</b> 크기마다 아틀라스가 따로라, 하나를
    /// 빠뜨리면 <b>그 크기에서만</b> 첫 그리기가 여전히 튄다 — 오류는 하나도 안 나고 화면을
    /// 봐서는 못 찾는 조용한 실패다(§11-4).</para>
    /// </summary>
    public sealed class UiWarmupTextTests
    {
        /// <summary>최소 지원(720p)부터 4K까지 — 배율 하한·상한이 모두 걸리는 구간을 포함한다.</summary>
        private static readonly float[] Heights = { 720f, 1080f, 1440f, 2160f, 4320f };

        [Test]
        public void 표시명에서_문자를_모은다()
        {
            char[] chars = UiWarmupText.Collect(new[] { "목재", "고철" });

            CollectionAssert.AreEquivalent(new[] { '목', '재', '고', '철' }, chars);
        }

        [Test]
        public void 중복은_한_번만_굽는다()
        {
            char[] chars = UiWarmupText.Collect(new[] { "목재", "목재", "재목" });

            Assert.AreEqual(2, chars.Length);
        }

        [Test]
        public void 공백과_줄바꿈은_굽지_않는다()
        {
            // HotbarItemLabels의 "보따리\n[좌클릭 풀기]"처럼 줄바꿈이 섞인 표시명이 실제로 있다.
            char[] chars = UiWarmupText.Collect(new[] { "가 죽\n옷\t" });

            CollectionAssert.AreEquivalent(new[] { '가', '죽', '옷' }, chars);
        }

        [Test]
        public void 빈_문자열과_null은_건너뛴다()
        {
            Assert.AreEqual(0, UiWarmupText.Collect(new[] { "", null, "   " }).Length);
            Assert.AreEqual(0, UiWarmupText.Collect(null).Length);
        }

        [Test]
        public void 크기_목록에_기본_크기_0이_들어_있다()
        {
            // HUD 대부분이 GUIStyle 없이 기본 스킨으로 그린다 — 0(폰트 기본 크기)이 실제로 제일 많이 쓰인다.
            CollectionAssert.Contains(UiWarmupText.FontSizes(1080f), 0);
        }

        [Test]
        public void 크기_목록이_UiMetrics가_내는_HUD_크기를_전부_덮는다()
        {
            foreach (float height in Heights)
            {
                var sizes = new List<int>(UiWarmupText.FontSizes(height));

                foreach (int size1440 in UiMetrics.HudSizes1440)
                {
                    int expected = UiMetrics.FontFor(size1440, height);
                    CollectionAssert.Contains(
                        sizes, expected, $"{height}px 화면에서 크기 {size1440}(→{expected})이 빠졌다");
                }
            }
        }

        [Test]
        public void 크기_목록에_중복이_없다()
        {
            // 배율이 낮으면 여러 기준 크기가 하한 14로 뭉친다 — 그때 같은 크기를 두 번 굽지 않아야 한다.
            foreach (float height in Heights)
            {
                int[] sizes = UiWarmupText.FontSizes(height);
                CollectionAssert.AllItemsAreUnique(sizes, $"{height}px 화면");
            }
        }

        [Test]
        public void HUD_크기_목록에_메뉴_크기가_섞이지_않았다()
        {
            // 로고 200px 글리프를 수백 자 굽는 것은 순수한 낭비다 — TMP는 IMGUI 아틀라스를 안 쓴다.
            CollectionAssert.DoesNotContain(UiMetrics.HudSizes1440, UiMetrics.DisplayLogo);
            CollectionAssert.DoesNotContain(UiMetrics.HudSizes1440, UiMetrics.MenuButton);
            CollectionAssert.DoesNotContain(UiMetrics.HudSizes1440, UiMetrics.SettingsTitle);
        }

        [Test]
        public void HUD_크기_목록은_비어_있지_않다()
        {
            Assert.Greater(UiMetrics.HudSizes1440.Length, 0);
        }
    }
}
