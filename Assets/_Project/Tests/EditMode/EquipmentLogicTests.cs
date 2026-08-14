using Game.Gameplay.Inventory;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>장비 착용·해제·효과 합산 규칙 검증 (기획서 §6.3 — 의류/방어구, M5 3차).</summary>
    public sealed class EquipmentLogicTests
    {
        /// <summary>테스트용 부위 판정 — 가죽 옷·사막 로브 = 상체, 고철 투구 = 머리. 그 외는 장비 아님.</summary>
        private static bool Resolve(HotbarItemType item, out EquipSlot slot)
        {
            switch (item)
            {
                case HotbarItemType.LeatherCoat:
                case HotbarItemType.DesertRobe:
                    slot = EquipSlot.Body;
                    return true;
                case HotbarItemType.ScrapHelmet:
                    slot = EquipSlot.Head;
                    return true;
                default:
                    slot = default;
                    return false;
            }
        }

        private static HotbarSlotView[] EmptyEquipment()
        {
            return new HotbarSlotView[4];
        }

        [Test]
        public void 장비는_자기_부위_칸에_착용된다()
        {
            var slots = new[] { new HotbarSlotView(HotbarItemType.LeatherCoat, 1) };
            HotbarSlotView[] equipment = EmptyEquipment();

            Assert.That(EquipmentLogic.TryEquip(slots, equipment, 0, Resolve), Is.True);
            Assert.That(equipment[(int)EquipSlot.Body].ItemType, Is.EqualTo(HotbarItemType.LeatherCoat));
            Assert.That(slots[0].IsEmpty, Is.True);
        }

        [Test]
        public void 점유된_부위에_착용하면_자리를_맞바꾼다()
        {
            // 상체 슬롯 경쟁 (가죽 옷 vs 사막 로브) — 숲 방한과 사막 내열의 선택 구도.
            var slots = new[] { new HotbarSlotView(HotbarItemType.DesertRobe, 1) };
            HotbarSlotView[] equipment = EmptyEquipment();
            equipment[(int)EquipSlot.Body] = new HotbarSlotView(HotbarItemType.LeatherCoat, 1);

            Assert.That(EquipmentLogic.TryEquip(slots, equipment, 0, Resolve), Is.True);
            Assert.That(equipment[(int)EquipSlot.Body].ItemType, Is.EqualTo(HotbarItemType.DesertRobe));
            Assert.That(slots[0].ItemType, Is.EqualTo(HotbarItemType.LeatherCoat), "이전 장비가 그 칸으로 돌아온다");
        }

        [Test]
        public void 장비가_아닌_아이템은_착용되지_않는다()
        {
            var slots = new[]
            {
                new HotbarSlotView(HotbarItemType.Revolver, 1),
                new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Wood),
                new HotbarSlotView(HotbarItemType.None, 0),
            };
            HotbarSlotView[] equipment = EmptyEquipment();

            Assert.That(EquipmentLogic.TryEquip(slots, equipment, 0, Resolve), Is.False, "무기");
            Assert.That(EquipmentLogic.TryEquip(slots, equipment, 1, Resolve), Is.False, "자원");
            Assert.That(EquipmentLogic.TryEquip(slots, equipment, 2, Resolve), Is.False, "빈 칸");
        }

        [Test]
        public void 해제는_첫_빈_칸으로_돌아오고_빈_칸이_없으면_실패한다()
        {
            HotbarSlotView[] equipment = EmptyEquipment();
            equipment[(int)EquipSlot.Head] = new HotbarSlotView(HotbarItemType.ScrapHelmet, 1);

            var full = new[] { new HotbarSlotView(HotbarItemType.Revolver, 1) };
            Assert.That(EquipmentLogic.TryUnequip(equipment, (int)EquipSlot.Head, full), Is.False,
                "빈 칸 없음 — 장비는 착용 칸에 보존된다");
            Assert.That(equipment[(int)EquipSlot.Head].IsEmpty, Is.False);

            var slots = new[] { new HotbarSlotView(HotbarItemType.Revolver, 1), new HotbarSlotView(HotbarItemType.None, 0) };
            Assert.That(EquipmentLogic.TryUnequip(equipment, (int)EquipSlot.Head, slots), Is.True);
            Assert.That(slots[1].ItemType, Is.EqualTo(HotbarItemType.ScrapHelmet));
            Assert.That(equipment[(int)EquipSlot.Head].IsEmpty, Is.True);
        }

        [Test]
        public void 빈_부위_해제는_실패한다()
        {
            var slots = new[] { new HotbarSlotView(HotbarItemType.None, 0) };

            Assert.That(EquipmentLogic.TryUnequip(EmptyEquipment(), (int)EquipSlot.Feet, slots), Is.False);
            Assert.That(EquipmentLogic.TryUnequip(EmptyEquipment(), 99, slots), Is.False, "범위 밖");
        }

        [Test]
        public void 피해_배율은_합산_감소를_상한으로_자른다()
        {
            Assert.That(EquipmentLogic.GetDamageMultiplier(0f, 0.6f), Is.EqualTo(1f), "장비 없음");
            Assert.That(EquipmentLogic.GetDamageMultiplier(0.35f, 0.6f), Is.EqualTo(0.65f).Within(0.001f));
            Assert.That(EquipmentLogic.GetDamageMultiplier(0.9f, 0.6f), Is.EqualTo(0.4f).Within(0.001f),
                "풀셋이라도 상한 0.6 — 무적 방지");
            Assert.That(EquipmentLogic.GetDamageMultiplier(-0.5f, 0.6f), Is.EqualTo(1f), "음수 방어");
        }

        [Test]
        public void 단열_합산은_유효_범위로_잘린다()
        {
            Assert.That(EquipmentLogic.ClampInsulation(0.5f), Is.EqualTo(0.5f));
            Assert.That(EquipmentLogic.ClampInsulation(1.5f), Is.EqualTo(0.9f), "완전 무효화 방지 상한");
            Assert.That(EquipmentLogic.ClampInsulation(-2f), Is.EqualTo(-1f), "역효과 하한");
        }

        [Test]
        public void 체온_상향_합산은_유효_범위로_잘린다()
        {
            Assert.That(EquipmentLogic.ClampBodyWarmth(0.7f), Is.EqualTo(0.7f), "가죽 옷 + 누비 바지");
            Assert.That(EquipmentLogic.ClampBodyWarmth(1.5f), Is.EqualTo(1f), "겹쳐 입어도 경고 임계 밑 안전선");
            Assert.That(EquipmentLogic.ClampBodyWarmth(-0.5f), Is.EqualTo(0f), "체온 상향에 역효과는 없다");
        }

        // ── 방한 세트 4부위 (M7 3차 §2.6) ─────────────────────────────────

        [Test]
        public void 방한_세트_풀셋은_단열_상한에_정확히_닿는다()
        {
            // 후드 0.2 + 파카 0.4 + 바지 0.2 + 부츠 0.15 = 0.95 → 상한 0.9.
            // "4부위를 다 갖춰야 북극 밤을 견딘다"가 수치로 성립한다 (계획 §1).
            Assert.That(EquipmentLogic.ClampInsulation(0.2f + 0.4f + 0.2f + 0.15f), Is.EqualTo(0.9f));
        }

        [Test]
        public void 한_부위라도_비면_단열이_상한에_못_미친다()
        {
            Assert.That(EquipmentLogic.ClampInsulation(0.4f + 0.2f + 0.15f), Is.EqualTo(0.75f).Within(0.001f),
                "머리를 비우면 0.75");
            Assert.That(EquipmentLogic.ClampInsulation(0.2f + 0.2f + 0.15f), Is.EqualTo(0.55f).Within(0.001f),
                "상체를 비우면 0.55 — 손실이 가장 크다");
        }

        [Test]
        public void 방한_세트_체온_상향은_상한에_정확히_닿는다()
        {
            // 0.2 + 0.4 + 0.25 + 0.15 = 1.0 — 36.5 + 1.0 = 37.5 로 고온 경고(38) 밑에 머문다.
            Assert.That(EquipmentLogic.ClampBodyWarmth(0.2f + 0.4f + 0.25f + 0.15f), Is.EqualTo(1f));
        }
    }
}
