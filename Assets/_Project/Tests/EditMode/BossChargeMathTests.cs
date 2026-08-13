using Game.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 보스 돌진 상태 기계 검증 (M7 2차 공통 패턴 — 대기 → 예고 → 돌진 → 경직 순환).
    /// 시간을 주입받는 순수 전이라 프레임 수와 무관하게 결정론적으로 검증된다.
    /// </summary>
    public sealed class BossChargeMathTests
    {
        private const float Cooldown = 9f;
        private const float Telegraph = 1.2f;
        private const float Duration = 1.6f;
        private const float Recover = 2f;

        private static BossChargeStep Step(
            BossChargeState state, float timer, float deltaTime, bool canStart = true, bool blocked = false)
        {
            return BossChargeMath.Step(
                state, timer, deltaTime, Cooldown, Telegraph, Duration, Recover, canStart, blocked);
        }

        [Test]
        public void 쿨다운이_차기_전에는_대기_상태를_유지한다()
        {
            BossChargeStep step = Step(BossChargeState.Ready, 3f, 0.5f);

            Assert.That(step.State, Is.EqualTo(BossChargeState.Ready));
            Assert.That(step.Timer, Is.EqualTo(3.5f).Within(0.0001f));
            Assert.That(step.EnteredTelegraph, Is.False);
        }

        [Test]
        public void 쿨다운이_차면_예고로_전이한다()
        {
            BossChargeStep step = Step(BossChargeState.Ready, 8.9f, 0.2f);

            Assert.That(step.State, Is.EqualTo(BossChargeState.Telegraph));
            Assert.That(step.Timer, Is.EqualTo(0f));
            Assert.That(step.EnteredTelegraph, Is.True, "방향 고정·연출 발신 지점");
        }

        [Test]
        public void 표적이_없으면_쿨다운이_차도_돌진하지_않는다()
        {
            BossChargeStep step = Step(BossChargeState.Ready, 20f, 0.2f, canStart: false);

            Assert.That(step.State, Is.EqualTo(BossChargeState.Ready));
            Assert.That(step.EnteredTelegraph, Is.False);
        }

        [Test]
        public void 흘려보낸_쿨다운은_표적_복귀_즉시_돌진으로_이어진다()
        {
            // 표적이 없어 대기한 시간이 버려지면 보스가 한참을 멍하니 서 있게 된다.
            BossChargeStep waited = Step(BossChargeState.Ready, 20f, 0.2f, canStart: false);
            BossChargeStep resumed = Step(BossChargeState.Ready, waited.Timer, 0.02f);

            Assert.That(resumed.State, Is.EqualTo(BossChargeState.Telegraph));
        }

        [Test]
        public void 예고가_끝나면_돌진이_개시된다()
        {
            BossChargeStep step = Step(BossChargeState.Telegraph, 1.1f, 0.2f);

            Assert.That(step.State, Is.EqualTo(BossChargeState.Charging));
            Assert.That(step.EnteredCharge, Is.True);
            Assert.That(step.Timer, Is.EqualTo(0f));
        }

        [Test]
        public void 돌진은_지속_시간이_끝나면_경직으로_간다()
        {
            BossChargeStep step = Step(BossChargeState.Charging, 1.5f, 0.2f);

            Assert.That(step.State, Is.EqualTo(BossChargeState.Recover));
            Assert.That(step.EnteredRecover, Is.True);
        }

        [Test]
        public void 벽에_부딪히면_남은_돌진을_자르고_즉시_경직된다()
        {
            // 반격 틈이 벽 충돌의 보상이다 — 남은 시간을 다 굴리면 그 틈이 사라진다.
            BossChargeStep step = Step(BossChargeState.Charging, 0.1f, 0.02f, blocked: true);

            Assert.That(step.State, Is.EqualTo(BossChargeState.Recover));
            Assert.That(step.EnteredRecover, Is.True);
        }

        [Test]
        public void 경직이_끝나면_대기로_돌아가_쿨다운을_다시_센다()
        {
            BossChargeStep step = Step(BossChargeState.Recover, 1.9f, 0.2f);

            Assert.That(step.State, Is.EqualTo(BossChargeState.Ready));
            Assert.That(step.Timer, Is.EqualTo(0f), "쿨다운이 0에서 다시 시작한다");
        }

        [Test]
        public void 한_순환은_대기_예고_돌진_경직을_모두_거친다()
        {
            var state = BossChargeState.Ready;
            float timer = 0f;
            bool sawTelegraph = false;
            bool sawCharge = false;
            bool sawRecover = false;

            // 0.1 s 스텝으로 한 순환(최대 20 s)을 굴린다 — 프레임 크기에 의존하지 않는지 확인.
            for (int i = 0; i < 200; i++)
            {
                BossChargeStep step = Step(state, timer, 0.1f);
                state = step.State;
                timer = step.Timer;

                sawTelegraph |= step.EnteredTelegraph;
                sawCharge |= step.EnteredCharge;
                sawRecover |= step.EnteredRecover;
            }

            Assert.That(sawTelegraph, Is.True);
            Assert.That(sawCharge, Is.True);
            Assert.That(sawRecover, Is.True);
        }

        [Test]
        public void 돌진_속도는_수평_고정_방향으로만_나간다()
        {
            Vector3 velocity = BossChargeMath.ComputeChargeVelocity(new Vector3(3f, 5f, 4f), 20f);

            Assert.That(velocity.y, Is.EqualTo(0f));
            Assert.That(velocity.magnitude, Is.EqualTo(20f).Within(0.001f));
            Assert.That(velocity.x, Is.EqualTo(12f).Within(0.001f), "수평 (3,4) 정규화 × 20");
            Assert.That(velocity.z, Is.EqualTo(16f).Within(0.001f));
        }

        [Test]
        public void 방향이_비면_돌진_속도가_0이다()
        {
            Assert.That(BossChargeMath.ComputeChargeVelocity(Vector3.zero, 20f), Is.EqualTo(Vector3.zero));
            Assert.That(BossChargeMath.ComputeChargeVelocity(Vector3.up, 20f), Is.EqualTo(Vector3.zero));
        }
    }
}
