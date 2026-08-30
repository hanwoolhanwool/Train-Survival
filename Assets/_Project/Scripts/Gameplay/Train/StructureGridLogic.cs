using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 바닥 건축 그리드의 순수 계산 로직 (건축 개편 1차 — 계획서 §2.3). <see cref="TrainLayoutMath"/>와
    /// 같은 결의 static 클래스로, 전부 에셋 파라미터(셀 크기·칸 폭·칸 길이)를 인자로 받아
    /// 물리·씬 없이 EditMode로 검증한다. 소유자 프리뷰와 호스트 확정이 같은 함수를 쓴다.
    ///
    /// <b>고정 예약 좌표계</b> (계획서 §1.2 기술 확정): 열 좌표는 "좌측 최대 판자열 = 열 0"으로 고정
    /// 예약한다 — 열 0~1 = 좌측 판자(3차), 열 2~5 = 칸 본체(폭 4.6 m 중 4열, 양옆 0.3 m 여백),
    /// 열 6~7 = 우측 판자. 좌측 판자를 나중에 설치해도 기존 항목의 CellX 재색인이 영원히 불필요하다.
    /// 행은 칸 후미(-Z)가 0, 전방(+Z)으로 증가한다.
    ///
    /// <b>판자 증축</b> (건축 개편 3차 — 계획서 §2.9, 결정 ⑥): 칸마다 좌/우 판자 열 수
    /// (<see cref="CarState.LeftPlanks"/>·<see cref="CarState.RightPlanks"/>)가 유효 열 범위를 넓힌다.
    /// 판자 열은 본체 그리드에 딱 붙으므로 셀 규격이 본체와 같고, 그 위에 건축물을 설치할 수 있다.
    /// </summary>
    public static class StructureGridLogic
    {
        /// <summary>
        /// 좌/우 각각 예약된 판자 열 수 — 컴파일 상수. 에셋의 판자 상한(3차)은 이 값을 초과할 수 없다
        /// (초과하면 좌표 재색인이 필요해지므로 클램프한다).
        /// </summary>
        public const int MaxPlankColumnsPerSide = 2;

        /// <summary>칸 본체의 첫 열 — 좌측 판자 예약 열 다음.</summary>
        public const int FirstBodyColumn = MaxPlankColumnsPerSide;

        /// <summary>부동소수 나눗셈이 4.6/1.0 = 4.599… 처럼 경계 직전 값이 되어도 열 수가 흔들리지 않게 하는 여유.</summary>
        private const float DivisionEpsilon = 0.0001f;

        /// <summary>칸 본체가 담는 그리드 열 수 — 폭에서 나오는 정수 열(잔여 폭은 양옆 여백).</summary>
        public static int BodyColumns(float carWidth, float cellSize)
        {
            return cellSize > 0f ? Mathf.Max(1, Mathf.FloorToInt(carWidth / cellSize + DivisionEpsilon)) : 1;
        }

        /// <summary>칸이 담는 그리드 행 수 — 길이에서 나오는 정수 행(잔여 길이는 앞뒤 여백).</summary>
        public static int Rows(float carLength, float cellSize)
        {
            return cellSize > 0f ? Mathf.Max(1, Mathf.FloorToInt(carLength / cellSize + DivisionEpsilon)) : 1;
        }

        // ── 판자 증축 (건축 개편 3차 — 계획서 §2.9) ──────────────────

        /// <summary>
        /// 판자 열 수를 좌표계 예약 상한으로 클램프한다 — 에셋 상한(<see cref="TrainExpansionSettings.MaxPlankColumns"/>)이
        /// <see cref="MaxPlankColumnsPerSide"/>를 넘으면 좌표 재색인이 필요해지므로 여기서 잘라낸다.
        /// </summary>
        public static int ClampPlankColumns(int columns)
        {
            return Mathf.Clamp(columns, 0, MaxPlankColumnsPerSide);
        }

        /// <summary>유효 열 범위의 첫 열 — 좌측 판자가 있으면 본체보다 그만큼 왼쪽에서 시작한다.</summary>
        private static int FirstColumn(int leftPlanks)
        {
            return FirstBodyColumn - ClampPlankColumns(leftPlanks);
        }

        /// <summary>유효 열 수 — 칸 본체 + 좌우 판자 열.</summary>
        private static int ValidColumns(int bodyColumns, int leftPlanks, int rightPlanks)
        {
            return bodyColumns + ClampPlankColumns(leftPlanks) + ClampPlankColumns(rightPlanks);
        }

        /// <summary>
        /// 그 쪽 <paramref name="ordinal"/>번째(0 = 본체에 붙은 안쪽) 판자 열의 열 좌표.
        /// 설치·제거·뷰 배치가 같은 좌표를 쓴다.
        /// </summary>
        public static int PlankColumn(PlankSide side, int ordinal, int bodyColumns)
        {
            return side == PlankSide.Left
                ? FirstBodyColumn - 1 - ordinal
                : FirstBodyColumn + bodyColumns + ordinal;
        }

        /// <summary>
        /// 열 하나의 중심 월드 X — 열차는 X=0 고정 주행이라 칸과 무관하게 열 좌표만으로 정해진다.
        /// 본체·판자 열이 같은 규격이므로 판자 뷰·프리뷰도 이 함수를 쓴다.
        /// </summary>
        public static float ColumnCenterWorldX(int cellX, int bodyColumns, float cellSize)
        {
            float bodyHalf = bodyColumns * cellSize * 0.5f;
            return -bodyHalf + (cellX - FirstBodyColumn + 0.5f) * cellSize;
        }

        /// <summary>
        /// 월드 X가 놓이는 열 좌표 — 범위 밖(허공)도 그대로 돌려준다. 조준이 본체 밖 판자 자리를
        /// 가리키는지 판단하는 데 쓴다 (갑판 평면 연장 조준 — 계획서 §2.9 확정).
        /// </summary>
        public static int WorldXToColumn(float worldX, int bodyColumns, float cellSize)
        {
            if (cellSize <= 0f)
            {
                return FirstBodyColumn;
            }

            float bodyHalf = bodyColumns * cellSize * 0.5f;
            return FirstBodyColumn + Mathf.FloorToInt((worldX + bodyHalf) / cellSize);
        }

        /// <summary>
        /// 월드 Z가 놓이는 칸 안 행 좌표 — 범위 밖(연결부·칸 밖)도 그대로 돌려준다.
        /// <see cref="TryWorldToPlacementCell"/>이 쓰는 행 식과 같은 것을 스냅 없이 노출한 것이라,
        /// 설치 자리와 "지금 서 있는 자리"가 같은 좌표계를 쓴다 (천막 그늘 판정 — 계획 §4.4).
        /// </summary>
        public static int WorldZToRow(float worldZ, float carCenterZ, float carLength, float cellSize)
        {
            if (cellSize <= 0f)
            {
                return 0;
            }

            int rows = Rows(carLength, cellSize);
            float rowSpanHalf = rows * cellSize * 0.5f;
            return Mathf.FloorToInt((worldZ - (carCenterZ - rowSpanHalf)) / cellSize);
        }

        /// <summary>그 열(모든 행)에 걸친 건축물이 하나라도 있는지 — 판자 제거 기각 판정.</summary>
        public static bool ColumnHasStructure(StructureEntry[] entries, int carIndex, int cellX)
        {
            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                StructureEntry entry = entries[i];
                if (entry.CarIndex != carIndex)
                {
                    continue;
                }

                RotatedFootprint(entry.FootprintWidth, entry.FootprintLength, entry.Rotation,
                    out int existingWidth, out _);
                if (cellX >= entry.CellX && cellX < entry.CellX + existingWidth)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>회전을 반영한 점유 면적 — 홀수 회전(90°·270°)이면 가로·세로가 스왑된다.</summary>
        public static void RotatedFootprint(int width, int length, int rotation, out int rotatedWidth, out int rotatedLength)
        {
            bool swapped = (rotation & 1) == 1;
            rotatedWidth = swapped ? length : width;
            rotatedLength = swapped ? width : length;
        }

        /// <summary>
        /// 그 셀을 실제로 <b>막는가</b> (천막 계획 결정 ⑥) — 발자국 사각형 안이라도 점유 모양이
        /// <see cref="StructureOccupancy.Corners"/>면 네 모서리 셀에서만 true다.
        /// 발자국(덮는 범위)과 점유(막는 범위)를 가르는 유일한 지점이라, 설치 판정·그늘·비용이
        /// 서로 다른 범위를 쓰면서도 한 함수를 근거로 삼는다.
        /// </summary>
        public static bool OccupiesCell(int originX, int originZ, int rotatedWidth, int rotatedLength,
            StructureOccupancy occupancy, int cellX, int cellZ)
        {
            if (cellX < originX || cellX >= originX + rotatedWidth
                || cellZ < originZ || cellZ >= originZ + rotatedLength)
            {
                return false;
            }

            if (occupancy != StructureOccupancy.Corners)
            {
                return true;
            }

            // 네 모서리 = 첫/끝 열이면서 동시에 첫/끝 행. 2x2면 네 셀이 전부 모서리라 Solid와 같아진다.
            bool edgeX = cellX == originX || cellX == originX + rotatedWidth - 1;
            bool edgeZ = cellZ == originZ || cellZ == originZ + rotatedLength - 1;
            return edgeX && edgeZ;
        }

        /// <summary>항목이 그 셀을 막는지 — <see cref="OccupiesCell"/>에 항목의 회전·원점을 풀어 넘긴다.</summary>
        public static bool EntryOccupiesCell(StructureEntry entry, StructureOccupancy occupancy, int cellX, int cellZ)
        {
            RotatedFootprint(entry.FootprintWidth, entry.FootprintLength, entry.Rotation,
                out int rotatedWidth, out int rotatedLength);
            return OccupiesCell(entry.CellX, entry.CellZ, rotatedWidth, rotatedLength, occupancy, cellX, cellZ);
        }

        /// <summary>
        /// 발자국 사각형 안인지 — 점유 모양과 무관한 <b>덮는 범위</b> 판정 (천막 계획 결정 ③의 그늘 축).
        /// 천막은 기둥만 막지만 그늘은 천 아래 전체에 든다.
        /// </summary>
        public static bool EntryCoversCell(StructureEntry entry, int cellX, int cellZ)
        {
            RotatedFootprint(entry.FootprintWidth, entry.FootprintLength, entry.Rotation,
                out int rotatedWidth, out int rotatedLength);
            return cellX >= entry.CellX && cellX < entry.CellX + rotatedWidth
                && cellZ >= entry.CellZ && cellZ < entry.CellZ + rotatedLength;
        }

        /// <summary>
        /// 월드 좌표(조준 hit 지점)를 점유 영역 좌하단 셀로 스냅한다 — 점유 면적이 커서에 <b>중심 정렬</b>되고,
        /// 유효 열(칸 본체 + 그 칸의 판자 열) 안으로 클램프된다. 열차는 X=0 고정 주행이므로 X는 월드
        /// 그대로, Z만 칸 중심 보정. 점유가 그리드보다 크면 false.
        /// </summary>
        public static bool TryWorldToPlacementCell(
            float worldX, float worldZ, float carCenterZ,
            float carWidth, float carLength, float cellSize,
            int rotatedWidth, int rotatedLength,
            int leftPlanks, int rightPlanks,
            out int cellX, out int cellZ)
        {
            cellX = 0;
            cellZ = 0;

            int bodyColumns = BodyColumns(carWidth, cellSize);
            int rows = Rows(carLength, cellSize);
            int firstColumn = FirstColumn(leftPlanks);
            int validColumns = ValidColumns(bodyColumns, leftPlanks, rightPlanks);
            if (cellSize <= 0f || rotatedWidth <= 0 || rotatedLength <= 0
                || rotatedWidth > validColumns || rotatedLength > rows)
            {
                return false;
            }

            // 연속 좌표(열·행 단위) — 본체 좌측 끝이 열 FirstBodyColumn의 왼쪽 변, 행 스팬은 칸 중심 정렬.
            float bodyHalf = bodyColumns * cellSize * 0.5f;
            float rowSpanHalf = rows * cellSize * 0.5f;
            float columnF = (worldX + bodyHalf) / cellSize + FirstBodyColumn;
            float rowF = (worldZ - (carCenterZ - rowSpanHalf)) / cellSize;

            cellX = Mathf.Clamp(Mathf.RoundToInt(columnF - rotatedWidth * 0.5f),
                firstColumn, firstColumn + validColumns - rotatedWidth);
            cellZ = Mathf.Clamp(Mathf.RoundToInt(rowF - rotatedLength * 0.5f), 0, rows - rotatedLength);
            return true;
        }

        /// <summary>점유 영역 중심의 월드 X·Z — 뷰 스폰·프리뷰·거리 검증이 같은 지점을 쓴다. Z는 이탈 오프셋 반영 칸 중심 기준.</summary>
        public static void CellRegionCenterWorld(
            int cellX, int cellZ, int rotatedWidth, int rotatedLength,
            float carCenterZ, float carWidth, float carLength, float cellSize,
            out float worldX, out float worldZ)
        {
            int bodyColumns = BodyColumns(carWidth, cellSize);
            int rows = Rows(carLength, cellSize);
            float rowSpanHalf = rows * cellSize * 0.5f;

            // 좌하단 셀의 중심에서 점유 폭·길이의 절반만큼 안쪽으로 — 판자 열(본체 밖 음/양 열)도 같은 식이다.
            worldX = ColumnCenterWorldX(cellX, bodyColumns, cellSize) + (rotatedWidth - 1) * cellSize * 0.5f;
            worldZ = carCenterZ - rowSpanHalf + cellZ * cellSize + rotatedLength * cellSize * 0.5f;
        }

        /// <summary>
        /// 항목 하나의 점유 영역 중심 월드 X·Z — 회전 반영 점유를 풀어 <see cref="CellRegionCenterWorld"/>에
        /// 넘기는 두 단계를 하나로 묶는다. 프리뷰·뷰 스폰·사거리 검증·보따리 배출·창고 접근·제작 조회가
        /// <b>전부 이 함수 하나</b>를 거치므로 "같은 지점"이 규약이 아니라 구조로 보장된다.
        /// </summary>
        public static void EntryCenterWorld(StructureEntry entry,
            float carCenterZ, float carWidth, float carLength, float cellSize,
            out float worldX, out float worldZ)
        {
            RotatedFootprint(entry.FootprintWidth, entry.FootprintLength, entry.Rotation,
                out int rotatedWidth, out int rotatedLength);
            CellRegionCenterWorld(entry.CellX, entry.CellZ, rotatedWidth, rotatedLength,
                carCenterZ, carWidth, carLength, cellSize, out worldX, out worldZ);
        }

        /// <summary>
        /// 점유 영역이 유효 열·행 안에 온전히 들어가는지 — 유효 열 = 칸 본체 + 그 칸의 판자 열
        /// (건축 개편 3차). 판자가 없는 칸은 본체 4열만 유효하다.
        /// </summary>
        private static bool IsWithinColumns(int cellX, int cellZ, int rotatedWidth, int rotatedLength,
            int bodyColumns, int rows, int leftPlanks, int rightPlanks)
        {
            int firstColumn = FirstColumn(leftPlanks);
            return cellX >= firstColumn
                && cellX + rotatedWidth <= firstColumn + ValidColumns(bodyColumns, leftPlanks, rightPlanks)
                && cellZ >= 0
                && cellZ + rotatedLength <= rows;
        }

        /// <summary>
        /// 같은 칸 위 기존 항목들과 <b>실제로 막는 셀</b>이 겹치는지.
        /// 점유가 전부 <see cref="StructureOccupancy.Solid"/>이면 사각형 교차 한 번으로 끝나
        /// 기존 8종의 판정 비용이 그대로다. 한쪽이라도 <see cref="StructureOccupancy.Corners"/>면
        /// 겹치는 사각형 안에서만 셀 단위로 확인한다 — 천막 기둥 넷과 그 안쪽을 가르는 지점이다.
        /// <paramref name="occupancies"/>가 null이면 전부 Solid로 본다(기존 호출 경로).
        /// </summary>
        private static bool OverlapsExisting(StructureEntry[] entries, StructureOccupancy[] occupancies,
            int carIndex, int cellX, int cellZ, int rotatedWidth, int rotatedLength,
            StructureOccupancy occupancy)
        {
            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                StructureEntry entry = entries[i];
                if (entry.CarIndex != carIndex)
                {
                    continue;
                }

                RotatedFootprint(entry.FootprintWidth, entry.FootprintLength, entry.Rotation,
                    out int existingWidth, out int existingLength);
                bool intersects = cellX < entry.CellX + existingWidth && entry.CellX < cellX + rotatedWidth
                    && cellZ < entry.CellZ + existingLength && entry.CellZ < cellZ + rotatedLength;
                if (!intersects)
                {
                    continue;
                }

                StructureOccupancy existing = occupancies != null && i < occupancies.Length
                    ? occupancies[i]
                    : StructureOccupancy.Solid;

                // 같은 모양끼리는 사각형 교차만으로 충돌이다.
                //  · Solid ↔ Solid — 기존 8종의 규약 그대로
                //  · Corners ↔ Corners — <b>천막 안에 천막을 세우지 않는다</b>. 기둥이 안 겹치면
                //    세워지긴 하지만 천이 두 겹으로 포개져 그늘은 그대로고 자원만 나간다.
                //    "덮되 막지 않는다"는 지붕과 지붕 사이에는 성립하지 않는다.
                if (occupancy == existing)
                {
                    return true;
                }

                if (AnyOccupiedCellShared(entry, existing,
                    cellX, cellZ, rotatedWidth, rotatedLength, occupancy,
                    Mathf.Max(cellX, entry.CellX), Mathf.Max(cellZ, entry.CellZ),
                    Mathf.Min(cellX + rotatedWidth, entry.CellX + existingWidth),
                    Mathf.Min(cellZ + rotatedLength, entry.CellZ + existingLength)))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 겹치는 사각형 안에서 <b>둘 다 막는 셀</b>이 하나라도 있는지. 사각형 교차가 이미 성립한
        /// 구간만 훑으므로 최대 그리드 한 칸(6x13) 크기를 넘지 않는다.
        /// </summary>
        private static bool AnyOccupiedCellShared(StructureEntry entry, StructureOccupancy existing,
            int cellX, int cellZ, int rotatedWidth, int rotatedLength, StructureOccupancy occupancy,
            int minX, int minZ, int maxX, int maxZ)
        {
            for (int x = minX; x < maxX; x++)
            {
                for (int z = minZ; z < maxZ; z++)
                {
                    if (OccupiesCell(cellX, cellZ, rotatedWidth, rotatedLength, occupancy, x, z)
                        && EntryOccupiesCell(entry, existing, x, z))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// 이 자리에 건축물을 설치할 수 있는지 — 칸 생존(기관차 제외) + 설치 가능 플래그 +
        /// 점유 셀 전부 그리드 내부·비점유. 소유자 프리뷰와 호스트 확정이 같은 판정을 쓴다.
        /// 창고 다중 설치 허용 (건축 개편 2차 — 결정 ⑦: 저장 블록이 건축물 Id 기반이라 칸당 제한이 없다).
        /// </summary>
        public static bool CanPlace(StructureEntry[] entries, CarState[] cars, int carIndex,
            int cellX, int cellZ, int rotation, StructureKind kind,
            int footprintWidth, int footprintLength, bool placeable,
            float carWidth, float carLength, float cellSize)
        {
            return CanPlace(entries, null, cars, carIndex, cellX, cellZ, rotation, kind,
                footprintWidth, footprintLength, placeable, StructureOccupancy.Solid,
                carWidth, carLength, cellSize);
        }

        /// <summary>
        /// 점유 모양을 반영한 설치 판정 (천막 계획 결정 ⑥). <paramref name="occupancies"/>는
        /// <paramref name="entries"/>와 같은 순서의 기존 항목 점유 모양이고, null이면 전부
        /// <see cref="StructureOccupancy.Solid"/>로 본다 — 위 오버로드(기존 8종 경로)가 그 경우다.
        /// 순수 함수를 유지하려고 카탈로그 조회를 호출부가 미리 풀어 넘긴다.
        /// </summary>
        public static bool CanPlace(StructureEntry[] entries, StructureOccupancy[] occupancies,
            CarState[] cars, int carIndex,
            int cellX, int cellZ, int rotation, StructureKind kind,
            int footprintWidth, int footprintLength, bool placeable, StructureOccupancy occupancy,
            float carWidth, float carLength, float cellSize)
        {
            if (entries == null || cars == null || carIndex < 0 || carIndex >= cars.Length
                || !placeable || footprintWidth <= 0 || footprintLength <= 0 || cellSize <= 0f)
            {
                return false;
            }

            CarState car = cars[carIndex];
            if (!TrainStateLogic.IsCarPresent(car) || !TrainStateLogic.IsDestructible(car.Type))
            {
                return false;
            }

            RotatedFootprint(footprintWidth, footprintLength, rotation, out int rotatedWidth, out int rotatedLength);
            int bodyColumns = BodyColumns(carWidth, cellSize);
            int rows = Rows(carLength, cellSize);

            return IsWithinColumns(cellX, cellZ, rotatedWidth, rotatedLength, bodyColumns, rows,
                    car.LeftPlanks, car.RightPlanks)
                && !OverlapsExisting(entries, occupancies, carIndex, cellX, cellZ,
                    rotatedWidth, rotatedLength, occupancy);
        }

        /// <summary>항목이 살아 있는지 — 리스트 존재 = 설치이므로 방어적 체력 검사만 남는다.</summary>
        public static bool IsAlive(StructureEntry entry)
        {
            return entry.Id != 0 && entry.Health > 0f;
        }

        /// <summary>Id로 항목을 찾는다 — 철거·피해·수리 RPC의 안정 참조 해석. 없으면 false.</summary>
        public static bool TryFindById(StructureEntry[] entries, int id, out int index)
        {
            if (entries != null && id > 0)
            {
                for (int i = 0; i < entries.Length; i++)
                {
                    if (entries[i].Id == id)
                    {
                        index = i;
                        return true;
                    }
                }
            }

            index = -1;
            return false;
        }

        /// <summary>
        /// 항목을 철거할 수 있는지 (건축 개편 2차 — 결정 ④) — 살아 있고 칸이 편성에 살아 붙어
        /// 있어야 한다 (이탈 칸 위 철거 불가 — 피해 규칙과 같은 게이트).
        /// </summary>
        public static bool CanDemolish(StructureEntry[] entries, CarState[] cars, int entryIndex)
        {
            return entries != null && cars != null
                && entryIndex >= 0 && entryIndex < entries.Length
                && IsAlive(entries[entryIndex])
                && entries[entryIndex].CarIndex < cars.Length
                && TrainStateLogic.IsCarPresent(cars[entries[entryIndex].CarIndex]);
        }

        /// <summary>철거 반환량 (결정 ⑤) — floor(건설 비용 × 반환 비율). 예: 비용 3 → 1, 4 → 2, 7 → 3 (비율 0.5).</summary>
        public static int RefundAmount(int buildCost, float refundRatio)
        {
            return buildCost > 0 && refundRatio > 0f
                ? Mathf.FloorToInt(buildCost * Mathf.Clamp01(refundRatio))
                : 0;
        }

        /// <summary>
        /// 항목 하나에 데미지를 적용한다 — 칸이 편성에 살아 붙어 있을 때만 유효하다
        /// (이탈·파괴된 칸 위의 건축물은 칸과 운명을 같이하므로 별도 표적이 아니다 — 구 규칙 이관).
        /// Destroyed면 호출부(호스트)가 항목을 리스트에서 제거한다.
        /// </summary>
        public static CarDamageResult ApplyDamage(StructureEntry[] entries, CarState[] cars, int entryIndex, float amount)
        {
            if (entries == null || cars == null || amount <= 0f
                || entryIndex < 0 || entryIndex >= entries.Length)
            {
                return CarDamageResult.Ignored;
            }

            StructureEntry entry = entries[entryIndex];
            if (!IsAlive(entry) || entry.CarIndex >= cars.Length || !TrainStateLogic.IsCarPresent(cars[entry.CarIndex]))
            {
                return CarDamageResult.Ignored;
            }

            entry.Health = Mathf.Max(0f, entry.Health - amount);
            entries[entryIndex] = entry;

            return entry.Health <= 0f ? CarDamageResult.Destroyed : CarDamageResult.Damaged;
        }

        /// <summary>항목을 수리한다 — 칸이 살아 붙어 있고 만피가 아닐 때만 (구 RepairStructure 이관).</summary>
        public static bool Repair(StructureEntry[] entries, CarState[] cars, int entryIndex, float amount)
        {
            if (entries == null || cars == null || amount <= 0f
                || entryIndex < 0 || entryIndex >= entries.Length)
            {
                return false;
            }

            StructureEntry entry = entries[entryIndex];
            if (!IsAlive(entry) || entry.CarIndex >= cars.Length || !TrainStateLogic.IsCarPresent(cars[entry.CarIndex])
                || entry.Health >= entry.MaxHealth)
            {
                return false;
            }

            entry.Health = Mathf.Min(entry.MaxHealth, entry.Health + amount);
            entries[entryIndex] = entry;
            return true;
        }

        /// <summary>
        /// 월드 X·Z 지점이 칸 위 항목의 점유 영역(여유 <paramref name="padding"/> 포함) 안인지 —
        /// 몬스터 관통 방지 최소 구현(계획서 §2.10)의 이동 차단 판정. 물리 쿼리가 아니라 그리드
        /// 점유 조회라 서버 시뮬레이션 비용만 든다. 칸 생존 여부는 호출부가 거른다.
        /// </summary>
        public static bool IsWorldPointOnEntry(StructureEntry entry,
            float worldX, float worldZ, float padding,
            float carCenterZ, float carWidth, float carLength, float cellSize)
        {
            RotatedFootprint(entry.FootprintWidth, entry.FootprintLength, entry.Rotation,
                out int rotatedWidth, out int rotatedLength);
            EntryCenterWorld(entry, carCenterZ, carWidth, carLength, cellSize,
                out float centerX, out float centerZ);

            float halfWidth = rotatedWidth * cellSize * 0.5f + padding;
            float halfLength = rotatedLength * cellSize * 0.5f + padding;
            return Mathf.Abs(worldX - centerX) <= halfWidth && Mathf.Abs(worldZ - centerZ) <= halfLength;
        }
    }
}
