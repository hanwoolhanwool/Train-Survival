using Game.Gameplay.Harpoon;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 그랩 유지 중 무기 전환 게이트 (집게 단계별 파지 계획 §3.2) — 상태 7종 × 등급 3종 × 대상 3종 전수.
    /// <para>
    /// 이 규칙은 M5 6차 2차의 "무기를 바꿔도 파지는 유지된다"를 <b>부분 번복</b>한다: 자유 전환은
    /// 2·3단계의 보상으로 재배치되고, 1단계는 잡으면 손이 묶이며, 양손 무기는 전환 시 그랩을 잃는다.
    /// 따라서 "1단계로 몬스터를 든 채 리볼버로 쏘기"가 막히는 것은 회귀가 아니라 설계 변경이다.
    /// </para>
    /// </summary>
    public sealed class HarpoonSwitchRulesTests
    {
        // 대상 3종 — 게이트가 실제로 구분하는 것은 "양손인가" 하나뿐이라 자원·빈 칸은 한손과 같은 편이다.
        private const bool OneHandedWeapon = false;
        private const bool TwoHandedWeapon = true;
        private const bool ResourceOrEmpty = false;

        private static readonly HarpoonState[] GrabHeldStates =
        {
            HarpoonState.PendingGrab, HarpoonState.Reeling, HarpoonState.Holding
        };

        private static readonly HarpoonState[] FreeStates =
        {
            HarpoonState.Ready, HarpoonState.Firing, HarpoonState.MissRecovery, HarpoonState.Cooldown
        };

        [Test]
        public void 잡고_있지_않으면_등급과_무관하게_전환된다()
        {
            foreach (HarpoonState state in FreeStates)
            {
                for (int tier = 1; tier <= 3; tier++)
                {
                    Assert.That(HarpoonSwitchRules.Evaluate(state, tier, OneHandedWeapon),
                        Is.EqualTo(SwitchOutcome.Allow), $"{state}/{tier}/한손");
                    Assert.That(HarpoonSwitchRules.Evaluate(state, tier, TwoHandedWeapon),
                        Is.EqualTo(SwitchOutcome.Allow), $"{state}/{tier}/양손");
                    Assert.That(HarpoonSwitchRules.Evaluate(state, tier, ResourceOrEmpty),
                        Is.EqualTo(SwitchOutcome.Allow), $"{state}/{tier}/자원");
                }
            }
        }

        [Test]
        public void 투사체_비행_중은_아직_잡은_것이_없으므로_막지_않는다()
        {
            // 명중 전에 무기를 바꾸면 기존 규약대로 Firing이 이어지다 승인 시점에 정리된다.
            Assert.That(HarpoonSwitchRules.Evaluate(HarpoonState.Firing, 1, TwoHandedWeapon),
                Is.EqualTo(SwitchOutcome.Allow));
        }

        [Test]
        public void 일단계는_잡고_있는_동안_무엇으로도_바꿀_수_없다()
        {
            foreach (HarpoonState state in GrabHeldStates)
            {
                Assert.That(HarpoonSwitchRules.Evaluate(state, 1, OneHandedWeapon),
                    Is.EqualTo(SwitchOutcome.Deny), $"{state}/한손");
                Assert.That(HarpoonSwitchRules.Evaluate(state, 1, TwoHandedWeapon),
                    Is.EqualTo(SwitchOutcome.Deny), $"{state}/양손");
                Assert.That(HarpoonSwitchRules.Evaluate(state, 1, ResourceOrEmpty),
                    Is.EqualTo(SwitchOutcome.Deny), $"{state}/자원");
            }
        }

        [Test]
        public void 이단계_이상은_잡은_채_한손_무기로_바꿀_수_있다()
        {
            foreach (HarpoonState state in GrabHeldStates)
            {
                for (int tier = HarpoonSwitchRules.HandFreeingTier; tier <= 3; tier++)
                {
                    Assert.That(HarpoonSwitchRules.Evaluate(state, tier, OneHandedWeapon),
                        Is.EqualTo(SwitchOutcome.Allow), $"{state}/{tier}/한손");
                    Assert.That(HarpoonSwitchRules.Evaluate(state, tier, ResourceOrEmpty),
                        Is.EqualTo(SwitchOutcome.Allow), $"{state}/{tier}/자원");
                }
            }
        }

        [Test]
        public void 이단계_이상이_양손_무기를_고르면_먼저_놓는다()
        {
            foreach (HarpoonState state in GrabHeldStates)
            {
                for (int tier = HarpoonSwitchRules.HandFreeingTier; tier <= 3; tier++)
                {
                    Assert.That(HarpoonSwitchRules.Evaluate(state, tier, TwoHandedWeapon),
                        Is.EqualTo(SwitchOutcome.ReleaseThenAllow), $"{state}/{tier}/양손");
                }
            }
        }

        [Test]
        public void 승인_대기_중에도_게이트가_걸린다()
        {
            // PendingGrab을 뚫어 두면 승인이 오는 사이에 양손 무기로 바꾼 뒤 뒤늦게 잡히는 구멍이 생긴다.
            Assert.That(HarpoonSwitchRules.Evaluate(HarpoonState.PendingGrab, 1, OneHandedWeapon),
                Is.EqualTo(SwitchOutcome.Deny));
            Assert.That(HarpoonSwitchRules.Evaluate(HarpoonState.PendingGrab, 2, TwoHandedWeapon),
                Is.EqualTo(SwitchOutcome.ReleaseThenAllow));
        }

        [Test]
        public void 손잡이_그랩도_같은_규칙을_받는다()
        {
            // 이탈 저항 중(Anchor 그랩도 Reeling·Holding을 밟는다) 1단계는 손이 묶인다.
            // "이탈 칸은 1단계도 잡는다"와 모순되지 않는다 — 잡을 수는 있고, 잡은 동안 못 바꿀 뿐이다.
            Assert.That(HarpoonSwitchRules.Evaluate(HarpoonState.Reeling, 1, OneHandedWeapon),
                Is.EqualTo(SwitchOutcome.Deny));
        }

        [Test]
        public void 잡고_있는_상태의_정의는_승인대기_릴감기_파지_셋이다()
        {
            Assert.That(HarpoonSwitchRules.IsGrabHeld(HarpoonState.PendingGrab), Is.True);
            Assert.That(HarpoonSwitchRules.IsGrabHeld(HarpoonState.Reeling), Is.True);
            Assert.That(HarpoonSwitchRules.IsGrabHeld(HarpoonState.Holding), Is.True);

            Assert.That(HarpoonSwitchRules.IsGrabHeld(HarpoonState.Ready), Is.False);
            Assert.That(HarpoonSwitchRules.IsGrabHeld(HarpoonState.Firing), Is.False);
            Assert.That(HarpoonSwitchRules.IsGrabHeld(HarpoonState.MissRecovery), Is.False);
            Assert.That(HarpoonSwitchRules.IsGrabHeld(HarpoonState.Cooldown), Is.False);
        }
    }
}
