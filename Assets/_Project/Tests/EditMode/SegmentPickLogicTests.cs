using Game.Gameplay.World;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 세그먼트 추첨 — 미결 ① 확정(인덱스 시드·전 피어 동일)이 실제로 성립하는지 검증한다.
    /// 결정론이 깨지면 피어마다 콜라이더가 갈려 몬스터가 없는 벽을 도는 것처럼 보인다.
    /// </summary>
    public sealed class SegmentPickLogicTests
    {
        private static readonly float[] Even = { 1f, 1f, 1f, 1f };

        [Test]
        public void 같은_타일_인덱스는_항상_같은_세그먼트를_준다()
        {
            for (int i = -5; i < 50; i++)
            {
                int first = SegmentPickLogic.Pick(i, Even, -1, null);
                int second = SegmentPickLogic.Pick(i, Even, -1, null);
                Assert.AreEqual(first, second, "인덱스 " + i + " 의 추첨이 흔들렸다");
            }
        }

        [Test]
        public void 인접_인덱스가_같은_값으로_뭉치지_않는다()
        {
            // 해시가 인덱스에 선형이면 연속 구간이 같은 값으로 몰린다 — 반복 인지의 최악 형태다.
            int changes = 0;
            int previous = SegmentPickLogic.Pick(0, Even, -1, null);
            for (int i = 1; i < 40; i++)
            {
                int picked = SegmentPickLogic.Pick(i, Even, -1, null);
                if (picked != previous)
                {
                    changes++;
                }

                previous = picked;
            }

            Assert.Greater(changes, 15, "40장 중 전환이 너무 적다 — 해시 분포가 뭉쳤다");
        }

        [Test]
        public void 가중치가_0인_후보는_뽑히지_않는다()
        {
            var weights = new float[] { 0f, 1f, 0f };
            for (int i = 0; i < 30; i++)
            {
                Assert.AreEqual(1, SegmentPickLogic.Pick(i, weights, -1, null));
            }
        }

        [Test]
        public void 인접_반복_금지_후보는_연달아_나오지_않는다()
        {
            var weights = new float[] { 1f, 1f };
            var noRepeat = new bool[] { true, true };
            for (int i = 0; i < 40; i++)
            {
                int previous = SegmentPickLogic.Pick(i - 1, weights, -1, noRepeat);
                int picked = SegmentPickLogic.Pick(i, weights, previous, noRepeat);
                Assert.AreNotEqual(previous, picked, "인덱스 " + i + " 에서 금지 후보가 연달아 나왔다");
            }
        }

        [Test]
        public void 금지_때문에_후보가_없어지면_제외를_포기한다()
        {
            // 후보가 하나뿐인데 그것이 인접 금지면 스폰이 멈추는 편이 더 나쁘다 — 같은 것을 다시 준다.
            var weights = new float[] { 1f };
            var noRepeat = new bool[] { true };
            Assert.AreEqual(0, SegmentPickLogic.Pick(7, weights, 0, noRepeat));
        }

        [Test]
        public void 빈_팔레트는_무효를_돌려준다()
        {
            Assert.AreEqual(-1, SegmentPickLogic.Pick(3, null, -1, null));
            Assert.AreEqual(-1, SegmentPickLogic.Pick(3, new float[0], -1, null));
            Assert.AreEqual(-1, SegmentPickLogic.Pick(3, new float[] { 0f, 0f }, -1, null));
        }

        [Test]
        public void 해시는_0과_1_사이에_있다()
        {
            for (int i = -100; i < 100; i++)
            {
                float h = SegmentPickLogic.Hash01(i, 1);
                Assert.GreaterOrEqual(h, 0f);
                Assert.Less(h, 1f);
            }
        }
    }
}
