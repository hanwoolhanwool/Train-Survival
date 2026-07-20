using Game.Gameplay.Harpoon;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>집게 훅 비행 단계 전이 검증 (발사 → 명중/빗나감 → 되감기·부착).</summary>
    public sealed class HarpoonHookMotionTests
    {
        private const float ImpactPause = 0.12f;
        private const float WaitingTimeout = 1.5f;

        private static HarpoonHookMotion Create()
        {
            return new HarpoonHookMotion(ImpactPause, WaitingTimeout);
        }

        [Test]
        public void 초기_상태는_Idle이다()
        {
            Assert.That(Create().Phase, Is.EqualTo(HookPhase.Idle));
        }

        [Test]
        public void 발사하면_Flying이_된다()
        {
            HarpoonHookMotion motion = Create();
            motion.StartFlying();

            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Flying));
        }

        [Test]
        public void 그랩_가능_명중은_WaitingForServer로_전이한다()
        {
            HarpoonHookMotion motion = Create();
            motion.StartFlying();

            motion.NotifyGrabbableHit();

            Assert.That(motion.Phase, Is.EqualTo(HookPhase.WaitingForServer));
        }

        [Test]
        public void 빗나감은_ImpactPause를_거쳐_자동으로_Retracting이_된다()
        {
            HarpoonHookMotion motion = Create();
            motion.StartFlying();

            motion.NotifyMiss();
            Assert.That(motion.Phase, Is.EqualTo(HookPhase.ImpactPause), "즉시 되감지 않고 잠깐 정지해야 한다");

            bool transitioned = motion.Tick(ImpactPause - 0.01f);
            Assert.That(transitioned, Is.False);
            Assert.That(motion.Phase, Is.EqualTo(HookPhase.ImpactPause));

            transitioned = motion.Tick(0.02f);
            Assert.That(transitioned, Is.True);
            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Retracting));
        }

        [Test]
        public void 대기_타임아웃이_지나면_안전장치로_Retracting이_된다()
        {
            HarpoonHookMotion motion = Create();
            motion.StartFlying();
            motion.NotifyGrabbableHit();

            motion.Tick(WaitingTimeout + 0.1f);

            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Retracting), "호스트 응답이 끝내 오지 않으면 되감기로 폴백해야 한다");
        }

        [Test]
        public void 승인되면_WaitingForServer에서_Attached로_전이한다()
        {
            HarpoonHookMotion motion = Create();
            motion.StartFlying();
            motion.NotifyGrabbableHit();

            motion.Attach();

            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Attached));
        }

        [Test]
        public void 거부되면_WaitingForServer에서_즉시_Retracting으로_전이한다()
        {
            HarpoonHookMotion motion = Create();
            motion.StartFlying();
            motion.NotifyGrabbableHit();

            motion.BeginRetract();

            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Retracting), "거부는 피격 정지 없이 즉시 되감아야 한다");
        }

        [Test]
        public void Attached_중_강제_해제되면_Retracting으로_전이한다()
        {
            HarpoonHookMotion motion = Create();
            motion.StartFlying();
            motion.NotifyGrabbableHit();
            motion.Attach();

            motion.BeginRetract();

            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Retracting));
        }

        [Test]
        public void 되감기_도착_통지는_Idle로_전이한다()
        {
            HarpoonHookMotion motion = Create();
            motion.StartFlying();
            motion.NotifyMiss();
            motion.Tick(ImpactPause + 0.01f);

            motion.NotifyRetractArrived();

            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Idle));
        }

        [Test]
        public void Idle에서의_되감기_도착_통지는_무시된다()
        {
            HarpoonHookMotion motion = Create();

            motion.NotifyRetractArrived();

            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Idle));
        }

        [Test]
        public void Cancel은_어떤_상태에서든_Idle로_되돌린다()
        {
            HarpoonHookMotion motion = Create();
            motion.StartFlying();
            motion.NotifyGrabbableHit();
            motion.Attach();

            motion.Cancel();

            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Idle));
        }

        [Test]
        public void Idle에서의_Attach와_BeginRetract는_무시된다()
        {
            HarpoonHookMotion motion = Create();

            motion.Attach();
            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Idle));

            motion.BeginRetract();
            Assert.That(motion.Phase, Is.EqualTo(HookPhase.Idle));
        }
    }
}
