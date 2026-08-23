using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 클리어 존 규격(레벨 디자인 가이드 §4.2·§4.7)의 순수 판정 — 세그먼트가 40장으로 늘기 전에
    /// "무엇이 결함인가"를 코드로 고정한다. 검사기 UI는 이 판정을 그리기만 한다.
    /// </summary>
    public sealed class ClearZoneRulesTests
    {
        private static Bounds Box(float centerX, float centerY, float centerZ, float sizeX, float sizeY, float sizeZ)
        {
            return new Bounds(
                new Vector3(centerX, centerY, centerZ), new Vector3(sizeX, sizeY, sizeZ));
        }

        private static ColliderProbe Probe(
            Bounds bounds, bool isTrigger = false, bool isMesh = false,
            bool hasSurfaceMarker = true, bool isTrackStructure = false)
        {
            return new ColliderProbe(bounds, isTrigger, isMesh, hasSurfaceMarker, isTrackStructure);
        }

        // ── 대역 겹침 ────────────────────────────────────────────────

        [Test]
        public void 좌우_대칭_대역은_어느_쪽에_걸쳐도_겹침이다()
        {
            // 자원 대역은 ±양쪽 모두다 — 음수 쪽만 걸친 것도 잡아야 한다.
            Assert.IsTrue(ClearZoneRules.OverlapsBandX(Box(-10f, 0f, 0f, 2f, 2f, 2f), 4f, 16f));
            Assert.IsTrue(ClearZoneRules.OverlapsBandX(Box(10f, 0f, 0f, 2f, 2f, 2f), 4f, 16f));
        }

        [Test]
        public void 대역_사이의_빈틈은_겹치지_않는다()
        {
            // |x| 4~16 대역과 자유 대역(24 초과) 사이의 20 m 지점.
            Assert.IsFalse(ClearZoneRules.OverlapsBandX(Box(20f, 0f, 0f, 2f, 2f, 2f), 4f, 16f));
        }

        // ── 궤도 통로·하차 대역 ──────────────────────────────────────

        [Test]
        public void 궤도_통로에_솟은_조형물은_침범이다()
        {
            ClearZoneIssue issues = ClearZoneRules.Evaluate(Probe(Box(0f, 1f, 0f, 2f, 2f, 2f)));
            Assert.IsTrue((issues & ClearZoneIssue.TrackCorridor) != 0);
        }

        [Test]
        public void 통로_안이어도_지면_높이면_침범이_아니다()
        {
            // 지면(60 × 0.2 × 40)은 통로를 지나야 한다 — 상면이 y ≈ 0이면 "솟은 것"이 아니다.
            ClearZoneIssue issues = ClearZoneRules.Evaluate(Probe(Box(0f, -0.1f, 0f, 60f, 0.2f, 40f)));
            Assert.IsTrue((issues & ClearZoneIssue.TrackCorridor) == 0);
        }

        [Test]
        public void 궤도_구조물은_통로_침범에서_제외된다()
        {
            // 궤도는 통로를 지나는 유일한 예외 — 규격이 위치·스케일을 고정한다.
            ClearZoneIssue issues = ClearZoneRules.Evaluate(
                Probe(Box(0f, 0.5f, 0f, 3f, 1f, 42f), isTrackStructure: true));

            Assert.IsTrue((issues & ClearZoneIssue.TrackCorridor) == 0);
            Assert.IsTrue((issues & ClearZoneIssue.OutsideTileFootprint) == 0,
                "궤도는 타일보다 1.12 m 길다(as-built) — 풋프린트 검사에서도 제외된다");
        }

        [Test]
        public void 궤도_구조물은_본체와_도상_둘뿐이다()
        {
            // 예외를 이름으로 고정한다 — 두께로 봐주면 "낮은 것은 통과"가 되어 규칙이 새어나간다.
            Assert.IsTrue(ClearZoneRules.IsTrackStructureName("RailTrack"));
            Assert.IsTrue(ClearZoneRules.IsTrackStructureName("TrackBed"), "도상은 궤도 규격의 일부다");
            Assert.IsFalse(ClearZoneRules.IsTrackStructureName("Ground"));
            Assert.IsFalse(ClearZoneRules.IsTrackStructureName("Env_Rock_A"));
        }

        [Test]
        public void 도상_두께의_턱도_궤도가_아니면_침범이다()
        {
            // TrackBed 상면 0.06 m — 같은 높이여도 이름이 궤도가 아니면 통로에 둘 수 없다.
            ClearZoneIssue issues = ClearZoneRules.Evaluate(Probe(Box(0f, 0.03f, 0f, 3.2f, 0.06f, 40f)));
            Assert.IsTrue((issues & ClearZoneIssue.TrackCorridor) != 0);
        }

        [Test]
        public void 하차_대역의_조형물은_침범이다()
        {
            // 3.3 < |x| ≤ 4 — 승차 램프·추락 복귀 동선.
            ClearZoneIssue issues = ClearZoneRules.Evaluate(Probe(Box(3.7f, 0.6f, 0f, 0.5f, 1.2f, 0.5f)));

            Assert.IsTrue((issues & ClearZoneIssue.DropZone) != 0);
            Assert.IsTrue((issues & ClearZoneIssue.TrackCorridor) == 0);
        }

        [Test]
        public void 자원_대역의_조형물은_침범이_아니다()
        {
            ClearZoneIssue issues = ClearZoneRules.Evaluate(Probe(Box(10f, 1f, 0f, 2f, 2f, 2f)));
            Assert.AreEqual(ClearZoneIssue.None, issues);
        }

        // ── 연속 장벽 ───────────────────────────────────────────────

        [Test]
        public void 몬스터_대역의_긴_벽은_결함이다()
        {
            // 회피 레이캐스트가 3 m뿐이라 8 m 초과 벽에 갇힌다.
            Assert.IsTrue(ClearZoneRules.IsLongWall(Box(12f, 1f, 0f, 1f, 2f, 12f)));
        }

        [Test]
        public void 낮은_지면_판때기는_길어도_벽이_아니다()
        {
            // 지면은 z 40 m지만 높이 0.2 m라 넘어 다닌다.
            Assert.IsFalse(ClearZoneRules.IsLongWall(Box(0f, -0.1f, 0f, 60f, 0.2f, 40f)));
        }

        [Test]
        public void 짧은_바위는_높아도_벽이_아니다()
        {
            Assert.IsFalse(ClearZoneRules.IsLongWall(Box(12f, 1.5f, 0f, 3f, 3f, 4f)));
        }

        [Test]
        public void 자유_대역의_절벽은_길어도_허용된다()
        {
            // |x| > 24는 시야 차단물의 자리다 — 절벽·숲·건물 벽이 여기 선다.
            Assert.IsFalse(ClearZoneRules.IsLongWall(Box(27f, 5f, 0f, 4f, 10f, 40f)));
        }

        // ── 콜라이더 종류·표면 마커 ──────────────────────────────────

        [Test]
        public void MeshCollider는_결함이다()
        {
            // 타일은 6.67초마다 켜지고 꺼진다 — 매번 굽는 비용을 낼 수 없다.
            ClearZoneIssue issues = ClearZoneRules.Evaluate(
                Probe(Box(10f, 1f, 0f, 2f, 2f, 2f), isMesh: true));

            Assert.IsTrue((issues & ClearZoneIssue.MeshColliderUsed) != 0);
        }

        [Test]
        public void 밟을_수_있는_표면에_마커가_없으면_결함이다()
        {
            ClearZoneIssue issues = ClearZoneRules.Evaluate(
                Probe(Box(0f, -0.1f, 0f, 60f, 0.2f, 40f), hasSurfaceMarker: false));

            Assert.IsTrue((issues & ClearZoneIssue.MissingWorldFrameSurface) != 0);
        }

        [Test]
        public void 트리거는_표면_마커_대상이_아니다()
        {
            ClearZoneIssue issues = ClearZoneRules.Evaluate(
                Probe(Box(0f, -0.1f, 0f, 60f, 0.2f, 40f), isTrigger: true, hasSurfaceMarker: false));

            Assert.IsTrue((issues & ClearZoneIssue.MissingWorldFrameSurface) == 0);
        }

        [Test]
        public void 좁은_밑동은_표면_마커_대상이_아니다()
        {
            // 나무 밑동·울타리 기둥에 컨베이어 마커를 요구하지 않는다.
            Assert.IsFalse(ClearZoneRules.IsWalkableSurface(Box(10f, 0.5f, 0f, 0.6f, 1f, 0.6f), false));
        }

        [Test]
        public void 절벽_상판은_표면_마커_대상이_아니다()
        {
            // 상면 11 m — 밟을 일이 없다.
            Assert.IsFalse(ClearZoneRules.IsWalkableSurface(Box(27f, 5.5f, 0f, 6f, 11f, 8f), false));
        }

        // ── 이음매·풋프린트 ─────────────────────────────────────────

        [Test]
        public void 타일_길이를_넘으면_이음매_결함이다()
        {
            // 앞뒤 끝단은 z ±20 — 넘으면 옆 타일과 겹친다.
            Assert.IsTrue(ClearZoneRules.ExceedsTileFootprint(Box(10f, 1f, 19f, 2f, 2f, 4f)));
        }

        [Test]
        public void 타일_규격_안이면_이음매_결함이_아니다()
        {
            Assert.IsFalse(ClearZoneRules.ExceedsTileFootprint(Box(27f, 5f, 0f, 6f, 10f, 8f)));
        }

        [Test]
        public void 넓힌_폭_안의_바깥_대역은_이음매_결함이_아니다()
        {
            // 판이 ±60으로 넓어진 뒤 절벽 벽이 서는 자리 — 예전 규격(±30)이었다면 결함으로 잡혔다.
            Assert.IsFalse(ClearZoneRules.ExceedsTileFootprint(Box(51f, 5f, 0f, 12f, 16f, 12f)));
        }

        [Test]
        public void 넓힌_폭을_넘으면_이음매_결함이다()
        {
            // 판 밖은 지면이 없다 — 여기에 놓인 것은 공중에 뜬다.
            Assert.IsTrue(ClearZoneRules.ExceedsTileFootprint(Box(58f, 5f, 0f, 8f, 10f, 8f)));
        }

        // ── 자원 앵커 ───────────────────────────────────────────────

        [Test]
        public void 앵커가_4m_안쪽이면_결함이다()
        {
            AnchorIssue issues = ClearZoneRules.EvaluateAnchor(new Vector3(2f, 0.4f, 0f));
            Assert.IsTrue((issues & AnchorIssue.TooCloseToTrack) != 0);
        }

        [Test]
        public void 앵커가_16m_밖이면_결함이다()
        {
            // 1단계 집게 사거리 20 m — 17 m 밖 자원은 영원히 닿지 않는다.
            AnchorIssue issues = ClearZoneRules.EvaluateAnchor(new Vector3(-18f, 0.4f, 0f));
            Assert.IsTrue((issues & AnchorIssue.BeyondGrabberReach) != 0);
        }

        [Test]
        public void 대역_안_앵커는_결함이_없다()
        {
            Assert.AreEqual(AnchorIssue.None, ClearZoneRules.EvaluateAnchor(new Vector3(-12f, 0.4f, 8f)));
        }

        [Test]
        public void 앵커가_타일_길이를_넘으면_결함이다()
        {
            AnchorIssue issues = ClearZoneRules.EvaluateAnchor(new Vector3(10f, 0.4f, 24f));
            Assert.IsTrue((issues & AnchorIssue.OutsideTileFootprint) != 0);
        }

        [Test]
        public void 앵커_개수_기준은_5에서_7이다()
        {
            Assert.IsFalse(ClearZoneRules.IsAnchorCountValid(4));
            Assert.IsTrue(ClearZoneRules.IsAnchorCountValid(5));
            Assert.IsTrue(ClearZoneRules.IsAnchorCountValid(7));
            Assert.IsFalse(ClearZoneRules.IsAnchorCountValid(8));
        }

        // ── AABB 변환 ───────────────────────────────────────────────

        [Test]
        public void 스케일이_AABB에_반영된다()
        {
            Bounds result = ClearZoneRules.TransformAabb(
                new Bounds(Vector3.zero, Vector3.one),
                Matrix4x4.TRS(new Vector3(5f, 0f, 0f), Quaternion.identity, new Vector3(2f, 3f, 4f)));

            Assert.AreEqual(5f, result.center.x, 1e-4f);
            Assert.AreEqual(2f, result.size.x, 1e-4f);
            Assert.AreEqual(3f, result.size.y, 1e-4f);
            Assert.AreEqual(4f, result.size.z, 1e-4f);
        }

        [Test]
        public void 회전하면_축_정렬_상자가_커진다()
        {
            // 45° 돌린 1 m 정사각형의 AABB는 √2 m — 회전을 무시하면 침범을 놓친다.
            Bounds result = ClearZoneRules.TransformAabb(
                new Bounds(Vector3.zero, new Vector3(1f, 1f, 1f)),
                Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 45f, 0f), Vector3.one));

            Assert.AreEqual(Mathf.Sqrt(2f), result.size.x, 1e-4f);
            Assert.AreEqual(1f, result.size.y, 1e-4f);
        }

        [Test]
        public void 회전한_긴_벽이_통로를_가로지르면_잡힌다()
        {
            // 타일 밖에 놓인 듯한 좌표라도 90° 돌면 통로를 가로지른다 — 실제로 겪은 함정의 회귀 방어.
            Bounds bounds = ClearZoneRules.TransformAabb(
                new Bounds(Vector3.zero, new Vector3(1f, 2f, 20f)),
                Matrix4x4.TRS(new Vector3(0f, 1f, 0f), Quaternion.Euler(0f, 90f, 0f), Vector3.one));

            ClearZoneIssue issues = ClearZoneRules.Evaluate(Probe(bounds));
            Assert.IsTrue((issues & ClearZoneIssue.TrackCorridor) != 0);
        }
    }
}
