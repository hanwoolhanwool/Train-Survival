using Game.Gameplay.Combat;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>리볼버 실린더 상태 머신 검증 (개발 가이드 M2 — 기본 총기 1종).</summary>
    public sealed class RevolverCylinderTests
    {
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
        public void 재장전이_끝나면_만탄으로_복귀한다()
        {
            var cylinder = Create();
            cylinder.TryFire();

            Assert.That(cylinder.TryStartReload(), Is.True);
            Assert.That(cylinder.IsReloading, Is.True);

            cylinder.Tick(1f);
            Assert.That(cylinder.IsReloading, Is.True, "재장전 시간 미경과");
            Assert.That(cylinder.TryFire(), Is.False, "재장전 중 발사 금지");

            cylinder.Tick(1.1f);
            Assert.That(cylinder.IsReloading, Is.False);
            Assert.That(cylinder.RoundsLoaded, Is.EqualTo(6));
        }

        [Test]
        public void 만탄이면_재장전이_거부된다()
        {
            var cylinder = Create();

            Assert.That(cylinder.TryStartReload(), Is.False);
        }

        [Test]
        public void 재장전_중_재장전은_거부된다()
        {
            var cylinder = Create();
            cylinder.TryFire();
            cylinder.TryStartReload();

            Assert.That(cylinder.TryStartReload(), Is.False);
        }
    }
}
