using Game.Gameplay.Monsters;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 지역별 몬스터 변종 구성 검증 (바다 지역 구현 계획 §12.3 안 ㉢).
    ///
    /// <para>추첨은 <b>Day만</b> 보므로, 바다 전용 위협에 등장 Day를 거는 것만으로는
    /// 지역을 벗어난 뒤에도 계속 나온다 — 이 계산이 그 구멍을 막는다.</para>
    /// </summary>
    public sealed class RegionVariantWeightsTests
    {
        // 카탈로그 기준 구성 — 일반 3 / 돌진 2 / 도약 2 / 돌격 1 / 들소 0(스탬피드 전용)
        private static readonly float[] BaseWeights = { 3f, 2f, 2f, 1f, 0f };

        private static float[] NewBuffer()
        {
            return new float[BaseWeights.Length];
        }

        [Test]
        public void 오버라이드가_없으면_기본_구성_그대로다()
        {
            float[] result = NewBuffer();
            int applied = RegionVariantWeights.Apply(BaseWeights, null, null, result);

            Assert.That(applied, Is.EqualTo(0));
            Assert.That(result, Is.EqualTo(BaseWeights));
        }

        [Test]
        public void 지정한_변종만_치환된다()
        {
            float[] result = NewBuffer();
            int applied = RegionVariantWeights.Apply(
                BaseWeights, new[] { 1 }, new[] { 5f }, result);

            Assert.That(applied, Is.EqualTo(1));
            Assert.That(result[1], Is.EqualTo(5f), "지정한 변종");
            Assert.That(result[0], Is.EqualTo(3f), "나머지는 기본 그대로");
            Assert.That(result[3], Is.EqualTo(1f));
        }

        [Test]
        public void 가중치_0이면_그_지역에서는_나오지_않는다()
        {
            // 바다에서 육상 돌격형을 빼는 식의 사용.
            float[] result = NewBuffer();
            RegionVariantWeights.Apply(BaseWeights, new[] { 3 }, new[] { 0f }, result);

            // Day 10이면 원래 돌격형(인덱스 3)도 후보다. 가중치를 0으로 만들면 절대 뽑히지 않는다.
            int[] minDays = { 1, 3, 8, 6, 1 };
            for (int i = 0; i <= 10; i++)
            {
                int picked = MonsterVariantPicker.Pick(10, minDays, result, i / 10f);
                Assert.That(picked, Is.Not.EqualTo(3), $"roll {i / 10f}");
            }
        }

        [Test]
        public void 기본이_0인_변종을_지역이_되살릴_수_있다()
        {
            // 겹치기가 곱이었다면 0 × 무엇이든 0이라 불가능하다 — 치환이라서 성립한다.
            // 바다 전용 도약(물고기 점프)을 카탈로그 기본 0으로 두고 바다에서만 켜는 방식.
            float[] result = NewBuffer();
            int applied = RegionVariantWeights.Apply(
                BaseWeights, new[] { 4 }, new[] { 4f }, result);

            Assert.That(applied, Is.EqualTo(1));
            Assert.That(result[4], Is.EqualTo(4f));

            int[] minDays = { 1, 3, 8, 6, 1 };
            bool everPicked = false;
            for (int i = 0; i <= 20; i++)
            {
                everPicked |= MonsterVariantPicker.Pick(10, minDays, result, i / 20f) == 4;
            }

            Assert.IsTrue(everPicked, "되살린 변종이 실제로 추첨된다");
        }

        [Test]
        public void 카탈로그에_없는_변종은_조용히_무시된다()
        {
            // 지역 에셋이 참조하던 변종이 카탈로그에서 빠져도 그 지역의 웨이브가 멈추면 안 된다.
            float[] result = NewBuffer();
            int applied = RegionVariantWeights.Apply(
                BaseWeights, new[] { -1, 2 }, new[] { 9f, 7f }, result);

            Assert.That(applied, Is.EqualTo(1), "유효한 것만 센다");
            Assert.That(result[2], Is.EqualTo(7f));
            Assert.That(result, Is.EqualTo(new[] { 3f, 2f, 7f, 1f, 0f }));
        }

        [Test]
        public void 범위_밖_인덱스는_버퍼를_넘지_않는다()
        {
            float[] result = NewBuffer();
            int applied = RegionVariantWeights.Apply(
                BaseWeights, new[] { BaseWeights.Length }, new[] { 9f }, result);

            Assert.That(applied, Is.EqualTo(0));
            Assert.That(result, Is.EqualTo(BaseWeights));
        }

        [Test]
        public void 음수_가중치는_0으로_눌린다()
        {
            float[] result = NewBuffer();
            RegionVariantWeights.Apply(BaseWeights, new[] { 0 }, new[] { -2f }, result);

            Assert.That(result[0], Is.EqualTo(0f));
        }

        [Test]
        public void 짝이_맞지_않는_오버라이드는_적용하지_않는다()
        {
            float[] result = NewBuffer();
            int applied = RegionVariantWeights.Apply(
                BaseWeights, new[] { 0, 1 }, new[] { 9f }, result);

            Assert.That(applied, Is.EqualTo(0));
            Assert.That(result, Is.EqualTo(BaseWeights), "기본 복사는 그대로 끝난다");
        }

        [Test]
        public void 버퍼_길이가_다르면_아무것도_하지_않는다()
        {
            var small = new float[2];
            int applied = RegionVariantWeights.Apply(BaseWeights, new[] { 0 }, new[] { 9f }, small);

            Assert.That(applied, Is.EqualTo(0));
            Assert.That(small, Is.EqualTo(new[] { 0f, 0f }));
        }
    }
}
