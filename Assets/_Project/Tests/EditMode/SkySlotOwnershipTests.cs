using Game.Gameplay.Cycle;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 하늘 슬롯 소유 판정 (레벨 3차 · 미결 ② B안 — 슬롯은 지역, 프로퍼티는 낮/밤 연출).
    /// 이 규칙이 흔들리면 지역 전환과 국면 전환이 서로의 하늘을 덮어쓴다.
    /// </summary>
    public sealed class SkySlotOwnershipTests
    {
        [Test]
        public void 지역_하늘이_걸려_있으면_지역이_주인이다()
        {
            SkySlotOwner owner = SkySlotOwnership.Resolve(
                slotIsRegionSky: true, slotIsOwnInstance: false, hasOwnSource: true);

            Assert.AreEqual(SkySlotOwner.Region, owner);
        }

        [Test]
        public void 지역_하늘이_걸려_있으면_원본이_있어도_복제본을_만들지_않는다()
        {
            // hasOwnSource 가 true 여도 슬롯을 빼앗으면 안 된다 — 지역이 먼저다.
            SkySlotOwner owner = SkySlotOwnership.Resolve(
                slotIsRegionSky: true, slotIsOwnInstance: false, hasOwnSource: true);

            Assert.AreNotEqual(SkySlotOwner.DayCycleNeedsInstance, owner);
        }

        [Test]
        public void 내_복제본이_걸려_있으면_낮밤_연출이_주인이다()
        {
            SkySlotOwner owner = SkySlotOwnership.Resolve(
                slotIsRegionSky: false, slotIsOwnInstance: true, hasOwnSource: true);

            Assert.AreEqual(SkySlotOwner.DayCycle, owner);
        }

        [Test]
        public void 지역_하늘도_내_복제본도_없고_원본이_있으면_복제본을_만든다()
        {
            SkySlotOwner owner = SkySlotOwnership.Resolve(
                slotIsRegionSky: false, slotIsOwnInstance: false, hasOwnSource: true);

            Assert.AreEqual(SkySlotOwner.DayCycleNeedsInstance, owner);
        }

        [Test]
        public void 원본이_없으면_하늘을_아예_건드리지_않는다()
        {
            SkySlotOwner owner = SkySlotOwnership.Resolve(
                slotIsRegionSky: false, slotIsOwnInstance: false, hasOwnSource: false);

            Assert.AreEqual(SkySlotOwner.None, owner);
        }

        [Test]
        public void 씬_기본_스카이박스는_지역_하늘로_치지_않는다()
        {
            // 슬롯이 비어 있지 않다는 것만으로 판정하면 씬 에셋에 직접 쓰게 된다.
            // 그래서 판정 입력은 "슬롯이 차 있는가"가 아니라 "지역이 건 것인가"다.
            SkySlotOwner owner = SkySlotOwnership.Resolve(
                slotIsRegionSky: false, slotIsOwnInstance: false, hasOwnSource: true);

            Assert.AreEqual(SkySlotOwner.DayCycleNeedsInstance, owner,
                "씬 기본 스카이박스가 걸려 있어도 복제본을 만들어 걸어야 한다");
        }

        [Test]
        public void 프로퍼티는_지역_하늘과_내_복제본에만_쓴다()
        {
            Assert.IsTrue(SkySlotOwnership.CanWriteProperties(SkySlotOwner.Region));
            Assert.IsTrue(SkySlotOwnership.CanWriteProperties(SkySlotOwner.DayCycle));
            Assert.IsFalse(SkySlotOwnership.CanWriteProperties(SkySlotOwner.None));
            Assert.IsFalse(SkySlotOwnership.CanWriteProperties(SkySlotOwner.DayCycleNeedsInstance));
        }

        [Test]
        public void 슬롯_원복은_내_복제본일_때만이다()
        {
            Assert.IsTrue(SkySlotOwnership.ShouldRestoreSlot(SkySlotOwner.DayCycle));

            // 지역이 건 하늘을 되돌리면 지역이 바뀐 적도 없는데 하늘이 씬 기본값으로 튄다.
            Assert.IsFalse(SkySlotOwnership.ShouldRestoreSlot(SkySlotOwner.Region));
            Assert.IsFalse(SkySlotOwnership.ShouldRestoreSlot(SkySlotOwner.None));
            Assert.IsFalse(SkySlotOwnership.ShouldRestoreSlot(SkySlotOwner.DayCycleNeedsInstance));
        }
    }
}
