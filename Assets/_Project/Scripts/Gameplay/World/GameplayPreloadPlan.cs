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
            return SegmentCounts(firstIndex, lastIndex, weights, noRepeatAdjacent, 0, 0);
        }

        /// <summary>
        /// 구간 편성이 있는 팔레트(북극)의 판 — 군까지 함께 넘겨 <b>스트리머와 같은 답</b>을 낸다.
        ///
        /// <para><b>여기를 빠뜨리면 프리웜이 통째로 헛일이 된다</b>: 2단 추첨이 고르는 세그먼트와
        /// 독립 추첨이 고르는 세그먼트가 달라 첫 프레임에 다른 타일을 새로 인스턴스화하게 되고,
        /// <b>아무 오류도 나지 않는다.</b> <see cref="SegmentPickLogic.PickForTile(int, float[], bool[], int[], int[], float[])"/>이
        /// 단일 출처인 이유다.</para>
        /// </summary>
        public static int[] SegmentCounts(
            int firstIndex, int lastIndex, float[] weights, bool[] noRepeatAdjacent,
            int stationBlockSize, int stationStageCount, int[] entryGroups, int[] groupSchedule)
        {
            if (weights == null || weights.Length == 0)
            {
                return System.Array.Empty<int>();
            }

            // 로딩 1회 경로라 버퍼를 여기서 만든다 — 프레임마다 도는 스트리머와 다르다.
            var scratch = new float[weights.Length];
            var counts = new int[weights.Length];
            for (int index = firstIndex; index <= lastIndex; index++)
            {
                if (StationSequenceLogic.IsStationTile(index, stationBlockSize, stationStageCount))
                {
                    continue;
                }

                int picked = SegmentPickLogic.PickForTile(
                    index, weights, noRepeatAdjacent, entryGroups, groupSchedule, scratch);
                if (picked >= 0 && picked < counts.Length)
                {
                    counts[picked]++;
                }
            }

            return counts;
        }

        /// <summary>
        /// 기차역이 깔리는 인덱스를 빼고 세는 판 — 역 타일 자리에는 팔레트 세그먼트가 오지 않는다.
        /// <paramref name="stationBlockSize"/>·<paramref name="stationStageCount"/>가 성립하지 않으면
        /// (0이거나 너무 좁으면) 아무것도 빠지지 않아 <b>위 오버로드와 같은 답</b>이 된다.
        /// </summary>
        public static int[] SegmentCounts(
            int firstIndex, int lastIndex, float[] weights, bool[] noRepeatAdjacent,
            int stationBlockSize, int stationStageCount)
        {
            if (weights == null || weights.Length == 0)
            {
                return System.Array.Empty<int>();
            }

            var counts = new int[weights.Length];
            for (int index = firstIndex; index <= lastIndex; index++)
            {
                if (StationSequenceLogic.IsStationTile(index, stationBlockSize, stationStageCount))
                {
                    continue;
                }

                int picked = SegmentPickLogic.PickForTile(index, weights, noRepeatAdjacent);
                if (picked >= 0 && picked < counts.Length)
                {
                    counts[picked]++;
                }
            }

            return counts;
        }

        /// <summary>
        /// 인덱스 구간이 역의 각 단계를 몇 번 쓰는지 (길이 = <paramref name="stageCount"/>).
        /// 설정이 성립하지 않으면 빈 배열이다.
        ///
        /// <para>출발 구간에 역이 걸리는 일은 드물지만(블록 260장에 역은 5장), 걸렸을 때
        /// 프리웜에서 빠지면 <b>첫 프레임에 역 타일을 새로 인스턴스화</b>하게 된다 —
        /// 이 계획이 없애려던 바로 그 스파이크다.</para>
        /// </summary>
        public static int[] StationStageCounts(
            int firstIndex, int lastIndex, int blockSize, int stageCount)
        {
            if (!StationSequenceLogic.IsValidConfig(blockSize, stageCount))
            {
                return System.Array.Empty<int>();
            }

            var counts = new int[stageCount];
            for (int index = firstIndex; index <= lastIndex; index++)
            {
                int stage = StationSequenceLogic.StageOf(index, blockSize, stageCount);
                if (stage != StationSequenceLogic.NoStage)
                {
                    counts[stage]++;
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
