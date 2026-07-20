using Game.Gameplay.World;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>지형 타일 스트리밍 인덱스 계산 검증.</summary>
    public sealed class TileStreamingLogicTests
    {
        [Test]
        public void 시작_시점에는_열차_주변_구간이_생성된다()
        {
            TileStreamingLogic.GetVisibleRange(0f, 40f, tilesAhead: 5, tilesBehind: 3, out int first, out int last);

            Assert.That(first, Is.EqualTo(-3));
            Assert.That(last, Is.EqualTo(5));
        }

        [Test]
        public void 한_타일_길이를_주행하면_구간이_한_칸_전진한다()
        {
            TileStreamingLogic.GetVisibleRange(40f, 40f, 5, 3, out int first, out int last);

            Assert.That(first, Is.EqualTo(-2));
            Assert.That(last, Is.EqualTo(6));
        }

        [Test]
        public void 타일의_월드_Z는_주행_거리만큼_후퇴한다()
        {
            Assert.That(TileStreamingLogic.GetTileZ(2, 40f, 0f), Is.EqualTo(80f));
            Assert.That(TileStreamingLogic.GetTileZ(2, 40f, 30f), Is.EqualTo(50f));
            Assert.That(TileStreamingLogic.GetTileZ(0, 40f, 100f), Is.EqualTo(-100f));
        }

        [Test]
        public void 음수_주행_거리도_안전하게_처리한다()
        {
            TileStreamingLogic.GetVisibleRange(-10f, 40f, 5, 3, out int first, out int last);

            Assert.That(first, Is.EqualTo(-4));
            Assert.That(last, Is.EqualTo(4));
        }
    }
}
