using Game.Gameplay.Harpoon;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>집게 상태 머신 전이 규칙 검증 (슬라이스 스펙 §2.1~§2.3).</summary>
    public sealed class HarpoonStateMachineTests
    {
        private const float MissRecovery = 2.5f;
        private const float Cooldown = 0.5f;

        private static HarpoonStateMachine Create()
        {
            return new HarpoonStateMachine(MissRecovery, Cooldown);
        }

        [Test]
        public void 초기_상태는_Ready다()
        {
            Assert.That(Create().State, Is.EqualTo(HarpoonState.Ready));
        }

        [Test]
        public void Ready에서_발사하면_Firing이_된다()
        {
            HarpoonStateMachine sm = Create();

            Assert.That(sm.TryFire(), Is.True);
            Assert.That(sm.State, Is.EqualTo(HarpoonState.Firing));
        }

        [Test]
        public void Firing_중_재발사는_거부된다()
        {
            HarpoonStateMachine sm = Create();
            sm.TryFire();

            Assert.That(sm.TryFire(), Is.False);
        }

        [Test]
        public void 투사체_비행_중_취소는_무효다()
        {
            HarpoonStateMachine sm = Create();
            sm.TryFire();

            Assert.That(sm.TryCancel(), Is.False, "§2.1 — 비행 중 취소로 미스 페널티를 회피하는 우회 차단");
            Assert.That(sm.State, Is.EqualTo(HarpoonState.Firing));
        }

        [Test]
        public void 로컬_명중_후_승인되면_Reeling이_된다()
        {
            HarpoonStateMachine sm = Create();
            sm.TryFire();
            sm.NotifyLocalHit();
            Assert.That(sm.State, Is.EqualTo(HarpoonState.PendingGrab));

            sm.NotifyGrabApproved();
            Assert.That(sm.State, Is.EqualTo(HarpoonState.Reeling));
        }

        [Test]
        public void 호스트_거부는_미스_페널티로_전환된다()
        {
            HarpoonStateMachine sm = Create();
            sm.TryFire();
            sm.NotifyLocalHit();

            sm.NotifyGrabRejected();

            Assert.That(sm.State, Is.EqualTo(HarpoonState.MissRecovery));
            Assert.That(sm.RemainingLockTime, Is.EqualTo(MissRecovery));
        }

        [Test]
        public void 빗나가면_미스_페널티_동안_재발사가_불가하다()
        {
            HarpoonStateMachine sm = Create();
            sm.TryFire();
            sm.NotifyMiss();

            Assert.That(sm.State, Is.EqualTo(HarpoonState.MissRecovery));
            Assert.That(sm.TryFire(), Is.False);

            sm.Tick(MissRecovery - 0.1f);
            Assert.That(sm.TryFire(), Is.False, "회수 시간이 끝나기 전에는 재발사 불가");

            sm.Tick(0.2f);
            Assert.That(sm.State, Is.EqualTo(HarpoonState.Ready));
            Assert.That(sm.TryFire(), Is.True);
        }

        [Test]
        public void 릴_감기_중_취소는_페널티_없이_쿨다운만_적용된다()
        {
            HarpoonStateMachine sm = Create();
            sm.TryFire();
            sm.NotifyLocalHit();
            sm.NotifyGrabApproved();

            Assert.That(sm.TryCancel(), Is.True);
            Assert.That(sm.State, Is.EqualTo(HarpoonState.Cooldown));
            Assert.That(sm.RemainingLockTime, Is.EqualTo(Cooldown), "§2.1 — 취소는 발사 쿨다운 0.5 s만 적용");

            sm.Tick(Cooldown + 0.01f);
            Assert.That(sm.State, Is.EqualTo(HarpoonState.Ready));
        }

        [Test]
        public void 대상_도착_후_쿨다운을_거쳐_Ready로_복귀한다()
        {
            HarpoonStateMachine sm = Create();
            sm.TryFire();
            sm.NotifyLocalHit();
            sm.NotifyGrabApproved();
            sm.NotifyTargetArrived();

            Assert.That(sm.State, Is.EqualTo(HarpoonState.Cooldown));
            sm.Tick(Cooldown);
            Assert.That(sm.State, Is.EqualTo(HarpoonState.Ready));
        }

        // ── 파지 (M5 6차) — Reeling --도착(Held)--> Holding --놓기--> Cooldown ─────

        private static HarpoonStateMachine CreateHolding()
        {
            HarpoonStateMachine sm = Create();
            sm.TryFire();
            sm.NotifyLocalHit();
            sm.NotifyGrabApproved();
            sm.NotifyHeld();
            return sm;
        }

        [Test]
        public void 도착이_파지면_Holding으로_전환한다()
        {
            Assert.That(CreateHolding().State, Is.EqualTo(HarpoonState.Holding));
        }

        [Test]
        public void Holding_중_재발사는_거부된다()
        {
            HarpoonStateMachine sm = CreateHolding();

            Assert.That(sm.TryFire(), Is.False, "파지 중 좌클릭은 Ready가 아니므로 자연 차단");
            Assert.That(sm.State, Is.EqualTo(HarpoonState.Holding));
        }

        [Test]
        public void Holding에서_놓기는_페널티_없이_쿨다운만_적용된다()
        {
            HarpoonStateMachine sm = CreateHolding();

            Assert.That(sm.TryCancel(), Is.True, "우클릭·무기 교체 = 놓기 — 기존 취소 경로 재사용");
            Assert.That(sm.State, Is.EqualTo(HarpoonState.Cooldown));
            Assert.That(sm.RemainingLockTime, Is.EqualTo(Cooldown));
        }

        [Test]
        public void Holding에서_강제_해제는_쿨다운으로_전환한다()
        {
            HarpoonStateMachine sm = CreateHolding();

            sm.NotifyForcedRelease();

            Assert.That(sm.State, Is.EqualTo(HarpoonState.Cooldown), "파지 중 대상 사망·디스폰 — 놓기 ③");
        }

        [Test]
        public void 강제_해제는_PendingGrab과_Reeling_모두에서_쿨다운으로_전환한다()
        {
            HarpoonStateMachine pending = Create();
            pending.TryFire();
            pending.NotifyLocalHit();
            pending.NotifyForcedRelease();
            Assert.That(pending.State, Is.EqualTo(HarpoonState.Cooldown));

            HarpoonStateMachine reeling = Create();
            reeling.TryFire();
            reeling.NotifyLocalHit();
            reeling.NotifyGrabApproved();
            reeling.NotifyForcedRelease();
            Assert.That(reeling.State, Is.EqualTo(HarpoonState.Cooldown));
        }
    }
}
