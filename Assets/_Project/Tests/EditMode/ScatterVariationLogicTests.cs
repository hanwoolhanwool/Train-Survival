using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 스캐터 변주(레벨 디자인 가이드 §4.5)의 순수 계산 — 팔레트를 늘리지 않고 반복 인지를 줄이는
    /// 주 장치라, 경계에서 조용히 어긋나면 "같은 걸 봤다"가 그대로 돌아온다.
    /// </summary>
    public sealed class ScatterVariationLogicTests
    {
        [Test]
        public void 밀도가_0이면_언제나_숨긴다()
        {
            Assert.IsFalse(ScatterVariationLogic.ShouldShow(0f, 0f));
            Assert.IsFalse(ScatterVariationLogic.ShouldShow(0f, 0.999f));
        }

        [Test]
        public void 밀도가_1이면_언제나_보인다()
        {
            // Random.value는 1.0을 낼 수 있다 — 단순 비교만 두면 밀도 1인 슬롯이 가끔 사라진다.
            Assert.IsTrue(ScatterVariationLogic.ShouldShow(1f, 0f));
            Assert.IsTrue(ScatterVariationLogic.ShouldShow(1f, 1f));
        }

        [Test]
        public void 난수가_밀도보다_작을_때만_보인다()
        {
            Assert.IsTrue(ScatterVariationLogic.ShouldShow(0.5f, 0.49f));
            Assert.IsFalse(ScatterVariationLogic.ShouldShow(0.5f, 0.5f));
            Assert.IsFalse(ScatterVariationLogic.ShouldShow(0.5f, 0.51f));
        }

        [Test]
        public void 회전_지터는_난수에_비례한다()
        {
            Assert.AreEqual(0f, ScatterVariationLogic.YawFor(0f, 360f), 1e-4f);
            Assert.AreEqual(180f, ScatterVariationLogic.YawFor(0.5f, 360f), 1e-4f);
            Assert.AreEqual(360f, ScatterVariationLogic.YawFor(1f, 360f), 1e-4f);
        }

        [Test]
        public void 지터_폭이_0이면_저작된_방향을_지킨다()
        {
            // 비대칭 프롭(이정표·신호기)은 방향이 뜻을 가지므로 돌리지 않는 선택지가 필요하다.
            Assert.AreEqual(0f, ScatterVariationLogic.YawFor(0.7f, 0f), 1e-4f);
        }

        [Test]
        public void 배율은_범위_안에서_보간된다()
        {
            Assert.AreEqual(0.8f, ScatterVariationLogic.ScaleFor(0f, 0.8f, 1.2f), 1e-4f);
            Assert.AreEqual(1f, ScatterVariationLogic.ScaleFor(0.5f, 0.8f, 1.2f), 1e-4f);
            Assert.AreEqual(1.2f, ScatterVariationLogic.ScaleFor(1f, 0.8f, 1.2f), 1e-4f);
        }

        [Test]
        public void 범위가_뒤집혀_있어도_안전하다()
        {
            // 인스펙터에서 min·max를 거꾸로 넣는 일은 실제로 일어난다 — 그때 0배로 사라지면 안 된다.
            Assert.AreEqual(0.8f, ScatterVariationLogic.ScaleFor(0f, 1.2f, 0.8f), 1e-4f);
            Assert.AreEqual(1.2f, ScatterVariationLogic.ScaleFor(1f, 1.2f, 0.8f), 1e-4f);
        }

        [Test]
        public void 난수가_범위를_벗어나도_배율이_튀지_않는다()
        {
            Assert.AreEqual(1.2f, ScatterVariationLogic.ScaleFor(5f, 0.8f, 1.2f), 1e-4f);
            Assert.AreEqual(0.8f, ScatterVariationLogic.ScaleFor(-3f, 0.8f, 1.2f), 1e-4f);
        }

        [Test]
        public void 슬롯_개수_기준은_4에서_10이다()
        {
            Assert.IsFalse(ClearZoneRules.IsScatterSlotCountValid(3));
            Assert.IsTrue(ClearZoneRules.IsScatterSlotCountValid(4));
            Assert.IsTrue(ClearZoneRules.IsScatterSlotCountValid(10));
            Assert.IsFalse(ClearZoneRules.IsScatterSlotCountValid(11));
        }

        [Test]
        public void 스캐터_아래_콜라이더는_결함이다()
        {
            // 변주는 피어마다 다르다 — 그 아래 콜라이더는 없는 벽을 도는 몬스터를 만든다.
            var probe = new ColliderProbe(
                new Bounds(new Vector3(10f, 1f, 0f), new Vector3(2f, 2f, 2f)),
                isTrigger: false, isMesh: false, hasSurfaceMarker: true, isTrackStructure: false,
                isUnderScatterSlot: true);

            Assert.IsTrue((ClearZoneRules.Evaluate(probe) & ClearZoneIssue.ColliderUnderScatterSlot) != 0);
        }
    }
}
