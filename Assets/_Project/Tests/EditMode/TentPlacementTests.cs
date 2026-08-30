using System.Collections.Generic;
using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 천막(가변 크기 그늘 건축물) 검증 — 천막 계획 1차 완료 기준을 못 박는다.
    /// 핵심은 <b>세 범위가 다르다</b>는 것: 점유는 기둥 넷, 그늘은 발자국 전체, 비용도 발자국 전체.
    /// 규격은 기존 <see cref="StructureGridLogicTests"/>와 같다 — 칸 4.6 × 15 m · 셀 1 m → 본체 4열.
    /// </summary>
    public sealed class TentPlacementTests
    {
        private const float CarWidth = 4.6f;
        private const float CarLength = 15f;
        private const float CellSize = 1f;

        // 고정 예약 좌표계 — 열 0~1이 좌측 판자 자리라 칸 본체는 열 2에서 시작한다.
        private const int FirstBody = StructureGridLogic.FirstBodyColumn;

        private static float MaxHealthFor(CarType type)
        {
            return type == CarType.Locomotive ? float.PositiveInfinity : 100f;
        }

        private static CarState[] BuildTrain(int carCount)
        {
            var order = new CarType[carCount];
            for (int i = 0; i < carCount; i++)
            {
                order[i] = i == 0 ? CarType.Locomotive : CarType.Standard;
            }

            return TrainStateLogic.BuildInitialCars(order, MaxHealthFor);
        }

        private static StructureEntry MakeEntry(int id, int carIndex, int cellX, int cellZ,
            StructureKind kind, int width, int length)
        {
            return new StructureEntry
            {
                Id = (ushort)id,
                CarIndex = (byte)carIndex,
                CellX = (byte)cellX,
                CellZ = (byte)cellZ,
                Rotation = 0,
                Kind = kind,
                FootprintWidth = (byte)width,
                FootprintLength = (byte)length,
                Health = 30f,
                MaxHealth = 30f,
            };
        }

        private static bool CanPlaceTent(StructureEntry[] entries, StructureOccupancy[] shapes,
            CarState[] cars, int carIndex, int cellX, int cellZ, int width, int length)
        {
            return StructureGridLogic.CanPlace(entries, shapes, cars, carIndex, cellX, cellZ, 0,
                StructureKind.Tent, width, length, true, StructureOccupancy.Corners,
                CarWidth, CarLength, CellSize);
        }

        // ── 점유는 네 기둥뿐 (결정 ⑥) ──────────────────

        [Test]
        public void Corners_3x3_점유는_모서리_넷뿐이다()
        {
            // 3×3의 아홉 셀 중 막는 것은 네 모서리뿐 — 나머지 다섯은 빈 자리다.
            int occupied = 0;
            for (int x = 0; x < 3; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    if (StructureGridLogic.OccupiesCell(0, 0, 3, 3, StructureOccupancy.Corners, x, z))
                    {
                        occupied++;
                    }
                }
            }

            Assert.AreEqual(4, occupied);
            Assert.IsTrue(StructureGridLogic.OccupiesCell(0, 0, 3, 3, StructureOccupancy.Corners, 0, 0));
            Assert.IsTrue(StructureGridLogic.OccupiesCell(0, 0, 3, 3, StructureOccupancy.Corners, 2, 2));
            Assert.IsFalse(StructureGridLogic.OccupiesCell(0, 0, 3, 3, StructureOccupancy.Corners, 1, 1),
                "한가운데는 비어 있어야 다른 건축물이 들어간다");
            Assert.IsFalse(StructureGridLogic.OccupiesCell(0, 0, 3, 3, StructureOccupancy.Corners, 1, 0),
                "변의 한가운데도 기둥이 아니다");
        }

        [Test]
        public void Corners_2x2는_Solid와_같다()
        {
            // 네 셀이 곧 네 모서리 — 최소 크기를 2로 강제하는 근거다.
            for (int x = 0; x < 2; x++)
            {
                for (int z = 0; z < 2; z++)
                {
                    Assert.IsTrue(StructureGridLogic.OccupiesCell(0, 0, 2, 2, StructureOccupancy.Corners, x, z));
                }
            }
        }

        [Test]
        public void Solid는_발자국_전체를_막는다()
        {
            // 기존 8종의 규약 — 이 테스트가 깨지면 천막 작업이 기존 건축물을 건드린 것이다.
            for (int x = 0; x < 3; x++)
            {
                for (int z = 0; z < 3; z++)
                {
                    Assert.IsTrue(StructureGridLogic.OccupiesCell(0, 0, 3, 3, StructureOccupancy.Solid, x, z));
                }
            }
        }

        // ── 천막 안에 다른 건축물이 산다 (결정 ⑥) ──────────────────

        [Test]
        public void 천막_안쪽에_난방기를_지을_수_있다()
        {
            CarState[] cars = BuildTrain(2);
            var entries = new[] { MakeEntry(1, 1, FirstBody, 0, StructureKind.Tent, 3, 3) };
            var shapes = new[] { StructureOccupancy.Corners };

            // 천막 한가운데(기둥이 아닌 자리)에 1×1 난방기.
            bool canPlace = StructureGridLogic.CanPlace(entries, shapes, cars, 1,
                FirstBody + 1, 1, 0, StructureKind.Heater, 1, 1, true, StructureOccupancy.Solid,
                CarWidth, CarLength, CellSize);

            Assert.IsTrue(canPlace, "천막은 지붕이라 안쪽이 비어 있어야 한다");
        }

        [Test]
        public void 천막_기둥_자리에는_지을_수_없다()
        {
            CarState[] cars = BuildTrain(2);
            var entries = new[] { MakeEntry(1, 1, FirstBody, 0, StructureKind.Tent, 3, 3) };
            var shapes = new[] { StructureOccupancy.Corners };

            bool canPlace = StructureGridLogic.CanPlace(entries, shapes, cars, 1,
                FirstBody, 0, 0, StructureKind.Heater, 1, 1, true, StructureOccupancy.Solid,
                CarWidth, CarLength, CellSize);

            Assert.IsFalse(canPlace, "기둥이 선 셀은 막혀 있어야 한다");
        }

        [Test]
        public void 이미_선_건축물_위로_천막을_덮을_수_있다()
        {
            CarState[] cars = BuildTrain(2);
            // 한가운데 난방기 1×1이 이미 서 있다.
            var entries = new[] { MakeEntry(1, 1, FirstBody + 1, 1, StructureKind.Heater, 1, 1) };
            var shapes = new[] { StructureOccupancy.Solid };

            bool canPlace = CanPlaceTent(entries, shapes, cars, 1, FirstBody, 0, 3, 3);

            Assert.IsTrue(canPlace, "기둥 넷이 기존 건축물과 겹치지 않으면 덮을 수 있어야 한다");
        }

        [Test]
        public void 기존_건축물이_기둥_자리에_있으면_천막을_못_세운다()
        {
            CarState[] cars = BuildTrain(2);
            var entries = new[] { MakeEntry(1, 1, FirstBody, 0, StructureKind.Heater, 1, 1) };
            var shapes = new[] { StructureOccupancy.Solid };

            bool canPlace = CanPlaceTent(entries, shapes, cars, 1, FirstBody, 0, 3, 3);

            Assert.IsFalse(canPlace);
        }

        [Test]
        public void 점유_배열이_없으면_전부_Solid로_본다()
        {
            // 기존 호출 경로(오버로드)가 이 경우다 — 천막 작업 전과 판정이 같아야 한다.
            CarState[] cars = BuildTrain(2);
            var entries = new[] { MakeEntry(1, 1, FirstBody, 0, StructureKind.Tent, 3, 3) };

            bool canPlace = StructureGridLogic.CanPlace(entries, cars, 1,
                FirstBody + 1, 1, 0, StructureKind.Heater, 1, 1, true,
                CarWidth, CarLength, CellSize);

            Assert.IsFalse(canPlace, "옛 오버로드는 발자국 전체를 점유로 봐야 한다");
        }

        // ── 그늘은 발자국 전체 (결정 ③) ──────────────────

        [Test]
        public void 그늘은_기둥이_아닌_안쪽에도_든다()
        {
            StructureEntry tent = MakeEntry(1, 1, FirstBody, 0, StructureKind.Tent, 3, 3);

            Assert.IsTrue(StructureGridLogic.EntryCoversCell(tent, FirstBody + 1, 1),
                "천 아래 한가운데가 가장 그늘져야 한다");
            Assert.IsTrue(StructureGridLogic.EntryCoversCell(tent, FirstBody, 0));
            Assert.IsFalse(StructureGridLogic.EntryCoversCell(tent, FirstBody + 3, 0),
                "천막 밖 한 걸음이면 효과가 끊긴다");
            Assert.IsFalse(StructureGridLogic.EntryCoversCell(tent, FirstBody, 3));
        }

        // ── 칸 분할 (결정 ④) ──────────────────

        [Test]
        public void 한_칸_안_드래그는_조각_하나다()
        {
            var spans = new List<ResizablePlacementLogic.Span>();
            ResizablePlacementLogic.ResolveSpans(1, FirstBody, 2, 1, FirstBody + 2, 5, 13, spans);

            Assert.AreEqual(1, spans.Count);
            Assert.AreEqual(1, spans[0].CarIndex);
            Assert.AreEqual(FirstBody, spans[0].CellX);
            Assert.AreEqual(2, spans[0].CellZ);
            Assert.AreEqual(3, spans[0].Width);
            Assert.AreEqual(4, spans[0].Length);
        }

        [Test]
        public void 세_칸에_걸치면_조각_셋으로_쪼개진다()
        {
            var spans = new List<ResizablePlacementLogic.Span>();
            ResizablePlacementLogic.ResolveSpans(0, FirstBody, 10, 2, FirstBody + 1, 4, 13, spans);

            Assert.AreEqual(3, spans.Count, "칸을 넘는 한 채는 없다 — 칸마다 한 채다");
            Assert.AreEqual(10, spans[0].CellZ, "시작 칸은 앵커 행부터");
            Assert.AreEqual(3, spans[0].Length, "시작 칸은 앵커 행 ~ 끝까지 (10~12)");
            Assert.AreEqual(0, spans[1].CellZ, "가운데 칸은 통째로");
            Assert.AreEqual(13, spans[1].Length);
            Assert.AreEqual(0, spans[2].CellZ);
            Assert.AreEqual(5, spans[2].Length, "끝 칸은 커서 행까지 (0~4)");
        }

        [Test]
        public void 칸_경계에_한_행만_걸친_조각은_버려진다()
        {
            // 기둥 넷이 서지 않는 1행짜리 조각 — 그 칸에는 세우지 않는다.
            var spans = new List<ResizablePlacementLogic.Span>();
            ResizablePlacementLogic.ResolveSpans(1, FirstBody, 12, 2, FirstBody + 1, 0, 13, spans);

            Assert.AreEqual(0, spans.Count,
                "시작 칸 1행(12) · 끝 칸 1행(0) 둘 다 최소 변보다 짧다");
        }

        [Test]
        public void 드래그_방향이_반대여도_같은_범위다()
        {
            var forward = new List<ResizablePlacementLogic.Span>();
            var backward = new List<ResizablePlacementLogic.Span>();
            ResizablePlacementLogic.ResolveSpans(1, FirstBody, 2, 1, FirstBody + 2, 6, 13, forward);
            ResizablePlacementLogic.ResolveSpans(1, FirstBody + 2, 6, 1, FirstBody, 2, 13, backward);

            Assert.AreEqual(forward.Count, backward.Count);
            Assert.AreEqual(forward[0].CellX, backward[0].CellX);
            Assert.AreEqual(forward[0].CellZ, backward[0].CellZ);
            Assert.AreEqual(forward[0].Width, backward[0].Width);
            Assert.AreEqual(forward[0].Length, backward[0].Length);
        }

        [Test]
        public void 한_셀만_끌면_최소_2x2로_넓어진다()
        {
            var spans = new List<ResizablePlacementLogic.Span>();
            ResizablePlacementLogic.ResolveSpans(1, FirstBody, 3, 1, FirstBody, 3, 13, spans);

            Assert.AreEqual(1, spans.Count);
            Assert.AreEqual(ResizablePlacementLogic.MinSide, spans[0].Width);
            Assert.AreEqual(ResizablePlacementLogic.MinSide, spans[0].Length);
        }

        // ── 비용은 넓이 비례 (결정 ⑤) ──────────────────

        [Test]
        public void 비용은_셀_수에_비례하고_올림한다()
        {
            Assert.AreEqual(1, ResizablePlacementLogic.ResolveCost(4, 0.25f, 3), "2×2 = 4셀 → 1");
            Assert.AreEqual(4, ResizablePlacementLogic.ResolveCost(16, 0.25f, 3), "4×4 = 16셀 → 4");
            Assert.AreEqual(13, ResizablePlacementLogic.ResolveCost(52, 0.25f, 3), "칸 하나 = 52셀 → 13");
            Assert.AreEqual(2, ResizablePlacementLogic.ResolveCost(5, 0.25f, 3), "1.25 → 올림 2");
        }

        [Test]
        public void 셀당_비용이_0이면_고정_비용이다()
        {
            // 기존 종류가 이 경로를 타도 값이 바뀌지 않는다.
            Assert.AreEqual(3, ResizablePlacementLogic.ResolveCost(52, 0f, 3));
        }

        [Test]
        public void 총_셀_수는_조각_합계다()
        {
            var spans = new List<ResizablePlacementLogic.Span>
            {
                new ResizablePlacementLogic.Span { Width = 3, Length = 4 },
                new ResizablePlacementLogic.Span { Width = 2, Length = 5 },
            };

            Assert.AreEqual(22, ResizablePlacementLogic.TotalCells(spans));
        }

        // ── 폭 클램프 ──────────────────

        [Test]
        public void 폭은_유효_열을_넘지_않는다()
        {
            int cellX = FirstBody;
            int width = 10;
            ResizablePlacementLogic.ClampToColumns(ref cellX, ref width, FirstBody, 4);

            Assert.AreEqual(4, width, "판자가 없으면 본체 4열이 전부다");
            Assert.AreEqual(FirstBody, cellX);
        }

        [Test]
        public void 판자가_있으면_그만큼_넓어진다()
        {
            int cellX = FirstBody;
            int width = 10;
            ResizablePlacementLogic.ClampToColumns(ref cellX, ref width, FirstBody - 1, 6);

            Assert.AreEqual(6, width);
            Assert.AreEqual(FirstBody - 1, cellX, "좌측 판자 열까지 밀려난다");
        }

        [Test]
        public void 시작_열이_격자를_넘으면_안쪽으로_밀린다()
        {
            int cellX = FirstBody + 3;
            int width = 3;
            ResizablePlacementLogic.ClampToColumns(ref cellX, ref width, FirstBody, 4);

            Assert.AreEqual(3, width);
            Assert.AreEqual(FirstBody + 1, cellX, "폭을 지키되 오른쪽 끝을 넘지 않는다");
        }
    }
}
