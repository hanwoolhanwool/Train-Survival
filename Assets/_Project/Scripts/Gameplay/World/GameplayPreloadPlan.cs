namespace Game.Gameplay.World
{
    /// <summary>
    /// 출발 시점에 필요한 지형 타일을 <b>미리 똑같이</b> 계산한다 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §5.2.
    ///
    /// <para><b>여기가 이 계획에서 가장 값싼 승리다.</b> 스트리머가 런타임에 하는 선택이
    /// 결정론적 순수 함수(<see cref="SegmentPickLogic.PickForTile"/>)이므로, 로딩 시점에
    /// 같은 답을 낼 수 있다. 프리웜이 "적당히 많이 만들어 두기"가 아니라
    /// <b>정확히 필요한 것만 만들기</b>가 되는 이유다.</para>
    ///
    /// <para><b>에셋을 모른다.</b> 팔레트도 지역 정의도 여기 들어오지 않는다 — 가중치 배열과
    /// 인덱스 구간만 받아 <b>슬롯별 개수</b>를 돌려준다. 슬롯을 프리팹으로 바꾸는 일은
    /// <see cref="TerrainPreloadStep"/>이 한다. 그래야 EditMode가 씬도 에셋도 없이 전부 덮는다.</para>
    ///
    /// <para><b>합산한다.</b> 9개 인덱스가 10종 팔레트에서 뽑히므로 실제로는 5~8종에 몰린다 —
    /// 합산하지 않으면 같은 프리팹의 풀을 여러 번 만들며 낭비한다.</para>
    /// </summary>
    public static class GameplayPreloadPlan
    {
        /// <summary>
        /// 출발 시점에 스트리머가 유지하려는 타일 인덱스 구간 <c>[first, last]</c>.
        ///
        /// <para><b>출발 거리는 항상 0이다</b> — 그래서 미리 알 수 있다. 이 상수를 여기서
        /// 명시적으로 못 박아, 호출부가 "그때 거리가 얼마였더라"를 다시 묻지 않게 한다.</para>
        /// </summary>
        public static void StartRange(
            float tileLength, int tilesAhead, int tilesBehind, out int first, out int last)
        {
            TileStreamingLogic.GetVisibleRange(0f, tileLength, tilesAhead, tilesBehind, out first, out last);
        }

        /// <summary>
        /// 인덱스 구간이 팔레트의 각 슬롯을 몇 번 고르는지. 길이는 <paramref name="weights"/>와 같다.
        /// 가중치가 없으면(팔레트가 비었으면) 빈 배열 — 그때는 폴백 프리팹 한 장이 답이다.
        /// </summary>
        public static int[] SegmentCounts(
            int firstIndex, int lastIndex, float[] weights, bool[] noRepeatAdjacent)
        {
            if (weights == null || weights.Length == 0)
            {
                return System.Array.Empty<int>();
            }

            var counts = new int[weights.Length];
            for (int index = firstIndex; index <= lastIndex; index++)
            {
                int picked = SegmentPickLogic.PickForTile(index, weights, noRepeatAdjacent);
                if (picked >= 0 && picked < counts.Length)
                {
                    counts[picked]++;
                }
            }

            return counts;
        }

        /// <summary>계획 총량 — 만들어야 할 인스턴스 수. 진행률의 분모다.</summary>
        public static int Total(int[] counts)
        {
            if (counts == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < counts.Length; i++)
            {
                total += counts[i] < 0 ? 0 : counts[i];
            }

            return total;
        }
    }
}
