using Game.Gameplay.World;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>지역 자원 가중 추첨 검증 (기획서 §4 — 지역별 자원 종류, M5).</summary>
    public sealed class ResourceSpawnPickerTests
    {
        [Test]
        public void 가중치_구간에_비례해_뽑힌다()
        {
            float[] weights = { 0.7f, 0.3f };

            Assert.That(ResourceSpawnPicker.Pick(weights, 0f), Is.EqualTo(0));
            Assert.That(ResourceSpawnPicker.Pick(weights, 0.69f), Is.EqualTo(0));
            Assert.That(ResourceSpawnPicker.Pick(weights, 0.71f), Is.EqualTo(1));
            Assert.That(ResourceSpawnPicker.Pick(weights, 0.99f), Is.EqualTo(1));
        }

        [Test]
        public void 가중치_합이_1이_아니어도_비율로_동작한다()
        {
            float[] weights = { 6f, 2f };

            Assert.That(ResourceSpawnPicker.Pick(weights, 0.74f), Is.EqualTo(0), "6/8 = 0.75 미만은 첫 항목");
            Assert.That(ResourceSpawnPicker.Pick(weights, 0.76f), Is.EqualTo(1));
        }

        [Test]
        public void 가중치_0인_항목은_건너뛴다()
        {
            float[] weights = { 0f, 1f, 0f };

            Assert.That(ResourceSpawnPicker.Pick(weights, 0f), Is.EqualTo(1));
            Assert.That(ResourceSpawnPicker.Pick(weights, 0.99f), Is.EqualTo(1));
        }

        [Test]
        public void 유효_가중치가_없으면_실패를_알린다()
        {
            Assert.That(ResourceSpawnPicker.Pick(null, 0.5f), Is.EqualTo(-1));
            Assert.That(ResourceSpawnPicker.Pick(new float[0], 0.5f), Is.EqualTo(-1));
            Assert.That(ResourceSpawnPicker.Pick(new[] { 0f, 0f }, 0.5f), Is.EqualTo(-1));
        }

        [Test]
        public void 경계_밖_난수도_안전하게_사상된다()
        {
            float[] weights = { 0.5f, 0.5f };

            Assert.That(ResourceSpawnPicker.Pick(weights, -0.2f), Is.EqualTo(0), "음수는 0으로");
            Assert.That(ResourceSpawnPicker.Pick(weights, 1f), Is.EqualTo(1), "1 이상은 마지막 구간으로");
        }

        [Test]
        public void 단일_항목은_항상_그_항목이다()
        {
            float[] weights = { 3f };

            Assert.That(ResourceSpawnPicker.Pick(weights, 0f), Is.EqualTo(0));
            Assert.That(ResourceSpawnPicker.Pick(weights, 0.99f), Is.EqualTo(0));
        }
    }
}
