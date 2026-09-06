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
        /// 열차에 <b>가장 가까운 실제 전환 경계</b>의 목록 인덱스. 없으면 -1.
        ///
        /// <para>목록 첫 항목의 <c>int.MinValue</c>(세션 최초 지역 = 전 구간)는 전환이 아니므로
        /// 후보에서 뺀다 — 곱셈에서 오버플로가 나기도 한다.</para>
        /// </summary>
        private static int FindNearestBoundary(
            float traveledDistance, float tileLength, IReadOnlyList<TerrainRegionBoundary> boundaries)
        {
            int nearest = -1;
            float nearestDistance = float.MaxValue;

            for (int i = 0; i < boundaries.Count; i++)
            {
                if (boundaries[i].TileIndex == int.MinValue)
                {
                    continue;
                }

                float distance = Math.Abs(GetTileZ(boundaries[i].TileIndex, tileLength, traveledDistance));
                if (distance >= nearestDistance)
                {
                    // 오름차순이라 한 번 멀어지면 계속 멀어진다.
                    break;
                }

                nearest = i;
                nearestDistance = distance;
            }

            return nearest;
        }

        /// <summary>
        /// 지역 전환 <b>진행도</b> — 경계를 <c>±blendRange</c> 구간에 걸쳐 건너는 0→1 값.
        ///
        /// <para><b>왜 계단이 아니라 진행도인가.</b> 하늘·안개는 지형처럼 "타일 단위로 교체"할 수
        /// 없어서 어느 한 프레임에 통째로 바뀐다. 선포 시점에 바꾸면 지형보다
        /// <c>tilesAhead + 1</c>장(240 m · 40초) 먼저 바뀌어 <b>지나온 숲 위에 사막 하늘</b>이
        /// 서고, 발밑 기준으로 바꾸면 그 튐이 경계 순간으로 옮겨질 뿐이다. 경계를 중심으로
        /// 앞뒤 <paramref name="blendRange"/>에 걸쳐 섞어야 어느 쪽에서도 튀지 않는다.</para>
        ///
        /// <para>경계 z가 <c>+blendRange</c>면 0(= 아직 이전 지역), 0이면 0.5(= 경계를 밟는 순간),
        /// <c>−blendRange</c>면 1(= 완전히 새 지역)이다. 그래서 구간 밖에서는 자동으로
        /// 발밑 지역과 같은 답이 나온다 — 판정을 두 벌로 나눌 필요가 없다.</para>
        /// </summary>
        /// <param name="blendRange">경계 앞뒤로 섞는 거리(m). 0 이하면 계단(발밑 기준)이 된다.</param>
        /// <param name="fromRegionIndex">섞기 시작하는 지역. 경계 기록이 없으면 -1.</param>
        /// <param name="toRegionIndex">섞여 들어오는 지역. 전환이 없으면 <paramref name="fromRegionIndex"/>와 같다.</param>
        /// <param name="blend">0 = from, 1 = to.</param>
        public static void ResolveRegionBlend(
            float traveledDistance, float tileLength, float blendRange,
            IReadOnlyList<TerrainRegionBoundary> boundaries,
            out int fromRegionIndex, out int toRegionIndex, out float blend)
        {
            if (tileLength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tileLength));
            }

            int center = GetCenterTileIndex(traveledDistance, tileLength);
            fromRegionIndex = ResolveRegionIndex(center, boundaries);
            toRegionIndex = fromRegionIndex;
            blend = 0f;

            if (blendRange <= 0f)
            {
                return;
            }

            int nearest = FindNearestBoundary(traveledDistance, tileLength, boundaries);
            if (nearest <= 0)
            {
                // 경계가 없거나, 있어도 그 앞 지역을 모른다(목록 첫 항목) — 섞을 상대가 없다.
                return;
            }

            float boundaryZ = GetTileZ(boundaries[nearest].TileIndex, tileLength, traveledDistance);
            if (boundaryZ > blendRange || boundaryZ < -blendRange)
            {
                return;
            }

            fromRegionIndex = boundaries[nearest - 1].RegionIndex;
            toRegionIndex = boundaries[nearest].RegionIndex;
            blend = (blendRange - boundaryZ) / (2f * blendRange);
        }

        /// <summary>
        /// 발밑 지역이 <b>이어지는 z 구간</b> [back, front] (열차 원점 기준, m)과 그 양쪽 이웃 지역.
        /// 경계가 없는 쪽은 무한대이고 이웃 지역은 -1이다 — 호출자가 자기 크기로 자른다.
        ///
        /// <para>지역 전체를 덮는 한 장짜리 표현(바다 수면·지평선판)이 이 구간을 벗어나면
        /// <b>다음 지역 지형 위에 얹힌다.</b> 물 600 m 한 장이 지형 앞끝 240 m를 넘어
        /// 대초원 타일을 60~100 m 잠근 채 다가오다가, 열차가 경계를 밟는 순간 통째로 사라졌던
        /// 것이 그 증상이다.</para>
        ///
        /// <para><b>이웃 지역까지 내는 이유</b>는 그 한 장이 발밑 구간에만 속하지 않기 때문이다.
        /// 열차가 바다를 막 벗어난 순간, 발밑은 대초원이지만 <b>뒤로 300 m는 아직 바다</b>다 —
        /// 발밑만 보고 끄면 그 바다가 통째로 마른다.</para>
        /// </summary>
        public static void ResolveRegionSpanZ(
            float traveledDistance, float tileLength,
            IReadOnlyList<TerrainRegionBoundary> boundaries,
            out float backZ, out float frontZ, out int backRegionIndex, out int frontRegionIndex)
        {
            if (tileLength <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(tileLength));
            }

            backZ = float.NegativeInfinity;
            frontZ = float.PositiveInfinity;
            backRegionIndex = -1;
            frontRegionIndex = -1;

            int center = GetCenterTileIndex(traveledDistance, tileLength);
            int spanStart = -1;

            for (int i = 0; i < boundaries.Count; i++)
            {
                // 세션 최초 기록(전 구간)은 경계가 아니다 — z 를 계산하면 곱셈이 넘친다.
                if (boundaries[i].TileIndex == int.MinValue)
                {
                    continue;
                }

                if (boundaries[i].TileIndex > center)
                {
                    frontZ = GetTileZ(boundaries[i].TileIndex, tileLength, traveledDistance);
                    frontRegionIndex = boundaries[i].RegionIndex;
                    break;
                }

                backZ = GetTileZ(boundaries[i].TileIndex, tileLength, traveledDistance);
                spanStart = i;
            }

            if (spanStart > 0)
            {
                backRegionIndex = boundaries[spanStart - 1].RegionIndex;
            }
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
