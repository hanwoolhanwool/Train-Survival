using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 판자 증축 조준의 순수 기하 (건축 개편 3차 — 계획서 §2.9의 "갑판 평면 연장 조준").
    /// <see cref="CarBuildAimLogic"/>·<see cref="CarRecoupleAimLogic"/>과 같은 결로, 컨트롤러가
    /// 입력 해석만 남기고 판정 기하는 여기서 EditMode로 검증한다.
    ///
    /// 판자가 아직 없는 자리에는 콜라이더가 없어 레이캐스트로 잡히지 않으므로, 갑판 높이 평면과
    /// 조준 레이의 교차로 "어느 열을 겨누는가"를 구한다.
    /// </summary>
    public static class PlankAimLogic
    {
        /// <summary>
        /// 조준 레이가 갑판 높이 평면과 만나는 지점 — 아래를 향하지 않거나, 사거리 밖이거나,
        /// 그 앞을 다른 물체가 확실히 가리고 있으면 false.
        /// <paramref name="blockedDistance"/>는 레이가 먼저 맞은 물체까지의 거리(없으면 무한대),
        /// <paramref name="occlusionTolerance"/>는 판자 두께·갑판 상면의 미세한 높이 차로 조준이
        /// 끊기지 않게 하는 여유다.
        /// </summary>
        public static bool TryDeckPlanePoint(Vector3 origin, Vector3 forward,
            float deckHeight, float maxRange, float blockedDistance, float occlusionTolerance,
            out Vector3 point)
        {
            point = default;

            float denominator = forward.y;
            if (Mathf.Abs(denominator) < 0.0001f)
            {
                return false;
            }

            float distance = (deckHeight - origin.y) / denominator;
            if (distance <= 0f || distance > maxRange || blockedDistance < distance - occlusionTolerance)
            {
                return false;
            }

            point = origin + forward * distance;
            return true;
        }

        /// <summary>
        /// 겨눈 월드 X가 어느 판자 열인지 — 이미 깔린 판자면 <paramref name="emptySlot"/> false와 함께
        /// <b>가장 바깥</b> 열(철거 대상)을, 아직 없는 다음 자리면 true와 함께 그 열(증축 대상)을 돌려준다.
        /// 칸 본체 열이거나 예약 범위 밖 허공이면 false.
        /// </summary>
        public static bool TryResolveColumn(float worldX, int bodyColumns, float cellSize,
            int leftPlanks, int rightPlanks,
            out PlankSide side, out bool emptySlot, out int previewColumn)
        {
            side = PlankSide.Left;
            emptySlot = false;
            previewColumn = 0;

            int column = StructureGridLogic.WorldXToColumn(worldX, bodyColumns, cellSize);

            if (PlankGridLogic.TryGetPlankColumn(column, bodyColumns, leftPlanks, rightPlanks,
                out PlankSide placedSide, out _))
            {
                // 철거는 항상 가장 바깥 열이 대상이다 — 안쪽 열을 겨눠도 프리뷰·판정이 바깥을 가리킨다.
                side = placedSide;
                int columns = StructureGridLogic.ClampPlankColumns(
                    side == PlankSide.Left ? leftPlanks : rightPlanks);
                previewColumn = StructureGridLogic.PlankColumn(side, columns - 1, bodyColumns);
                return true;
            }

            if (PlankGridLogic.IsNextPlankColumn(column, bodyColumns, leftPlanks, rightPlanks,
                out PlankSide nextSide))
            {
                side = nextSide;
                emptySlot = true;
                previewColumn = column;
                return true;
            }

            return false;
        }

        /// <summary>판자 열 하나의 프리뷰 상자 — 폭 = 셀 1칸, 길이 = 칸의 그리드 행 전체.</summary>
        public static void ColumnVolume(int column, int bodyColumns, int rows,
            float carCenterZ, float cellSize, float deckHeight, float ghostHeight,
            out Vector3 center, out Vector3 size)
        {
            float worldX = StructureGridLogic.ColumnCenterWorldX(column, bodyColumns, cellSize);

            center = new Vector3(worldX, deckHeight + ghostHeight * 0.5f, carCenterZ);
            size = new Vector3(cellSize, ghostHeight, rows * cellSize);
        }
    }
}
