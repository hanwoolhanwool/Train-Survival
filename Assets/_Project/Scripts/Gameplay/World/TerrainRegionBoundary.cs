using System;
using Unity.Netcode;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지역 전환 경계 기록 — "타일 인덱스 <see cref="TileIndex"/>부터 지역 <see cref="RegionIndex"/>의
    /// 지형 프리팹을 쓴다" (M6 1차 §2.4). 호스트가 <see cref="WorldScrollController"/>의 NetworkList로
    /// 복제해, 후발 피어도 과거 구간 타일을 당시 지역 프리팹으로 생성할 수 있게 한다.
    /// 타일 자체는 계속 로컬 구동 — 복제되는 것은 이 경계 데이터뿐이다.
    /// </summary>
    public struct TerrainRegionBoundary : INetworkSerializable, IEquatable<TerrainRegionBoundary>
    {
        /// <summary>이 경계의 지역이 처음 적용되는 타일 인덱스. 세션 최초 기록은 int.MinValue(전 구간).</summary>
        public int TileIndex;

        /// <summary>지역 배열 인덱스 (RegionTimelineSettings 기준 — 전 피어 동일 에셋).</summary>
        public int RegionIndex;

        public TerrainRegionBoundary(int tileIndex, int regionIndex)
        {
            TileIndex = tileIndex;
            RegionIndex = regionIndex;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref TileIndex);
            serializer.SerializeValue(ref RegionIndex);
        }

        public bool Equals(TerrainRegionBoundary other)
        {
            return TileIndex == other.TileIndex && RegionIndex == other.RegionIndex;
        }
    }

    /// <summary>
    /// 지역 경계 목록 변경 권위 이벤트 — 호스트 기록이 복제로 도착했을 때 각 피어에서 발행된다.
    /// 구독자(TerrainTileStreamer)는 이미 깔린 타일을 재판정해 어긋난 것을 교체한다.
    /// </summary>
    public readonly struct TerrainRegionBoundariesChangedEvent
    {
    }

    /// <summary>타일 인덱스 → 지역 결정 조회 계약 — <see cref="WorldScrollController"/>가 노출한다.</summary>
    public interface ITerrainBoundaryService
    {
        /// <summary>타일 인덱스가 속하는 지역 인덱스. 경계 기록이 없거나 기록 이전 구간이면 -1
        /// (호출자는 현행 "현재 지역 프리팹" 동작을 유지한다).</summary>
        int ResolveRegionIndex(int tileIndex);
    }
}
