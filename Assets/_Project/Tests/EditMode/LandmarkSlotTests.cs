using Game.Gameplay.World;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 랜드마크 자리의 희소도 판정과 개수 규격 (레벨 디자인 가이드 §4.4).
    /// 소비자(배치기)는 계획 3차에 붙지만, 규칙은 자리를 저작하기 전에 고정돼 있어야
    /// 2차에 만든 세그먼트를 나중에 다시 열지 않는다.
    /// </summary>
    public sealed class LandmarkSlotTests
    {
        [TearDown]
        public void TearDown()
        {
            LandmarkSlot.ClearRegistry();
        }

        [Test]
        public void 같은_희소도의_자리는_적격이다()
        {
            Assert.IsTrue(LandmarkSlot.IsEligible(LandmarkRarity.Rare, LandmarkRarity.Rare));
        }

        [Test]
        public void 더_진귀한_자리에는_흔한_것도_놓을_수_있다()
        {
            Assert.IsTrue(LandmarkSlot.IsEligible(LandmarkRarity.Rare, LandmarkRarity.Common));
        }

        [Test]
        public void 흔한_자리에_진귀한_것은_놓지_않는다()
        {
            // 흔한 자리에 유적을 놓으면 "지나쳤음을 깨닫게" 하는 힘이 사라진다.
            Assert.IsFalse(LandmarkSlot.IsEligible(LandmarkRarity.Common, LandmarkRarity.Rare));
            Assert.IsFalse(LandmarkSlot.IsEligible(LandmarkRarity.Uncommon, LandmarkRarity.Rare));
        }

        [Test]
        public void 자리가_없으면_null이다()
        {
            // 랜드마크는 없어도 되는 것이라 폴백이 필요 없다 — 자원과 다른 점이다.
            Assert.IsNull(LandmarkSlot.TryPick(LandmarkRarity.Common));
        }

        [Test]
        public void 슬롯_개수_상한은_1이다()
        {
            Assert.IsTrue(ClearZoneRules.IsLandmarkSlotCountValid(0));
            Assert.IsTrue(ClearZoneRules.IsLandmarkSlotCountValid(1));
            Assert.IsFalse(ClearZoneRules.IsLandmarkSlotCountValid(2));
        }
    }
}
