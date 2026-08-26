using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 역 소품 전리품 추첨 — 계획 §4.3이 규칙으로 못박은 것을 고정한다.
    /// 특히 <b>금고 = 3단계</b>는 이 기능의 성장 축이라 저작 실수로 흔들리면 안 된다.
    /// </summary>
    public sealed class StationLootLogicTests
    {
        [Test]
        public void 요구_집게_등급은_종류가_규칙으로_정한다()
        {
            Assert.AreEqual(3, StationLootLogic.RequiredTierFor(StationPropKind.Safe), "금고");
            Assert.AreEqual(2, StationLootLogic.RequiredTierFor(StationPropKind.Vending), "자판기");
            Assert.AreEqual(1, StationLootLogic.RequiredTierFor(StationPropKind.Crate), "상자");
            Assert.AreEqual(1, StationLootLogic.RequiredTierFor(StationPropKind.Bin), "쓰레기통");
        }

        [Test]
        public void 구간_추첨은_양_끝을_모두_낸다()
        {
            Assert.AreEqual(2, StationLootLogic.RollRange(2, 5, 0f), "roll 0");
            Assert.AreEqual(5, StationLootLogic.RollRange(2, 5, 0.999f), "roll 0.999");

            // 1.0이 들어와도 구간 밖으로 나가지 않는다 — Random.value는 1.0을 포함한다.
            Assert.AreEqual(5, StationLootLogic.RollRange(2, 5, 1f), "roll 1.0");
        }

        [Test]
        public void 구간_추첨이_전_구간을_고르게_덮는다()
        {
            var seen = new bool[4];
            for (int i = 0; i < 40; i++)
            {
                int value = StationLootLogic.RollRange(2, 5, i / 40f);
                Assert.GreaterOrEqual(value, 2);
                Assert.LessOrEqual(value, 5);
                seen[value - 2] = true;
            }

            for (int i = 0; i < seen.Length; i++)
            {
                Assert.IsTrue(seen[i], (i + 2) + " 이 한 번도 안 나왔다");
            }
        }

        [Test]
        public void 한_칸_구간과_뒤집힌_구간도_방어한다()
        {
            Assert.AreEqual(3, StationLootLogic.RollRange(3, 3, 0.5f), "min == max");

            // 저작 실수로 뒤집혀도 스폰이 멈추는 편이 더 나쁘다 — 뒤집어 받아 준다.
            Assert.AreEqual(1, StationLootLogic.RollRange(4, 1, 0f));
            Assert.AreEqual(4, StationLootLogic.RollRange(4, 1, 0.999f));
        }

        [Test]
        public void 빈_자리_판정은_확률_0이면_절대_비우지_않는다()
        {
            for (int i = 0; i <= 10; i++)
            {
                Assert.IsFalse(StationLootLogic.RollEmpty(0f, i / 10f), "roll " + i / 10f);
            }
        }

        [Test]
        public void 빈_자리_판정은_확률만큼_비운다()
        {
            Assert.IsTrue(StationLootLogic.RollEmpty(0.5f, 0.2f), "확률 안");
            Assert.IsFalse(StationLootLogic.RollEmpty(0.5f, 0.7f), "확률 밖");
            Assert.IsFalse(StationLootLogic.RollEmpty(0.5f, 0.5f), "경계는 비우지 않는다");
        }

        [Test]
        public void 항목_추첨은_가중치를_따르고_0인_줄을_건너뛴다()
        {
            // 0 가중치 줄은 작업 중인 자리 — 절대 뽑히면 안 된다.
            float[] weights = { 0f, 1f, 0f };
            for (int i = 0; i <= 10; i++)
            {
                Assert.AreEqual(1, StationLootLogic.RollEntry(weights, i / 10f), "roll " + i / 10f);
            }
        }

        [Test]
        public void 뽑을_것이_없으면_무효를_낸다()
        {
            Assert.Less(StationLootLogic.RollEntry(null, 0.5f), 0, "null");
            Assert.Less(StationLootLogic.RollEntry(new float[0], 0.5f), 0, "빈 배열");
            Assert.Less(StationLootLogic.RollEntry(new[] { 0f, 0f }, 0.5f), 0, "전부 0");
        }

        [Test]
        public void 슬롯_수는_표가_정한_범위를_벗어나지_않는다()
        {
            const int Min = 1;
            const int Max = 3;
            for (int i = 0; i <= 20; i++)
            {
                int slots = StationLootLogic.RollRange(Min, Max, i / 20f);
                Assert.GreaterOrEqual(slots, Min);
                Assert.LessOrEqual(slots, Max);
            }
        }

        [Test]
        public void 음수_롤이_새어들어와도_구간_안이다()
        {
            // 방어 코드가 실제로 도는지 — 상류가 바뀌어도 스폰이 깨지지 않게 한다.
            Assert.AreEqual(2, StationLootLogic.RollRange(2, 5, -0.5f));
            Assert.AreEqual(2, StationLootLogic.RollRange(2, 5, Mathf.NegativeInfinity));
        }
    }
}
