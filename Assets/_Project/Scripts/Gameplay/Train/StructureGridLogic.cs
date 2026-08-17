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

        /// <summary>회전을 반영한 점유 면적 — 홀수 회전(90°·270°)이면 가로·세로가 스왑된다.</summary>
        public static void RotatedFootprint(int width, int length, int rotation, out int rotatedWidth, out int rotatedLength)
        {
            bool swapped = (rotation & 1) == 1;
            rotatedWidth = swapped ? length : width;
            rotatedLength = swapped ? width : length;
        }

        /// <summary>
        /// 월드 좌표(조준 hit 지점)를 점유 영역 좌하단 셀로 스냅한다 — 점유 면적이 커서에 <b>중심 정렬</b>되고,
        /// 본체 그리드 안으로 클램프된다. 열차는 X=0 고정 주행이므로 X는 월드 그대로, Z만 칸 중심 보정.
        /// 점유가 그리드보다 크면 false.
        /// </summary>
        public static bool TryWorldToPlacementCell(
            float worldX, float worldZ, float carCenterZ,
            float carWidth, float carLength, float cellSize,
            int rotatedWidth, int rotatedLength,
            out int cellX, out int cellZ)
        {
            cellX = 0;
            cellZ = 0;

            int bodyColumns = BodyColumns(carWidth, cellSize);
            int rows = Rows(carLength, cellSize);
            if (cellSize <= 0f || rotatedWidth <= 0 || rotatedLength <= 0
                || rotatedWidth > bodyColumns || rotatedLength > rows)
            {
                return false;
            }

            // 연속 좌표(열·행 단위) — 본체 좌측 끝이 열 FirstBodyColumn의 왼쪽 변, 행 스팬은 칸 중심 정렬.
            float bodyHalf = bodyColumns * cellSize * 0.5f;
            float rowSpanHalf = rows * cellSize * 0.5f;
            float columnF = (worldX + bodyHalf) / cellSize + FirstBodyColumn;
            float rowF = (worldZ - (carCenterZ - rowSpanHalf)) / cellSize;

            cellX = Mathf.Clamp(Mathf.RoundToInt(columnF - rotatedWidth * 0.5f),
                FirstBodyColumn, FirstBodyColumn + bodyColumns - rotatedWidth);
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
            float bodyHalf = bodyColumns * cellSize * 0.5f;
            float rowSpanHalf = rows * cellSize * 0.5f;

            worldX = -bodyHalf + (cellX - FirstBodyColumn) * cellSize + rotatedWidth * cellSize * 0.5f;
            worldZ = carCenterZ - rowSpanHalf + cellZ * cellSize + rotatedLength * cellSize * 0.5f;
        }

        /// <summary>
        /// 점유 영역이 유효 열·행 안에 온전히 들어가는지 — 1차에서는 유효 열 = 칸 본체뿐이다
        /// (판자 확장 열은 3차에서 칸별 판자 상태가 이 판정에 들어온다).
        /// </summary>
        public static bool IsWithinBody(int cellX, int cellZ, int rotatedWidth, int rotatedLength,
            int bodyColumns, int rows)
        {
            return cellX >= FirstBodyColumn
                && cellX + rotatedWidth <= FirstBodyColumn + bodyColumns
                && cellZ >= 0
                && cellZ + rotatedLength <= rows;
        }

        /// <summary>같은 칸 위 기존 항목들과 점유 셀이 교차하는지 — 셀 사각형 교차 판정.</summary>
        public static bool OverlapsExisting(StructureEntry[] entries, int carIndex,
            int cellX, int cellZ, int rotatedWidth, int rotatedLength)
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
                if (intersects)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 이 자리에 건축물을 설치할 수 있는지 — 칸 생존(기관차 제외) + 설치 가능 플래그 +
        /// 점유 셀 전부 그리드 내부·비점유. 소유자 프리뷰와 호스트 확정이 같은 판정을 쓴다.
        /// 창고는 저장 모델 개편(2차) 전까지 칸당 1개로 제한한다 — 2차에서 이 가드만 걷어낸다 (계획서 §3 1차).
        /// </summary>
        public static bool CanPlace(StructureEntry[] entries, CarState[] cars, int carIndex,
            int cellX, int cellZ, int rotation, StructureKind kind,
            int footprintWidth, int footprintLength, bool placeable,
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

            // 창고 임시 가드 (1차) — TrainStorage의 "저장 블록 = 칸 인덱스" 규약이 유지되는 동안만.
            if (kind == StructureKind.Storage && HasKindOnCar(entries, carIndex, StructureKind.Storage))
            {
                return false;
            }

            RotatedFootprint(footprintWidth, footprintLength, rotation, out int rotatedWidth, out int rotatedLength);
            int bodyColumns = BodyColumns(carWidth, cellSize);
            int rows = Rows(carLength, cellSize);

            return IsWithinBody(cellX, cellZ, rotatedWidth, rotatedLength, bodyColumns, rows)
                && !OverlapsExisting(entries, carIndex, cellX, cellZ, rotatedWidth, rotatedLength);
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

        /// <summary>칸 위에 해당 종류 항목이 있는지 — 1차 창고 가드·종류별 기능 조회용.</summary>
        public static bool HasKindOnCar(StructureEntry[] entries, int carIndex, StructureKind kind)
        {
            if (entries == null)
            {
                return false;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                if (entries[i].CarIndex == carIndex && entries[i].Kind == kind && IsAlive(entries[i]))
                {
                    return true;
                }
            }

            return false;
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
            CellRegionCenterWorld(entry.CellX, entry.CellZ, rotatedWidth, rotatedLength,
                carCenterZ, carWidth, carLength, cellSize, out float centerX, out float centerZ);

            float halfWidth = rotatedWidth * cellSize * 0.5f + padding;
            float halfLength = rotatedLength * cellSize * 0.5f + padding;
            return Mathf.Abs(worldX - centerX) <= halfWidth && Mathf.Abs(worldZ - centerZ) <= halfLength;
        }
    }
}
