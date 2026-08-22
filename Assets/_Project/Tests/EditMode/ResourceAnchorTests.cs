using Game.Gameplay.Inventory;
using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 자원 앵커 선택의 순수 판정 — 클리어 존(레벨 디자인 가이드 §4.2)과
    /// 종류 ↔ 지형 성격 짝(§5.2)이 규칙으로 고정되는지 검증한다.
    /// </summary>
    public sealed class ResourceAnchorTests
    {
        private const float MinLateral = 4f;
        private const float MaxLateral = 16f;
        private const float MaxZ = 20f;

        [TearDown]
        public void TearDown()
        {
            // 정적 레지스트리는 테스트 간에 새지 않게 비운다 (프로젝트 공통 규약).
            ResourceAnchor.ClearRegistry();
        }

        [Test]
        public void 측면_대역_안이고_Z가_가까우면_적격이다()
        {
            Assert.IsTrue(ResourceAnchor.IsEligible(
                new Vector3(10f, 0f, 5f), 0f, MinLateral, MaxLateral, MaxZ));
        }

        [Test]
        public void 갑판_쪽_대역은_부적격이다()
        {
            // 4 m 미만은 열차·하차 동선이라 자원을 심지 않는다.
            Assert.IsFalse(ResourceAnchor.IsEligible(
                new Vector3(2f, 0f, 0f), 0f, MinLateral, MaxLateral, MaxZ));
        }

        [Test]
        public void 집게_사거리_밖은_부적격이다()
        {
            // 16 m 초과는 1단계 집게(사거리 20 m)로 영원히 닿지 않는다.
            Assert.IsFalse(ResourceAnchor.IsEligible(
                new Vector3(24f, 0f, 0f), 0f, MinLateral, MaxLateral, MaxZ));
        }

        [Test]
        public void 좌우_어느_쪽이든_대역만_맞으면_적격이다()
        {
            Assert.IsTrue(ResourceAnchor.IsEligible(
                new Vector3(-10f, 0f, 0f), 0f, MinLateral, MaxLateral, MaxZ));
        }

        [Test]
        public void 목표_Z에서_너무_멀면_부적격이다()
        {
            Assert.IsFalse(ResourceAnchor.IsEligible(
                new Vector3(10f, 0f, 30f), 0f, MinLateral, MaxLateral, MaxZ));
        }

        [Test]
        public void 종류가_어울리는_지형_성격을_고른다()
        {
            Assert.AreEqual(ResourceAnchorKind.Rock, ResourceAnchor.PreferredKindFor(ResourceType.OreVein));
            Assert.AreEqual(ResourceAnchorKind.Water, ResourceAnchor.PreferredKindFor(ResourceType.Timber));
            Assert.AreEqual(ResourceAnchorKind.Wreck, ResourceAnchor.PreferredKindFor(ResourceType.Scrap));
            Assert.AreEqual(ResourceAnchorKind.Ground, ResourceAnchor.PreferredKindFor(ResourceType.Wood));
        }

        [Test]
        public void 미등재_종류는_평지로_떨어진다()
        {
            // 어느 앵커든 쓸 수 있어야 새 자원이 추가돼도 스폰이 멈추지 않는다.
            Assert.AreEqual(ResourceAnchorKind.Ground, ResourceAnchor.PreferredKindFor(ResourceType.None));
        }

        [Test]
        public void 앵커가_없으면_고르지_못한다()
        {
            // 폴백(기존 랜덤 좌표) 경로가 살아 있다는 뜻 — 팔레트 없는 지역에서 자원이 끊기지 않는다.
            Assert.IsNull(ResourceAnchor.TryPick(0f, MinLateral, MaxLateral, MaxZ, ResourceAnchorKind.Ground));
        }
    }
}
