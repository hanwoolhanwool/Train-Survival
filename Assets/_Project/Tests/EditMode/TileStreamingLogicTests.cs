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

        // ── 지역 전환 경계 (M6 1차 §2.4) ─────────────────────────────────

        [Test]
        public void 전환_경계는_전방_생성분_다음_타일부터다()
        {
            // 전환 순간 전방 tilesAhead장이 이미 구 프리팹으로 깔려 있다 — floor(D/L) + tilesAhead + 1.
            Assert.That(TileStreamingLogic.GetBoundaryTileIndex(0f, 40f, tilesAhead: 5), Is.EqualTo(6));
            Assert.That(TileStreamingLogic.GetBoundaryTileIndex(399f, 40f, 5), Is.EqualTo(15));
            Assert.That(TileStreamingLogic.GetBoundaryTileIndex(400f, 40f, 5), Is.EqualTo(16));
        }

        [Test]
        public void 경계가_없으면_지역을_결정하지_않는다()
        {
            var boundaries = new System.Collections.Generic.List<TerrainRegionBoundary>();

            Assert.That(TileStreamingLogic.ResolveRegionIndex(0, boundaries), Is.EqualTo(-1));
        }

        [Test]
        public void 타일은_인덱스_이하_중_가장_뒤의_경계가_결정한다()
        {
            var boundaries = new System.Collections.Generic.List<TerrainRegionBoundary>
            {
                new TerrainRegionBoundary(int.MinValue, 0),
                new TerrainRegionBoundary(10, 1),
                new TerrainRegionBoundary(20, 2),
            };

            Assert.That(TileStreamingLogic.ResolveRegionIndex(-100, boundaries), Is.EqualTo(0));
            Assert.That(TileStreamingLogic.ResolveRegionIndex(9, boundaries), Is.EqualTo(0));
            Assert.That(TileStreamingLogic.ResolveRegionIndex(10, boundaries), Is.EqualTo(1));
            Assert.That(TileStreamingLogic.ResolveRegionIndex(19, boundaries), Is.EqualTo(1));
            Assert.That(TileStreamingLogic.ResolveRegionIndex(20, boundaries), Is.EqualTo(2));
            Assert.That(TileStreamingLogic.ResolveRegionIndex(1000, boundaries), Is.EqualTo(2));
        }

        [Test]
        public void 후방_가시_구간_밖_경계만_트림된다()
        {
            var boundaries = new System.Collections.Generic.List<TerrainRegionBoundary>
            {
                new TerrainRegionBoundary(int.MinValue, 0),
                new TerrainRegionBoundary(10, 1),
                new TerrainRegionBoundary(20, 2),
            };

            // 다음 경계가 첫 가시 인덱스 이하가 되기 전에는 앞 경계도 가시 타일을 결정한다.
            Assert.That(TileStreamingLogic.CountTrimmableBoundaries(boundaries, firstVisibleIndex: 9), Is.EqualTo(0));
            Assert.That(TileStreamingLogic.CountTrimmableBoundaries(boundaries, 10), Is.EqualTo(1));
            Assert.That(TileStreamingLogic.CountTrimmableBoundaries(boundaries, 19), Is.EqualTo(1));
            Assert.That(TileStreamingLogic.CountTrimmableBoundaries(boundaries, 25), Is.EqualTo(2));

            // 마지막 경계는 항상 남는다 — 이후 전 구간의 결정 근거다.
            Assert.That(TileStreamingLogic.CountTrimmableBoundaries(boundaries, int.MaxValue), Is.EqualTo(2));
        }
    }
}
