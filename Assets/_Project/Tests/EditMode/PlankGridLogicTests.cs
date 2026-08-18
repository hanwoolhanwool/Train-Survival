using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 판자 증축 순수 로직 검증 (건축 개편 3차 — 계획서 §2.9·§4, 결정 ⑥).
    /// 확정 규격: 칸 4.6 × 15 m · 셀 1.0 m → 본체 4열(열 2~5) + 좌/우 각 최대 2열 예약(열 0~1 · 6~7).
    /// 그리드 좌표계 자체는 <see cref="StructureGridLogicTests"/>가, 여기서는 판자 상태 규칙을 못 박는다.
    /// </summary>
    public sealed class PlankGridLogicTests
    {
        private const float CarWidth = 4.6f;
        private const float CellSize = 1f;
        private const int BodyColumns = 4;

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

        /// <summary>판자 열 수를 지정한 칸 배열 — 좌/우 열은 CarState가 들고 있다.</summary>
        private static CarState[] BuildTrainWithPlanks(int carCount, int carIndex, int left, int right)
        {
            CarState[] cars = BuildTrain(carCount);
            CarState car = cars[carIndex];
            car.LeftPlanks = (byte)left;
            car.RightPlanks = (byte)right;
            cars[carIndex] = car;
            return cars;
        }

        private static StructureEntry MakeEntry(int id, int carIndex, int cellX, int cellZ,
            int width = 1, int length = 1)
        {
            return new StructureEntry
            {
                Id = (ushort)id,
                CarIndex = (byte)carIndex,
                CellX = (byte)cellX,
                CellZ = (byte)cellZ,
                Rotation = 0,
                Kind = StructureKind.Campfire,
                FootprintWidth = (byte)width,
                FootprintLength = (byte)length,
                Health = 40f,
                MaxHealth = 40f,
            };
        }

        // ── 판자 자리 식별 (§2.9 — 갑판 평면 연장 조준의 판정 기반) ──────────────────

        [Test]
        public void 다음_판자_자리는_본체_바로_바깥_한_열뿐이다()
        {
            // 판자 없음 — 좌 1, 우 6만 다음 자리다 (중간을 건너뛴 허공 판자를 막는다).
            Assert.That(PlankGridLogic.IsNextPlankColumn(1, BodyColumns, 0, 0, out PlankSide left), Is.True);
            Assert.That(left, Is.EqualTo(PlankSide.Left));
            Assert.That(PlankGridLogic.IsNextPlankColumn(6, BodyColumns, 0, 0, out PlankSide right), Is.True);
            Assert.That(right, Is.EqualTo(PlankSide.Right));
            Assert.That(PlankGridLogic.IsNextPlankColumn(0, BodyColumns, 0, 0, out _), Is.False, "한 칸 건너뛴 자리");
            Assert.That(PlankGridLogic.IsNextPlankColumn(3, BodyColumns, 0, 0, out _), Is.False, "본체 열");

            // 좌측 1열이 이미 있으면 다음 자리는 열 0으로 옮겨간다.
            Assert.That(PlankGridLogic.IsNextPlankColumn(0, BodyColumns, 1, 0, out _), Is.True);
            Assert.That(PlankGridLogic.IsNextPlankColumn(1, BodyColumns, 1, 0, out _), Is.False, "이미 깔린 열");

            // 예약 상한(2열)까지 찼으면 그 쪽은 더 이상 다음 자리가 없다.
            Assert.That(PlankGridLogic.IsNextPlankColumn(0, BodyColumns, 2, 0, out _), Is.False);
        }

        [Test]
        public void 깔린_판자_열은_쪽과_서수로_식별된다()
        {
            Assert.That(PlankGridLogic.TryGetPlankColumn(1, BodyColumns, 1, 0,
                out PlankSide side, out int ordinal), Is.True);
            Assert.That((side, ordinal), Is.EqualTo((PlankSide.Left, 0)));

            Assert.That(PlankGridLogic.TryGetPlankColumn(7, BodyColumns, 0, 2,
                out PlankSide farSide, out int farOrdinal), Is.True);
            Assert.That((farSide, farOrdinal), Is.EqualTo((PlankSide.Right, 1)), "우측 바깥 열");

            Assert.That(PlankGridLogic.TryGetPlankColumn(0, BodyColumns, 1, 0, out _, out _), Is.False, "아직 없는 열");
            Assert.That(PlankGridLogic.TryGetPlankColumn(3, BodyColumns, 1, 1, out _, out _), Is.False, "본체 열");
        }

        // ── 증축·철거 판정 (§2.9) ──────────────────

        [Test]
        public void 판자_증축은_상한과_칸_상태를_지킨다()
        {
            CarState[] cars = BuildTrain(3);

            Assert.That(PlankGridLogic.CanBuildPlank(cars, 0, PlankSide.Left, 1), Is.False, "기관차 제외");
            Assert.That(PlankGridLogic.CanBuildPlank(cars, 1, PlankSide.Left, 1), Is.True);
            Assert.That(PlankGridLogic.CanBuildPlank(cars, 1, PlankSide.Left, 0), Is.False, "상한 0 = 증축 불가");

            CarState[] full = BuildTrainWithPlanks(3, 1, left: 1, right: 0);
            Assert.That(PlankGridLogic.CanBuildPlank(full, 1, PlankSide.Left, 1), Is.False, "에셋 상한 도달");
            Assert.That(PlankGridLogic.CanBuildPlank(full, 1, PlankSide.Left, 2), Is.True, "상한을 올리면 한 열 더");
            Assert.That(PlankGridLogic.CanBuildPlank(full, 1, PlankSide.Right, 1), Is.True, "반대쪽은 별개");

            CarState[] beyond = BuildTrainWithPlanks(3, 1, left: 2, right: 0);
            Assert.That(PlankGridLogic.CanBuildPlank(beyond, 1, PlankSide.Left, 9), Is.False,
                "에셋 상한이 커도 좌표계 예약(2열)을 넘지 않는다");

            TrainStateLogic.DetachFrom(cars, 2);
            Assert.That(PlankGridLogic.CanBuildPlank(cars, 2, PlankSide.Left, 1), Is.False, "이탈 칸 불가");
        }

        [Test]
        public void 판자_철거는_가장_바깥_열만_대상이고_그_위_건축물이_있으면_기각된다()
        {
            CarState[] cars = BuildTrainWithPlanks(3, 1, left: 1, right: 0);

            Assert.That(PlankGridLogic.CanRemovePlank(new StructureEntry[0], cars, 1, PlankSide.Left,
                CarWidth, CellSize), Is.True);
            Assert.That(PlankGridLogic.CanRemovePlank(new StructureEntry[0], cars, 1, PlankSide.Right,
                CarWidth, CellSize), Is.False, "없는 판자는 뜯을 수 없다");

            // 좌측 판자 열(1) 위에 화덕 — 철거 기각.
            StructureEntry[] onPlank = { MakeEntry(1, 1, 1, 3) };
            Assert.That(PlankGridLogic.CanRemovePlank(onPlank, cars, 1, PlankSide.Left,
                CarWidth, CellSize), Is.False, "판자 위 건축물이 먼저다");

            // 본체 열 위 건축물은 판자 철거를 막지 않는다.
            StructureEntry[] onBody = { MakeEntry(1, 1, 2, 3) };
            Assert.That(PlankGridLogic.CanRemovePlank(onBody, cars, 1, PlankSide.Left,
                CarWidth, CellSize), Is.True);

            // 본체 끝 열과 판자 열에 걸친 2×1은 판자 철거를 막는다 (실물이 허공에 뜨면 안 된다).
            CarState[] rightPlanked = BuildTrainWithPlanks(3, 1, left: 0, right: 1);
            StructureEntry[] straddling = { MakeEntry(1, 1, 5, 3, width: 2) };
            Assert.That(PlankGridLogic.CanRemovePlank(straddling, rightPlanked, 1, PlankSide.Right,
                CarWidth, CellSize), Is.False, "걸친 건축물도 막는다");

            // 2열일 때는 바깥(0) 열만 본다 — 안쪽(1) 위 건축물은 무관.
            CarState[] two = BuildTrainWithPlanks(3, 1, left: 2, right: 0);
            Assert.That(PlankGridLogic.CanRemovePlank(onPlank, two, 1, PlankSide.Left,
                CarWidth, CellSize), Is.True, "안쪽 열 위 건축물은 바깥 열 철거를 막지 않는다");
            StructureEntry[] onOuter = { MakeEntry(1, 1, 0, 3) };
            Assert.That(PlankGridLogic.CanRemovePlank(onOuter, two, 1, PlankSide.Left,
                CarWidth, CellSize), Is.False);

            TrainStateLogic.DetachFrom(cars, 1);
            Assert.That(PlankGridLogic.CanRemovePlank(new StructureEntry[0], cars, 1, PlankSide.Left,
                CarWidth, CellSize), Is.False, "이탈 칸 불가 — 건축물 철거와 같은 게이트");
        }

        // ── 폭 파생 판정 (§2.9 — 낙하·몬스터 승차) ──────────────────

        [Test]
        public void 갑판_반폭은_판자_열만큼_넓어진다()
        {
            // 판자 0 = 칸 실물 반폭(4.6 / 2). 1열부터는 본체 그리드 반폭(2.0) + 열 폭.
            Assert.That(PlankGridLogic.DeckHalfWidth(CarWidth, CellSize, 0), Is.EqualTo(2.3f).Within(0.001f));
            Assert.That(PlankGridLogic.DeckHalfWidth(CarWidth, CellSize, 1), Is.EqualTo(3f).Within(0.001f));
            Assert.That(PlankGridLogic.DeckHalfWidth(CarWidth, CellSize, 2), Is.EqualTo(4f).Within(0.001f));
            Assert.That(PlankGridLogic.DeckHalfWidth(CarWidth, CellSize, 9), Is.EqualTo(4f).Within(0.001f),
                "예약 상한을 넘는 값은 클램프");
        }

        // ── 상태 복제 (판자 열은 CarState가 나른다) ──────────────────

        [Test]
        public void 칸_상태_비교는_판자_열_변화를_잡아낸다()
        {
            CarState plain = BuildTrain(2)[1];
            CarState planked = BuildTrainWithPlanks(2, 1, left: 1, right: 0)[1];

            Assert.That(plain.Equals(planked), Is.False, "판자 열이 다르면 복제가 값 변화를 알아야 한다");
            Assert.That(planked.Equals(BuildTrainWithPlanks(2, 1, left: 1, right: 0)[1]), Is.True);
            Assert.That(planked.Equals(BuildTrainWithPlanks(2, 1, left: 0, right: 1)[1]), Is.False, "좌우는 별개 축");
        }
    }
}
