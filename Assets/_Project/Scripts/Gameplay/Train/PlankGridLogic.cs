using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 옆면 판자 증축의 순수 계산 로직 (건축 개편 3차 — 계획서 §2.9, 결정 ⑥: 셀 열 단위).
    /// 그리드 좌표계 자체(열·행 변환, 예약 상한)는 <see cref="StructureGridLogic"/>이 들고, 여기서는
    /// <b>판자 상태 규칙</b>만 본다 — 어디가 다음 판자 자리인가, 지금 붙일 수 있는가, 뜯을 수 있는가,
    /// 판자가 갑판 폭을 얼마나 넓히는가. 계획서 §3.1 SRP 규약("설치·철거·창고 블록·판자 로직이
    /// 한 클래스에 뭉치지 않게 분리")에 따른 분리다.
    /// Unity 비의존 static 클래스라 물리·씬 없이 EditMode로 검증하고, 소유자 프리뷰와 호스트 확정이
    /// 같은 함수를 쓴다.
    /// </summary>
    public static class PlankGridLogic
    {

        /// <summary>
        /// 그 열이 <b>지금 지을 수 있는 다음 판자 자리</b>인가 — 본체 바로 바깥의 첫 빈 판자 열
        /// 하나만 참이다(중간을 건너뛴 허공 판자를 막는다). 맞으면 어느 쪽인지 돌려준다.
        /// </summary>
        public static bool IsNextPlankColumn(int cellX, int bodyColumns, int leftPlanks, int rightPlanks,
            out PlankSide side)
        {
            side = PlankSide.Left;
            int left = StructureGridLogic.ClampPlankColumns(leftPlanks);
            int right = StructureGridLogic.ClampPlankColumns(rightPlanks);

            if (left < StructureGridLogic.MaxPlankColumnsPerSide && cellX == StructureGridLogic.PlankColumn(PlankSide.Left, left, bodyColumns))
            {
                side = PlankSide.Left;
                return true;
            }

            if (right < StructureGridLogic.MaxPlankColumnsPerSide && cellX == StructureGridLogic.PlankColumn(PlankSide.Right, right, bodyColumns))
            {
                side = PlankSide.Right;
                return true;
            }

            return false;
        }

        /// <summary>
        /// 그 열이 이미 깔린 판자 열이면 어느 쪽 몇 번째인지 — 판자 조준(철거)·뷰 배치용.
        /// 본체 열이거나 판자가 없는 열이면 false.
        /// </summary>
        public static bool TryGetPlankColumn(int cellX, int bodyColumns, int leftPlanks, int rightPlanks,
            out PlankSide side, out int ordinal)
        {
            int left = StructureGridLogic.ClampPlankColumns(leftPlanks);
            int right = StructureGridLogic.ClampPlankColumns(rightPlanks);

            if (cellX < StructureGridLogic.FirstBodyColumn)
            {
                side = PlankSide.Left;
                ordinal = StructureGridLogic.FirstBodyColumn - 1 - cellX;
                return ordinal >= 0 && ordinal < left;
            }

            if (cellX >= StructureGridLogic.FirstBodyColumn + bodyColumns)
            {
                side = PlankSide.Right;
                ordinal = cellX - (StructureGridLogic.FirstBodyColumn + bodyColumns);
                return ordinal >= 0 && ordinal < right;
            }

            side = PlankSide.Left;
            ordinal = -1;
            return false;
        }

        /// <summary>
        /// 판자를 포함한 갑판 반폭(m) — 그 쪽으로 걸어 나갈 수 있는 한계다. 판자가 없으면 칸 실물 반폭
        /// (본체 그리드 밖 여백 0.3 m 포함), 있으면 본체 그리드 반폭 + 판자 열 폭.
        /// 낙하 판정·몬스터 승차 판정이 칸 폭 상수 대신 이 값을 쓴다 (계획서 §2.9 — 폭 파생 판정 확장).
        /// </summary>
        public static float DeckHalfWidth(float carWidth, float cellSize, int plankColumns)
        {
            int columns = StructureGridLogic.ClampPlankColumns(plankColumns);
            if (columns <= 0)
            {
                return carWidth * 0.5f;
            }

            float bodyHalf = StructureGridLogic.BodyColumns(carWidth, cellSize) * cellSize * 0.5f;
            return Mathf.Max(carWidth * 0.5f, bodyHalf + columns * cellSize);
        }

        /// <summary>
        /// 이 쪽에 판자 1열을 더 붙일 수 있는지 — 칸이 편성에 살아 붙어 있고(기관차 제외),
        /// 현재 열 수가 상한(에셋 · 좌표계 예약 중 작은 쪽) 미만이어야 한다.
        /// 소유자 프리뷰와 호스트 확정이 같은 판정을 쓴다.
        /// </summary>
        public static bool CanBuildPlank(CarState[] cars, int carIndex, PlankSide side, int maxColumns)
        {
            if (cars == null || carIndex < 0 || carIndex >= cars.Length)
            {
                return false;
            }

            CarState car = cars[carIndex];
            if (!TrainStateLogic.IsCarPresent(car) || !TrainStateLogic.IsDestructible(car.Type))
            {
                return false;
            }

            int current = side == PlankSide.Left ? car.LeftPlanks : car.RightPlanks;
            return current < Mathf.Min(StructureGridLogic.ClampPlankColumns(maxColumns), StructureGridLogic.MaxPlankColumnsPerSide);
        }

        /// <summary>
        /// 이 쪽 가장 바깥 판자 1열을 뜯을 수 있는지 (계획서 §2.9 — 그 열 위에 건축물이 있으면 기각).
        /// 안쪽 열부터 사라져 허공 판자가 남는 일이 없도록 항상 가장 바깥 열만 대상이다.
        /// </summary>
        public static bool CanRemovePlank(StructureEntry[] entries, CarState[] cars, int carIndex, PlankSide side,
            float carWidth, float cellSize)
        {
            if (cars == null || carIndex < 0 || carIndex >= cars.Length)
            {
                return false;
            }

            CarState car = cars[carIndex];
            if (!TrainStateLogic.IsCarPresent(car) || !TrainStateLogic.IsDestructible(car.Type))
            {
                return false;
            }

            int current = StructureGridLogic.ClampPlankColumns(side == PlankSide.Left ? car.LeftPlanks : car.RightPlanks);
            if (current <= 0)
            {
                return false;
            }

            // 뜯을 열 = 가장 바깥(ordinal current - 1). 그 열에 걸친 건축물이 하나라도 있으면 기각.
            int bodyColumns = StructureGridLogic.BodyColumns(carWidth, cellSize);
            return !StructureGridLogic.ColumnHasStructure(entries, carIndex, StructureGridLogic.PlankColumn(side, current - 1, bodyColumns));
        }
    }
}
