using Game.Core.Services;
using Game.Gameplay.Region;

namespace Game.Gameplay.World
{
    /// <summary>
    /// <b>발밑에 물이 있는가</b> — 물에 기대는 모든 판정의 단일 출처.
    ///
    /// <para><b>왜 "현재 지역"을 쓰면 안 되는가.</b> 지역 전환은 전방 <c>tilesAhead + 1</c>장
    /// <b>너머</b>에 경계를 찍는다(이미 깔린 타일을 바꾸지 않기 위해). 그래서 Day가 넘어간 순간
    /// 지역은 대초원인데 <b>발밑은 아직 바다 교량</b>이고, 그 상태가 현행 설정에서 6타일 ·
    /// 240 m · 전속 <b>40초</b>간 이어진다.</para>
    ///
    /// <para>그 사이 물을 "현재 지역"으로 끄면 <b>다리만 남고 물이 사라진다</b> — 바다에서 나갈 때
    /// 허공 위를 달리고, 들어올 때는 반대로 평지가 물에 잠긴다. 수면 렌더·수영 판정·몬스터
    /// 지지면이 각자 "현재 지역"을 보고 있어 <b>지형과 어긋나 있었다</b> (검증 A3).</para>
    ///
    /// <para>경계 기록이 없으면(세션 초반·단일 지역) 현재 지역으로 되돌아간다 — 종전 동작이다.</para>
    /// </summary>
    public static class WaterSurfaceQuery
    {
        /// <summary>발밑 지형의 지역 — 경계를 못 얻으면 현재 지역.</summary>
        public static RegionDefinition ResolveLocalRegion()
        {
            if (!ServiceLocator.TryGet(out IRegionService region))
            {
                return null;
            }

            if (ServiceLocator.TryGet(out ITerrainBoundaryService boundaries))
            {
                int index = boundaries.RegionIndexAtTrain;
                if (index >= 0)
                {
                    RegionDefinition local = region.GetRegion(index);
                    if (local != null)
                    {
                        return local;
                    }
                }
            }

            return region.CurrentRegion;
        }

        /// <summary>발밑에 물이 있으면 그 표면 높이. 물이 없는 지역이면 false.</summary>
        public static bool TryGetWaterSurfaceY(out float waterSurfaceY)
        {
            waterSurfaceY = 0f;

            RegionDefinition definition = ResolveLocalRegion();
            if (definition == null || !definition.HasWater)
            {
                return false;
            }

            waterSurfaceY = definition.WaterSurfaceY;
            return true;
        }

        /// <summary>
        /// 지상 개체가 서는 높이 — 물이면 물면, 아니면 지면(0).
        /// 물 없는 지역에서는 0이라 <b>동작이 종전과 같다.</b>
        /// </summary>
        public static float SurfaceY()
        {
            return TryGetWaterSurfaceY(out float y) ? y : 0f;
        }
    }
}
