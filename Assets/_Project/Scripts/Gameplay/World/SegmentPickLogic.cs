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

        // ── 2단 추첨: 구간 군 → 세그먼트 (북극 계획 §5.3) ─────────────────────────

        /// <summary>
        /// 타일 인덱스가 속한 <b>구간 군</b>. <paramref name="groupSchedule"/>은 한 바퀴의 군 번호를
        /// 타일 한 장에 하나씩 늘어놓은 배열이다 — 북극은
        /// <c>[얼음×6, 전이, 바다×5, 전이]</c> 13장이 한 바퀴(520 m · 87초)다.
        ///
        /// <para>비어 있으면 <b>−1</b>을 돌려준다. 그 값이 곧 "구간 편성 없음"이고,
        /// 다른 네 지역은 이 배열을 비워 둔 채 현행 독립 추첨 그대로 돈다(팔레트 폴백과 같은 규약).</para>
        /// </summary>
        public static int GroupAtTile(int tileIndex, int[] groupSchedule)
        {
            if (groupSchedule == null || groupSchedule.Length == 0)
            {
                return -1;
            }

            // 음수 인덱스도 같은 바퀴 위에 놓는다 — 프리웜이 0 앞을 들여다볼 수 있다.
            int cycle = groupSchedule.Length;
            int position = tileIndex % cycle;
            if (position < 0)
            {
                position += cycle;
            }

            return groupSchedule[position];
        }

        /// <summary>
        /// 군에 속하지 않는 후보의 가중치를 0으로 지운 배열을 <paramref name="destination"/>에 쓴다.
        ///
        /// <para><b>군이 비면 원래 가중치를 그대로 남긴다.</b> 편성에 적힌 군에 세그먼트가 하나도
        /// 없으면 그 타일이 통째로 비는데, 그것은 <b>오류 로그 한 줄 없이 지형이 사라지는</b>
        /// 실패 방식이다(사막 §6.8이 계기를 붙인 바로 그 종류). 어울리지 않는 세그먼트가 한 장
        /// 끼는 편이 낫다.</para>
        /// </summary>
        public static void ApplyGroupMask(float[] weights, int[] entryGroups, int group, float[] destination)
        {
            if (weights == null || destination == null || destination.Length != weights.Length)
            {
                return;
            }

            bool any = false;
            for (int i = 0; i < weights.Length; i++)
            {
                bool inGroup = entryGroups != null && i < entryGroups.Length && entryGroups[i] == group;
                destination[i] = inGroup ? weights[i] : 0f;
                any |= inGroup && weights[i] > 0f;
            }

            if (any)
            {
                return;
            }

            for (int i = 0; i < weights.Length; i++)
            {
                destination[i] = weights[i];
            }
        }

        /// <summary>
        /// 구간 편성이 있는 팔레트의 타일 추첨 — <b>군을 먼저 정하고 군 안에서 뽑는다</b>.
        ///
        /// <para><b>왜 2단인가.</b> 타일 한 장마다 독립 추첨하면 얼음 우세와 바다 우세가
        /// <b>6.67초마다 뒤바뀐다</b> — 교차가 아니라 뒤죽박죽이다. 군을 바퀴로 묶으면
        /// "얼음 → 전이 → 바다 → 전이"의 리듬이 읽힌다(북극 계획 §5.3).</para>
        ///
        /// <para><b>결정론은 그대로다.</b> 군도 시드도 전부 타일 인덱스의 함수라 전 피어가 같은
        /// 답에 도달하고, 후발 접속자가 과거 구간을 그릴 때도 같다.</para>
        ///
        /// <para><paramref name="groupSchedule"/>이 비었거나 <paramref name="scratch"/>가 맞지 않으면
        /// <see cref="PickForTile(int, float[], bool[])"/>과 <b>완전히 같은 답</b>을 낸다 —
        /// 다른 네 지역이 이 경로를 그대로 지나가게 하는 폴백이다.</para>
        /// </summary>
        /// <param name="scratch">군 마스크를 쓸 버퍼. 길이가 <paramref name="weights"/>와 같아야 한다.</param>
        public static int PickForTile(
            int tileIndex, float[] weights, bool[] noRepeatAdjacent,
            int[] entryGroups, int[] groupSchedule, float[] scratch)
        {
            if (weights == null || weights.Length == 0
                || groupSchedule == null || groupSchedule.Length == 0
                || entryGroups == null
                || scratch == null || scratch.Length != weights.Length)
            {
                return PickForTile(tileIndex, weights, noRepeatAdjacent);
            }

            // 직전 타일은 그 타일의 군으로 다시 뽑는다 — 군이 다르면 인접 반복 자체가 성립하지 않는다.
            ApplyGroupMask(weights, entryGroups, GroupAtTile(tileIndex - 1, groupSchedule), scratch);
            int previous = WeightedPick(scratch, Hash01(tileIndex - 1, 1), -1);

            ApplyGroupMask(weights, entryGroups, GroupAtTile(tileIndex, groupSchedule), scratch);
            return Pick(tileIndex, scratch, previous, noRepeatAdjacent);
        }
    }
}
