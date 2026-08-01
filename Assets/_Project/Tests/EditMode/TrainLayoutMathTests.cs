using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 열차 레이아웃 좌표 계산 검증 — "어느 칸 위에 서 있는가" 역산이 차폐(체온) 판정의 입력이다.
    /// 기본 편성(칸 3개 · 길이 12 m · 연결부 간격 1.5 m) 기준.
    /// </summary>
    public sealed class TrainLayoutMathTests
    {
        private const float CarLength = 12f;
        private const float CouplingGap = 1.5f;
        private const int CarCount = 3;

        // 총 길이 = 3×12 + 2×1.5 = 39 → 선두 Z = 19.5
        private const float FrontZ = 19.5f;

        private static bool TryResolve(float z, out int index, int carCount = CarCount)
        {
            return TrainLayoutMath.TryGetCarIndexAtZ(z, FrontZ, CarLength, CouplingGap, carCount, out index);
        }

        [Test]
        public void 칸_중심_Z는_선두에서_뒤로_일정_간격으로_배치된다()
        {
            Assert.That(TrainLayoutMath.GetCarCenterZ(0, FrontZ, CarLength, CouplingGap), Is.EqualTo(13.5f).Within(0.001f));
            Assert.That(TrainLayoutMath.GetCarCenterZ(1, FrontZ, CarLength, CouplingGap), Is.EqualTo(0f).Within(0.001f));
            Assert.That(TrainLayoutMath.GetCarCenterZ(2, FrontZ, CarLength, CouplingGap), Is.EqualTo(-13.5f).Within(0.001f));
        }

        [Test]
        public void 각_칸의_중심에서_그_칸_인덱스가_나온다()
        {
            for (int i = 0; i < CarCount; i++)
            {
                float centerZ = TrainLayoutMath.GetCarCenterZ(i, FrontZ, CarLength, CouplingGap);

                Assert.That(TryResolve(centerZ, out int index), Is.True);
                Assert.That(index, Is.EqualTo(i));
            }
        }

        [Test]
        public void 칸_가장자리_안쪽은_여전히_그_칸이다()
        {
            Assert.That(TryResolve(13.5f + 5.9f, out int front), Is.True);
            Assert.That(front, Is.EqualTo(0));

            Assert.That(TryResolve(13.5f - 5.9f, out int rear), Is.True);
            Assert.That(rear, Is.EqualTo(0));
        }

        [Test]
        public void 연결부_간격_위는_어느_칸에도_속하지_않는다()
        {
            // 0번 칸 후단 7.5 ~ 1번 칸 전단 6.0 사이가 연결부 간격.
            Assert.That(TryResolve(6.75f, out _), Is.False);
        }

        [Test]
        public void 편성_밖은_실패한다()
        {
            Assert.That(TryResolve(FrontZ + 10f, out _), Is.False, "열차 앞");
            Assert.That(TryResolve(-30f, out _), Is.False, "열차 뒤");
        }

        [Test]
        public void 증설로_칸_수가_늘면_뒤쪽_칸도_잡힌다()
        {
            float fourthCarZ = TrainLayoutMath.GetCarCenterZ(3, FrontZ, CarLength, CouplingGap);

            Assert.That(TryResolve(fourthCarZ, out _, carCount: 3), Is.False, "증설 전에는 범위 밖");
            Assert.That(TryResolve(fourthCarZ, out int index, carCount: 4), Is.True);
            Assert.That(index, Is.EqualTo(3));
        }

        [Test]
        public void 잘못된_규격은_안전하게_실패한다()
        {
            Assert.That(TrainLayoutMath.TryGetCarIndexAtZ(0f, FrontZ, CarLength, CouplingGap, 0, out _), Is.False);
            Assert.That(TrainLayoutMath.TryGetCarIndexAtZ(0f, FrontZ, 0f, CouplingGap, CarCount, out _), Is.False);
        }
    }
}
