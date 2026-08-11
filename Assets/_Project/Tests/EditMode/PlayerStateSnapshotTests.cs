using Game.Gameplay.Inventory;
using Game.Gameplay.Session;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>재접속 상태 스냅샷 검증 (M6 1차) — 슬롯 보존 왕복·잔여 부활 대기 계산.</summary>
    public sealed class PlayerStateSnapshotTests
    {
        private static PlayerStateSnapshot MakeSnapshot(
            HotbarSlotView[] slots, bool respawnPending = false,
            double deathServerTime = 0d, float respawnDelaySeconds = 0f)
        {
            return new PlayerStateSnapshot(
                slots,
                equipment: new[] { new HotbarSlotView(HotbarItemType.LeatherCoat, 1) },
                harpoonTier: 2,
                health: 55f, hunger: 40f, temperature: 36.2f,
                respawnPending, deathServerTime, respawnDelaySeconds);
        }

        [Test]
        public void 빈_슬롯과_스탯이_그대로_보존된다()
        {
            var slots = new[]
            {
                new HotbarSlotView(HotbarItemType.None, 0),
                new HotbarSlotView(HotbarItemType.None, 0),
            };

            PlayerStateSnapshot snapshot = MakeSnapshot(slots);

            Assert.That(snapshot.Slots, Is.SameAs(slots));
            Assert.That(snapshot.Slots[0].IsEmpty, Is.True);
            Assert.That(snapshot.HarpoonTier, Is.EqualTo(2));
            Assert.That(snapshot.Health, Is.EqualTo(55f));
            Assert.That(snapshot.Hunger, Is.EqualTo(40f));
            Assert.That(snapshot.Temperature, Is.EqualTo(36.2f));
            Assert.That(snapshot.Equipment[0].ItemType, Is.EqualTo(HotbarItemType.LeatherCoat));
        }

        [Test]
        public void 가득_찬_자원_슬롯이_종류와_수량을_유지한다()
        {
            var slots = new[]
            {
                new HotbarSlotView(HotbarItemType.Harpoon, 1),
                new HotbarSlotView(HotbarItemType.Resource, 20, ResourceType.Wood),
                new HotbarSlotView(HotbarItemType.Resource, 20, ResourceType.Scrap),
            };

            PlayerStateSnapshot snapshot = MakeSnapshot(slots);

            Assert.That(snapshot.Slots[1].ItemType, Is.EqualTo(HotbarItemType.Resource));
            Assert.That(snapshot.Slots[1].Resource, Is.EqualTo(ResourceType.Wood));
            Assert.That(snapshot.Slots[1].Count, Is.EqualTo(20));
            Assert.That(snapshot.Slots[2].Resource, Is.EqualTo(ResourceType.Scrap));
        }

        [Test]
        public void 보따리_슬롯은_Count의_보관소_id를_유지한다()
        {
            // Bundle 칸의 Count는 수량이 아니라 서버 보관소 id다 (M5 8차) — 세션 내 재접속에서 유효.
            var slots = new[] { new HotbarSlotView(HotbarItemType.Bundle, 17) };

            PlayerStateSnapshot snapshot = MakeSnapshot(slots);

            Assert.That(snapshot.Slots[0].ItemType, Is.EqualTo(HotbarItemType.Bundle));
            Assert.That(snapshot.Slots[0].Count, Is.EqualTo(17));
        }

        // ── 잔여 부활 대기 (결정 ⑦ — 사망 시각 + 대기 시간 − 현재 시각) ────────

        [Test]
        public void 잔여_대기는_끊겨_있던_시간을_포함해_계산된다()
        {
            PlayerStateSnapshot snapshot = MakeSnapshot(
                new HotbarSlotView[0], respawnPending: true,
                deathServerTime: 100d, respawnDelaySeconds: 30f);

            // 사망 10초 후 재접속 — 접속을 유지한 경우와 같은 시점(사망 + 30초)에 부활한다.
            Assert.That(snapshot.GetRemainingRespawnSeconds(110d), Is.EqualTo(20f).Within(0.0001f));
        }

        [Test]
        public void 대기_시간을_넘겨_재접속하면_잔여가_0_이하다()
        {
            PlayerStateSnapshot snapshot = MakeSnapshot(
                new HotbarSlotView[0], respawnPending: true,
                deathServerTime: 100d, respawnDelaySeconds: 30f);

            // 잔여 ≤ 0 = 즉시 부활 (결정 ⑦).
            Assert.That(snapshot.GetRemainingRespawnSeconds(130d), Is.LessThanOrEqualTo(0f));
            Assert.That(snapshot.GetRemainingRespawnSeconds(500d), Is.LessThan(0f));
        }

        [Test]
        public void 사망_직후_재접속은_대기_시간_전체가_남는다()
        {
            PlayerStateSnapshot snapshot = MakeSnapshot(
                new HotbarSlotView[0], respawnPending: true,
                deathServerTime: 250d, respawnDelaySeconds: 45f);

            Assert.That(snapshot.GetRemainingRespawnSeconds(250d), Is.EqualTo(45f).Within(0.0001f));
        }
    }
}
