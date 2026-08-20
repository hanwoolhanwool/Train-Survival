using Game.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 로비 배경 cover 정합 검증 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §8.1.
    ///
    /// <para>고정하려는 것은 두 가지다 — <b>어느 종횡비에서도 여백이 생기지 않는다</b>와
    /// <b>배율이 1 미만으로 내려가지 않는다</b>. 앞쪽은 검은 띠를, 뒤쪽은 원본보다 작게 깔려
    /// 흐려지는 것을 막는다.</para>
    ///
    /// <para>실제 화면 해상도는 에디터 창 크기에 좌우되므로, 화면 → 캔버스 환산까지
    /// <see cref="BackgroundCoverMath.CanvasSize(Vector2)"/>로 들여와 순수 함수로 검증한다.</para>
    /// </summary>
    public sealed class BackgroundCoverMathTests
    {
        /// <summary>배경 원본 크기 — <c>T_Menu_Background</c>(1672×941 → 1920×1080 업스케일, 계획 §4.1-1).</summary>
        private static readonly Vector2 SourceSize = new Vector2(1920f, 1080f);

        /// <summary>계획 §8.1이 요구하는 종횡비 4종 + 대표 해상도.</summary>
        private static readonly Vector2[] Resolutions =
        {
            new Vector2(1280f, 720f),   // 16:9  최소
            new Vector2(1920f, 1080f),  // 16:9  기준
            new Vector2(2560f, 1440f),  // 16:9
            new Vector2(3840f, 2160f),  // 16:9  4K
            new Vector2(1920f, 1200f),  // 16:10
            new Vector2(2560f, 1600f),  // 16:10
            new Vector2(2560f, 1080f),  // 21:9
            new Vector2(3440f, 1440f),  // 21:9
            new Vector2(1440f, 1080f),  // 4:3
            new Vector2(1600f, 1200f),  // 4:3
        };

        /// <summary>로그·거듭제곱을 거친 픽셀 크기 비교라 부동소수 오차를 조금 넉넉히 잡는다 — 여백은 수십 px 단위로 생긴다.</summary>
        private const float Tolerance = 0.01f;

        [Test]
        public void 기준_해상도에서는_배율도_크기도_원본_그대로다()
        {
            Vector2 canvas = BackgroundCoverMath.CanvasSize(new Vector2(1920f, 1080f));

            Assert.AreEqual(1920f, canvas.x, Tolerance);
            Assert.AreEqual(1080f, canvas.y, Tolerance);
            Assert.AreEqual(1f, BackgroundCoverMath.CoverScale(canvas, SourceSize), Tolerance);

            Vector2 delta = BackgroundCoverMath.CoverSizeDelta(canvas, SourceSize);
            Assert.AreEqual(0f, delta.x, Tolerance);
            Assert.AreEqual(0f, delta.y, Tolerance);
        }

        [Test]
        public void 모든_종횡비에서_여백이_생기지_않는다()
        {
            foreach (Vector2 resolution in Resolutions)
            {
                Vector2 canvas = BackgroundCoverMath.CanvasSize(resolution);
                Vector2 cover = BackgroundCoverMath.CoverSize(canvas, SourceSize);

                Assert.GreaterOrEqual(cover.x, canvas.x - Tolerance, $"{resolution} 가로에 여백");
                Assert.GreaterOrEqual(cover.y, canvas.y - Tolerance, $"{resolution} 세로에 여백");
            }
        }

        [Test]
        public void 어느_종횡비에서도_원본을_축소하지_않는다()
        {
            foreach (Vector2 resolution in Resolutions)
            {
                Vector2 canvas = BackgroundCoverMath.CanvasSize(resolution);
                float scale = BackgroundCoverMath.CoverScale(canvas, SourceSize);

                Assert.GreaterOrEqual(scale, 1f - Tolerance, $"{resolution}에서 배율 {scale}");
            }
        }

        [Test]
        public void 덮은_뒤에도_원본_종횡비가_유지된다()
        {
            float sourceAspect = SourceSize.x / SourceSize.y;

            foreach (Vector2 resolution in Resolutions)
            {
                Vector2 canvas = BackgroundCoverMath.CanvasSize(resolution);
                Vector2 cover = BackgroundCoverMath.CoverSize(canvas, SourceSize);

                Assert.AreEqual(sourceAspect, cover.x / cover.y, Tolerance, $"{resolution}에서 종횡비 왜곡");
            }
        }

        [Test]
        public void 넘치는_양은_음수가_되지_않는다()
        {
            foreach (Vector2 resolution in Resolutions)
            {
                Vector2 canvas = BackgroundCoverMath.CanvasSize(resolution);
                Vector2 delta = BackgroundCoverMath.CoverSizeDelta(canvas, SourceSize);

                Assert.GreaterOrEqual(delta.x, -Tolerance, $"{resolution} 가로가 모자란다");
                Assert.GreaterOrEqual(delta.y, -Tolerance, $"{resolution} 세로가 모자란다");
            }
        }

        [Test]
        public void 좁은_화면일수록_한_축만_넘친다()
        {
            // 21:9는 가로가 남으므로 세로가 넘치고, 4:3은 그 반대다.
            Vector2 ultrawide = BackgroundCoverMath.CanvasSize(new Vector2(2560f, 1080f));
            Vector2 ultrawideDelta = BackgroundCoverMath.CoverSizeDelta(ultrawide, SourceSize);

            Assert.Greater(ultrawideDelta.y, Tolerance, "21:9에서는 세로가 넘쳐야 한다");
            Assert.AreEqual(0f, ultrawideDelta.x, Tolerance, "21:9에서 가로는 딱 맞아야 한다");

            Vector2 standard = BackgroundCoverMath.CanvasSize(new Vector2(1440f, 1080f));
            Vector2 standardDelta = BackgroundCoverMath.CoverSizeDelta(standard, SourceSize);

            Assert.Greater(standardDelta.x, Tolerance, "4:3에서는 가로가 넘쳐야 한다");
            Assert.AreEqual(0f, standardDelta.y, Tolerance, "4:3에서 세로는 딱 맞아야 한다");
        }

        [Test]
        public void 잘못된_입력에도_NaN이_나오지_않는다()
        {
            Assert.AreEqual(1f, BackgroundCoverMath.CanvasScaleFactor(Vector2.zero, BackgroundCoverMath.ReferenceResolution, 0.5f));
            Assert.AreEqual(1f, BackgroundCoverMath.CoverScale(new Vector2(1920f, 1080f), Vector2.zero));

            Vector2 fallback = BackgroundCoverMath.CoverSize(new Vector2(1920f, 1080f), Vector2.zero);
            Assert.AreEqual(new Vector2(1920f, 1080f), fallback);

            Vector2 negative = BackgroundCoverMath.CoverSize(new Vector2(-100f, -100f), SourceSize);
            Assert.IsFalse(float.IsNaN(negative.x) || float.IsNaN(negative.y));
        }
    }
}
