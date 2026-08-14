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
        public void 끊김_위치가_보존된다()
        {
            var snapshot = new PlayerStateSnapshot(
                new HotbarSlotView[0], new HotbarSlotView[0], 1, 100f, 50f, 36.5f,
                respawnPending: false, deathServerTime: 0d, respawnDelaySeconds: 0f,
                position: new UnityEngine.Vector3(1.5f, 5f, -12f));

            Assert.That(snapshot.Position.x, Is.EqualTo(1.5f));
            Assert.That(snapshot.Position.y, Is.EqualTo(5f));
            Assert.That(snapshot.Position.z, Is.EqualTo(-12f));
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

        // ── 부위별 동상 복원 (M7 3차) ─────────────────────────────────────

        [Test]
        public void 동상_단계가_스냅샷을_왕복한다()
        {
            // 버프가 스냅샷에서 의도적으로 제외된 것과 달리 동상은 누적 상태라 복원해야 한다 —
            // 지워지면 "잠깐 나갔다 오면 낫는다"가 성립해 버린다.
            byte packed = Game.Gameplay.Player.FrostbiteMath.Pack(
                Game.Gameplay.Player.FrostbiteStage.Severe,
                Game.Gameplay.Player.FrostbiteStage.None,
                Game.Gameplay.Player.FrostbiteStage.Mild,
                Game.Gameplay.Player.FrostbiteStage.Severe);

            var snapshot = new PlayerStateSnapshot(
                new HotbarSlotView[0], new HotbarSlotView[0], harpoonTier: 1,
                health: 80f, hunger: 50f, temperature: 34.5f,
                respawnPending: false, deathServerTime: 0d, respawnDelaySeconds: 0f,
                position: default, frostbiteStages: packed);

            Assert.That(snapshot.FrostbiteStages, Is.EqualTo(packed));
            Assert.That(
                Game.Gameplay.Player.FrostbiteMath.SumStages(snapshot.FrostbiteStages),
                Is.EqualTo(5));
        }

        [Test]
        public void 동상_인자를_생략하면_기본이_동상_없음이다()
        {
            // 무회귀 — M6 1차의 기존 호출부(인자 9개)가 그대로 컴파일되고 동상 0으로 남는다.
            PlayerStateSnapshot snapshot = MakeSnapshot(new HotbarSlotView[0]);

            Assert.That(snapshot.FrostbiteStages, Is.EqualTo(0));
        }
    }
}
