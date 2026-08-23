using Game.Gameplay.World;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>화구 화염 세기 계산 검증 (화구 연료구 교체 계획 §6).</summary>
    public sealed class FireboxFlameMathTests
    {
        [Test]
        public void 잔량이_가득이면_최대_세기_바닥이면_잉걸불이다()
        {
            Assert.That(FireboxFlameMath.ComputeBaseIntensity(100f, 100f, 0.15f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(FireboxFlameMath.ComputeBaseIntensity(0f, 100f, 0.15f), Is.EqualTo(0.15f).Within(0.001f));
        }

        [Test]
        public void 기본_세기는_잔량에_비례한다()
        {
            Assert.That(FireboxFlameMath.ComputeBaseIntensity(50f, 100f, 0f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(FireboxFlameMath.ComputeBaseIntensity(50f, 100f, 0.2f), Is.EqualTo(0.6f).Within(0.001f));
        }

        [Test]
        public void 저장량이_0이면_잉걸불로_폴백한다()
        {
            // 설정 미배선 — 잔량을 알 수 없으므로 꺼지지도 타오르지도 않는다.
            Assert.That(FireboxFlameMath.ComputeBaseIntensity(50f, 0f, 0.15f), Is.EqualTo(0.15f).Within(0.001f));
        }

        [Test]
        public void 잔량이_저장량을_넘어도_세기는_1을_넘지_않는다()
        {
            Assert.That(FireboxFlameMath.ComputeBaseIntensity(200f, 100f, 0.15f), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void 버스트는_투입_순간에_최대이고_지속이_끝나면_사라진다()
        {
            Assert.That(FireboxFlameMath.ComputeBurstFactor(0f, 1f, 0.8f), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(FireboxFlameMath.ComputeBurstFactor(1f, 1f, 0.8f), Is.EqualTo(0f));
            Assert.That(FireboxFlameMath.ComputeBurstFactor(2f, 1f, 0.8f), Is.EqualTo(0f));
        }

        [Test]
        public void 버스트는_시간이_갈수록_단조_감소한다()
        {
            float previous = float.MaxValue;
            for (int i = 0; i <= 20; i++)
            {
                float value = FireboxFlameMath.ComputeBurstFactor(i * 0.05f, 1f, 0.8f);
                Assert.That(value, Is.LessThan(previous));
                previous = value;
            }
        }

        [Test]
        public void 비정상_경과와_지속은_버스트_없음으로_처리된다()
        {
            Assert.That(FireboxFlameMath.ComputeBurstFactor(-1f, 1f, 0.8f), Is.EqualTo(0f));
            Assert.That(FireboxFlameMath.ComputeBurstFactor(0.5f, 0f, 0.8f), Is.EqualTo(0f));
            Assert.That(FireboxFlameMath.ComputeBurstFactor(0.5f, -1f, 0.8f), Is.EqualTo(0f));
        }

        [Test]
        public void 발열량이_클수록_버스트가_크고_상한에서_잘린다()
        {
            Assert.That(FireboxFlameMath.ComputeBurstPeak(5f, 10f, 0.8f), Is.EqualTo(0.4f).Within(0.001f));
            Assert.That(FireboxFlameMath.ComputeBurstPeak(10f, 10f, 0.8f), Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(FireboxFlameMath.ComputeBurstPeak(100f, 10f, 0.8f), Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void 기준_발열량이_0이면_최대_버스트로_본다()
        {
            // 카탈로그 미배선 — 종류를 구분할 수 없으니 연출을 죽이지 않는다.
            Assert.That(FireboxFlameMath.ComputeBurstPeak(5f, 0f, 0.8f), Is.EqualTo(0.8f).Within(0.001f));
        }

        [Test]
        public void 합성_세기는_0과_1_사이로_잘린다()
        {
            Assert.That(FireboxFlameMath.ComposeIntensity(0.7f, 0.8f), Is.EqualTo(1f).Within(0.001f));
            Assert.That(FireboxFlameMath.ComposeIntensity(0.3f, 0.2f), Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(FireboxFlameMath.ComposeIntensity(-1f, 0f), Is.EqualTo(0f));
        }
    }
}
