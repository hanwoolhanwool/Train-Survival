using Game.UI;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 임계 시 등장 줄의 등퇴장 검증 (비주얼·UI/UX 가이드 §9.2 B계층).
    /// 지키는 것은 <b>정상 범위에서는 보이지 않는다</b>, <b>유예를 두고 사라진다</b>,
    /// <b>임계값 근처에서 깜빡이지 않는다</b> 셋이다.
    /// </summary>
    public sealed class HudTransientFadeTests
    {
        private const float Step = 1f / 60f;

        private HudTransientFade _fade;

        [SetUp]
        public void SetUp()
        {
            _fade = new HudTransientFade();
        }

        [Test]
        public void 처음에는_보이지_않는다()
        {
            Assert.AreEqual(0f, _fade.Evaluate(false, 0f, Step));
            Assert.IsFalse(_fade.IsVisible);
        }

        [Test]
        public void 비정상이_되면_등장한다()
        {
            Advance(stressed: true, from: 0f, seconds: HudTransientFade.RiseSeconds);

            Assert.AreEqual(1f, _fade.Alpha, 0.001f);
            Assert.IsTrue(_fade.IsVisible);
        }

        [Test]
        public void 등장은_퇴장보다_빠르다()
        {
            Assert.That(HudTransientFade.RiseSeconds, Is.LessThan(HudTransientFade.FadeSeconds),
                "위험은 먼저 눈에 들어와야 한다.");
        }

        /// <summary>가이드 §9.2 — "안전 복귀 시 2초 후 페이드 아웃".</summary>
        [Test]
        public void 안전으로_돌아와도_유예_동안은_남는다()
        {
            float now = Advance(stressed: true, from: 0f, seconds: 1f);

            // 유예가 끝나기 직전까지는 완전히 보인다.
            now = Advance(stressed: false, from: now, seconds: HudTransientFade.GraceSeconds - 0.1f);

            Assert.AreEqual(1f, _fade.Alpha, 0.001f);
        }

        [Test]
        public void 유예가_끝나면_사라진다()
        {
            float now = Advance(stressed: true, from: 0f, seconds: 1f);
            now = Advance(stressed: false, from: now,
                seconds: HudTransientFade.GraceSeconds + HudTransientFade.FadeSeconds + 0.1f);

            Assert.AreEqual(0f, _fade.Alpha, 0.001f);
            Assert.IsFalse(_fade.IsVisible);
        }

        /// <summary>
        /// 임계값 근처에서 값이 흔들려도 줄이 깜빡이면 안 된다 — 유예가 그것을 막는 장치다.
        /// 매 프레임 상태가 뒤집혀도 불투명도는 계속 1이어야 한다.
        /// </summary>
        [Test]
        public void 임계값_근처에서_깜빡이지_않는다()
        {
            float now = Advance(stressed: true, from: 0f, seconds: 1f);

            for (int i = 0; i < 60; i++)
            {
                now += Step;
                _fade.Evaluate(i % 2 == 0, now, Step);
            }

            Assert.AreEqual(1f, _fade.Alpha, 0.001f, "1초 동안 매 프레임 뒤집혔지만 계속 보여야 한다.");
        }

        [Test]
        public void 사라지는_도중_다시_비정상이_되면_되돌아온다()
        {
            float now = Advance(stressed: true, from: 0f, seconds: 1f);
            now = Advance(stressed: false, from: now,
                seconds: HudTransientFade.GraceSeconds + HudTransientFade.FadeSeconds * 0.5f);

            Assert.That(_fade.Alpha, Is.GreaterThan(0f).And.LessThan(1f), "절반쯤 사라진 상태여야 한다.");

            Advance(stressed: true, from: now, seconds: HudTransientFade.RiseSeconds);
            Assert.AreEqual(1f, _fade.Alpha, 0.001f);
        }

        [Test]
        public void Reset은_즉시_감춘다()
        {
            Advance(stressed: true, from: 0f, seconds: 1f);
            _fade.Reset();

            Assert.AreEqual(0f, _fade.Alpha);
            Assert.IsFalse(_fade.IsVisible);
        }

        /// <summary>불투명도는 어떤 경우에도 0~1을 벗어나지 않는다 (GUI.color에 그대로 들어간다).</summary>
        [Test]
        public void 불투명도는_항상_0과_1_사이다()
        {
            float now = 0f;
            for (int i = 0; i < 600; i++)
            {
                now += Step;
                float alpha = _fade.Evaluate(i % 97 < 30, now, Step);
                Assert.That(alpha, Is.InRange(0f, 1f));
            }
        }

        /// <summary>진행한 뒤의 시각을 돌려준다.</summary>
        private float Advance(bool stressed, float from, float seconds)
        {
            float now = from;
            int steps = Mathf.CeilToInt(seconds / Step);

            for (int i = 0; i < steps; i++)
            {
                now += Step;
                _fade.Evaluate(stressed, now, Step);
            }

            return now;
        }
    }
}
