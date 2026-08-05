using Game.Gameplay.Combat;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 총기 장탄부 상태 머신 검증 (M2 리볼버 → M5 2차 총기 공통화).
    /// M5부터 재장전은 예비 탄약을 소모한다 — 완료 조건 = 시간 경과 AND 호스트 차감 확정.
    /// </summary>
    public sealed class GunMagazineTests
    {
        private const int ReserveEnough = 99;

        private static GunMagazine Create()
        {
            return new GunMagazine(capacity: 6, fireInterval: 0.4f, reloadDuration: 2f);
        }

        [Test]
        public void 시작_시_만탄이다()
        {
            var magazine = Create();

            Assert.That(magazine.RoundsLoaded, Is.EqualTo(6));
            Assert.That(magazine.IsReloading, Is.False);
        }

        [Test]
        public void 발사하면_1발_소모된다()
        {
            var magazine = Create();

            Assert.That(magazine.TryFire(), Is.True);
            Assert.That(magazine.RoundsLoaded, Is.EqualTo(5));
        }

        [Test]
        public void 발사_간격_안에는_연사가_거부된다()
        {
            var magazine = Create();
            magazine.TryFire();

            Assert.That(magazine.TryFire(), Is.False);

            magazine.Tick(0.5f);
            Assert.That(magazine.TryFire(), Is.True);
        }

        [Test]
        public void 탄이_없으면_발사가_거부된다()
        {
            var magazine = Create();
            for (int i = 0; i < 6; i++)
            {
                magazine.Tick(0.5f);
                magazine.TryFire();
            }

            Assert.That(magazine.RoundsLoaded, Is.EqualTo(0));
            magazine.Tick(0.5f);
            Assert.That(magazine.TryFire(), Is.False);
        }

        [Test]
        public void 시간과_확정이_모두_지나야_장전이_끝난다()
        {
            var magazine = Create();
            magazine.TryFire();

            Assert.That(magazine.TryStartReload(ReserveEnough), Is.True);
            Assert.That(magazine.IsReloading, Is.True);
            Assert.That(magazine.PendingLoad, Is.EqualTo(1), "빈 약실만큼 요청");

            magazine.Tick(1f);
            Assert.That(magazine.IsReloading, Is.True, "재장전 시간 미경과");
            Assert.That(magazine.TryFire(), Is.False, "재장전 중 발사 금지");

            magazine.Tick(1.1f);
            Assert.That(magazine.IsReloading, Is.True, "시간이 지나도 확정 전에는 장전되지 않는다");

            magazine.ConfirmPendingLoad(1);
            Assert.That(magazine.IsReloading, Is.False);
            Assert.That(magazine.RoundsLoaded, Is.EqualTo(6));
        }

        [Test]
        public void 확정이_먼저_와도_시간이_지나야_장전된다()
        {
            var magazine = Create();
            magazine.TryFire();
            magazine.TryStartReload(ReserveEnough);

            magazine.ConfirmPendingLoad(1);
            Assert.That(magazine.IsReloading, Is.True, "확정이 와도 시간 전에는 미완료");

            magazine.Tick(2.1f);
            Assert.That(magazine.IsReloading, Is.False);
            Assert.That(magazine.RoundsLoaded, Is.EqualTo(6));
        }

        [Test]
        public void 예비가_요청보다_적으면_확정_발수만_장전된다()
        {
            var magazine = Create();
            for (int i = 0; i < 4; i++)
            {
                magazine.Tick(0.5f);
                magazine.TryFire();
            }

            Assert.That(magazine.TryStartReload(ReserveEnough), Is.True);
            Assert.That(magazine.PendingLoad, Is.EqualTo(4));

            magazine.Tick(2.1f);
            magazine.ConfirmPendingLoad(2);

            Assert.That(magazine.RoundsLoaded, Is.EqualTo(4), "2 + 확정 2");
        }

        [Test]
        public void 확정_0이면_재장전이_취소된다()
        {
            var magazine = Create();
            magazine.TryFire();
            magazine.TryStartReload(ReserveEnough);

            magazine.ConfirmPendingLoad(0);

            Assert.That(magazine.IsReloading, Is.False);
            Assert.That(magazine.RoundsLoaded, Is.EqualTo(5), "장전 없음");
            magazine.Tick(0.5f);
            Assert.That(magazine.TryFire(), Is.True, "취소 후 남은 탄으로 즉시 발사 가능");
        }

        [Test]
        public void 예비가_없으면_재장전이_거부된다()
        {
            var magazine = Create();
            magazine.TryFire();

            Assert.That(magazine.TryStartReload(0), Is.False);
            Assert.That(magazine.IsReloading, Is.False);
        }

        [Test]
        public void 요청_발수는_빈_약실과_예비량의_최솟값이다()
        {
            var magazine = Create();
            for (int i = 0; i < 3; i++)
            {
                magazine.Tick(0.5f);
                magazine.TryFire();
            }

            Assert.That(magazine.TryStartReload(2), Is.True, "빈 약실 3, 예비 2");
            Assert.That(magazine.PendingLoad, Is.EqualTo(2));
        }

        [Test]
        public void 만탄이면_재장전이_거부된다()
        {
            var magazine = Create();

            Assert.That(magazine.TryStartReload(ReserveEnough), Is.False);
        }

        [Test]
        public void 재장전_중_재장전은_거부된다()
        {
            var magazine = Create();
            magazine.TryFire();
            magazine.TryStartReload(ReserveEnough);

            Assert.That(magazine.TryStartReload(ReserveEnough), Is.False);
        }
    }
}
