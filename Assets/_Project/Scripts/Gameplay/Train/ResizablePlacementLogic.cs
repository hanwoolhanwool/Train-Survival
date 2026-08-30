using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 크기를 끌어서 정하는 건축물(천막)의 순수 배치 계산 (천막 계획 §4.2·§4.3·§4.5).
    /// <see cref="StructureGridLogic"/>과 같은 결의 static 클래스로, 씬·물리 없이 EditMode로 검증한다.
    ///
    /// <b>칸을 넘는 한 채는 없다</b> — <see cref="StructureEntry.CarIndex"/>가 byte 하나라
    /// 편성을 가로지르는 항목은 그릇에 담기지 않는다. 그래서 드래그 사각형을 칸별로 잘라
    /// <b>칸마다 한 채</b>를 만든다 (결정 ④). 화면에서는 이어진 차양으로 보이고,
    /// 칸이 이탈·파괴되면 그 칸 몫만 사라진다.
    /// </summary>
    public static class ResizablePlacementLogic
    {
        /// <summary>기둥 넷이 성립하는 최소 변 길이 — 1이면 모서리가 둘로 줄어 "기둥 4개" 형태가 깨진다.</summary>
        public const int MinSide = 2;

        /// <summary>드래그를 칸별로 자른 조각 하나 — 그대로 항목 한 채가 된다.</summary>
        public struct Span
        {
            public int CarIndex;
            public int CellX;
            public int CellZ;
            public int Width;
            public int Length;

            public int CellCount => Width * Length;
        }

        /// <summary>
        /// 드래그(앵커 셀 → 커서 셀)를 칸별 조각으로 자른다. 두 지점은 각자의 칸 안 좌표이며,
        /// 칸 인덱스 사이의 칸은 전부 덮인다. 열 범위는 칸과 무관하게 같고, 행 범위만 칸에서 잘린다.
        ///
        /// <paramref name="rowsPerCar"/>는 갑판 행 수(<see cref="StructureGridLogic.Rows"/>).
        /// 결과가 비면 설치할 것이 없다는 뜻이다(전부 <see cref="MinSide"/> 미만).
        /// </summary>
        public static void ResolveSpans(
            int anchorCar, int anchorX, int anchorZ,
            int cursorCar, int cursorX, int cursorZ,
            int rowsPerCar, List<Span> results)
        {
            if (results == null)
            {
                return;
            }

            results.Clear();
            if (rowsPerCar <= 0)
            {
                return;
            }

            int firstCar = Mathf.Min(anchorCar, cursorCar);
            int lastCar = Mathf.Max(anchorCar, cursorCar);
            int minX = Mathf.Min(anchorX, cursorX);
            int width = Mathf.Abs(cursorX - anchorX) + 1;

            // 폭이 최소에 못 미치면 커서 반대쪽으로 넓힌다 — 클램프는 호출부가 유효 열로 한 번 더 건다.
            if (width < MinSide)
            {
                width = MinSide;
            }

            for (int car = firstCar; car <= lastCar; car++)
            {
                ResolveRowRange(car, firstCar, lastCar, anchorCar, anchorZ, cursorCar, cursorZ,
                    rowsPerCar, out int startZ, out int endZ);

                int length = endZ - startZ + 1;
                if (length < MinSide)
                {
                    if (firstCar != lastCar || rowsPerCar < MinSide)
                    {
                        // 칸 경계에 한 행만 걸친 잔여물 — 기둥 넷이 안 서므로 그 칸은 건너뛴다.
                        // (한 칸 안 드래그와 달리 사용자가 의도한 크기가 아니다.)
                        continue;
                    }

                    // 한 칸 안에서 짧게 끈 경우는 버리지 않고 최소 크기로 넓힌다 — 폭과 같은 규칙이다.
                    length = MinSide;
                    startZ = Mathf.Min(startZ, rowsPerCar - length);
                }

                results.Add(new Span
                {
                    CarIndex = car,
                    CellX = minX,
                    CellZ = startZ,
                    Width = width,
                    Length = length,
                });
            }
        }

        /// <summary>
        /// 그 칸이 덮이는 행 범위 — 시작 칸은 앵커 행부터, 끝 칸은 커서 행까지, 사이 칸은 전체다.
        /// 드래그 방향(앞→뒤 / 뒤→앞)에 따라 시작·끝이 뒤집힌다.
        /// </summary>
        private static void ResolveRowRange(int car, int firstCar, int lastCar,
            int anchorCar, int anchorZ, int cursorCar, int cursorZ,
            int rowsPerCar, out int startZ, out int endZ)
        {
            startZ = 0;
            endZ = rowsPerCar - 1;

            if (firstCar == lastCar)
            {
                // 한 칸 안 드래그 — 두 행 사이 전부.
                startZ = Mathf.Min(anchorZ, cursorZ);
                endZ = Mathf.Max(anchorZ, cursorZ);
                return;
            }

            bool anchorIsFirst = anchorCar == firstCar;
            if (car == firstCar)
            {
                startZ = anchorIsFirst ? anchorZ : cursorZ;
            }
            else if (car == lastCar)
            {
                endZ = anchorIsFirst ? cursorZ : anchorZ;
            }
        }

        /// <summary>
        /// 조각 전체의 셀 수 — 비용은 <b>칸별이 아니라 한 번에</b> 계산해야 한다(결정 ④).
        /// 칸마다 올림하면 같은 넓이라도 쪼개질수록 비싸진다.
        /// </summary>
        public static int TotalCells(List<Span> spans)
        {
            if (spans == null)
            {
                return 0;
            }

            int total = 0;
            for (int i = 0; i < spans.Count; i++)
            {
                total += spans[i].CellCount;
            }

            return total;
        }

        /// <summary>
        /// 가변 크기 건축 비용 (결정 ⑤) — <c>ceil(셀 수 × 셀당 비용)</c>.
        /// <paramref name="costPerCell"/>이 0이면 크기와 무관한 고정 비용(<paramref name="flatCost"/>)이라
        /// 기존 종류가 이 경로를 타도 값이 바뀌지 않는다.
        /// </summary>
        public static int ResolveCost(int totalCells, float costPerCell, int flatCost)
        {
            if (costPerCell <= 0f)
            {
                return flatCost;
            }

            return Mathf.Max(1, Mathf.CeilToInt(totalCells * costPerCell));
        }

        /// <summary>
        /// 폭을 그 칸의 유효 열 범위 안으로 자른다 — 유효 열은 칸마다 다르다(판자 증축).
        /// 시작 열도 함께 밀어 **넓이를 지키되 격자를 넘지 않게** 한다.
        /// </summary>
        public static void ClampToColumns(ref int cellX, ref int width, int firstColumn, int validColumns)
        {
            if (validColumns < MinSide)
            {
                width = 0;
                return;
            }

            width = Mathf.Clamp(width, MinSide, validColumns);
            cellX = Mathf.Clamp(cellX, firstColumn, firstColumn + validColumns - width);
        }
    }
}
