using System;
using System.Collections.Generic;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지형 타일 스트리밍 인덱스 계산 순수 로직.
    /// 타일 i의 월드 Z = i × 타일 길이 − 누적 주행 거리 (열차 원점 기준).
    /// </summary>
    public static class TileStreamingLogic
    {
        public static float GetTileZ(int tileIndex, float tileLength, float traveledDistance)
        {
            return tileIndex * tileLength - traveledDistance;
        }

        /// <summary>
        /// 열차가 <b>지금 지나고 있는</b> 타일의 인덱스 (z = 0 자리).
        ///
        /// <para>지역 전환은 전방 <c>tilesAhead + 1</c>장 <b>너머</b>에 경계를 찍으므로
        /// (<see cref="GetBoundaryTileIndex"/>), "지금 선포된 지역"과 "발밑 지형의 지역"은
        /// 전환 직후 한동안 다르다. 지형에 맞춰 켜고 꺼야 하는 것들은 이 인덱스로 판정한다.</para>
        /// </summary>
        public static int GetCenterTileIndex(float traveledDistance, float tileLength)
        {
            if (tileLength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tileLength));
            }

            return (int)Math.Floor(traveledDistance / tileLength);
        }

        /// <summary>현재 누적 거리에서 유지해야 할 타일 인덱스 구간 [first, last]를 계산한다.</summary>
        public static void GetVisibleRange(
            float traveledDistance, float tileLength, int tilesAhead, int tilesBehind,
            out int first, out int last)
        {
            if (tileLength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tileLength));
            }

            int center = (int)Math.Floor(traveledDistance / tileLength);
            first = center - tilesBehind;
            last = center + tilesAhead;
        }

        // ── 지역 전환 경계 (M6 1차 §2.4 — 후발 접속 지형 표시) ──────────────

        /// <summary>
        /// 지역 전환 순간, 새 지역 프리팹이 처음 적용되는 타일 인덱스.
        /// 전환 시점에 전방 tilesAhead장이 이미 구 프리팹으로 생성돼 있으므로, 전환 거리로
        /// 경계를 잡으면 실제 경계보다 (tilesAhead × 타일 길이)만큼 앞당겨진다 — 그 다음
        /// 인덱스(floor(D/L) + tilesAhead + 1)부터가 새 프리팹이다.
        /// </summary>
        public static int GetBoundaryTileIndex(float traveledDistance, float tileLength, int tilesAhead)
        {
            if (tileLength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tileLength));
            }

            return (int)Math.Floor(traveledDistance / tileLength) + tilesAhead + 1;
        }

        /// <summary>
        /// 타일 인덱스가 속하는 지역 인덱스 — TileIndex 오름차순 경계 목록에서
        /// "인덱스 이하 중 가장 뒤의 경계"가 결정한다. 해당 경계가 없으면 -1.
        /// </summary>
        public static int ResolveRegionIndex(int tileIndex, IReadOnlyList<TerrainRegionBoundary> boundaries)
        {
            int result = -1;
            for (int i = 0; i < boundaries.Count; i++)
            {
                if (boundaries[i].TileIndex > tileIndex)
                {
                    break;
                }

                result = boundaries[i].RegionIndex;
            }

            return result;
        }

        /// <summary>
        /// 후방 가시 구간 밖으로 지나간 경계 수 = 목록 앞에서 잘라낼 개수 (§2.4 트림 —
        /// 순환 지형이라 경계가 세션 시간에 비례해 누적된다). 경계 b는 "다음 경계의 TileIndex가
        /// 첫 가시 인덱스 이하"가 되는 순간 어떤 가시 타일의 결정에도 참여하지 않는다.
        /// </summary>
        public static int CountTrimmableBoundaries(
            IReadOnlyList<TerrainRegionBoundary> boundaries, int firstVisibleIndex)
        {
            int trimmable = 0;
            while (trimmable + 1 < boundaries.Count
                && boundaries[trimmable + 1].TileIndex <= firstVisibleIndex)
            {
                trimmable++;
            }

            return trimmable;
        }
    }
}
