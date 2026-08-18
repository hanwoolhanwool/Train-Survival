using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 창고 저장 블록 순수 로직 검증 (건축 개편 2차 — 계획서 §2.8, 결정 ⑦).
    /// 저장 구조: 블록 소유자 목록 + 평탄 슬롯(블록 i의 슬롯 = i × 블록당 슬롯 수 + 칸 내 슬롯).
    /// 블록 제거는 swap-remove — 잔여 블록의 Id 매핑 무결이 핵심이다.
    /// </summary>
    public sealed class StorageBlockLogicTests
    {
        /// <summary>
        /// 소유자 목록에서 블록을 찾는 조회 — 런타임은 복제 목록(NetworkList)을 그대로 훑으므로
        /// (사본을 만들지 않는다) 여기서는 같은 규칙을 배열 위에 재현해 swap-remove 결과를 검증한다.
        /// </summary>
        private static int FindBlock(System.Collections.Generic.IReadOnlyList<ushort> owners, int storageId)
        {
            for (int i = 0; i < owners.Count; i++)
            {
                if (owners[i] == storageId)
                {
                    return i;
                }
            }

            return -1;
        }

        [Test]
        public void 슬롯_오프셋은_블록_인덱스_곱이다()
        {
            Assert.That(StorageBlockLogic.SlotOffset(0, 10), Is.EqualTo(0));
            Assert.That(StorageBlockLogic.SlotOffset(2, 10), Is.EqualTo(20));
        }

        [Test]
        public void swap_remove는_마지막_블록을_빈자리로_옮긴다()
        {
            // 3블록에서 가운데(1) 제거 — 마지막 블록(2)이 그 자리로 온다.
            Assert.That(StorageBlockLogic.TryPlanSwapRemove(3, 1, out int moveFrom), Is.True);
            Assert.That(moveFrom, Is.EqualTo(2));

            // 마지막 블록 제거 — 이동 없이 꼬리만 잘라낸다.
            Assert.That(StorageBlockLogic.TryPlanSwapRemove(3, 2, out moveFrom), Is.True);
            Assert.That(moveFrom, Is.EqualTo(-1));

            // 단일 블록 제거도 이동 없음.
            Assert.That(StorageBlockLogic.TryPlanSwapRemove(1, 0, out moveFrom), Is.True);
            Assert.That(moveFrom, Is.EqualTo(-1));
        }

        [Test]
        public void 범위_밖_제거는_기각된다()
        {
            Assert.That(StorageBlockLogic.TryPlanSwapRemove(3, 3, out _), Is.False);
            Assert.That(StorageBlockLogic.TryPlanSwapRemove(3, -1, out _), Is.False);
            Assert.That(StorageBlockLogic.TryPlanSwapRemove(0, 0, out _), Is.False);
        }

        [Test]
        public void swap_remove_적용_후_잔여_블록의_Id_매핑이_무결하다()
        {
            // TrainStorage가 계획(TryPlanSwapRemove)을 적용하는 절차를 배열로 재현한다 —
            // 소유자 목록과 슬롯이 같은 규칙으로 움직여야 Id 조회가 계속 맞는다.
            var owners = new System.Collections.Generic.List<ushort> { 7, 12, 3 };
            var slots = new System.Collections.Generic.List<int>();
            const int SlotsPerBlock = 2;
            for (int b = 0; b < owners.Count; b++)
            {
                for (int s = 0; s < SlotsPerBlock; s++)
                {
                    slots.Add(owners[b] * 100 + s); // 내용물 표식 = 소유자 Id 기반
                }
            }

            int remove = FindBlock(owners, 7);
            Assert.That(StorageBlockLogic.TryPlanSwapRemove(owners.Count, remove, out int moveFrom), Is.True);
            if (moveFrom >= 0)
            {
                int destination = StorageBlockLogic.SlotOffset(remove, SlotsPerBlock);
                int source = StorageBlockLogic.SlotOffset(moveFrom, SlotsPerBlock);
                for (int s = 0; s < SlotsPerBlock; s++)
                {
                    slots[destination + s] = slots[source + s];
                }

                owners[remove] = owners[moveFrom];
            }

            slots.RemoveRange(StorageBlockLogic.SlotOffset(owners.Count - 1, SlotsPerBlock), SlotsPerBlock);
            owners.RemoveAt(owners.Count - 1);

            // 남은 창고(12·3)의 내용물이 각자 소유자 기준으로 그대로 조회된다.
            int block12 = FindBlock(owners, 12);
            int block3 = FindBlock(owners, 3);
            Assert.That(slots[StorageBlockLogic.SlotOffset(block12, SlotsPerBlock)], Is.EqualTo(1200));
            Assert.That(slots[StorageBlockLogic.SlotOffset(block3, SlotsPerBlock)], Is.EqualTo(300));
            Assert.That(FindBlock(owners, 7), Is.EqualTo(-1), "제거된 창고는 조회 불가");
        }
    }
}
