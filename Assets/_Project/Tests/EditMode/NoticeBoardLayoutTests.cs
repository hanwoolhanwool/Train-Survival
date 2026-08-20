using Game.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 운행 공고 종이의 자리 검증 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.4 · §8.2.
    ///
    /// <para>배너와 같은 규칙이다 — <b>높이로 폭을 정한다.</b> 폭 기준으로 잡으면 21:9에서 공고문이
    /// 같이 커져 화면을 파고들고, 4:3에서는 오른쪽으로 밀려 잘린다.</para>
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
        public void 어느_종횡비에서도_종이가_늘어나지_않는다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 size = NoticeBoardView.BoardSize(canvas);
                Assert.AreEqual(NoticeBoardView.Aspect, size.x / size.y, Tolerance, $"{canvas}에서 종횡비가 깨졌다");
            }
        }

        [Test]
        public void 종이는_화면_안에_전부_들어온다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 size = NoticeBoardView.BoardSize(canvas);
                float centerX = canvas.x + NoticeBoardView.BoardPosition(canvas).x;   // 앵커가 우변이다
                float left = centerX - size.x * 0.5f;
                float right = centerX + size.x * 0.5f;

                Assert.Greater(left, 0f, $"{canvas}에서 종이 왼쪽이 화면 밖으로 나갔다");
                Assert.Less(right, canvas.x, $"{canvas}에서 종이 오른쪽이 잘렸다");

                float centerY = canvas.y * NoticeBoardView.CenterY;
                Assert.Greater(centerY - size.y * 0.5f, 0f, $"{canvas}에서 종이 아래가 잘렸다");
                Assert.Less(centerY + size.y * 0.5f, canvas.y, $"{canvas}에서 종이 위가 잘렸다");
            }
        }

        [Test]
        public void 종이는_배너와_겹치지_않는다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 banner = MenuPlateLayout.BannerSize(canvas);
                float bannerRight = MenuPlateLayout.BannerPosition(canvas).x + banner.x * 0.5f;

                Vector2 size = NoticeBoardView.BoardSize(canvas);
                float noticeLeft = canvas.x + NoticeBoardView.BoardPosition(canvas).x - size.x * 0.5f;

                Assert.Less(bannerRight, noticeLeft, $"{canvas}에서 배너와 공고문이 겹친다");
            }
        }

        [Test]
        public void 캔버스_높이가_0이어도_예외가_없다()
        {
            Assert.AreEqual(Vector2.zero, NoticeBoardView.BoardSize(new Vector2(100f, 0f)));
            Assert.AreEqual(Vector2.zero, NoticeBoardView.BoardSize(Vector2.zero));
        }

        [Test]
        public void 버전_문자열은_비어_있지_않다()
        {
            Assert.IsNotEmpty(NoticeBoardView.Version);
        }
    }
}
