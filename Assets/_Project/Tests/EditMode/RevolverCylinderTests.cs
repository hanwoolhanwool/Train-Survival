using Game.Gameplay.Combat;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 리볼버 실린더 상태 머신 검증 (개발 가이드 M2 — 기본 총기 1종).
    /// M5부터 재장전은 예비 탄약을 소모한다 — 완료 조건 = 시간 경과 AND 호스트 차감 확정.
    /// </summary>
    public sealed class RevolverCylinderTests
    {
        private const int ReserveEnough = 99;

        private static RevolverCylinder Create()
        {
            return new RevolverCylinder(capacity: 6, fireInterval: 0.4f, reloadDuration: 2f);
        }

        [Test]
        public void 시작_시_만탄이다()
        {
            var cylinder = Create();

            Assert.That(cylinder.RoundsLoaded, Is.EqualTo(6));
            Assert.That(cylinder.IsReloading, Is.False);
        }

        [Test]
        public void 발사하면_1발_소모된다()
        {
            var cylinder = Create();

            Assert.That(cylinder.TryFire(), Is.True);
            Assert.That(cylinder.RoundsLoaded, Is.EqualTo(5));
        }

        [Test]
        public void 발사_간격_안에는_연사가_거부된다()
        {
            var cylinder = Create();
            cylinder.TryFire();

            Assert.That(cylinder.TryFire(), Is.False);

            cylinder.Tick(0.5f);
            Assert.That(cylinder.TryFire(), Is.True);
        }

        [Test]
        public void 탄이_없으면_발사가_거부된다()
        {
            var cylinder = Create();
            for (int i = 0; i < 6; i++)
            {
                cylinder.Tick(0.5f);
                cylinder.TryFire();
            }

            Assert.That(cylinder.RoundsLoaded, Is.EqualTo(0));
            cylinder.Tick(0.5f);
            Assert.That(cylinder.TryFire(), Is.False);
        }

        [Test]
        public void 시간과_확정이_모두_지나야_장전이_끝난다()
        {
            var cylinder = Create();
            cylinder.TryFire();

            Assert.That(cylinder.TryStartReload(ReserveEnough), Is.True);
            Assert.That(cylinder.IsReloading, Is.True);
            Assert.That(cylinder.PendingLoad, Is.EqualTo(1), "빈 약실만큼 요청");

            cylinder.Tick(1f);
            Assert.That(cylinder.IsReloading, Is.True, "재장전 시간 미경과");
            Assert.That(cylinder.TryFire(), Is.False, "재장전 중 발사 금지");

            cylinder.Tick(1.1f);
            Assert.That(cylinder.IsReloading, Is.True, "시간이 지나도 확정 전에는 장전되지 않는다");

            cylinder.ConfirmPendingLoad(1);
            Assert.That(cylinder.IsReloading, Is.False);
            Assert.That(cylinder.RoundsLoaded, Is.EqualTo(6));
        }

        [Test]
        public void 확정이_먼저_와도_시간이_지나야_장전된다()
        {
            var cylinder = Create();
            cylinder.TryFire();
            cylinder.TryStartReload(ReserveEnough);

            cylinder.ConfirmPendingLoad(1);
            Assert.That(cylinder.IsReloading, Is.True, "확정이 와도 시간 전에는 미완료");

            cylinder.Tick(2.1f);
            Assert.That(cylinder.IsReloading, Is.False);
            Assert.That(cylinder.RoundsLoaded, Is.EqualTo(6));
        }

        [Test]
        public void 예비가_요청보다_적으면_확정_발수만_장전된다()
        {
            var cylinder = Create();
            for (int i = 0; i < 4; i++)
            {
                cylinder.Tick(0.5f);
                cylinder.TryFire();
            }

            Assert.That(cylinder.TryStartReload(ReserveEnough), Is.True);
            Assert.That(cylinder.PendingLoad, Is.EqualTo(4));

            cylinder.Tick(2.1f);
            cylinder.ConfirmPendingLoad(2);

            Assert.That(cylinder.RoundsLoaded, Is.EqualTo(4), "2 + 확정 2");
        }

        [Test]
        public void 확정_0이면_재장전이_취소된다()
        {
            var cylinder = Create();
            cylinder.TryFire();
            cylinder.TryStartReload(ReserveEnough);

            cylinder.ConfirmPendingLoad(0);

            Assert.That(cylinder.IsReloading, Is.False);
            Assert.That(cylinder.RoundsLoaded, Is.EqualTo(5), "장전 없음");
            cylinder.Tick(0.5f);
            Assert.That(cylinder.TryFire(), Is.True, "취소 후 남은 탄으로 즉시 발사 가능");
        }

        [Test]
        public void 예비가_없으면_재장전이_거부된다()
        {
            var cylinder = Create();
            cylinder.TryFire();

            Assert.That(cylinder.TryStartReload(0), Is.False);
            Assert.That(cylinder.IsReloading, Is.False);
        }

        [Test]
        public void 요청_발수는_빈_약실과_예비량의_최솟값이다()
        {
            var cylinder = Create();
            for (int i = 0; i < 3; i++)
            {
                cylinder.Tick(0.5f);
                cylinder.TryFire();
            }

            Assert.That(cylinder.TryStartReload(2), Is.True, "빈 약실 3, 예비 2");
            Assert.That(cylinder.PendingLoad, Is.EqualTo(2));
        }

        [Test]
        public void 만탄이면_재장전이_거부된다()
        {
            var cylinder = Create();

            Assert.That(cylinder.TryStartReload(ReserveEnough), Is.False);
        }

        [Test]
        public void 재장전_중_재장전은_거부된다()
        {
            var cylinder = Create();
            cylinder.TryFire();
            cylinder.TryStartReload(ReserveEnough);

            Assert.That(cylinder.TryStartReload(ReserveEnough), Is.False);
        }
    }
}
