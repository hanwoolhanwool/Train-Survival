using Game.Gameplay.World;
using NUnit.Framework;
using System.Collections.Generic;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 지역 전환을 <b>구간으로</b> 건너는 계산 검증 — 하늘이 지형보다 240 m 먼저 바뀌던 문제와,
    /// 바다가 경계에서 한 프레임에 마르던 문제의 순수 로직 쪽이다.
    /// </summary>
    public sealed class RegionTransitionSpanTests
    {
        private const float TileLength = 40f;
        private const float BlendRange = 60f;

        /// <summary>숲(0) → 사막(1) 경계가 타일 10(= 400 m 지점)에 찍힌 목록.</summary>
        private static List<TerrainRegionBoundary> ForestToDesert()
        {
            return new List<TerrainRegionBoundary>
            {
                new TerrainRegionBoundary(int.MinValue, 0),
                new TerrainRegionBoundary(10, 1),
            };
        }

        // ── 전환 진행도 ──────────────────────────────────────────────────

        [Test]
        public void 경계에서_멀면_발밑_지역_그대로다()
        {
            // 경계는 400 m 지점, 열차는 200 m — 200 m 앞이라 블렌드 구간(60 m) 밖이다.
            TileStreamingLogic.ResolveRegionBlend(
                200f, TileLength, BlendRange, ForestToDesert(), out int from, out int to, out float blend);

            Assert.That(from, Is.EqualTo(0));
            Assert.That(to, Is.EqualTo(0));
            Assert.That(blend, Is.EqualTo(0f));
        }

        [Test]
        public void 경계를_밟는_순간이_정확히_절반이다()
        {
            TileStreamingLogic.ResolveRegionBlend(
                400f, TileLength, BlendRange, ForestToDesert(), out int from, out int to, out float blend);

            Assert.That(from, Is.EqualTo(0));
            Assert.That(to, Is.EqualTo(1));
            Assert.That(blend, Is.EqualTo(0.5f).Within(1e-4f));
        }

        [Test]
        public void 경계_앞_한계에서_0이고_뒤_한계에서_1이다()
        {
            // 경계 60 m 전 — 아직 온전히 숲이다.
            TileStreamingLogic.ResolveRegionBlend(
                340f, TileLength, BlendRange, ForestToDesert(), out _, out _, out float before);

            // 경계 60 m 후 — 온전히 사막이다.
            TileStreamingLogic.ResolveRegionBlend(
                460f, TileLength, BlendRange, ForestToDesert(), out _, out _, out float after);

            Assert.That(before, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(after, Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void 진행도는_구간_안에서_단조증가한다()
        {
            float previous = -1f;
            for (float distance = 340f; distance <= 460f; distance += 10f)
            {
                TileStreamingLogic.ResolveRegionBlend(
                    distance, TileLength, BlendRange, ForestToDesert(),
                    out int from, out int to, out float blend);

                Assert.That(from, Is.EqualTo(0));
                Assert.That(to, Is.EqualTo(1));
                Assert.That(blend, Is.GreaterThanOrEqualTo(previous), $"{distance} m 에서 뒷걸음쳤다");
                Assert.That(blend, Is.InRange(0f, 1f));
                previous = blend;
            }
        }

        /// <summary>
        /// 구간을 벗어나면 진행도가 0으로 돌아가지만 <b>보이는 값은 이어진다</b> —
        /// from과 to가 함께 다음 지역이 되므로 Lerp 결과가 같기 때문이다. 이 성질이 깨지면
        /// 경계를 벗어나는 순간 하늘이 이전 지역으로 되튄다.
        /// </summary>
        [Test]
        public void 구간을_벗어나면_양끝이_같은_지역으로_수렴한다()
        {
            TileStreamingLogic.ResolveRegionBlend(
                461f, TileLength, BlendRange, ForestToDesert(), out int from, out int to, out float blend);

            Assert.That(from, Is.EqualTo(1));
            Assert.That(to, Is.EqualTo(1));
            Assert.That(blend, Is.EqualTo(0f));
        }

        [Test]
        public void 블렌드_구간이_0이면_섞지_않는다()
        {
            TileStreamingLogic.ResolveRegionBlend(
                400f, TileLength, 0f, ForestToDesert(), out int from, out int to, out float blend);

            Assert.That(from, Is.EqualTo(1));
            Assert.That(to, Is.EqualTo(1));
            Assert.That(blend, Is.EqualTo(0f));
        }

        [Test]
        public void 세션_최초_기록만_있으면_섞을_상대가_없다()
        {
            var boundaries = new List<TerrainRegionBoundary>
            {
                new TerrainRegionBoundary(int.MinValue, 3),
            };

            TileStreamingLogic.ResolveRegionBlend(
                400f, TileLength, BlendRange, boundaries, out int from, out int to, out float blend);

            Assert.That(from, Is.EqualTo(3));
            Assert.That(to, Is.EqualTo(3));
            Assert.That(blend, Is.EqualTo(0f));
        }

        [Test]
        public void 경계가_전혀_없으면_지역을_결정하지_않는다()
        {
            TileStreamingLogic.ResolveRegionBlend(
                400f, TileLength, BlendRange, new List<TerrainRegionBoundary>(),
                out int from, out int to, out float blend);

            Assert.That(from, Is.EqualTo(-1));
            Assert.That(to, Is.EqualTo(-1));
            Assert.That(blend, Is.EqualTo(0f));
        }

        [Test]
        public void 경계가_둘이면_가까운_쪽이_이긴다()
        {
            var boundaries = new List<TerrainRegionBoundary>
            {
                new TerrainRegionBoundary(int.MinValue, 0),
                new TerrainRegionBoundary(10, 1),
                new TerrainRegionBoundary(20, 2),
            };

            // 열차 780 m — 타일 20(800 m) 경계가 20 m 앞, 타일 10(400 m) 경계는 380 m 뒤다.
            // 가까운 쪽이 이겨 1 → 2 를 섞는다. 아직 경계 앞이라 절반에 못 미친다.
            TileStreamingLogic.ResolveRegionBlend(
                780f, TileLength, BlendRange, boundaries, out int from, out int to, out float blend);

            Assert.That(from, Is.EqualTo(1));
            Assert.That(to, Is.EqualTo(2));
            Assert.That(blend, Is.EqualTo(40f / 120f).Within(1e-4f));
        }

        // ── 지역 구간 z (수면·지평선판 재단) ─────────────────────────────

        [Test]
        public void 경계가_없으면_구간이_양쪽_무한대다()
        {
            TileStreamingLogic.ResolveRegionSpanZ(
                400f, TileLength, new List<TerrainRegionBoundary>(),
                out float backZ, out float frontZ, out int back, out int front);

            Assert.That(backZ, Is.EqualTo(float.NegativeInfinity));
            Assert.That(frontZ, Is.EqualTo(float.PositiveInfinity));
            Assert.That(back, Is.EqualTo(-1));
            Assert.That(front, Is.EqualTo(-1));
        }

        [Test]
        public void 경계_앞에서는_앞쪽_끝이_경계_거리다()
        {
            // 열차 300 m, 경계 400 m — 앞으로 100 m 남았다.
            TileStreamingLogic.ResolveRegionSpanZ(
                300f, TileLength, ForestToDesert(),
                out float backZ, out float frontZ, out int back, out int front);

            Assert.That(backZ, Is.EqualTo(float.NegativeInfinity));
            Assert.That(frontZ, Is.EqualTo(100f).Within(1e-3f));
            Assert.That(back, Is.EqualTo(-1));
            Assert.That(front, Is.EqualTo(1), "앞 구간은 사막이어야 한다");
        }

        [Test]
        public void 경계를_지나면_뒤쪽_끝이_음수로_물러난다()
        {
            // 열차 450 m, 경계 400 m — 50 m 지나왔다.
            TileStreamingLogic.ResolveRegionSpanZ(
                450f, TileLength, ForestToDesert(),
                out float backZ, out float frontZ, out int back, out int front);

            Assert.That(backZ, Is.EqualTo(-50f).Within(1e-3f));
            Assert.That(frontZ, Is.EqualTo(float.PositiveInfinity));
            Assert.That(back, Is.EqualTo(0), "뒤 구간은 아직 숲이어야 한다 — 물이 마르면 안 되는 이유다");
            Assert.That(front, Is.EqualTo(-1));
        }

        [Test]
        public void 구간_사이에_있으면_양쪽_경계를_모두_낸다()
        {
            var boundaries = new List<TerrainRegionBoundary>
            {
                new TerrainRegionBoundary(int.MinValue, 0),
                new TerrainRegionBoundary(10, 1),
                new TerrainRegionBoundary(20, 2),
            };

            // 열차 500 m — 뒤 경계 400 m(−100), 앞 경계 800 m(+300).
            TileStreamingLogic.ResolveRegionSpanZ(
                500f, TileLength, boundaries,
                out float backZ, out float frontZ, out int back, out int front);

            Assert.That(backZ, Is.EqualTo(-100f).Within(1e-3f));
            Assert.That(frontZ, Is.EqualTo(300f).Within(1e-3f));
            Assert.That(back, Is.EqualTo(0));
            Assert.That(front, Is.EqualTo(2));
        }

        /// <summary>
        /// 종전 버그의 회귀 방어선 — 물 반경 300 m가 지형 앞끝(200~240 m)을 넘어 다음 지역을
        /// 잠그던 자리다. 구간으로 자르면 앞끝이 경계에서 멈춘다.
        /// </summary>
        [Test]
        public void 수면은_경계_너머로_넘어가지_않는다()
        {
            const float HalfSize = 300f;

            // 열차 250 m — 경계(400 m)까지 150 m. 물은 300 m 까지 뻗을 수 있지만 150 m 에서 잘려야 한다.
            TileStreamingLogic.ResolveRegionSpanZ(
                250f, TileLength, ForestToDesert(), out float backZ, out float frontZ, out _, out _);

            float far = System.Math.Min(frontZ, HalfSize);
            float near = System.Math.Max(backZ, -HalfSize);

            Assert.That(far, Is.EqualTo(150f).Within(1e-3f));
            Assert.That(near, Is.EqualTo(-300f).Within(1e-3f));
        }
    }
}
