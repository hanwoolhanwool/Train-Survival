using Game.UI.Ready;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 패널 등장 곡선 검증 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §9.1.
    ///
    /// <para>고정할 것은 셋이다 — <b>출발과 도착이 정확하다</b>, <b>반동이 있다</b>,
    /// 그리고 <b>반동이 아주 살짝이다.</b></para>
    ///
    /// <para>마지막이 이 시험의 이유다. 표준 <c>easeOutBack</c> 계수를 그대로 쓰면 10 % 가까이
    /// 튀어 <b>무쇠 간판이 고무처럼 보인다.</b> "조금 더 시원하게"라며 계수를 올리고 싶어지는
    /// 자리라 상한을 숫자로 박아 둔다.</para>
    /// </summary>
    public sealed class ReadyPanelSlideMathTests
    {
        private const float Tolerance = 0.0005f;

        [Test]
        public void 출발과_도착이_정확하다()
        {
            // 여기가 어긋나면 패널이 제자리에 안 붙거나, 시작부터 화면 안에 들어와 있다.
            Assert.AreEqual(0f, ReadyPanelSlideMath.Ease(0f, ReadyPanelSlideMath.DefaultBack), Tolerance);
            Assert.AreEqual(1f, ReadyPanelSlideMath.Ease(1f, ReadyPanelSlideMath.DefaultBack), Tolerance);
        }

        [Test]
        public void 반동이_없으면_1을_넘지_않는다()
        {
            for (int i = 0; i <= 100; i++)
            {
                float e = ReadyPanelSlideMath.Ease(i / 100f, 0f);
                Assert.LessOrEqual(e, 1f + Tolerance, "반동 0인데 제자리를 지나쳤다");
            }
        }

        [Test]
        public void 기본_반동은_제자리를_지나친다()
        {
            float peak = 0f;
            for (int i = 0; i <= 100; i++)
            {
                peak = Mathf.Max(peak, ReadyPanelSlideMath.Ease(i / 100f, ReadyPanelSlideMath.DefaultBack));
            }

            Assert.Greater(peak, 1f, "반동이 아예 없다 — 미끄러져 붙는 게 아니라 그냥 나타난다");
        }

        [Test]
        public void 반동은_아주_살짝이다()
        {
            float peak = 0f;
            for (int i = 0; i <= 1000; i++)
            {
                peak = Mathf.Max(peak, ReadyPanelSlideMath.Ease(i / 1000f, ReadyPanelSlideMath.DefaultBack));
            }

            // 5 %를 넘으면 무쇠 간판이 고무처럼 보인다.
            Assert.Less(peak, 1.05f, "반동이 과하다 (" + peak.ToString("0.000") + ")");
        }

        [Test]
        public void 범위_밖_입력을_접어_넣는다()
        {
            Assert.AreEqual(0f, ReadyPanelSlideMath.Ease(-3f, ReadyPanelSlideMath.DefaultBack), Tolerance);
            Assert.AreEqual(1f, ReadyPanelSlideMath.Ease(7f, ReadyPanelSlideMath.DefaultBack), Tolerance);
        }

        [Test]
        public void 음수_반동은_0으로_본다()
        {
            // 계수를 음수로 두면 곡선이 뒤로 물러났다 오는 이상한 모양이 된다.
            for (int i = 0; i <= 20; i++)
            {
                float t = i / 20f;
                Assert.AreEqual(ReadyPanelSlideMath.Ease(t, 0f), ReadyPanelSlideMath.Ease(t, -5f), Tolerance);
            }
        }

        [Test]
        public void 남은_거리는_출발에서_1_도착에서_0이다()
        {
            Assert.AreEqual(1f, ReadyPanelSlideMath.Remaining(0f, ReadyPanelSlideMath.DefaultBack), Tolerance);
            Assert.AreEqual(0f, ReadyPanelSlideMath.Remaining(1f, ReadyPanelSlideMath.DefaultBack), Tolerance);
        }

        [Test]
        public void 강조_배율은_커지는_방향이다()
        {
            // 버튼도 패널 그림 위에 겹쳐 있다 — 1보다 작아지면 밑그림 테두리가 삐져나온다.
            Assert.Greater(ReadyButtonAccent.HoverScale, 1f, "호버가 버튼을 줄인다");
            Assert.Less(ReadyButtonAccent.RestTint, ReadyButtonAccent.HoverTint,
                "쉬는 상태가 강조보다 밝다 — 마우스를 올려도 변화가 없다");
            Assert.LessOrEqual(ReadyButtonAccent.HoverTint, 1f,
                "1을 넘는 밝기는 uGUI 정점 색(Color32)에서 잘려 아무 일도 일어나지 않는다");
        }
    }
}
