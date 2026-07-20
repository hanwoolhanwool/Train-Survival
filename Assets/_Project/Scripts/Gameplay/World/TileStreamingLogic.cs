using System;

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
    }
}
