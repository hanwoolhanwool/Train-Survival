namespace Game.Gameplay.Inventory
{
    /// <summary>
    /// 컨테이너 간 슬롯 이동의 순수 규칙 (M5 3차 — 공유 창고).
    /// 개인 인벤토리와 창고가 같은 슬롯 표현(<see cref="HotbarSlotView"/>)을 쓰므로
    /// 방향(넣기/빼기/창고 내 재배치)과 무관하게 한 함수가 판정한다.
    /// 슬롯 배열을 직접 수정한다 — 호출자(TrainStorage)가 권위 상태로의 반영을 책임진다.
    /// </summary>
    public static class StorageLogic
    {
        /// <summary>
        /// from 칸의 내용을 to 칸으로 옮긴다 — 빈 칸이면 전량 이동, 같은 자원이면 병합(상한까지,
        /// 잔량은 from에 남는다), 그 외에는 스왑. stackSize는 병합 대상 종류의 스택 상한
        /// (호출자가 카탈로그에서 푼다). 같은 배열의 같은 칸이거나 from이 비어 있으면 실패.
        /// </summary>
        public static bool TryTransfer(
            HotbarSlotView[] from, int fromIndex, HotbarSlotView[] to, int toIndex, int stackSize)
        {
            if (from == null || to == null
                || fromIndex < 0 || fromIndex >= from.Length
                || toIndex < 0 || toIndex >= to.Length)
            {
                return false;
            }

            if (ReferenceEquals(from, to) && fromIndex == toIndex)
            {
                return false;
            }

            HotbarSlotView source = from[fromIndex];
            if (source.IsEmpty)
            {
                return false;
            }

            HotbarSlotView target = to[toIndex];
            if (target.IsEmpty)
            {
                to[toIndex] = source;
                from[fromIndex] = new HotbarSlotView(HotbarItemType.None, 0);
                return true;
            }

            if (source.ItemType == HotbarItemType.Resource
                && target.ItemType == HotbarItemType.Resource
                && source.Resource == target.Resource)
            {
                int moved = System.Math.Min(source.Count, stackSize - target.Count);
                if (moved > 0)
                {
                    to[toIndex] = new HotbarSlotView(HotbarItemType.Resource, target.Count + moved, target.Resource);
                    int remaining = source.Count - moved;
                    from[fromIndex] = remaining > 0
                        ? new HotbarSlotView(HotbarItemType.Resource, remaining, source.Resource)
                        : new HotbarSlotView(HotbarItemType.None, 0);
                    return true;
                }

                // 대상 스택이 가득이면 스왑으로 떨어진다 — 수량 교환도 유효한 재배치다.
            }

            to[toIndex] = source;
            from[fromIndex] = target;
            return true;
        }

        /// <summary>
        /// 보따리 내용물의 창고 풀기 판정 (M5 8차 — 3차에서 스왑 지원). 먼저 현 배치 그대로
        /// 전량 적재를 시도하고, 안 되면 대상 칸(toIndex)이 점유된 경우에 한해 그 칸이 보따리
        /// 자리로 나가는 스왑 결과 기준(비운 뒤)으로 재시도한다. 성공 시에만 result(새 배열)와
        /// swappedOut(스왑으로 나온 칸 — 스왑이 없었으면 빈 뷰)을 내어 준다. storage는 변형하지
        /// 않는다 — 호출자가 result 반영과 swappedOut의 보따리 자리 배치를 책임진다.
        /// </summary>
        public static bool TryUnpackBundle(
            HotbarSlotView[] storage, int toIndex, HotbarSlotView[] contents,
            System.Func<ResourceType, int> stackSizeOf,
            out HotbarSlotView[] result, out HotbarSlotView swappedOut)
        {
            result = null;
            swappedOut = new HotbarSlotView(HotbarItemType.None, 0);
            if (storage == null || contents == null)
            {
                return false;
            }

            HotbarSlotView[] attempt = (HotbarSlotView[])storage.Clone();
            if (HotbarLogic.TryAddAll(attempt, contents, stackSizeOf))
            {
                result = attempt;
                return true;
            }

            if (toIndex < 0 || toIndex >= storage.Length || storage[toIndex].IsEmpty)
            {
                return false;
            }

            attempt = (HotbarSlotView[])storage.Clone();
            HotbarSlotView vacated = attempt[toIndex];
            attempt[toIndex] = new HotbarSlotView(HotbarItemType.None, 0);
            if (HotbarLogic.TryAddAll(attempt, contents, stackSizeOf))
            {
                result = attempt;
                swappedOut = vacated;
                return true;
            }

            return false;
        }
    }
}
