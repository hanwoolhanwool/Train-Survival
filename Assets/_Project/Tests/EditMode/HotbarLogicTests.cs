using Game.Gameplay.Inventory;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>통합 핫바 규칙 검증 (기획서 §3.4 — 무기+자원 5칸, 자유 배치, 슬롯당 스택).</summary>
    public sealed class HotbarLogicTests
    {
        private const int StackSize = 5;

        private static HotbarSlotView[] CreateDefaultSlots()
        {
            // 시작 배치: 1번 집게 · 2번 리볼버 · 3~5번 빈 칸.
            return new[]
            {
                new HotbarSlotView(HotbarItemType.Harpoon, 1),
                new HotbarSlotView(HotbarItemType.Revolver, 1),
                new HotbarSlotView(HotbarItemType.None, 0),
                new HotbarSlotView(HotbarItemType.None, 0),
                new HotbarSlotView(HotbarItemType.None, 0),
            };
        }

        [Test]
        public void 자원은_빈_칸에_새_스택으로_적재된다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();

            Assert.That(HotbarLogic.TryAddResource(slots, ResourceType.Wood, StackSize), Is.True);
            Assert.That(slots[2].ItemType, Is.EqualTo(HotbarItemType.Resource));
            Assert.That(slots[2].Resource, Is.EqualTo(ResourceType.Wood));
            Assert.That(slots[2].Count, Is.EqualTo(1));
        }

        [Test]
        public void 여유_있는_기존_스택을_빈_칸보다_먼저_채운다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Wood);

            HotbarLogic.TryAddResource(slots, ResourceType.Wood, StackSize);

            Assert.That(slots[3].Count, Is.EqualTo(3), "기존 스택 우선");
            Assert.That(slots[2].IsEmpty, Is.True, "빈 칸은 그대로");
        }

        [Test]
        public void 전_슬롯이_차면_적재가_실패한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            for (int i = 2; i < 5; i++)
            {
                slots[i] = new HotbarSlotView(HotbarItemType.Resource, StackSize, ResourceType.Wood);
            }

            Assert.That(HotbarLogic.TryAddResource(slots, ResourceType.Wood, StackSize), Is.False,
                "가득 → 획득 불가·낙하 규칙");
        }

        [Test]
        public void 차감은_뒤에서부터_스택을_비우고_빈_칸으로_되돌린다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Wood);
            slots[4] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Wood);

            Assert.That(HotbarLogic.TryRemoveResource(slots, ResourceType.Wood), Is.True);
            Assert.That(slots[4].IsEmpty, Is.True, "뒤쪽 스택(1개)이 먼저 비워진다");
            Assert.That(slots[2].Count, Is.EqualTo(3));
        }

        [Test]
        public void 자원이_없으면_차감이_실패한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();

            Assert.That(HotbarLogic.TryRemoveResource(slots, ResourceType.Wood), Is.False);
        }

        [Test]
        public void 지정_칸_차감은_다른_칸을_건드리지_않는다()
        {
            // 엔진 투입은 든 칸(선택 슬롯)만 소모해야 한다 — 3번 칸을 들고 눌러도 4번 칸이 줄면 안 된다.
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 5);
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 3);

            Assert.That(HotbarLogic.TryRemoveResourceAt(slots, 2), Is.True);
            Assert.That(slots[2].Count, Is.EqualTo(4), "든 칸(3번)만 줄어든다");
            Assert.That(slots[3].Count, Is.EqualTo(3), "다른 칸(4번)은 그대로");
        }

        [Test]
        public void 지정_칸이_자원이_아니면_차감이_실패한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();

            Assert.That(HotbarLogic.TryRemoveResourceAt(slots, 0), Is.False, "집게 칸은 소모 대상이 아니다");
            Assert.That(HotbarLogic.TryRemoveResourceAt(slots, 4), Is.False, "빈 칸도 실패");
        }

        [Test]
        public void 총량과_상한은_현재_배치를_따른다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 4, ResourceType.Wood);

            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Wood), Is.EqualTo(4));
            Assert.That(HotbarLogic.ResourceCapacity(slots, StackSize), Is.EqualTo(15), "무기 2칸 제외 3칸 × 5");
        }

        [Test]
        public void 같은_종류의_스택에만_병합된다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Wood);

            Assert.That(HotbarLogic.TryAddResource(slots, ResourceType.Stone, StackSize), Is.True);
            Assert.That(slots[2].Count, Is.EqualTo(2), "목재 스택은 돌과 병합되지 않는다");
            Assert.That(slots[3].Resource, Is.EqualTo(ResourceType.Stone), "돌은 새 칸에 새 스택");
            Assert.That(slots[3].Count, Is.EqualTo(1));
        }

        [Test]
        public void 같은_종류는_기존_스택을_먼저_채운다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Wood);

            Assert.That(HotbarLogic.TryAddResource(slots, ResourceType.Wood, StackSize), Is.True);
            Assert.That(slots[3].Count, Is.EqualTo(3));
            Assert.That(slots[2].IsEmpty, Is.True, "빈 칸은 그대로");
        }

        [Test]
        public void 종류_차감은_그_종류만_뒤에서부터_비운다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Wood);
            slots[4] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Stone);

            Assert.That(HotbarLogic.TryRemoveResource(slots, ResourceType.Wood), Is.True);
            Assert.That(slots[2].Count, Is.EqualTo(2), "목재만 줄어든다");
            Assert.That(slots[4].Count, Is.EqualTo(1), "돌(더 뒤 칸)은 종류가 달라 건드리지 않는다");

            Assert.That(HotbarLogic.TryRemoveResource(slots, ResourceType.Scrap), Is.False, "없는 종류는 실패");
        }

        [Test]
        public void 조건_차감은_조건에_맞는_종류를_건너뛰지_않고_고른다()
        {
            // 건설 비용은 건자재만 소모해야 한다 — 탄약이 비용으로 새면 안 된다.
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Wood);
            slots[4] = new HotbarSlotView(HotbarItemType.Resource, 4, ResourceType.RevolverAmmo);

            static bool IsBuildMaterial(ResourceType type) => type == ResourceType.Wood;

            Assert.That(HotbarLogic.TryRemoveAnyResource(slots, IsBuildMaterial), Is.True);
            Assert.That(slots[2].Count, Is.EqualTo(1), "건자재(목재)가 소모된다");
            Assert.That(slots[4].Count, Is.EqualTo(4), "탄약(뒤 칸)은 조건 밖이라 그대로");
        }

        [Test]
        public void 조건_차감은_소모된_종류를_돌려준다()
        {
            // HUD 소모 미리보기가 이 반환값으로 내역을 집계한다 (M5 검증 E1).
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Wood);
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Scrap);

            Assert.That(HotbarLogic.TryRemoveAnyResource(slots, _ => true, out ResourceType first), Is.True);
            Assert.That(first, Is.EqualTo(ResourceType.Scrap), "뒤 칸(고철)부터 소모");
            Assert.That(HotbarLogic.TryRemoveAnyResource(slots, _ => true, out ResourceType second), Is.True);
            Assert.That(second, Is.EqualTo(ResourceType.Wood));
            Assert.That(HotbarLogic.TryRemoveAnyResource(slots, _ => true, out ResourceType none), Is.False);
            Assert.That(none, Is.EqualTo(ResourceType.None), "실패 시 None");
        }

        [Test]
        public void 종류별_총량과_조건_총량을_구분해_센다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Wood);
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Niter);

            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Wood), Is.EqualTo(3));
            Assert.That(HotbarLogic.CountResource(slots, ResourceType.Scrap), Is.EqualTo(0));
            Assert.That(HotbarLogic.CountResource(slots, t => t == ResourceType.Wood || t == ResourceType.Niter), Is.EqualTo(5));
        }

        [Test]
        public void 지정_칸_차감은_종류를_보존한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Scrap);

            Assert.That(HotbarLogic.TryRemoveResourceAt(slots, 2), Is.True);
            Assert.That(slots[2].Resource, Is.EqualTo(ResourceType.Scrap), "부분 차감 후에도 종류 유지");
            Assert.That(slots[2].Count, Is.EqualTo(2));
        }

        [Test]
        public void 슬롯_교환은_범위_안의_서로_다른_두_칸만_유효하다()
        {
            Assert.That(HotbarLogic.IsValidSwap(0, 4, 5), Is.True);
            Assert.That(HotbarLogic.IsValidSwap(2, 2, 5), Is.False, "같은 칸");
            Assert.That(HotbarLogic.IsValidSwap(-1, 3, 5), Is.False);
            Assert.That(HotbarLogic.IsValidSwap(0, 5, 5), Is.False);
        }

        [Test]
        public void 무기_아이템은_첫_빈_칸에_1개_적재된다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[2] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Wood);

            Assert.That(HotbarLogic.TryAddItem(slots, HotbarItemType.Shotgun), Is.True);
            Assert.That(slots[3].ItemType, Is.EqualTo(HotbarItemType.Shotgun), "첫 빈 칸");
            Assert.That(slots[3].Count, Is.EqualTo(1), "무기는 스택 없음");
        }

        [Test]
        public void 빈_칸이_없으면_무기_적재가_실패한다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i].IsEmpty)
                {
                    slots[i] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Stone);
                }
            }

            Assert.That(HotbarLogic.TryAddItem(slots, HotbarItemType.Melee), Is.False);
        }

        [Test]
        public void 무효_아이템_종류는_적재가_거부된다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();

            Assert.That(HotbarLogic.TryAddItem(slots, HotbarItemType.None), Is.False);
            Assert.That(HotbarLogic.TryAddItem(slots, HotbarItemType.Resource), Is.False, "자원은 TryAddResource 소관");
        }

        // ── 버리기 (M5 3차 — hotbar 명세 §11 해소 · 수량 지정은 M5 8차) ──────────────────

        [Test]
        public void 보유량_이상을_요청하면_자원_칸을_전량_비운다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 4, ResourceType.Stone);

            Assert.That(HotbarLogic.TryTakeFromResourceSlot(
                slots, 3, requested: 99, out ResourceType type, out int taken), Is.True);
            Assert.That(type, Is.EqualTo(ResourceType.Stone));
            Assert.That(taken, Is.EqualTo(4), "보유량으로 클램프 — 스택 전량");
            Assert.That(slots[3].IsEmpty, Is.True);
        }

        [Test]
        public void 부분_수량_버리기는_잔량을_남긴다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 5, ResourceType.Stone);

            Assert.That(HotbarLogic.TryTakeFromResourceSlot(
                slots, 3, requested: 2, out ResourceType type, out int taken), Is.True);
            Assert.That(type, Is.EqualTo(ResourceType.Stone));
            Assert.That(taken, Is.EqualTo(2));
            Assert.That(slots[3].ItemType, Is.EqualTo(HotbarItemType.Resource), "잔량 스택 유지");
            Assert.That(slots[3].Count, Is.EqualTo(3));
            Assert.That(slots[3].Resource, Is.EqualTo(ResourceType.Stone));
        }

        [Test]
        public void 마지막_한_개를_버리면_칸이_비워진다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Wood);

            Assert.That(HotbarLogic.TryTakeFromResourceSlot(slots, 3, 1, out _, out int taken), Is.True);
            Assert.That(taken, Is.EqualTo(1));
            Assert.That(slots[3].IsEmpty, Is.True, "잔량 0 = 빈 칸");
        }

        // ── 보따리 일괄 획득 (M5 8차 — 1차 검증 개선 2) ──────────────────

        [Test]
        public void 일괄_획득은_전부_들어갈_때만_수납한다()
        {
            var slots = new HotbarSlotView[3];
            slots[0] = new HotbarSlotView(HotbarItemType.Resource, 3, ResourceType.Wood);
            var contents = new[]
            {
                new HotbarSlotView(HotbarItemType.Resource, 2, ResourceType.Wood),
                new HotbarSlotView(HotbarItemType.Melee, 1),
            };

            Assert.That(HotbarLogic.TryAddAll(slots, contents, _ => 5), Is.True);
            Assert.That(slots[0].Count, Is.EqualTo(5), "자원은 스택 병합 (3 + 2)");
            Assert.That(slots[1].ItemType, Is.EqualTo(HotbarItemType.Melee), "무기는 빈 칸 1개");
        }

        [Test]
        public void 일괄_획득은_한_칸이라도_부족하면_실패한다()
        {
            var slots = new HotbarSlotView[2];
            slots[0] = new HotbarSlotView(HotbarItemType.Harpoon, 1);
            slots[1] = new HotbarSlotView(HotbarItemType.Resource, 5, ResourceType.Stone);
            var contents = new[]
            {
                new HotbarSlotView(HotbarItemType.Resource, 1, ResourceType.Stone), // 스택 만재(5/5) + 빈 칸 없음
            };

            Assert.That(HotbarLogic.TryAddAll(slots, contents, _ => 5), Is.False,
                "실패 시 반영은 호출자 몫 — 복사본 위에서 부르고 버린다");
        }

        [Test]
        public void 일괄_획득은_보따리_아이템의_보관_id를_보존한다()
        {
            var slots = new HotbarSlotView[2];
            var contents = new[]
            {
                new HotbarSlotView(HotbarItemType.Bundle, 37), // Count = 보관 id
            };

            Assert.That(HotbarLogic.TryAddAll(slots, contents, _ => 5), Is.True);
            Assert.That(slots[0].ItemType, Is.EqualTo(HotbarItemType.Bundle));
            Assert.That(slots[0].Count, Is.EqualTo(37), "Count는 수량이 아니라 보관 id — 그대로 옮긴다");
        }

        [Test]
        public void 무기_칸과_빈_칸과_수량_0은_버릴_수_없다()
        {
            HotbarSlotView[] slots = CreateDefaultSlots();
            slots[3] = new HotbarSlotView(HotbarItemType.Resource, 4, ResourceType.Stone);

            Assert.That(HotbarLogic.TryTakeFromResourceSlot(slots, 0, 1, out _, out _), Is.False,
                "무기는 버리기 불가 — 처분은 공유 창고 보관");
            Assert.That(slots[0].ItemType, Is.EqualTo(HotbarItemType.Harpoon), "무기 칸은 그대로");
            Assert.That(HotbarLogic.TryTakeFromResourceSlot(slots, 2, 1, out _, out _), Is.False, "빈 칸");
            Assert.That(HotbarLogic.TryTakeFromResourceSlot(slots, 99, 1, out _, out _), Is.False, "범위 밖");
            Assert.That(HotbarLogic.TryTakeFromResourceSlot(slots, 3, 0, out _, out _), Is.False, "수량 0 기각");
            Assert.That(slots[3].Count, Is.EqualTo(4), "기각 시 무변");
        }
    }
}
