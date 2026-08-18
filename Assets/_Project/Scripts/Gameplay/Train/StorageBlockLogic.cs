namespace Game.Gameplay.Train
{
    /// <summary>
    /// 창고 저장 블록의 순수 계산 (건축 개편 2차 — 계획서 §2.8, 결정 ⑦).
    /// 저장 구조는 "블록 소유자 목록(_blockOwners) + 평탄 슬롯 목록(_slots)"이고,
    /// 블록 i의 슬롯 = i × 블록당 슬롯 수 + 칸 내 슬롯이다. 블록 제거는 swap-remove
    /// (마지막 블록을 빈자리로 이동 — Id 매핑은 소유자 목록이 담보)로 복제량을 아낀다.
    /// MonoBehaviour·NetworkList 비의존 — EditMode 테스트 대상.
    /// </summary>
    public static class StorageBlockLogic
    {
        /// <summary>블록의 평탄 슬롯 시작 오프셋.</summary>
        public static int SlotOffset(int blockIndex, int slotsPerBlock)
        {
            return blockIndex * slotsPerBlock;
        }

        /// <summary>
        /// swap-remove 계획 — 제거할 블록 자리에 마지막 블록을 복사해야 하는지, 어느 블록을
        /// 옮기는지 알려준다. 제거 대상이 이미 마지막이면 이동 없이 꼬리만 잘라낸다.
        /// 적용(리스트 되쓰기)은 호출부(TrainStorage)의 몫이다.
        /// </summary>
        public static bool TryPlanSwapRemove(int blockCount, int removeIndex, out int moveFromBlock)
        {
            moveFromBlock = -1;
            if (removeIndex < 0 || removeIndex >= blockCount)
            {
                return false;
            }

            int last = blockCount - 1;
            if (removeIndex != last)
            {
                moveFromBlock = last;
            }

            return true;
        }
    }
}
