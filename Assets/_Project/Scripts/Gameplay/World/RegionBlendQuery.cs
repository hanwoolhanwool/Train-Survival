using Game.Core.Services;
using Game.Gameplay.Region;

namespace Game.Gameplay.World
{
    /// <summary>
    /// <b>지역 전환을 구간으로 건너는</b> 조회의 단일 출처 — 하늘·안개처럼 한 벌뿐이라
    /// 타일 단위로 교체할 수 없는 것들이 쓴다.
    ///
    /// <para><b>왜 필요한가.</b> 지역 전환에는 세 가지 시각이 있고, 셋을 섞어 쓰면 화면이 어긋난다.
    /// ① <c>RegionChangedEvent</c> 선포 시점 — 지형보다 <c>tilesAhead + 1</c>장(240 m · 40초)
    /// 이르다. ② <see cref="ITerrainBoundaryService.RegionIndexAtTrain"/> 발밑 — 지형과 맞지만
    /// <b>계단</b>이라 경계를 밟는 한 프레임에 통째로 튄다. ③ 이 클래스의 <b>구간 블렌드</b> —
    /// 경계 앞뒤에 걸쳐 섞으므로 어느 쪽에서도 튀지 않는다.</para>
    ///
    /// <para>하늘이 ①을 쓰고 있어서 <b>지나온 숲 위에 사막 하늘</b>이 섰다. 물·원경은 ②라
    /// 지형과는 맞았지만 경계에서 한 프레임에 사라졌다. 둘 다 여기로 모은다.</para>
    ///
    /// <para>경계 기록이 없으면(세션 초반·단일 지역) <b>현재 지역 한 장</b>으로 되돌아간다 —
    /// 종전 동작이다 (<see cref="WaterSurfaceQuery"/>와 같은 회귀 방어선).</para>
    /// </summary>
    public static class RegionBlendQuery
    {
        /// <summary>
        /// 전환 진행도로 지역 두 벌을 낸다. <paramref name="blend"/> 0 = <paramref name="from"/>,
        /// 1 = <paramref name="to"/>. 전환 구간 밖에서는 둘이 같고 진행도는 0이다.
        /// </summary>
        /// <returns>지역을 하나라도 얻었으면 true.</returns>
        public static bool TryResolve(
            float blendRange, out RegionDefinition from, out RegionDefinition to, out float blend)
        {
            from = null;
            to = null;
            blend = 0f;

            if (!ServiceLocator.TryGet(out IRegionService region))
            {
                return false;
            }

            if (ServiceLocator.TryGet(out ITerrainBoundaryService boundaries))
            {
                boundaries.ResolveRegionBlend(blendRange, out int fromIndex, out int toIndex, out float t);
                if (fromIndex >= 0)
                {
                    from = region.GetRegion(fromIndex);
                    to = toIndex >= 0 ? region.GetRegion(toIndex) : from;
                    blend = ReferenceEquals(from, to) ? 0f : t;

                    if (from != null)
                    {
                        return true;
                    }
                }
            }

            from = region.CurrentRegion;
            to = from;
            blend = 0f;
            return from != null;
        }

        /// <summary>
        /// <paramref name="predicate"/>를 만족하는 지역이 <b>보이는 범위에 있으면</b> 그 지역과
        /// 그 지역이 차지하는 z 구간을 낸다.
        ///
        /// <para>수면·지평선판처럼 <b>지역 전체를 한 장으로 덮는 표현</b>의 공통 골격이다.
        /// 그런 한 장은 반경이 수백 m라 발밑 한 점보다 훨씬 넓다 — 발밑만 보고 켜고 끄면
        /// <b>구간을 벗어난 쪽이 한 프레임에 통째로 나타나거나 사라진다.</b> 대신 구간으로
        /// 자르면 잘린 끝이 경계(해안선·지평선)로 지나간다.</para>
        ///
        /// <para>발밑 → 뒤 → 앞 순으로 찾는다. 지역 전환은 지역당 1회뿐이라 보이는 범위 안에
        /// 후보가 둘 이상 되지 않는다.</para>
        /// </summary>
        public static bool TryResolveSpanFor(
            System.Func<RegionDefinition, bool> predicate,
            out RegionDefinition region, out float backZ, out float frontZ)
        {
            ResolveLocalSpan(out RegionDefinition back, out RegionDefinition front, out backZ, out frontZ);

            RegionDefinition local = WaterSurfaceQuery.ResolveLocalRegion();
            if (local != null && predicate(local))
            {
                region = local;
                return true;
            }

            // 발밑은 아니다 — 뒤에 남아 있는 구간이 그것일 수 있다(바다를 막 벗어난 자리).
            if (back != null && predicate(back))
            {
                region = back;
                frontZ = backZ;
                backZ = float.NegativeInfinity;
                return true;
            }

            // 앞에서 다가오는 구간일 수도 있다(바다에 들어가기 전).
            if (front != null && predicate(front))
            {
                region = front;
                backZ = frontZ;
                frontZ = float.PositiveInfinity;
                return true;
            }

            region = null;
            return false;
        }

        /// <summary>
        /// 발밑 지역이 이어지는 z 구간 [back, front] (열차 원점 기준, m)과 그 양쪽 이웃 지역.
        /// 경계를 못 얻으면 양쪽 무한대에 이웃은 null — 호출자는 자기 크기를 그대로 쓴다(종전 동작).
        /// </summary>
        public static void ResolveLocalSpan(
            out RegionDefinition backRegion, out RegionDefinition frontRegion,
            out float backZ, out float frontZ)
        {
            backRegion = null;
            frontRegion = null;
            backZ = float.NegativeInfinity;
            frontZ = float.PositiveInfinity;

            if (!ServiceLocator.TryGet(out ITerrainBoundaryService boundaries)
                || !ServiceLocator.TryGet(out IRegionService region))
            {
                return;
            }

            boundaries.ResolveRegionSpanZ(out backZ, out frontZ, out int backIndex, out int frontIndex);

            if (backIndex >= 0)
            {
                backRegion = region.GetRegion(backIndex);
            }

            if (frontIndex >= 0)
            {
                frontRegion = region.GetRegion(frontIndex);
            }
        }
    }
}
