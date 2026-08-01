using Game.Gameplay.Monsters;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 몬스터 변종 추첨 검증 (기획서 §5 — Day가 진행될수록 새로운 행동이 웨이브에 추가된다).
    /// 일반형(Day 1) · 돌진형(Day 3) · 돌격형(Day 6) 구성 기준.
    /// </summary>
    public sealed class MonsterVariantPickerTests
    {
        private static readonly int[] MinDays = { 1, 3, 6 };
        private static readonly float[] Weights = { 3f, 2f, 1f };

        [Test]
        public void Day1에는_첫_변종만_등장한다()
        {
            // 등장 가능한 게 하나뿐이면 어떤 난수든 그것이 나온다.
            Assert.That(MonsterVariantPicker.Pick(1, MinDays, Weights, 0f), Is.EqualTo(0));
            Assert.That(MonsterVariantPicker.Pick(1, MinDays, Weights, 0.5f), Is.EqualTo(0));
            Assert.That(MonsterVariantPicker.Pick(1, MinDays, Weights, 1f), Is.EqualTo(0));
        }

        [Test]
        public void 해금_Day가_되면_새_변종이_추첨에_들어온다()
        {
            // Day 3 — 가중치 3:2, 총합 5. roll 0.9 → 4.5 > 3 이므로 두 번째 변종.
            Assert.That(MonsterVariantPicker.Pick(3, MinDays, Weights, 0.9f), Is.EqualTo(1));

            // 아직 Day 3이므로 세 번째(Day 6)는 절대 나오지 않는다.
            for (int i = 0; i <= 10; i++)
            {
                Assert.That(MonsterVariantPicker.Pick(3, MinDays, Weights, i / 10f), Is.Not.EqualTo(2));
            }
        }

        [Test]
        public void 가중치_비율대로_구간이_나뉜다()
        {
            // Day 6 — 가중치 3:2:1, 총합 6. 구간 [0,3) [3,5) [5,6].
            Assert.That(MonsterVariantPicker.Pick(6, MinDays, Weights, 0.1f), Is.EqualTo(0), "0.6 → 첫 구간");
            Assert.That(MonsterVariantPicker.Pick(6, MinDays, Weights, 0.6f), Is.EqualTo(1), "3.6 → 둘째 구간");
            Assert.That(MonsterVariantPicker.Pick(6, MinDays, Weights, 0.95f), Is.EqualTo(2), "5.7 → 셋째 구간");
        }

        [Test]
        public void 난수_경계에서도_유효한_인덱스가_나온다()
        {
            Assert.That(MonsterVariantPicker.Pick(6, MinDays, Weights, 0f), Is.EqualTo(0));
            Assert.That(MonsterVariantPicker.Pick(6, MinDays, Weights, 1f), Is.EqualTo(2));

            // 범위를 벗어난 난수도 클램프되어 유효 인덱스를 낸다.
            Assert.That(MonsterVariantPicker.Pick(6, MinDays, Weights, -5f), Is.EqualTo(0));
            Assert.That(MonsterVariantPicker.Pick(6, MinDays, Weights, 5f), Is.EqualTo(2));
        }

        [Test]
        public void 등장_가능한_변종이_없으면_실패한다()
        {
            int[] futureOnly = { 10 };
            float[] weights = { 1f };

            Assert.That(MonsterVariantPicker.Pick(1, futureOnly, weights, 0.5f), Is.EqualTo(-1));
        }

        [Test]
        public void 가중치가_0인_변종은_뽑히지_않는다()
        {
            int[] minDays = { 1, 1 };
            float[] weights = { 0f, 1f };

            for (int i = 0; i <= 10; i++)
            {
                Assert.That(MonsterVariantPicker.Pick(5, minDays, weights, i / 10f), Is.EqualTo(1));
            }
        }

        [Test]
        public void 잘못된_입력은_안전하게_실패한다()
        {
            Assert.That(MonsterVariantPicker.Pick(1, null, Weights, 0.5f), Is.EqualTo(-1));
            Assert.That(MonsterVariantPicker.Pick(1, MinDays, null, 0.5f), Is.EqualTo(-1));
            Assert.That(MonsterVariantPicker.Pick(1, new int[0], new float[0], 0.5f), Is.EqualTo(-1));
            Assert.That(MonsterVariantPicker.Pick(1, MinDays, new float[] { 1f }, 0.5f), Is.EqualTo(-1), "길이 불일치");
        }

        [Test]
        public void 잘못된_Day_번호는_시작일로_고정된다()
        {
            Assert.That(MonsterVariantPicker.Pick(0, MinDays, Weights, 0.5f), Is.EqualTo(0));
            Assert.That(MonsterVariantPicker.Pick(-10, MinDays, Weights, 0.5f), Is.EqualTo(0));
        }
    }
}
