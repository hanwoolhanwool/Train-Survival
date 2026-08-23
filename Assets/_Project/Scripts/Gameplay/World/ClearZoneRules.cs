using System;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 세그먼트 하나가 어긴 클리어 존 규격 (레벨 디자인 가이드 §4.2·§4.7).
    /// 한 콜라이더가 여러 개를 동시에 어길 수 있어 플래그로 둔다.
    /// </summary>
    [Flags]
    public enum ClearZoneIssue
    {
        None = 0,

        /// <summary>궤도 통로(|x| ≤ 3.3)에 솟은 것 — 열차가 지형에 파묻힌다.</summary>
        TrackCorridor = 1 << 0,

        /// <summary>하차·낙하 대역(3.3 &lt; |x| ≤ 4)에 솟은 것 — 승차 램프·추락 복귀 동선을 막는다.</summary>
        DropZone = 1 << 1,

        /// <summary>몬스터 주행 대역의 8 m 초과 연속 장벽 — 회피 레이캐스트가 3 m뿐이라 갇힌다.</summary>
        LongWall = 1 << 2,

        /// <summary>타일은 6.67초마다 켜지고 꺼진다 — 매번 굽는 비용을 낼 수 없다.</summary>
        MeshColliderUsed = 1 << 3,

        /// <summary>밟을 수 있는데 <see cref="WorldFrameSurface"/>가 없다 — 밟아도 땅이 안 흐른다.</summary>
        MissingWorldFrameSurface = 1 << 4,

        /// <summary>타일 규격(120 × 40 m)을 벗어난다 — 이음매가 벌어지거나 옆 타일과 겹친다.</summary>
        OutsideTileFootprint = 1 << 5,

        /// <summary>
        /// 스캐터 슬롯 아래의 콜라이더 — 변주는 피어마다 다르므로 <b>없는 벽을 도는 몬스터</b>가 생긴다.
        /// </summary>
        ColliderUnderScatterSlot = 1 << 6,
    }

    /// <summary>자원 앵커 하나가 어긴 배치 규격 (가이드 §4.2 자원 대역).</summary>
    [Flags]
    public enum AnchorIssue
    {
        None = 0,

        /// <summary>4 m 미만 — 열차·하차 동선 위라 자원을 심을 수 없다.</summary>
        TooCloseToTrack = 1 << 0,

        /// <summary>16 m 초과 — 1단계 집게(사거리 20 m)로 영원히 닿지 않는다.</summary>
        BeyondGrabberReach = 1 << 1,

        /// <summary>타일 길이 밖 — 옆 타일 구간에 심겨 이음매에서 두 번 겹친다.</summary>
        OutsideTileFootprint = 1 << 2,
    }

    /// <summary>
    /// 콜라이더 하나를 판정에 필요한 값만으로 요약한 것. 에디터 검사기가 이 구조체를 채워 넣으므로
    /// 판정 자체는 <see cref="UnityEditor"/> 없이 테스트된다.
    /// </summary>
    public readonly struct ColliderProbe
    {
        /// <summary>타일 루트 로컬 공간의 AABB. 루트가 곧 배치점이라 이 좌표가 대역과 직접 비교된다.</summary>
        public readonly Bounds Bounds;

        public readonly bool IsTrigger;
        public readonly bool IsMesh;
        public readonly bool HasSurfaceMarker;

        /// <summary>
        /// 궤도 구조물(<see cref="ClearZoneRules.IsTrackStructureName"/>)인지. 궤도는 통로를 지나야 하고
        /// 타일보다 1.12 m 길다(as-built) — 대역·풋프린트 검사의 <b>유일한</b> 예외다.
        /// </summary>
        public readonly bool IsTrackStructure;

        /// <summary>
        /// <see cref="ScatterSlot"/> 아래에 있는지. 스캐터 변주는 각 피어 로컬이라,
        /// 그 아래 콜라이더는 피어마다 있고 없고가 갈린다 (가이드 §4.5 결정론 주의).
        /// </summary>
        public readonly bool IsUnderScatterSlot;

        public ColliderProbe(
            Bounds bounds, bool isTrigger, bool isMesh, bool hasSurfaceMarker, bool isTrackStructure,
            bool isUnderScatterSlot = false)
        {
            Bounds = bounds;
            IsTrigger = isTrigger;
            IsMesh = isMesh;
            HasSurfaceMarker = hasSurfaceMarker;
            IsTrackStructure = isTrackStructure;
            IsUnderScatterSlot = isUnderScatterSlot;
        }
    }

    /// <summary>
    /// 클리어 존 규격의 단일 진실 원천 (레벨 디자인 가이드 §4.1~§4.2) — 전부 순수 판정.
    /// 세그먼트가 40장으로 늘어나기 전에 자동 판정을 세워, 사람 눈으로 검수하지 않는다(계획 리스크 6·7).
    /// </summary>
    public static class ClearZoneRules
    {
        /// <summary>궤도 통로 반폭 — 열차 편성 폭 4.6·기관차 오버행 5.37에서 나온 값.</summary>
        public const float TrackCorridorHalfWidth = 3.3f;

        /// <summary>하차·낙하 대역 바깥 경계.</summary>
        public const float DropZoneOuterX = 4f;

        /// <summary>자원 앵커 대역 안쪽 경계 — <c>ResourceSpawnSettings.MinLateralOffset</c>과 같은 값.</summary>
        public const float ResourceBandMinX = 4f;

        /// <summary>자원 앵커 대역 바깥 경계 — 집게 1단계 사거리 20 m가 정한 상한. 협상 대상이 아니다.</summary>
        public const float ResourceBandMaxX = 16f;

        /// <summary>몬스터 주행 대역 — 스폰 14~24 m에서 열차로 접근하는 길.</summary>
        public const float MonsterBandMinX = 4f;
        public const float MonsterBandMaxX = 24f;

        /// <summary>연속 장벽 상한. 회피 레이캐스트가 3 m뿐이라 이보다 길면 몬스터가 갇힌다.</summary>
        public const float MaxWallLengthZ = 8f;

        /// <summary>벽으로 읽히는 최소 높이. 이보다 낮으면 넘어 다닐 수 있어 장벽이 아니다.</summary>
        public const float WallMinHeightY = 0.5f;

        /// <summary>
        /// 타일 규격 — 길이 40 m(±20) · 폭 120 m(±60).
        ///
        /// <para><b>폭이 60에서 120으로 넓어진 이유.</b> 판 끝이 ±30이면 열차 위 눈높이에서
        /// 바깥 나무 사이로 판 너머 <b>스카이박스 하반구</b>가 그대로 보였다. 판을 두 배로 넓혀
        /// 나무 틈으로 보이는 것이 "하늘"이 아니라 "더 먼 지면"이 되게 한 것이 근본 처방이고,
        /// 바깥 대역의 트리라인·절벽 벽은 그 위에 얹힌 차폐다.</para>
        /// </summary>
        public const float TileHalfLengthZ = 20f;
        public const float TileHalfWidthX = 60f;

        /// <summary>지면 상면 y = 0의 허용 오차. 이 안이면 "솟은 것"이 아니라 지면이다.</summary>
        public const float GroundHeightTolerance = 0.05f;

        /// <summary>밟을 수 있는 표면으로 보는 최소 상면 크기 — 사람이 올라설 수 있는 넓이.</summary>
        public const float WalkableMinFootprint = 2f;

        /// <summary>밟을 수 있는 상면 높이 범위. 아래는 묻힌 둔덕, 위는 절벽 상판이라 밟을 일이 없다.</summary>
        public const float WalkableMinTopY = -0.5f;
        public const float WalkableMaxTopY = 3f;

        /// <summary>타일당 자원 앵커 개수 기준 (가이드 §4.4).</summary>
        public const int MinAnchorsPerTile = 5;
        public const int MaxAnchorsPerTile = 7;

        /// <summary>타일당 랜드마크 슬롯 상한 (가이드 §4.4) — 0~1개. 없는 타일이 있어도 좋다.</summary>
        public const int MaxLandmarkSlotsPerTile = 1;

        /// <summary>타일당 스캐터 슬롯 기준 (가이드 §4.4) — 변주가 반복 인지를 줄이는 주 장치다.</summary>
        public const int MinScatterSlotsPerTile = 4;
        public const int MaxScatterSlotsPerTile = 10;

        /// <summary>궤도 본체 — 자식 이름이 규격으로 고정돼 있다 (가이드 §4.1 · train-art-layout §7.1).</summary>
        public const string RailTrackName = "RailTrack";

        /// <summary>궤도 도상(자갈층) — 상면 0.06 m 판때기라 통로를 지나도 열차를 파묻지 않는다.</summary>
        public const string TrackBedName = "TrackBed";

        /// <summary>
        /// 궤도 구조물의 이름인가. 통로를 지나야 하는 것은 규격이 이름으로 고정한 이 둘뿐이고,
        /// 그 밖에 통로에 솟은 것은 전부 침범이다 — 두께로 봐주면 "5 cm 바위는 통과"가 되어 규칙이 새어나간다.
        /// </summary>
        public static bool IsTrackStructureName(string name)
        {
            return name == RailTrackName || name == TrackBedName;
        }

        /// <summary>
        /// AABB가 좌우 대칭 대역 [<paramref name="minAbsX"/>, <paramref name="maxAbsX"/>]와 겹치는가.
        /// 대역은 ±양쪽 모두이므로 어느 한쪽만 걸쳐도 겹침이다.
        /// </summary>
        public static bool OverlapsBandX(Bounds bounds, float minAbsX, float maxAbsX)
        {
            // 양(+) 쪽 대역: [minAbsX, maxAbsX]
            if (bounds.max.x >= minAbsX && bounds.min.x <= maxAbsX)
            {
                return true;
            }

            // 음(−) 쪽 대역: [−maxAbsX, −minAbsX]
            return bounds.max.x >= -maxAbsX && bounds.min.x <= -minAbsX;
        }

        /// <summary>지면(y ≈ 0)이 아니라 위로 솟아 있는가 — 대역 침범은 솟은 것에만 성립한다.</summary>
        public static bool RisesAboveGround(Bounds bounds)
        {
            return bounds.max.y > GroundHeightTolerance;
        }

        /// <summary>
        /// 몬스터 주행 대역을 막는 연속 장벽인가. 길이(Z)와 높이를 함께 본다 —
        /// 낮은 지면 판때기는 40 m여도 장벽이 아니고, 짧은 바위는 높아도 돌아갈 수 있다.
        /// </summary>
        public static bool IsLongWall(Bounds bounds)
        {
            if (!OverlapsBandX(bounds, MonsterBandMinX, MonsterBandMaxX))
            {
                return false;
            }

            if (bounds.size.z <= MaxWallLengthZ)
            {
                return false;
            }

            return bounds.size.y >= WallMinHeightY && RisesAboveGround(bounds);
        }

        /// <summary>타일 규격(120 × 40 m)을 벗어나는가 — 이음매 규칙 §4.3.</summary>
        public static bool ExceedsTileFootprint(Bounds bounds)
        {
            return bounds.max.x > TileHalfWidthX || bounds.min.x < -TileHalfWidthX
                || bounds.max.z > TileHalfLengthZ || bounds.min.z < -TileHalfLengthZ;
        }

        /// <summary>
        /// 플레이어가 올라설 수 있는 표면인가 — <see cref="WorldFrameSurface"/>가 필요한 대상을 고른다.
        /// 트리거는 밟히지 않고, 좁은 것(나무 밑동 등)은 올라설 자리가 아니다.
        /// </summary>
        public static bool IsWalkableSurface(Bounds bounds, bool isTrigger)
        {
            if (isTrigger)
            {
                return false;
            }

            if (bounds.size.x < WalkableMinFootprint || bounds.size.z < WalkableMinFootprint)
            {
                return false;
            }

            float top = bounds.max.y;
            return top >= WalkableMinTopY && top <= WalkableMaxTopY;
        }

        /// <summary>콜라이더 하나가 어긴 규격 전부를 낸다 — 검사기의 판정 본체.</summary>
        public static ClearZoneIssue Evaluate(in ColliderProbe probe)
        {
            ClearZoneIssue issues = ClearZoneIssue.None;

            if (probe.IsMesh)
            {
                issues |= ClearZoneIssue.MeshColliderUsed;
            }

            // 궤도 구조물만 통로를 지나고 타일 밖으로 나간다 — 규격이 위치·스케일을 고정하므로 검사에서 뺀다.
            if (!probe.IsTrackStructure)
            {
                if (RisesAboveGround(probe.Bounds))
                {
                    if (OverlapsBandX(probe.Bounds, 0f, TrackCorridorHalfWidth))
                    {
                        issues |= ClearZoneIssue.TrackCorridor;
                    }

                    if (OverlapsBandX(probe.Bounds, TrackCorridorHalfWidth, DropZoneOuterX))
                    {
                        issues |= ClearZoneIssue.DropZone;
                    }
                }

                if (IsLongWall(probe.Bounds))
                {
                    issues |= ClearZoneIssue.LongWall;
                }

                if (ExceedsTileFootprint(probe.Bounds))
                {
                    issues |= ClearZoneIssue.OutsideTileFootprint;
                }
            }

            if (!probe.HasSurfaceMarker && IsWalkableSurface(probe.Bounds, probe.IsTrigger))
            {
                issues |= ClearZoneIssue.MissingWorldFrameSurface;
            }

            if (probe.IsUnderScatterSlot)
            {
                issues |= ClearZoneIssue.ColliderUnderScatterSlot;
            }

            return issues;
        }

        /// <summary>자원 앵커 하나가 어긴 배치 규격 — 위치는 타일 루트 로컬 좌표.</summary>
        public static AnchorIssue EvaluateAnchor(Vector3 localPosition)
        {
            AnchorIssue issues = AnchorIssue.None;

            float lateral = Mathf.Abs(localPosition.x);
            if (lateral < ResourceBandMinX)
            {
                issues |= AnchorIssue.TooCloseToTrack;
            }
            else if (lateral > ResourceBandMaxX)
            {
                issues |= AnchorIssue.BeyondGrabberReach;
            }

            if (Mathf.Abs(localPosition.z) > TileHalfLengthZ)
            {
                issues |= AnchorIssue.OutsideTileFootprint;
            }

            return issues;
        }

        /// <summary>타일당 앵커 개수가 기준(5~7개) 안인가.</summary>
        public static bool IsAnchorCountValid(int count)
        {
            return count >= MinAnchorsPerTile && count <= MaxAnchorsPerTile;
        }

        /// <summary>랜드마크 슬롯 개수가 상한(0~1개) 안인가 — 없는 타일이 대부분이다.</summary>
        public static bool IsLandmarkSlotCountValid(int count)
        {
            return count <= MaxLandmarkSlotsPerTile;
        }

        /// <summary>스캐터 슬롯 개수가 기준(4~10개) 안인가.</summary>
        public static bool IsScatterSlotCountValid(int count)
        {
            return count >= MinScatterSlotsPerTile && count <= MaxScatterSlotsPerTile;
        }

        /// <summary>
        /// 로컬 AABB를 행렬로 옮긴 월드(타일 로컬) AABB. 회전이 섞이면 축 정렬 상자가 커지므로
        /// 8코너를 도는 대신 절댓값 행렬로 익스텐트만 변환한다 — 결과는 같고 곱셈은 9번이면 끝난다.
        /// </summary>
        public static Bounds TransformAabb(Bounds localBounds, Matrix4x4 matrix)
        {
            Vector3 center = matrix.MultiplyPoint3x4(localBounds.center);
            Vector3 e = localBounds.extents;

            var extents = new Vector3(
                Mathf.Abs(matrix.m00) * e.x + Mathf.Abs(matrix.m01) * e.y + Mathf.Abs(matrix.m02) * e.z,
                Mathf.Abs(matrix.m10) * e.x + Mathf.Abs(matrix.m11) * e.y + Mathf.Abs(matrix.m12) * e.z,
                Mathf.Abs(matrix.m20) * e.x + Mathf.Abs(matrix.m21) * e.y + Mathf.Abs(matrix.m22) * e.z);

            return new Bounds(center, extents * 2f);
        }

        /// <summary>침범 한 건을 사람이 읽는 한 줄로. 검사기 목록과 콘솔 로그가 같은 문구를 쓴다.</summary>
        public static string Describe(ClearZoneIssue issue)
        {
            switch (issue)
            {
                case ClearZoneIssue.TrackCorridor:
                    return $"궤도 통로(|x| ≤ {TrackCorridorHalfWidth}) 침범 — 열차가 파묻힌다";
                case ClearZoneIssue.DropZone:
                    return $"하차·낙하 대역({TrackCorridorHalfWidth} < |x| ≤ {DropZoneOuterX}) 침범";
                case ClearZoneIssue.LongWall:
                    return $"몬스터 주행 대역의 {MaxWallLengthZ} m 초과 연속 장벽 — 웨이브가 갇힌다";
                case ClearZoneIssue.MeshColliderUsed:
                    return "MeshCollider — 타일은 6.67초마다 켜지므로 BoxCollider만 쓴다";
                case ClearZoneIssue.MissingWorldFrameSurface:
                    return "WorldFrameSurface 누락 — 밟았을 때 땅이 안 흐른다";
                case ClearZoneIssue.OutsideTileFootprint:
                    return $"타일 규격({TileHalfWidthX * 2} × {TileHalfLengthZ * 2} m) 밖 — 이음매가 어긋난다";
                case ClearZoneIssue.ColliderUnderScatterSlot:
                    return "스캐터 슬롯 아래의 콜라이더 — 변주는 피어마다 달라 없는 벽이 생긴다";
                default:
                    return issue.ToString();
            }
        }

        /// <summary>앵커 결함 한 건을 사람이 읽는 한 줄로.</summary>
        public static string Describe(AnchorIssue issue)
        {
            switch (issue)
            {
                case AnchorIssue.TooCloseToTrack:
                    return $"자원 대역 안쪽 이탈(|x| < {ResourceBandMinX}) — 열차·하차 동선 위다";
                case AnchorIssue.BeyondGrabberReach:
                    return $"자원 대역 바깥 이탈(|x| > {ResourceBandMaxX}) — 1단계 집게로 닿지 않는다";
                case AnchorIssue.OutsideTileFootprint:
                    return $"타일 길이(|z| ≤ {TileHalfLengthZ}) 밖";
                default:
                    return issue.ToString();
            }
        }
    }
}
