using System.Collections.Generic;
using Game.Gameplay.World;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 기차역 블록 배치 — 계획 §4.2가 약속한 세 가지를 코드로 고정한다.
    /// <b>블록마다 정확히 한 번</b>(지역 내내 안 나오는 일이 없다) ·
    /// <b>최소 간격 보장</b>(연달아 나오지 않는다) · <b>결정론</b>(피어마다 다른 역이 아니다).
    /// </summary>
    public sealed class StationSequenceLogicTests
    {
        private const int Block = 260;
        private const int Stages = 5;

        [Test]
        public void 블록마다_역이_정확히_한_번_나온다()
        {
            for (int block = -3; block <= 5; block++)
            {
                int count = 0;
                for (int i = block * Block; i < (block + 1) * Block; i++)
                {
                    if (StationSequenceLogic.IsStationTile(i, Block, Stages))
                    {
                        count++;
                    }
                }

                // 역 타일이 정확히 시퀀스 길이만큼 = 역 하나가 통째로 이 블록 안에 있다.
                Assert.AreEqual(Stages, count, "블록 " + block + " 의 역 타일 수");
            }
        }

        [Test]
        public void 음수_인덱스에서도_블록이_어긋나지_않는다()
        {
            // C# 나눗셈은 0으로 절단하므로 floor로 고치지 않으면 블록 0이 두 배로 넓어지고
            // 그 구간만 역이 하나 빠진다 — 후방 타일이 음수 인덱스라 실제로 밟는 경로다.
            Assert.AreEqual(-1, StationSequenceLogic.BlockOf(-1, Block));
            Assert.AreEqual(-1, StationSequenceLogic.BlockOf(-Block, Block));
            Assert.AreEqual(-2, StationSequenceLogic.BlockOf(-Block - 1, Block));
            Assert.AreEqual(0, StationSequenceLogic.BlockOf(0, Block));
            Assert.AreEqual(0, StationSequenceLogic.BlockOf(Block - 1, Block));
            Assert.AreEqual(1, StationSequenceLogic.BlockOf(Block, Block));
        }

        [Test]
        public void 이웃_역_사이가_최소_절반_블록_벌어진다()
        {
            int previousEnd = int.MinValue;
            for (int block = -5; block < 30; block++)
            {
                int start = StationSequenceLogic.StationStartIndex(block, Block, Stages);

                if (previousEnd != int.MinValue)
                {
                    int gap = start - previousEnd - 1;
                    Assert.GreaterOrEqual(gap, Block / 2,
                        "블록 " + block + " 의 역이 직전 역에 너무 붙었다 (간격 " + gap + ")");
                }

                previousEnd = start + Stages - 1;
            }
        }

        [Test]
        public void 역은_자기_블록을_넘지_않는다()
        {
            for (int block = -5; block < 30; block++)
            {
                int start = StationSequenceLogic.StationStartIndex(block, Block, Stages);
                int end = start + Stages - 1;

                Assert.GreaterOrEqual(start, block * Block, "블록 " + block + " 시작이 앞으로 샜다");
                Assert.Less(end, (block + 1) * Block, "블록 " + block + " 끝이 다음 블록을 침범했다");
            }
        }

        [Test]
        public void 같은_인덱스는_항상_같은_단계를_준다()
        {
            for (int i = -300; i < 600; i += 7)
            {
                int first = StationSequenceLogic.StageOf(i, Block, Stages);
                int second = StationSequenceLogic.StageOf(i, Block, Stages);
                Assert.AreEqual(first, second, "인덱스 " + i + " 의 판정이 흔들렸다");
            }
        }

        [Test]
        public void 역_타일은_0부터_차례로_단계를_받는다()
        {
            int start = StationSequenceLogic.StationStartIndex(0, Block, Stages);

            for (int stage = 0; stage < Stages; stage++)
            {
                Assert.AreEqual(stage, StationSequenceLogic.StageOf(start + stage, Block, Stages));
            }

            Assert.AreEqual(StationSequenceLogic.NoStage,
                StationSequenceLogic.StageOf(start - 1, Block, Stages), "역 앞 타일이 역으로 잡혔다");
            Assert.AreEqual(StationSequenceLogic.NoStage,
                StationSequenceLogic.StageOf(start + Stages, Block, Stages), "역 뒤 타일이 역으로 잡혔다");
        }

        [Test]
        public void 역_시작_기준점은_다섯_장이_모두_같다()
        {
            for (int block = 0; block < 20; block++)
            {
                int start = StationSequenceLogic.StationStartIndex(block, Block, Stages);
                for (int stage = 0; stage < Stages; stage++)
                {
                    Assert.AreEqual(start,
                        StationSequenceLogic.StationStartOf(start + stage, Block, Stages),
                        "블록 " + block + " 의 " + stage + "번째 장이 다른 기준점을 봤다");
                }
            }
        }

        [Test]
        public void 미러는_역_단위로_일관되고_양쪽_모두_나온다()
        {
            int mirrored = 0;
            for (int block = 0; block < 40; block++)
            {
                int start = StationSequenceLogic.StationStartIndex(block, Block, Stages);
                bool expected = StationSequenceLogic.IsMirrored(start);

                // 다섯 장이 제각각 뒤집히면 승강장이 중간에서 끊긴다.
                for (int stage = 0; stage < Stages; stage++)
                {
                    int resolved = StationSequenceLogic.StationStartOf(start + stage, Block, Stages);
                    Assert.AreEqual(expected, StationSequenceLogic.IsMirrored(resolved));
                }

                if (expected)
                {
                    mirrored++;
                }
            }

            // 한쪽으로 쏠리면 편측 배치가 늘 같은 쪽이라 두 번째 역부터 지루해진다.
            Assert.Greater(mirrored, 5, "미러가 거의 안 걸린다");
            Assert.Less(mirrored, 35, "미러가 거의 항상 걸린다");
        }

        [Test]
        public void 블록_안_시작_위치가_다양하다()
        {
            // 오프셋이 늘 같으면 "정확히 260장마다"가 되어 규칙성이 드러난다.
            var offsets = new HashSet<int>();
            for (int block = 0; block < 30; block++)
            {
                offsets.Add(StationSequenceLogic.StationStartIndex(block, Block, Stages) - block * Block);
            }

            Assert.Greater(offsets.Count, 15, "시작 위치가 몰려 있다 (서로 다른 오프셋 " + offsets.Count + "개)");
        }

        [Test]
        public void 설정이_성립하지_않으면_역이_배치되지_않는다()
        {
            Assert.IsFalse(StationSequenceLogic.IsValidConfig(0, Stages), "블록 0");
            Assert.IsFalse(StationSequenceLogic.IsValidConfig(Block, 0), "단계 0");
            Assert.IsFalse(StationSequenceLogic.IsValidConfig(Stages * 2 - 1, Stages), "블록이 시퀀스의 두 배 미만");
            Assert.IsTrue(StationSequenceLogic.IsValidConfig(Stages * 2, Stages), "딱 두 배는 성립한다");

            // 회귀 방어선 — 설정이 비면 어떤 인덱스도 역이 아니다(현행 지형 그대로).
            for (int i = -50; i < 50; i++)
            {
                Assert.AreEqual(StationSequenceLogic.NoStage, StationSequenceLogic.StageOf(i, 0, 0));
                Assert.AreEqual(StationSequenceLogic.NoStage, StationSequenceLogic.StageOf(i, Block, 0));
            }
        }

        [Test]
        public void 빠듯한_설정에서도_역이_사라지지_않는다()
        {
            // 블록이 딱 두 배면 시작 창이 1칸으로 좁아진다 — 그래도 역은 나와야 한다.
            const int Tight = Stages * 2;
            Assert.AreEqual(1, StationSequenceLogic.StartWindow(Tight, Stages));

            for (int block = 0; block < 5; block++)
            {
                int count = 0;
                for (int i = block * Tight; i < (block + 1) * Tight; i++)
                {
                    if (StationSequenceLogic.IsStationTile(i, Tight, Stages))
                    {
                        count++;
                    }
                }

                Assert.AreEqual(Stages, count, "빠듯한 블록 " + block);
            }
        }

        [Test]
        public void 프리웜_계획이_스트리머와_같은_답을_낸다()
        {
            // 두 곳이 어긋나면 프리웜이 통째로 헛일이 되고 아무도 눈치채지 못한다.
            const int First = 0;
            const int Last = 600;

            int[] planned = GameplayPreloadPlan.StationStageCounts(First, Last, Block, Stages);
            Assert.AreEqual(Stages, planned.Length);

            var actual = new int[Stages];
            for (int i = First; i <= Last; i++)
            {
                int stage = StationSequenceLogic.StageOf(i, Block, Stages);
                if (stage != StationSequenceLogic.NoStage)
                {
                    actual[stage]++;
                }
            }

            CollectionAssert.AreEqual(actual, planned);
        }

        [Test]
        public void 프리웜_계획은_역_타일을_팔레트에서_뺀다()
        {
            float[] weights = { 1f, 1f, 1f };
            const int First = 0;
            const int Last = 600;

            int[] withStation = GameplayPreloadPlan.SegmentCounts(First, Last, weights, null, Block, Stages);
            int[] withoutStation = GameplayPreloadPlan.SegmentCounts(First, Last, weights, null);

            int stationTiles = 0;
            int[] stageCounts = GameplayPreloadPlan.StationStageCounts(First, Last, Block, Stages);
            for (int i = 0; i < stageCounts.Length; i++)
            {
                stationTiles += stageCounts[i];
            }

            Assert.Greater(stationTiles, 0, "이 구간에는 역이 있어야 검증이 성립한다");
            Assert.AreEqual(GameplayPreloadPlan.Total(withoutStation) - stationTiles,
                GameplayPreloadPlan.Total(withStation),
                "역이 깔린 자리에는 팔레트 세그먼트가 오지 않아야 한다");
        }

        [Test]
        public void 역_설정이_없으면_프리웜_계획이_현행과_같다()
        {
            float[] weights = { 1f, 2f, 1f };
            int[] legacy = GameplayPreloadPlan.SegmentCounts(0, 40, weights, null);
            int[] withOffStation = GameplayPreloadPlan.SegmentCounts(0, 40, weights, null, 0, 0);

            CollectionAssert.AreEqual(legacy, withOffStation);
        }
    }
}
