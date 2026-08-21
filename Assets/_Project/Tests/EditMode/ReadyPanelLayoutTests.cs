using Game.UI.Ready;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 준비 화면 두 패널의 자리 검증 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §9.1.
    ///
    /// <para>지키려는 것은 셋이다 — <b>서로 겹치지 않는다</b>, <b>패널 밖으로 나가지 않는다</b>,
    /// 그리고 <b>화면이 넓어져도 패널이 화면을 삼키지 않는다</b>. 세 번째가 패널 크기를
    /// 폭이 아니라 높이로 정하는 이유다.</para>
    ///
    /// <para>배너 명판(<c>MenuPlateLayoutTests</c>)과 달리 <b>원근을 요구하지 않는다.</b>
    /// 이 패널은 정면 뷰라 칸 높이가 거의 같고, 남은 5 % 편차는 원근이 아니라
    /// 생성 이미지의 흔들림이다(§0.2).</para>
    /// </summary>
    public sealed class ReadyPanelLayoutTests
    {
        private const float Tolerance = 0.0005f;

        private static readonly Vector2[] Canvases =
        {
            new Vector2(1920f, 1080f),      // 16:9
            new Vector2(2560f, 1440f),      // 16:9 고해상도
            new Vector2(1920f, 1200f),      // 16:10
            new Vector2(2560f, 1080f),      // 21:9
            new Vector2(1280f, 1024f),      // 5:4
        };

        [Test]
        public void 칸은_서로_겹치지_않는다()
        {
            for (int i = 0; i < ReadyPanelLayout.SlotCount - 1; i++)
            {
                Assert.Less(ReadyPanelLayout.SlotBottom(i), ReadyPanelLayout.SlotTop(i + 1),
                    $"칸 {i}의 밑변이 칸 {i + 1}의 윗변보다 아래에 있다");
            }
        }

        [Test]
        public void 칸은_위에서_아래로_정렬돼_있다()
        {
            for (int i = 0; i < ReadyPanelLayout.SlotCount; i++)
            {
                Assert.Greater(ReadyPanelLayout.SlotHeight(i), 0f, $"칸 {i}의 높이가 0 이하다");
            }
        }

        [Test]
        public void 모든_칸이_로스터_안에_있다()
        {
            for (int i = 0; i < ReadyPanelLayout.SlotCount; i++)
            {
                Vector2 min = ReadyPanelLayout.SlotAnchorMin(i);
                Vector2 max = ReadyPanelLayout.SlotAnchorMax(i);

                Assert.GreaterOrEqual(min.x, 0f, $"칸 {i}이 왼쪽으로 삐져나갔다");
                Assert.LessOrEqual(max.x, 1f, $"칸 {i}이 오른쪽으로 삐져나갔다");
                Assert.GreaterOrEqual(min.y, 0f, $"칸 {i}이 아래로 삐져나갔다");
                Assert.LessOrEqual(max.y, 1f, $"칸 {i}이 위로 삐져나갔다");
                Assert.Less(min.x, max.x, $"칸 {i}의 좌우가 뒤집혔다");
                Assert.Less(min.y, max.y, $"칸 {i}의 상하가 뒤집혔다");
            }
        }

        [Test]
        public void 칸_높이는_사실상_균등하다_정면뷰()
        {
            // 배너 명판과 반대다. 여기서 큰 편차가 생기면 그림을 잘못 실측한 것이다.
            float min = float.MaxValue;
            float max = float.MinValue;
            for (int i = 0; i < ReadyPanelLayout.SlotCount; i++)
            {
                float h = ReadyPanelLayout.SlotHeight(i);
                min = Mathf.Min(min, h);
                max = Mathf.Max(max, h);
            }

            Assert.Less(max / min, 1.12f, $"칸 높이 편차가 12 %를 넘는다 ({min:F4} ~ {max:F4})");
        }

        [Test]
        public void 칸_밖으로_나가는_조각이_없다()
        {
            Rect[] parts =
            {
                ReadyPanelLayout.SlotIcon,
                ReadyPanelLayout.SlotRole,
                ReadyPanelLayout.SlotName,
                ReadyPanelLayout.SlotEmptyLabel,
            };

            foreach (Rect part in parts)
            {
                AssertInsideUnitSquare(part, "칸 안 조각");
            }
        }

        [Test]
        public void 아이콘은_이름보다_왼쪽에_있다()
        {
            Assert.LessOrEqual(ReadyPanelLayout.SlotIcon.xMax, ReadyPanelLayout.SlotName.xMin + Tolerance,
                "아이콘이 이름 위로 올라탄다");

            // 접속 표시를 지운 뒤(2026-08-22) 이름이 오른쪽 끝까지 넓어졌다 — 칸 안에는 남아야 한다.
            Assert.LessOrEqual(ReadyPanelLayout.SlotName.xMax, 1f + Tolerance, "이름이 칸 밖으로 나간다");
        }

        [Test]
        public void 역할_줄은_이름_위에_있다()
        {
            // 앵커는 아래가 원점이라 "위"가 값이 크다.
            Assert.Greater(ReadyPanelLayout.SlotRole.yMin, ReadyPanelLayout.SlotName.yMax - Tolerance,
                "HOST 줄이 이름과 겹친다");
        }

        [Test]
        public void 조작_패널_요소는_서로_겹치지_않는다()
        {
            Rect prev = ReadyPanelLayout.DifficultyPrev;
            Rect value = ReadyPanelLayout.DifficultyValue;
            Rect next = ReadyPanelLayout.DifficultyNext;

            Assert.LessOrEqual(prev.xMax, value.xMin + Tolerance, "◀ 가 값 박스와 겹친다");
            Assert.LessOrEqual(value.xMax, next.xMin + Tolerance, "값 박스가 ▶ 와 겹친다");

            Rect[] stacked =
            {
                ReadyPanelLayout.DifficultyLabel,
                ReadyPanelLayout.DifficultyValue,
                ReadyPanelLayout.StartButton,
                ReadyPanelLayout.InviteButton,
                ReadyPanelLayout.LeaveButton,
            };

            for (int i = 0; i < stacked.Length - 1; i++)
            {
                Assert.GreaterOrEqual(stacked[i].yMin, stacked[i + 1].yMax - Tolerance,
                    $"조작 패널 {i}번 요소가 아래 요소와 겹친다");
            }
        }

        [Test]
        public void 조작_패널_요소가_패널_밖으로_나가지_않는다()
        {
            Rect[] parts =
            {
                ReadyPanelLayout.DifficultyLabel,
                ReadyPanelLayout.DifficultyPrev,
                ReadyPanelLayout.DifficultyValue,
                ReadyPanelLayout.DifficultyNext,
                ReadyPanelLayout.StartButton,
                ReadyPanelLayout.InviteButton,
                ReadyPanelLayout.LeaveButton,
            };

            foreach (Rect part in parts)
            {
                AssertInsideUnitSquare(part, "조작 패널 요소");
            }
        }

        [Test]
        public void 게임_시작이_가장_큰_버튼이다()
        {
            // 시안에서 크기·금색·발광이 전부 이 버튼에 몰려 있다(§5.2-4).
            float start = Area(ReadyPanelLayout.StartButton);
            Assert.Greater(start, Area(ReadyPanelLayout.InviteButton), "초대 하기가 게임 시작보다 크다");
            Assert.Greater(start, Area(ReadyPanelLayout.LeaveButton), "나가기가 게임 시작보다 크다");
        }

        [Test]
        public void 패널은_화면이_넓어져도_늘어나지_않는다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 roster = ReadyPanelLayout.RosterSize(canvas);
                Vector2 controls = ReadyPanelLayout.ControlsSize(canvas);

                Assert.AreEqual(ReadyPanelLayout.RosterAspect, roster.x / roster.y, 0.001f,
                    $"{canvas} 에서 로스터의 가로세로비가 흐트러졌다");
                Assert.AreEqual(ReadyPanelLayout.ControlsAspect, controls.x / controls.y, 0.001f,
                    $"{canvas} 에서 조작 패널의 가로세로비가 흐트러졌다");

                Assert.AreEqual(canvas.y * ReadyPanelLayout.RosterHeightScale, roster.y, 0.01f,
                    $"{canvas} 에서 로스터 높이가 화면 높이에 비례하지 않는다");
            }
        }

        [Test]
        public void 두_패널은_어떤_화면비에서도_겹치지_않는다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 rosterSize = ReadyPanelLayout.RosterSize(canvas);
                Vector2 controlsSize = ReadyPanelLayout.ControlsSize(canvas);
                float rosterRight = ReadyPanelLayout.RosterPosition(canvas).x + rosterSize.x * 0.5f;
                float controlsLeft = canvas.x + ReadyPanelLayout.ControlsPosition(canvas).x - controlsSize.x * 0.5f;

                Assert.Less(rosterRight, controlsLeft,
                    $"{canvas} 에서 로스터 오른쪽 끝({rosterRight:F1})이 조작 패널 왼쪽({controlsLeft:F1})을 넘는다");
            }
        }

        [Test]
        public void 두_패널은_어떤_화면비에서도_화면_안에_있다()
        {
            foreach (Vector2 canvas in Canvases)
            {
                Vector2 rosterSize = ReadyPanelLayout.RosterSize(canvas);
                Vector2 controlsSize = ReadyPanelLayout.ControlsSize(canvas);

                Assert.LessOrEqual(rosterSize.y, canvas.y, $"{canvas} 에서 로스터가 화면보다 높다");
                Assert.LessOrEqual(controlsSize.y, canvas.y, $"{canvas} 에서 조작 패널이 화면보다 높다");

                float rosterLeft = ReadyPanelLayout.RosterPosition(canvas).x - rosterSize.x * 0.5f;
                Assert.GreaterOrEqual(rosterLeft, -1f, $"{canvas} 에서 로스터가 화면 왼쪽으로 잘린다");

                float controlsRight = canvas.x + ReadyPanelLayout.ControlsPosition(canvas).x + controlsSize.x * 0.5f;
                Assert.LessOrEqual(controlsRight, canvas.x + 1f, $"{canvas} 에서 조작 패널이 화면 오른쪽으로 잘린다");
            }
        }

        [Test]
        public void 타이틀은_첫_칸_위에_있다()
        {
            Assert.Greater(ReadyPanelLayout.RosterTitle.yMin, ReadyPanelLayout.SlotAnchorMax(0).y - Tolerance,
                "타이틀이 첫 칸을 덮는다");
            AssertInsideUnitSquare(ReadyPanelLayout.RosterTitle, "타이틀");
        }

        [Test]
        public void 칸_연출은_커지는_방향으로만_움직인다()
        {
            // 칸은 프레임 그림에 구워진 빈 칸 위에 정확히 겹쳐 있다. 1보다 작게 줄이면
            // 밑에 깔린 그림의 테두리가 삐져나온다 — 배너 명판에서 실제로 겪은 함정이다
            // (로비 계획 §4.2-3). "0.96에서 커지며 나타난다"로 고치고 싶어지는 자리라 못박는다.
            Assert.Greater(ReadySlotView.TransitionScale, 1f, "칸 연출이 밑그림을 드러낸다");
            Assert.Greater(ReadySlotView.TransitionSeconds, 0f, "연출 시간이 0 이하면 변화를 볼 틈이 없다");
        }

        private static float Area(Rect r)
        {
            return r.width * r.height;
        }

        private static void AssertInsideUnitSquare(Rect r, string what)
        {
            Assert.GreaterOrEqual(r.xMin, 0f, $"{what}이 왼쪽으로 삐져나갔다 ({r})");
            Assert.GreaterOrEqual(r.yMin, 0f, $"{what}이 아래로 삐져나갔다 ({r})");
            Assert.LessOrEqual(r.xMax, 1f, $"{what}이 오른쪽으로 삐져나갔다 ({r})");
            Assert.LessOrEqual(r.yMax, 1f, $"{what}이 위로 삐져나갔다 ({r})");
            Assert.Greater(r.width, 0f, $"{what}의 폭이 0 이하다 ({r})");
            Assert.Greater(r.height, 0f, $"{what}의 높이가 0 이하다 ({r})");
        }
    }
}
