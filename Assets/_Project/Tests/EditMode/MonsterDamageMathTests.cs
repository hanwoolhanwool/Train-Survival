using Game.Gameplay.Monsters;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>처형 규칙 (M5 5차) — 그로기 중에는 무기 종류와 무관하게 배율이 곱해진다.</summary>
    public sealed class MonsterDamageMathTests
    {
        [Test]
        public void 평상시에는_피해가_그대로다()
        {
            Assert.That(MonsterDamageMath.ResolveDamage(20f, stunned: false, stunnedMultiplier: 999f),
                Is.EqualTo(20f));
        }

        [Test]
        public void 그로기_중에는_배율이_곱해진다()
        {
            Assert.That(MonsterDamageMath.ResolveDamage(20f, stunned: true, stunnedMultiplier: 3f),
                Is.EqualTo(60f));
        }

        [Test]
        public void 즉사_배율은_최대_체력을_넘긴다()
        {
            // 기본값 999 — 어떤 무기로 마무리해도 한 방에 끝난다 (근접 처형·협동 처치 공통).
            const float bruteMaxHealth = 240f;
            float applied = MonsterDamageMath.ResolveDamage(1f, stunned: true, stunnedMultiplier: 999f);

            Assert.That(applied, Is.GreaterThan(bruteMaxHealth));
        }

        [Test]
        public void 배율_1_이하는_처형이_아니다()
        {
            // 에셋으로 배율을 1로 낮추면 처형 축을 끌 수 있다 — 그로기는 이동 정지 효과만 남는다.
            Assert.That(MonsterDamageMath.ResolveDamage(20f, stunned: true, stunnedMultiplier: 1f),
                Is.EqualTo(20f));
            Assert.That(MonsterDamageMath.ResolveDamage(20f, stunned: true, stunnedMultiplier: 0.5f),
                Is.EqualTo(20f));
        }

        [Test]
        public void 음수_피해는_회복이_되지_않는다()
        {
            Assert.That(MonsterDamageMath.ResolveDamage(-5f, stunned: true, stunnedMultiplier: 999f),
                Is.EqualTo(0f));
        }
    }
}
