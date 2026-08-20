using Game.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 운행 공고대의 자리 검증 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.4 · §8.2.
    ///
    /// <para>배너와 같은 규칙이다 — <b>높이로 폭을 정한다.</b> 폭 기준으로 잡으면 21:9에서 공고대가
    /// 같이 커져 화면을 파고들고, 4:3에서는 오른쪽으로 밀려 잘린다.</para>
    ///
    /// <para><b>7차에 규약이 바뀌었다.</b> 6차까지는 스프라이트가 종이 한 장이어서 "스프라이트 전체가
    /// 화면 안에 들어온다"가 곧 "글자가 다 보인다"였다. 지금은 스프라이트가 기둥·받침까지 포함한
    /// 공고대 전체이고, <b>받침은 일부러 화면 아래로 내보낸다.</b> 그래서 검증 대상을 스프라이트가
    /// 아니라 <see cref="NoticeBoardView.PaperRect"/>로 옮겼다 — 잘려도 되는 것은 쇠붙이뿐이다.</para>
    /// </summary>
    public sealed class NoticeBoardLayoutTests
    {
        private const float Tolerance = 0.01f;

        private static readonly Vector2[] Canvases =
        {
            new Vector2(1920f, 1080f),  // 16:9 기준
            new Vector2(2217f, 935f),   // 21:9 (CanvasScaler 환산 후)
            new Vector2(1663f, 1247f),  // 4:3  (환산 후)
            new Vector2(1920f, 1200f),  // 16:10
        };

        [Test]
        public void 어느_종횡비에서도_공고대가_늘어나지_않는다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 size = NoticeBoardView.BoardSize(canvas);
                Assert.AreEqual(NoticeBoardView.Aspect, size.x / size.y, Tolerance, $"{canvas}에서 종횡비가 깨졌다");
            }
        }

        [Test]
        public void 글자가_실린_종이는_어느_종횡비에서도_전부_보인다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Rect paper = NoticeBoardView.PaperRect(canvas);

                Assert.Greater(paper.xMin, 0f, $"{canvas}에서 종이 왼쪽이 화면 밖으로 나갔다");
                Assert.Less(paper.xMax, canvas.x, $"{canvas}에서 종이 오른쪽이 잘렸다");
                Assert.Greater(paper.yMin, 0f, $"{canvas}에서 종이 아래가 잘렸다");
                Assert.Less(paper.yMax, canvas.y, $"{canvas}에서 종이 위가 잘렸다");
            }
        }

        [Test]
        public void 받침은_화면_아래로_나가고_기둥_머리는_남는다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 size = NoticeBoardView.BoardSize(canvas);
                float centerY = NoticeBoardView.BoardCenter(canvas).y;

                Assert.Less(centerY - size.y * 0.5f, 0f,
                    $"{canvas}에서 받침이 다 보인다 — 어디에 서 있는지를 묻게 된다");
                Assert.Less(centerY + size.y * 0.5f, canvas.y,
                    $"{canvas}에서 기둥 머리가 잘렸다");
            }
        }

        [Test]
        public void 종이는_명판과_겹치지_않는다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 banner = MenuPlateLayout.BannerSize(canvas);
                float bannerLeft = MenuPlateLayout.BannerPosition(canvas).x - banner.x * 0.5f;
                float plateRight = bannerLeft + MenuPlateLayout.PlateRight * banner.x;

                Rect paper = NoticeBoardView.PaperRect(canvas);

                Assert.Less(plateRight, paper.xMin, $"{canvas}에서 명판과 공고문이 겹친다");
            }
        }

        [Test]
        public void 요약_문구_자리는_종이_안에_있다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Rect paper = NoticeBoardView.PaperRect(canvas);
                Rect summary = NoticeBoardView.SummaryRect(canvas);

                Assert.Greater(summary.xMin, paper.xMin, $"{canvas}에서 요약이 종이 왼쪽을 넘었다");
                Assert.Less(summary.xMax, paper.xMax, $"{canvas}에서 요약이 종이 오른쪽을 넘었다");
                Assert.Greater(summary.yMin, paper.yMin, $"{canvas}에서 요약이 종이 아래를 넘었다");
                Assert.Less(summary.yMax, paper.yMax, $"{canvas}에서 요약이 종이 위를 넘었다");
            }
        }

        [Test]
        public void 캔버스_높이가_0이어도_예외가_없다()
        {
            Assert.AreEqual(Vector2.zero, NoticeBoardView.BoardSize(new Vector2(100f, 0f)));
            Assert.AreEqual(Vector2.zero, NoticeBoardView.BoardSize(Vector2.zero));
            Assert.AreEqual(Rect.zero, NoticeBoardView.PaperRect(Vector2.zero));
            Assert.AreEqual(Rect.zero, NoticeBoardView.SummaryRect(Vector2.zero));
        }

        [Test]
        public void 버전_문자열은_비어_있지_않다()
        {
            Assert.IsNotEmpty(NoticeBoardView.Version);
        }
    }
}
