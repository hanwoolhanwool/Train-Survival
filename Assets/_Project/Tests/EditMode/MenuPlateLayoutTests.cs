using Game.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 명판 4장의 자리 검증 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §8.1.
    ///
    /// <para>지키려는 것은 셋이다 — <b>서로 겹치지 않는다</b>, <b>배너 밖으로 나가지 않는다</b>,
    /// 그리고 <b>원근이 살아 있다</b>(아래 슬롯일수록 명판이 낮다). 세 번째가 이 표를 실측으로
    /// 두는 이유다. 균등 배치로 바꾸면 이 테스트가 먼저 깨진다.</para>
    /// </summary>
    public sealed class MenuPlateLayoutTests
    {
        private const float Tolerance = 0.0005f;

        [Test]
        public void 슬롯은_서로_겹치지_않는다()
        {
            for (int i = 0; i < MenuPlateLayout.SlotCount - 1; i++)
            {
                Assert.Less(MenuPlateLayout.Bottom(i), MenuPlateLayout.Top(i + 1),
                    $"슬롯 {i}의 밑변이 슬롯 {i + 1}의 윗변보다 아래에 있다");
            }
        }

        [Test]
        public void 슬롯은_위에서_아래로_정렬돼_있다()
        {
            for (int i = 0; i < MenuPlateLayout.SlotCount; i++)
            {
                Assert.Less(MenuPlateLayout.Top(i), MenuPlateLayout.Bottom(i), $"슬롯 {i}의 높이가 0 이하다");
                Assert.Greater(MenuPlateLayout.Height(i), 0f);
            }

            for (int i = 0; i < MenuPlateLayout.SlotCount - 1; i++)
            {
                Assert.Less(MenuPlateLayout.Center(i), MenuPlateLayout.Center(i + 1));
            }
        }

        [Test]
        public void 아래_슬롯일수록_명판이_낮다_원근()
        {
            // 균등 배치(VerticalLayoutGroup)로 바꾸면 높이가 모두 같아져 여기서 걸린다.
            for (int i = 0; i < MenuPlateLayout.SlotCount - 1; i++)
            {
                Assert.Less(MenuPlateLayout.Height(i + 1), MenuPlateLayout.Height(i) + Tolerance,
                    $"슬롯 {i + 1}이 슬롯 {i}보다 높다 — 원근이 뒤집혔다");
            }

            Assert.Greater(MenuPlateLayout.Height(0) - MenuPlateLayout.Height(3), 0.001f,
                "첫 슬롯과 마지막 슬롯의 높이가 사실상 같다 — 원근이 사라졌다");
        }

        [Test]
        public void 모든_슬롯이_배너_안에_있다()
        {
            for (int i = 0; i < MenuPlateLayout.SlotCount; i++)
            {
                Assert.GreaterOrEqual(MenuPlateLayout.Top(i), 0f);
                Assert.LessOrEqual(MenuPlateLayout.Bottom(i), 1f);
            }

            Assert.Greater(MenuPlateLayout.PlateLeft, 0f);
            Assert.Less(MenuPlateLayout.PlateRight, 1f);
            Assert.Less(MenuPlateLayout.PlateLeft, MenuPlateLayout.PlateRight);
        }

        [Test]
        public void 앵커는_y를_뒤집어_돌려준다()
        {
            for (int i = 0; i < MenuPlateLayout.SlotCount; i++)
            {
                Vector2 min = MenuPlateLayout.ToAnchorMin(i);
                Vector2 max = MenuPlateLayout.ToAnchorMax(i);

                Assert.Less(min.x, max.x, $"슬롯 {i} 가로 앵커가 뒤집혔다");
                Assert.Less(min.y, max.y, $"슬롯 {i} 세로 앵커가 뒤집혔다");
                Assert.AreEqual(1f - MenuPlateLayout.Bottom(i), min.y, Tolerance);
                Assert.AreEqual(1f - MenuPlateLayout.Top(i), max.y, Tolerance);
            }

            // 위 슬롯이 앵커에서도 위에 있어야 한다 (유니티는 아래가 원점이라 y가 더 크다)
            Assert.Greater(MenuPlateLayout.ToAnchorMin(0).y, MenuPlateLayout.ToAnchorMin(3).y);
        }

        [Test]
        public void 화살표는_명판_오른쪽에_슬롯_중심으로_붙는다()
        {
            for (int i = 0; i < MenuPlateLayout.SlotCount; i++)
            {
                Vector2 min = MenuPlateLayout.ArrowAnchorMin(i);
                Vector2 max = MenuPlateLayout.ArrowAnchorMax(i);

                Assert.GreaterOrEqual(min.x, MenuPlateLayout.PlateRight - 0.01f, "화살표가 명판 안으로 들어왔다");
                Assert.Less(min.y, max.y);

                float arrowCenter = 1f - (min.y + max.y) * 0.5f;
                Assert.AreEqual(MenuPlateLayout.Center(i), arrowCenter, Tolerance,
                    $"슬롯 {i} 화살표가 명판 중심과 어긋났다");
            }
        }

        /// <summary>계획 §8.2가 요구하는 종횡비 4종.</summary>
        private static readonly Vector2[] Canvases =
        {
            new Vector2(1920f, 1080f),  // 16:9 기준
            new Vector2(2217f, 935f),   // 21:9 (CanvasScaler 환산 후)
            new Vector2(1663f, 1247f),  // 4:3  (환산 후)
            new Vector2(1920f, 1200f),  // 16:10
        };

        [Test]
        public void 배너는_어느_종횡비에서도_늘어나지_않는다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 size = MenuPlateLayout.BannerSize(canvas);
                Assert.AreEqual(MenuPlateLayout.BannerAspect, size.x / size.y, Tolerance,
                    $"{canvas}에서 배너 종횡비가 깨졌다");
            }
        }

        [Test]
        public void 배너는_화면보다_커서_위아래가_잘린다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 size = MenuPlateLayout.BannerSize(canvas);
                Assert.Greater(size.y, canvas.y, $"{canvas}에서 배너가 화면 안에 들어와 버렸다");
            }
        }

        [Test]
        public void 배너_왼쪽은_화면_밖으로_나가고_명판은_안에_남는다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 size = MenuPlateLayout.BannerSize(canvas);
                float centerX = MenuPlateLayout.BannerPosition(canvas).x;   // 캔버스 좌변 기준
                float left = centerX - size.x * 0.5f;

                Assert.Less(left, 0f, $"{canvas}에서 배너 왼쪽이 화면 안에 있다 — 잘린 느낌이 사라진다");

                float plateLeft = left + size.x * MenuPlateLayout.PlateLeft;
                float plateRight = left + size.x * MenuPlateLayout.PlateRight;
                Assert.Greater(plateLeft, 0f, $"{canvas}에서 명판 왼쪽이 화면 밖으로 나갔다");
                Assert.Less(plateRight, canvas.x, $"{canvas}에서 명판 오른쪽이 화면 밖으로 나갔다");
            }
        }

        [Test]
        public void 캔버스_높이가_0이어도_예외가_없다()
        {
            Assert.AreEqual(Vector2.zero, MenuPlateLayout.BannerSize(new Vector2(100f, 0f)));
            Assert.AreEqual(Vector2.zero, MenuPlateLayout.BannerSize(Vector2.zero));
        }

        [Test]
        public void 범위를_벗어난_슬롯은_양끝으로_잘린다()
        {
            Assert.AreEqual(MenuPlateLayout.Top(0), MenuPlateLayout.Top(-5), Tolerance);
            Assert.AreEqual(MenuPlateLayout.Bottom(MenuPlateLayout.SlotCount - 1), MenuPlateLayout.Bottom(99), Tolerance);
        }
    }
}
