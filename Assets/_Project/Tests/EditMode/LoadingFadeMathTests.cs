using Game.Systems.Loading;
using Game.UI.Loading;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 로딩 화면의 시간 규칙 검증 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §8.3 · §8.5.
    ///
    /// <para>고정하려는 것은 <b>깜빡이지 않는다</b>와 <b>페이드가 잘리지 않는다</b> 둘이다.
    /// 빠른 로딩에서 화면이 스치기만 하면 "빨라 보인다"가 아니라 "뭔가 잘못됐다"로 읽힌다.</para>
    /// </summary>
    public sealed class LoadingFadeMathTests
    {
        private const float Tolerance = 1e-4f;

        // ── 출발 단계 길이 ───────────────────────────────────────────────

        [Test]
        public void 아주_빠른_로딩에서는_최소_표시_시간을_채운다()
        {
            // 0.1초 만에 끝났다 — 0.5초를 더 서 있어야 0.6초가 된다.
            Assert.AreEqual(0.5f, LoadingFadeMath.DepartSeconds(0.1f), Tolerance);
        }

        [Test]
        public void 느린_로딩에서는_페이드_아웃_길이만_머문다()
        {
            // 이미 최소 표시 시간을 넘겼으므로 남은 것은 페이드뿐이다.
            Assert.AreEqual(LoadingFadeMath.FadeOutSeconds, LoadingFadeMath.DepartSeconds(10f), Tolerance);
            Assert.AreEqual(LoadingFadeMath.FadeOutSeconds, LoadingFadeMath.DepartSeconds(0.6f), Tolerance);
        }

        [Test]
        public void 출발_단계는_페이드_아웃보다_짧아지지_않는다()
        {
            // 어떤 입력에도 페이드가 잘리면 안 된다.
            foreach (float visible in new[] { 0f, 0.05f, 0.3f, 0.6f, 5f, 60f })
            {
                Assert.GreaterOrEqual(
                    LoadingFadeMath.DepartSeconds(visible),
                    LoadingFadeMath.FadeOutSeconds - Tolerance,
                    $"화면이 {visible}초 떠 있었을 때");
            }
        }

        [Test]
        public void 화면은_최소_표시_시간보다_짧게_떠_있지_않는다()
        {
            foreach (float visible in new[] { 0f, 0.1f, 0.3f, 0.59f })
            {
                float total = visible + LoadingFadeMath.DepartSeconds(visible);
                Assert.GreaterOrEqual(
                    total, LoadingFadeMath.MinVisibleSeconds - Tolerance, $"{visible}초에 출발했을 때");
            }
        }

        // ── 불투명도 ─────────────────────────────────────────────────────

        [Test]
        public void 올라올_때는_0에서_1로_찬다()
        {
            Assert.AreEqual(0f, LoadingFadeMath.Alpha(0f, 0f, 0f, false), Tolerance);
            Assert.AreEqual(0.5f, LoadingFadeMath.Alpha(LoadingFadeMath.FadeInSeconds * 0.5f, 0f, 0f, false), Tolerance);
            Assert.AreEqual(1f, LoadingFadeMath.Alpha(LoadingFadeMath.FadeInSeconds, 0f, 0f, false), Tolerance);
        }

        [Test]
        public void 가운데는_계속_불투명하다()
        {
            Assert.AreEqual(1f, LoadingFadeMath.Alpha(5f, 0f, 0f, false), Tolerance);
            Assert.AreEqual(1f, LoadingFadeMath.Alpha(60f, 0f, 0f, false), Tolerance);
        }

        [Test]
        public void 페이드_아웃은_출발_단계의_끝에_붙는다()
        {
            // 최소 표시 시간이 긴 경우(빠른 로딩) — 앞부분은 불투명해야 한다.
            float total = LoadingFadeMath.DepartSeconds(0.1f);   // 0.5초
            Assert.AreEqual(1f, LoadingFadeMath.Alpha(10f, 0f, total, true), Tolerance);
            Assert.AreEqual(1f, LoadingFadeMath.Alpha(10f, total - LoadingFadeMath.FadeOutSeconds, total, true), Tolerance);
        }

        [Test]
        public void 출발_단계의_끝에서_투명해진다()
        {
            float total = LoadingFadeMath.DepartSeconds(10f);
            Assert.AreEqual(0f, LoadingFadeMath.Alpha(10f, total, total, true), Tolerance);
            Assert.AreEqual(0.5f, LoadingFadeMath.Alpha(10f, total - LoadingFadeMath.FadeOutSeconds * 0.5f, total, true), Tolerance);
        }

        [Test]
        public void 불투명도는_언제나_0과_1_사이다()
        {
            foreach (float visible in new[] { -1f, 0f, 0.07f, 1f, 100f })
            {
                foreach (float elapsed in new[] { -1f, 0f, 0.2f, 5f })
                {
                    float a = LoadingFadeMath.Alpha(visible, elapsed, 0.5f, true);
                    Assert.GreaterOrEqual(a, 0f);
                    Assert.LessOrEqual(a, 1f);
                }
            }
        }

        // ── 팁 고르기 ────────────────────────────────────────────────────

        [Test]
        public void 팁이_없으면_고를_것이_없다()
        {
            Assert.AreEqual(-1, LoadingTipCatalog.PickIndex(0, -1, 0.5f));
        }

        [Test]
        public void 팁이_하나뿐이면_직전과_같아도_그것을_고른다()
        {
            Assert.AreEqual(0, LoadingTipCatalog.PickIndex(1, 0, 0.5f));
        }

        [Test]
        public void 직전에_보여_준_팁은_피한다()
        {
            for (int previous = 0; previous < 10; previous++)
            {
                for (int roll = 0; roll <= 10; roll++)
                {
                    int picked = LoadingTipCatalog.PickIndex(10, previous, roll / 10f);
                    Assert.AreNotEqual(previous, picked, $"직전 {previous} · roll {roll / 10f}");
                }
            }
        }

        [Test]
        public void 뽑은_팁은_언제나_범위_안이다()
        {
            foreach (int count in new[] { 1, 2, 8, 12 })
            {
                foreach (float roll in new[] { -1f, 0f, 0.5f, 1f, 2f })
                {
                    int picked = LoadingTipCatalog.PickIndex(count, -1, roll);
                    Assert.GreaterOrEqual(picked, 0);
                    Assert.Less(picked, count, $"{count}개 · roll {roll}");
                }
            }
        }
    }
}
