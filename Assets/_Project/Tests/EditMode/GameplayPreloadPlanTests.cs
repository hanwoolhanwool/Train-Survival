using Game.Gameplay.World;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 지형 프리웜 계획 검증 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §5.2 · §10.
    ///
    /// <para><b>"스트리머와 일치"가 이 파일에서 가장 중요한 검사다.</b> 계획이 뽑은 세그먼트와
    /// 스트리머가 실제로 뽑는 세그먼트가 어긋나면 <b>프리웜은 전부 헛일이고 아무도 눈치채지
    /// 못한다</b> — 렉이 그대로 남을 뿐 오류는 하나도 안 난다.</para>
    ///
    /// <para>그래서 여기서는 "같은 함수를 부른다"를 믿지 않고, 스트리머가 쓰는 규칙을
    /// <b>이 파일 안에 손으로 다시 적어</b> 대조한다. 누군가 한쪽만 고치면 여기서 걸린다.</para>
    /// </summary>
    public sealed class GameplayPreloadPlanTests
    {
        /// <summary><c>WorldScrollSettings.asset</c>의 실측값 — 이 값이 바뀌면 §5.2의 9장도 바뀐다.</summary>
        private const float TileLength = 40f;
        private const int TilesAhead = 5;
        private const int TilesBehind = 3;

        /// <summary><c>TerrainSegmentPalette_Forest.asset</c>의 가중치 10종 (기본 5 · 특징 3 · 이벤트 2).</summary>
        private static float[] ForestWeights() => new[]
        {
            0.13f, 0.13f, 0.13f, 0.13f, 0.13f,
            0.0833f, 0.0833f, 0.0834f,
            0.05f, 0.05f,
        };

        /// <summary>이벤트형 둘만 인접 반복을 금지한다.</summary>
        private static bool[] ForestNoRepeat() => new[]
        {
            false, false, false, false, false,
            false, false, false,
            true, true,
        };

        [Test]
        public void 출발_구간은_인덱스_마이너스3에서_5까지_아홉_장이다()
        {
            GameplayPreloadPlan.StartRange(TileLength, TilesAhead, TilesBehind, out int first, out int last);

            Assert.AreEqual(-3, first);
            Assert.AreEqual(5, last);
            Assert.AreEqual(9, last - first + 1);
        }

        [Test]
        public void 계획_총량은_구간의_타일_수와_같다()
        {
            GameplayPreloadPlan.StartRange(TileLength, TilesAhead, TilesBehind, out int first, out int last);
            int[] counts = GameplayPreloadPlan.SegmentCounts(first, last, ForestWeights(), ForestNoRepeat());

            Assert.AreEqual(9, GameplayPreloadPlan.Total(counts));
        }

        [Test]
        public void 같은_세그먼트는_한_줄로_합산된다()
        {
            // 후보가 하나뿐이면 9장이 전부 그 한 종이다 — 합산이 안 되면 여기서 1이 아홉 번 나온다.
            int[] counts = GameplayPreloadPlan.SegmentCounts(-3, 5, new[] { 1f }, new[] { false });

            Assert.AreEqual(1, counts.Length);
            Assert.AreEqual(9, counts[0]);
        }

        [Test]
        public void 숲_팔레트에서는_아홉_장이_열_종보다_적은_종에_몰린다()
        {
            GameplayPreloadPlan.StartRange(TileLength, TilesAhead, TilesBehind, out int first, out int last);
            int[] counts = GameplayPreloadPlan.SegmentCounts(first, last, ForestWeights(), ForestNoRepeat());

            int kinds = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] > 0)
                {
                    kinds++;
                }
            }

            // §5.2가 "실제로는 5~8종에 몰린다"고 본 자리. 정확한 수를 고정하지는 않는다 —
            // 팔레트 가중치는 레벨 작업으로 바뀔 값이고, 여기서 보장할 것은 "합산이 값을 한다"뿐이다.
            Assert.Greater(kinds, 0);
            Assert.LessOrEqual(kinds, counts.Length);
            Assert.AreEqual(9, GameplayPreloadPlan.Total(counts));
        }

        [Test]
        public void 팔레트_선택이_스트리머와_일치한다()
        {
            float[] weights = ForestWeights();
            bool[] noRepeat = ForestNoRepeat();

            GameplayPreloadPlan.StartRange(TileLength, TilesAhead, TilesBehind, out int first, out int last);
            int[] counts = GameplayPreloadPlan.SegmentCounts(first, last, weights, noRepeat);

            // TerrainTileStreamer.TryPickFromPalette가 하는 일을 손으로 다시 적는다.
            var expected = new int[weights.Length];
            for (int index = first; index <= last; index++)
            {
                int previous = SegmentPickLogic.WeightedPick(
                    weights, SegmentPickLogic.Hash01(index - 1, 1), -1);
                int picked = SegmentPickLogic.Pick(index, weights, previous, noRepeat);
                expected[picked]++;
            }

            CollectionAssert.AreEqual(expected, counts);
        }

        [Test]
        public void 어느_인덱스든_계획과_스트리머의_선택이_같다()
        {
            float[] weights = ForestWeights();
            bool[] noRepeat = ForestNoRepeat();

            // 출발 구간 밖까지 넓게 훑는다 — 지역이 바뀌면 다른 구간에서도 같은 규칙을 쓴다.
            for (int index = -50; index <= 50; index++)
            {
                int previous = SegmentPickLogic.WeightedPick(
                    weights, SegmentPickLogic.Hash01(index - 1, 1), -1);
                int streamer = SegmentPickLogic.Pick(index, weights, previous, noRepeat);

                Assert.AreEqual(streamer, SegmentPickLogic.PickForTile(index, weights, noRepeat), $"인덱스 {index}");
            }
        }

        [Test]
        public void 선택은_결정론적이라_몇_번을_불러도_같다()
        {
            float[] weights = ForestWeights();
            bool[] noRepeat = ForestNoRepeat();

            GameplayPreloadPlan.StartRange(TileLength, TilesAhead, TilesBehind, out int first, out int last);
            int[] a = GameplayPreloadPlan.SegmentCounts(first, last, weights, noRepeat);
            int[] b = GameplayPreloadPlan.SegmentCounts(first, last, weights, noRepeat);

            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void 팔레트가_없으면_빈_계획이다()
        {
            // 폴백은 프리팹 한 종이므로 슬롯 계산이 아니라 스텝이 처리한다 — 여기서는 빈 배열이어야 한다.
            Assert.AreEqual(0, GameplayPreloadPlan.SegmentCounts(-3, 5, null, null).Length);
            Assert.AreEqual(0, GameplayPreloadPlan.SegmentCounts(-3, 5, new float[0], null).Length);
        }

        [Test]
        public void 가중치가_전부_0이면_아무것도_뽑지_않는다()
        {
            // 팔레트 슬롯이 전부 비어 있는(프리팹 미배치) 상태 — 조용히 0을 돌려줘야 한다.
            int[] counts = GameplayPreloadPlan.SegmentCounts(-3, 5, new[] { 0f, 0f, 0f }, null);

            Assert.AreEqual(3, counts.Length);
            Assert.AreEqual(0, GameplayPreloadPlan.Total(counts));
        }

        [Test]
        public void 총량은_음수를_세지_않는다()
        {
            Assert.AreEqual(0, GameplayPreloadPlan.Total(null));
            Assert.AreEqual(3, GameplayPreloadPlan.Total(new[] { 1, -5, 2 }));
        }
    }
}
