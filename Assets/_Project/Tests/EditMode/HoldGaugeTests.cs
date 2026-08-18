using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 홀드 게이지 검증 (건축 개편 2·3차 — 결정 ④: 짧은 홀드 + 게이지로 오철거 방지).
    /// 건축물 철거와 판자 철거가 이 상태 기계 하나를 공유하므로, 리셋 규약을 여기서 못 박는다.
    /// </summary>
    public sealed class HoldGaugeTests
    {
        private const float Hold = 0.5f;

        [Test]
        public void 홀드를_채우면_한_번만_완료된다()
        {
            var gauge = new HoldGauge();

            Assert.That(gauge.Update(true, 1, Hold, 0.2f, out bool completed), Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(completed, Is.False);

            gauge.Update(true, 1, Hold, 0.2f, out completed);
            Assert.That(completed, Is.False, "0.4초 — 아직");

            Assert.That(gauge.Update(true, 1, Hold, 0.2f, out completed), Is.EqualTo(0f), "완료 프레임은 0");
            Assert.That(completed, Is.True);

            // 완료 후 누적이 비어 다음 홀드가 처음부터 시작한다 (연타로 즉시 재발동하지 않는다).
            Assert.That(gauge.Update(true, 1, Hold, 0.2f, out completed), Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(completed, Is.False);
        }

        [Test]
        public void 키를_놓으면_누적이_버려진다()
        {
            var gauge = new HoldGauge();
            gauge.Update(true, 1, Hold, 0.4f, out _);

            Assert.That(gauge.Update(false, 1, Hold, 0.4f, out bool completed), Is.EqualTo(0f));
            Assert.That(completed, Is.False);

            // 다시 눌러도 이어지지 않는다.
            Assert.That(gauge.Update(true, 1, Hold, 0.4f, out completed), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(completed, Is.False, "0.4초부터 다시 — 이어졌다면 즉시 완료였을 것");
        }

        [Test]
        public void 표적이_바뀌면_누적이_버려진다()
        {
            var gauge = new HoldGauge();
            gauge.Update(true, 1, Hold, 0.4f, out _);

            Assert.That(gauge.Update(true, 2, Hold, 0.4f, out bool completed), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(completed, Is.False, "다른 표적으로 옮기면 처음부터");
        }

        [Test]
        public void 명시적_리셋과_비정상_홀드시간은_진행을_지운다()
        {
            var gauge = new HoldGauge();
            gauge.Update(true, 1, Hold, 0.4f, out _);
            gauge.Reset();

            Assert.That(gauge.Update(true, 1, Hold, 0.4f, out bool completed), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(completed, Is.False);

            Assert.That(gauge.Update(true, 1, 0f, 0.4f, out completed), Is.EqualTo(0f), "홀드 시간 0 = 비활성");
            Assert.That(completed, Is.False);
        }
    }
}
