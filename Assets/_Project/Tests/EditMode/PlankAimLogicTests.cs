using Game.Gameplay.Train;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 판자 조준 기하 검증 (건축 개편 3차 — 계획서 §2.9의 "갑판 평면 연장 조준").
    /// 확정 규격: 칸 4.6 × 15 m · 셀 1.0 m · 갑판 높이 3 m → 본체 열 2~5, 판자 열 0~1 · 6~7.
    /// 컨트롤러가 아니라 여기서 검증되므로 조준 규칙이 씬 없이 고정된다.
    /// </summary>
    public sealed class PlankAimLogicTests
    {
        private const float CellSize = 1f;
        private const float DeckHeight = 3f;
        private const int BodyColumns = 4;
        private const float MaxRange = 4f;
        private const float OcclusionTolerance = 0.5f;

        // ── 갑판 평면 교차 ──────────────────

        [Test]
        public void 아래를_향한_조준은_갑판_평면_지점을_돌려준다()
        {
            // 갑판보다 1.6 m 위에서 45° 아래·바깥으로 — 평면까지 거리는 1.6 / sin45 ≈ 2.26 m.
            var origin = new Vector3(0f, DeckHeight + 1.6f, 0f);
            Vector3 forward = new Vector3(1f, -1f, 0f).normalized;

            Assert.That(PlankAimLogic.TryDeckPlanePoint(origin, forward, DeckHeight, MaxRange,
                float.PositiveInfinity, OcclusionTolerance, out Vector3 point), Is.True);
            Assert.That(point.y, Is.EqualTo(DeckHeight).Within(0.001f));
            Assert.That(point.x, Is.EqualTo(1.6f).Within(0.001f), "수평 이동량 = 높이차(45°)");
        }

        [Test]
        public void 수평_조준과_사거리_밖은_기각된다()
        {
            var origin = new Vector3(0f, DeckHeight + 1.6f, 0f);

            Assert.That(PlankAimLogic.TryDeckPlanePoint(origin, Vector3.forward, DeckHeight, MaxRange,
                float.PositiveInfinity, OcclusionTolerance, out _), Is.False, "평면과 평행");

            // 거의 수평(얕은 각) — 평면까지 거리가 사거리를 넘는다.
            Vector3 shallow = new Vector3(1f, -0.1f, 0f).normalized;
            Assert.That(PlankAimLogic.TryDeckPlanePoint(origin, shallow, DeckHeight, MaxRange,
                float.PositiveInfinity, OcclusionTolerance, out _), Is.False, "사거리 초과");

            // 위를 향하면 평면 교차가 뒤쪽이다.
            Vector3 up = new Vector3(1f, 1f, 0f).normalized;
            Assert.That(PlankAimLogic.TryDeckPlanePoint(origin, up, DeckHeight, MaxRange,
                float.PositiveInfinity, OcclusionTolerance, out _), Is.False, "위쪽 — 교차가 뒤");
        }

        [Test]
        public void 앞을_가로막은_물체가_있으면_기각되고_여유_안이면_통과한다()
        {
            var origin = new Vector3(0f, DeckHeight + 1.6f, 0f);
            Vector3 forward = new Vector3(1f, -1f, 0f).normalized;
            float planeDistance = 1.6f * Mathf.Sqrt(2f);

            // 평면보다 확실히 앞(여유 초과)에서 막히면 그 물체를 겨눈 것이다.
            Assert.That(PlankAimLogic.TryDeckPlanePoint(origin, forward, DeckHeight, MaxRange,
                planeDistance - OcclusionTolerance - 0.1f, OcclusionTolerance, out _), Is.False);

            // 판자 두께·갑판 상면과의 미세한 차이는 여유 안이라 조준이 끊기지 않는다.
            Assert.That(PlankAimLogic.TryDeckPlanePoint(origin, forward, DeckHeight, MaxRange,
                planeDistance - 0.1f, OcclusionTolerance, out _), Is.True);
        }

        // ── 열 선택 ──────────────────

        [Test]
        public void 판자가_없으면_본체_바로_바깥이_증축_자리다()
        {
            // 좌측 판자 자리(열 1)의 중심 x = -2.5.
            Assert.That(PlankAimLogic.TryResolveColumn(-2.5f, BodyColumns, CellSize, 0, 0,
                out PlankSide side, out bool emptySlot, out int previewColumn), Is.True);
            Assert.That(side, Is.EqualTo(PlankSide.Left));
            Assert.That(emptySlot, Is.True);
            Assert.That(previewColumn, Is.EqualTo(1));

            // 우측(열 6) 중심 x = 2.5.
            Assert.That(PlankAimLogic.TryResolveColumn(2.5f, BodyColumns, CellSize, 0, 0,
                out PlankSide rightSide, out _, out int rightColumn), Is.True);
            Assert.That(rightSide, Is.EqualTo(PlankSide.Right));
            Assert.That(rightColumn, Is.EqualTo(6));
        }

        [Test]
        public void 본체_열과_예약_밖_허공은_조준이_아니다()
        {
            Assert.That(PlankAimLogic.TryResolveColumn(0f, BodyColumns, CellSize, 0, 0,
                out _, out _, out _), Is.False, "본체 가운데");
            Assert.That(PlankAimLogic.TryResolveColumn(-3.5f, BodyColumns, CellSize, 0, 0,
                out _, out _, out _), Is.False, "판자 한 칸 건너뛴 허공");
        }

        [Test]
        public void 이미_깔린_판자는_가장_바깥_열이_철거_대상이다()
        {
            // 좌측 2열(열 0·1)이 깔린 상태에서 안쪽(열 1, x = -2.5)을 겨눠도 바깥(열 0)이 대상이다.
            Assert.That(PlankAimLogic.TryResolveColumn(-2.5f, BodyColumns, CellSize, 2, 0,
                out PlankSide side, out bool emptySlot, out int previewColumn), Is.True);
            Assert.That(side, Is.EqualTo(PlankSide.Left));
            Assert.That(emptySlot, Is.False, "이미 깔림 — 철거 대상");
            Assert.That(previewColumn, Is.EqualTo(0), "가장 바깥 열");

            // 1열만 있으면 그 열(1)이 곧 가장 바깥이다.
            Assert.That(PlankAimLogic.TryResolveColumn(-2.5f, BodyColumns, CellSize, 1, 0,
                out _, out bool oneEmpty, out int oneColumn), Is.True);
            Assert.That(oneEmpty, Is.False);
            Assert.That(oneColumn, Is.EqualTo(1));
        }

        [Test]
        public void 상한까지_찬_쪽은_바깥을_겨눠도_증축_자리가_없다()
        {
            // 좌측 2열(예약 상한)이 찬 상태에서 그 바깥(x = -4.5)은 어떤 열도 아니다.
            Assert.That(PlankAimLogic.TryResolveColumn(-4.5f, BodyColumns, CellSize, 2, 0,
                out _, out _, out _), Is.False);
        }

        // ── 프리뷰 상자 ──────────────────

        [Test]
        public void 판자_프리뷰_상자는_셀_폭과_칸_행_전체를_덮는다()
        {
            PlankAimLogic.ColumnVolume(1, BodyColumns, rows: 15, carCenterZ: -20f, cellSize: CellSize,
                deckHeight: DeckHeight, ghostHeight: 1.2f, out Vector3 center, out Vector3 size);

            Assert.That(center.x, Is.EqualTo(-2.5f).Within(0.001f), "좌측 판자 열 중심");
            Assert.That(center.y, Is.EqualTo(DeckHeight + 0.6f).Within(0.001f), "갑판 위 절반 높이");
            Assert.That(center.z, Is.EqualTo(-20f).Within(0.001f), "칸 중심 정렬");
            Assert.That(size, Is.EqualTo(new Vector3(1f, 1.2f, 15f)));
        }
    }
}
