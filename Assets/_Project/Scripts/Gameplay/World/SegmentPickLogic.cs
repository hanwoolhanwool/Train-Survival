namespace Game.Gameplay.World
{
    /// <summary>
    /// 지형 세그먼트 추첨 — 타일 인덱스 하나에서 결정론적으로 유도하는 순수 함수
    /// (레벨 디자인 가이드 §4.5·§4.6, 계획 미결 ① 확정: <b>인덱스 시드 · 전 피어 동일</b>).
    ///
    /// <para>각 피어가 로컬로 추첨하면 배경만 달라 보이는 것이 아니라 <b>콜라이더가 갈린다</b> —
    /// 클라이언트 화면에서 몬스터가 없는 벽을 도는 것처럼 보인다. 그래서 지역·낮밤과 같은 규약
    /// (복제 값 하나에서 순수 파생)을 여기서도 재사용해, 네트워크 상태를 새로 만들지 않고
    /// 타일 인덱스만으로 모든 피어가 같은 결과에 도달하게 한다.</para>
    /// </summary>
    public static class SegmentPickLogic
    {
        /// <summary>타일 인덱스 → 0~1 난수. 결정론적이며 인접 인덱스끼리 상관이 낮다.</summary>
        public static float Hash01(int index, int salt)
        {
            unchecked
            {
                uint x = (uint)(index * 73856093) ^ (uint)(salt * 19349663);
                x ^= x >> 16;
                x *= 2246822519u;
                x ^= x >> 13;
                x *= 3266489917u;
                x ^= x >> 16;
                return (x & 0xFFFFFF) / (float)0x1000000;
            }
        }

        /// <summary>
        /// 가중 추첨 — <paramref name="roll"/>은 0~1. 가중치 합이 0 이하면 -1(무효).
        /// <paramref name="excludeIndex"/>가 0 이상이면 그 후보를 빼고 뽑는다.
        /// </summary>
        public static int WeightedPick(float[] weights, float roll, int excludeIndex)
        {
            if (weights == null || weights.Length == 0)
            {
                return -1;
            }

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (i == excludeIndex || weights[i] <= 0f)
                {
                    continue;
                }

                total += weights[i];
            }

            if (total <= 0f)
            {
                // 제외 때문에 후보가 사라졌으면 제외를 무시한다 — 스폰이 멈추는 편이 더 나쁘다.
                return excludeIndex >= 0 ? WeightedPick(weights, roll, -1) : -1;
            }

            float cursor = roll * total;
            for (int i = 0; i < weights.Length; i++)
            {
                if (i == excludeIndex || weights[i] <= 0f)
                {
                    continue;
                }

                cursor -= weights[i];
                if (cursor <= 0f)
                {
                    return i;
                }
            }

            // 부동소수 오차로 끝까지 왔을 때의 폴백 — 마지막 유효 후보.
            for (int i = weights.Length - 1; i >= 0; i--)
            {
                if (i != excludeIndex && weights[i] > 0f)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// 타일 인덱스의 세그먼트를 고른다.
        /// <paramref name="previousPick"/>과 같고 그 후보가 <paramref name="noRepeatAdjacent"/>면
        /// 한 번 다시 뽑는다 — 교량·유적처럼 강한 세그먼트가 연달아 나오면 즉시 티가 나기 때문이다.
        /// </summary>
        public static int Pick(int tileIndex, float[] weights, int previousPick, bool[] noRepeatAdjacent)
        {
            int picked = WeightedPick(weights, Hash01(tileIndex, 1), -1);
            if (picked < 0 || picked != previousPick)
            {
                return picked;
            }

            bool blocked = noRepeatAdjacent != null
                && picked < noRepeatAdjacent.Length
                && noRepeatAdjacent[picked];

            return blocked ? WeightedPick(weights, Hash01(tileIndex, 2), picked) : picked;
        }

        /// <summary>
        /// 타일 인덱스 하나가 실제로 받게 될 세그먼트 — <b>직전 인덱스의 선택까지 포함한 전체 규칙</b>이다.
        ///
        /// <para><b>이 함수가 단일 출처여야 한다.</b> 런타임 스트리밍
        /// (<see cref="Game.Gameplay.World.TerrainTileStreamer"/>)과 로딩 프리웜 계획
        /// (<see cref="GameplayPreloadPlan"/>)이 이걸 함께 부른다 — 두 곳이 각자 계산하면
        /// <b>프리웜은 전부 헛일이 되고 아무도 눈치채지 못한다</b>
        /// ([인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §10).</para>
        ///
        /// <para>직전 선택을 <see cref="WeightedPick"/>로 다시 구하는 것은 <b>의도한 근사</b>다 —
        /// 인접 금지의 재추첨까지 거슬러 올라가지 않는다. 그래야 어느 인덱스든 <b>혼자서</b>
        /// 답이 나오고, 후발 접속자가 과거 구간을 그릴 때도 같은 결과에 도달한다.</para>
        /// </summary>
        public static int PickForTile(int tileIndex, float[] weights, bool[] noRepeatAdjacent)
        {
            int previous = WeightedPick(weights, Hash01(tileIndex - 1, 1), -1);
            return Pick(tileIndex, weights, previous, noRepeatAdjacent);
        }
    }
}
