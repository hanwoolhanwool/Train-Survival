using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 건축 그리드 순수 로직 검증 (건축 개편 1차 — 계획서 §2.3·§4).
    /// 확정 규격: 칸 4.6 × 15 m · 셀 1.0 m → 본체 4열(고정 예약 좌표계로 열 2~5) × 15행.
    /// 행은 칸 후미(-Z)가 0. 소유자 프리뷰와 호스트 확정이 같은 함수를 쓰므로 여기서 전 경계를 못 박는다.
    /// </summary>
    public sealed class StructureGridLogicTests
    {
        private const float CarWidth = 4.6f;
        private const float CarLength = 15f;
        private const float CellSize = 1f;
        private const float CarCenterZ = 0f;

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
            int rotation = 0, StructureKind kind = StructureKind.Campfire, int width = 1, int length = 1,
            float health = 40f)
        {
            return new StructureEntry
            {
                Id = (ushort)id,
                CarIndex = (byte)carIndex,
                CellX = (byte)cellX,
                CellZ = (byte)cellZ,
                Rotation = (byte)rotation,
                Kind = kind,
                FootprintWidth = (byte)width,
                FootprintLength = (byte)length,
                Health = health,
                MaxHealth = health,
            };
        }

        private static bool CanPlace(StructureEntry[] entries, CarState[] cars, int carIndex,
            int cellX, int cellZ, int rotation = 0, StructureKind kind = StructureKind.Campfire,
            int width = 1, int length = 1, bool placeable = true)
        {
            return StructureGridLogic.CanPlace(entries, cars, carIndex, cellX, cellZ, rotation,
                kind, width, length, placeable, CarWidth, CarLength, CellSize);
        }

        // ── 그리드 파생 규격 (결정 ①) ──────────────────

        [Test]
        public void 확정_규격에서_본체_4열_15행이_나온다()
        {
            Assert.That(StructureGridLogic.BodyColumns(CarWidth, CellSize), Is.EqualTo(4), "폭 4.6 m — 4열 + 양옆 0.3 m 여백");
            Assert.That(StructureGridLogic.Rows(CarLength, CellSize), Is.EqualTo(15));
            Assert.That(StructureGridLogic.FirstBodyColumn, Is.EqualTo(2), "좌측 판자 2열 고정 예약 — 본체는 열 2부터");
        }

        [Test]
        public void 회전_footprint는_홀수_회전에서_가로세로가_스왑된다()
        {
            StructureGridLogic.RotatedFootprint(2, 1, 0, out int w0, out int l0);
            Assert.That((w0, l0), Is.EqualTo((2, 1)));

            StructureGridLogic.RotatedFootprint(2, 1, 1, out int w1, out int l1);
            Assert.That((w1, l1), Is.EqualTo((1, 2)), "90°");

            StructureGridLogic.RotatedFootprint(2, 1, 2, out int w2, out int l2);
            Assert.That((w2, l2), Is.EqualTo((2, 1)), "180°");

            StructureGridLogic.RotatedFootprint(2, 1, 3, out int w3, out int l3);
            Assert.That((w3, l3), Is.EqualTo((1, 2)), "270°");
        }

        // ── 셀 변환 왕복 (§2.3 — 고정 예약 좌표계: 본체 열 2~5) ──────────────────

        [Test]
        public void 셀_중심_월드_좌표는_왕복_변환에서_같은_셀로_돌아온다()
        {
            for (int cellX = 2; cellX <= 5; cellX++)
            {
                for (int cellZ = 0; cellZ < 15; cellZ += 7)
                {
                    StructureGridLogic.CellRegionCenterWorld(cellX, cellZ, 1, 1,
                        CarCenterZ, CarWidth, CarLength, CellSize, out float worldX, out float worldZ);

                    Assert.That(StructureGridLogic.TryWorldToPlacementCell(worldX, worldZ, CarCenterZ,
                        CarWidth, CarLength, CellSize, 1, 1, 0, 0, out int backX, out int backZ), Is.True);
                    Assert.That((backX, backZ), Is.EqualTo((cellX, cellZ)), $"셀 ({cellX},{cellZ}) 왕복");
                }
            }
        }

        [Test]
        public void 좌하단_본체_셀의_월드_중심은_확정_규격과_일치한다()
        {
            // 본체 좌측 끝 열(2)·후미 행(0)의 1×1 중심 — 본체 폭 4 m가 X=0 중심이므로 x = -1.5,
            // 행 스팬 15 m가 칸 중심 정렬이므로 z = 칸중심 - 7.5 + 0.5.
            StructureGridLogic.CellRegionCenterWorld(2, 0, 1, 1,
                CarCenterZ, CarWidth, CarLength, CellSize, out float worldX, out float worldZ);

            Assert.That(worldX, Is.EqualTo(-1.5f).Within(0.001f));
            Assert.That(worldZ, Is.EqualTo(-7f).Within(0.001f));
        }

        [Test]
        public void 다중_셀_점유는_커서에_중심_정렬로_스냅된다()
        {
            // 2×2를 (x=-1, z=-6.5)에 겨눔 — 점유 중심이 커서가 되도록 좌하단 (2,0)으로 스냅.
            Assert.That(StructureGridLogic.TryWorldToPlacementCell(-1f, -6.5f, CarCenterZ,
                CarWidth, CarLength, CellSize, 2, 2, 0, 0, out int cellX, out int cellZ), Is.True);
            Assert.That((cellX, cellZ), Is.EqualTo((2, 0)));
        }

        [Test]
        public void 가장자리_여백을_겨눠도_점유는_본체_그리드_안으로_스냅된다()
        {
            // 폭 4.6 m 중 양옆 0.3 m는 여백 — 여백을 찍어도 클램프로 본체 끝 열에 스냅되므로
            // 점유가 여백에 걸치는 일이 없다 (설치 유효성 자체는 CanPlace의 본체 내부 검사가 지킨다).
            Assert.That(StructureGridLogic.TryWorldToPlacementCell(-2.29f, 0f, CarCenterZ,
                CarWidth, CarLength, CellSize, 1, 1, 0, 0, out int leftX, out _), Is.True);
            Assert.That(leftX, Is.EqualTo(2), "좌측 여백 → 본체 첫 열");

            Assert.That(StructureGridLogic.TryWorldToPlacementCell(2.29f, 0f, CarCenterZ,
                CarWidth, CarLength, CellSize, 1, 1, 0, 0, out int rightX, out _), Is.True);
            Assert.That(rightX, Is.EqualTo(5), "우측 여백 → 본체 끝 열");
        }

        [Test]
        public void 그리드보다_큰_점유는_변환이_기각된다()
        {
            Assert.That(StructureGridLogic.TryWorldToPlacementCell(0f, 0f, CarCenterZ,
                CarWidth, CarLength, CellSize, 5, 1, 0, 0, out _, out _), Is.False, "본체 4열 초과");
        }

        // ── 설치 판정 CanPlace (§2.3) ──────────────────

        [Test]
        public void 빈_그리드의_본체_셀에는_설치할_수_있다()
        {
            CarState[] cars = BuildTrain(3);
            var entries = new StructureEntry[0];

            Assert.That(CanPlace(entries, cars, 1, 2, 0), Is.True);
            Assert.That(CanPlace(entries, cars, 1, 5, 14), Is.True, "본체 끝 셀");
            Assert.That(CanPlace(entries, cars, 1, 4, 13, width: 2, length: 2), Is.True, "2×2가 본체 끝에 딱 맞음");
        }

        [Test]
        public void 본체_밖_열과_행은_기각된다()
        {
            CarState[] cars = BuildTrain(3);
            var entries = new StructureEntry[0];

            Assert.That(CanPlace(entries, cars, 1, 1, 0), Is.False, "판자 예약 열(1차 — 판자 없음)");
            Assert.That(CanPlace(entries, cars, 1, 6, 0), Is.False, "우측 판자 예약 열");
            Assert.That(CanPlace(entries, cars, 1, 5, 14, width: 2, length: 1), Is.False, "2×1이 본체 우측을 벗어남");
            Assert.That(CanPlace(entries, cars, 1, 2, 14, width: 1, length: 2), Is.False, "1×2가 전방 행을 벗어남");
        }

        [Test]
        public void 기관차와_이탈_칸에는_설치할_수_없다()
        {
            CarState[] cars = BuildTrain(3);
            var entries = new StructureEntry[0];

            Assert.That(CanPlace(entries, cars, 0, 2, 0), Is.False, "기관차 기각");

            TrainStateLogic.DetachFrom(cars, 2);
            Assert.That(CanPlace(entries, cars, 2, 2, 0), Is.False, "이탈 칸 기각");
        }

        [Test]
        public void 점유_셀이_겹치면_기각되고_다른_칸의_같은_셀은_허용된다()
        {
            CarState[] cars = BuildTrain(3);
            StructureEntry[] entries = { MakeEntry(1, 1, 3, 5, width: 2, length: 1) };

            Assert.That(CanPlace(entries, cars, 1, 3, 5), Is.False, "같은 셀");
            Assert.That(CanPlace(entries, cars, 1, 4, 5), Is.False, "2×1의 오른쪽 절반과 교차");
            Assert.That(CanPlace(entries, cars, 1, 2, 5), Is.True, "바로 왼쪽 옆 셀은 비어 있다");
            Assert.That(CanPlace(entries, cars, 1, 3, 6), Is.True, "바로 앞 행은 비어 있다");
            Assert.That(CanPlace(entries, cars, 2, 3, 5), Is.True, "다른 칸의 같은 셀 좌표는 독립");
        }

        [Test]
        public void 회전된_기존_점유와의_교차도_판정된다()
        {
            CarState[] cars = BuildTrain(3);
            // 2×1을 90° 회전 — 실점유는 (3,5)와 (3,6)의 1×2.
            StructureEntry[] entries = { MakeEntry(1, 1, 3, 5, rotation: 1, width: 2, length: 1) };

            Assert.That(CanPlace(entries, cars, 1, 3, 6), Is.False, "회전 반영 점유 셀과 교차");
            Assert.That(CanPlace(entries, cars, 1, 4, 5), Is.True, "회전 전 점유였을 셀은 비어 있다");
        }

        [Test]
        public void 설치_불가_플래그가_꺼진_종류는_기각된다()
        {
            CarState[] cars = BuildTrain(3);
            var entries = new StructureEntry[0];

            Assert.That(CanPlace(entries, cars, 1, 2, 0, kind: StructureKind.Dome, placeable: false),
                Is.False, "돔 — 설치 목록 제외 (계획서 §1.2)");
        }

        [Test]
        public void 창고도_한_칸에_여러_개_설치할_수_있다()
        {
            // 건축 개편 2차 (결정 ⑦) — 저장 블록이 건축물 Id 기반이라 1차의 칸당 1개 가드가 해제됐다.
            CarState[] cars = BuildTrain(3);
            StructureEntry[] entries = { MakeEntry(1, 1, 2, 0, kind: StructureKind.Storage, width: 2) };

            Assert.That(CanPlace(entries, cars, 1, 2, 5, kind: StructureKind.Storage, width: 2),
                Is.True, "같은 칸 두 번째 창고 — 빈 셀이면 허용");
            Assert.That(CanPlace(entries, cars, 1, 2, 0, kind: StructureKind.Storage, width: 2),
                Is.False, "셀 겹침은 여전히 기각 — 제한은 그리드 점유뿐");
        }

        // ── 철거 (건축 개편 2차 — 결정 ④·⑤) ──────────────────

        [Test]
        public void 철거_반환량은_비용의_절반_내림이다()
        {
            // 확정 예시 (§1.1): 화덕 3 → 1, 창고 4 → 2, 강화 난방로 7 → 3.
            Assert.That(StructureGridLogic.RefundAmount(3, 0.5f), Is.EqualTo(1));
            Assert.That(StructureGridLogic.RefundAmount(4, 0.5f), Is.EqualTo(2));
            Assert.That(StructureGridLogic.RefundAmount(7, 0.5f), Is.EqualTo(3));
            Assert.That(StructureGridLogic.RefundAmount(0, 0.5f), Is.EqualTo(0));
            Assert.That(StructureGridLogic.RefundAmount(5, 0f), Is.EqualTo(0), "비율 0 = 무반환");
        }

        [Test]
        public void 철거는_살아_붙은_칸_위_건축물만_가능하다()
        {
            CarState[] cars = BuildTrain(3);
            StructureEntry[] entries = { MakeEntry(1, 2, 2, 0) };

            Assert.That(StructureGridLogic.CanDemolish(entries, cars, 0), Is.True);

            TrainStateLogic.DetachFrom(cars, 2);
            Assert.That(StructureGridLogic.CanDemolish(entries, cars, 0), Is.False,
                "이탈 칸 위는 철거 불가 — 피해 규칙과 같은 게이트");
            Assert.That(StructureGridLogic.CanDemolish(entries, cars, 1), Is.False, "범위 밖");
        }

        [Test]
        public void 제거된_항목_자리에는_다시_지을_수_있다()
        {
            CarState[] cars = BuildTrain(3);
            StructureEntry[] occupied = { MakeEntry(1, 1, 3, 5) };
            Assert.That(CanPlace(occupied, cars, 1, 3, 5), Is.False);

            // 파괴 = 리스트에서 항목 제거 — 빈 배열이면 같은 자리가 다시 열린다.
            var afterDestroy = new StructureEntry[0];
            Assert.That(CanPlace(afterDestroy, cars, 1, 3, 5), Is.True);
        }

        // ── 피해·수리 (구 TrainStateLogic 규칙 이관) ──────────────────

        [Test]
        public void 건축물_데미지는_체력을_줄이고_0에서_파괴를_알린다()
        {
            CarState[] cars = BuildTrain(3);
            StructureEntry[] entries = { MakeEntry(1, 1, 2, 0, health: 50f) };

            Assert.That(StructureGridLogic.ApplyDamage(entries, cars, 0, 20f), Is.EqualTo(CarDamageResult.Damaged));
            Assert.That(entries[0].Health, Is.EqualTo(30f).Within(0.001f));
            Assert.That(entries[0].Kind, Is.EqualTo(StructureKind.Campfire), "데미지에도 종류 유지");

            Assert.That(StructureGridLogic.ApplyDamage(entries, cars, 0, 999f), Is.EqualTo(CarDamageResult.Destroyed));
            Assert.That(entries[0].Health, Is.EqualTo(0f));
        }

        [Test]
        public void 이탈_칸_위_건축물의_데미지는_무시된다()
        {
            CarState[] cars = BuildTrain(3);
            StructureEntry[] entries = { MakeEntry(1, 2, 2, 0) };

            TrainStateLogic.DetachFrom(cars, 2);
            Assert.That(StructureGridLogic.ApplyDamage(entries, cars, 0, 10f),
                Is.EqualTo(CarDamageResult.Ignored), "이탈 칸 위 건축물은 별도 표적이 아니다 (규칙 이관)");
        }

        [Test]
        public void 건축물_수리는_손상분만_회복하고_만피_초과와_이탈_칸은_불가다()
        {
            CarState[] cars = BuildTrain(3);
            StructureEntry[] entries = { MakeEntry(1, 1, 2, 0, health: 50f) };
            StructureGridLogic.ApplyDamage(entries, cars, 0, 20f);

            Assert.That(StructureGridLogic.Repair(entries, cars, 0, 5f), Is.True);
            Assert.That(entries[0].Health, Is.EqualTo(35f).Within(0.001f));

            Assert.That(StructureGridLogic.Repair(entries, cars, 0, 999f), Is.True);
            Assert.That(entries[0].Health, Is.EqualTo(50f), "만피 초과 금지");
            Assert.That(StructureGridLogic.Repair(entries, cars, 0, 5f), Is.False, "만피는 수리 불가");

            TrainStateLogic.DetachFrom(cars, 1);
            StructureGridLogic.ApplyDamage(entries, cars, 0, 10f);
            Assert.That(StructureGridLogic.Repair(entries, cars, 0, 5f), Is.False, "이탈 칸 위는 수리 불가");
        }

        [Test]
        public void Id_조회는_안정_참조로_동작한다()
        {
            StructureEntry[] entries = { MakeEntry(7, 1, 2, 0), MakeEntry(9, 2, 3, 1) };

            Assert.That(StructureGridLogic.TryFindById(entries, 9, out int index), Is.True);
            Assert.That(index, Is.EqualTo(1));
            Assert.That(StructureGridLogic.TryFindById(entries, 8, out _), Is.False);
            Assert.That(StructureGridLogic.TryFindById(entries, 0, out _), Is.False, "0은 무효 Id");
        }

        // ── 몬스터 관통 방지 (§2.10 — 이동 차단 판정) ──────────────────

        [Test]
        public void 점유_영역_안팎의_이동_차단_판정이_여유_폭을_반영한다()
        {
            // (3,7) 1×1 — 월드 중심 (-0.5, 0.0). 반폭 0.5 + 여유 0.3.
            StructureEntry entry = MakeEntry(1, 1, 3, 7);

            Assert.That(StructureGridLogic.IsWorldPointOnEntry(entry, -0.5f, 0f, 0.3f,
                CarCenterZ, CarWidth, CarLength, CellSize), Is.True, "중심");
            Assert.That(StructureGridLogic.IsWorldPointOnEntry(entry, -0.5f + 0.75f, 0f, 0.3f,
                CarCenterZ, CarWidth, CarLength, CellSize), Is.True, "여유 폭 안");
            Assert.That(StructureGridLogic.IsWorldPointOnEntry(entry, -0.5f + 0.9f, 0f, 0.3f,
                CarCenterZ, CarWidth, CarLength, CellSize), Is.False, "여유 폭 밖 — 통과");
            Assert.That(StructureGridLogic.IsWorldPointOnEntry(entry, -0.5f, 1.5f, 0.3f,
                CarCenterZ, CarWidth, CarLength, CellSize), Is.False, "행 방향 밖");
        }

        // ── 판자 증축 (건축 개편 3차 — §2.9, 결정 ⑥) ──────────────────

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

        [Test]
        public void 판자_열_수는_좌표계_예약_상한으로_클램프된다()
        {
            Assert.That(StructureGridLogic.MaxPlankColumnsPerSide, Is.EqualTo(2), "좌/우 각 2열 고정 예약");
            Assert.That(StructureGridLogic.ClampPlankColumns(5), Is.EqualTo(2), "예약 초과는 잘린다 — 재색인 방지");
            Assert.That(StructureGridLogic.ClampPlankColumns(-1), Is.EqualTo(0));
        }

        [Test]
        public void 판자_열_좌표는_본체_바깥에_붙어_고정된다()
        {
            // 본체 = 열 2~5. 좌측 판자는 1 → 0으로, 우측은 6 → 7로 자란다 (좌측을 나중에 지어도 재색인 없음).
            Assert.That(StructureGridLogic.PlankColumn(PlankSide.Left, 0, 4), Is.EqualTo(1));
            Assert.That(StructureGridLogic.PlankColumn(PlankSide.Left, 1, 4), Is.EqualTo(0));
            Assert.That(StructureGridLogic.PlankColumn(PlankSide.Right, 0, 4), Is.EqualTo(6));
            Assert.That(StructureGridLogic.PlankColumn(PlankSide.Right, 1, 4), Is.EqualTo(7));
        }

        [Test]
        public void 판자_열의_월드_중심은_본체_그리드에_딱_붙는다()
        {
            // 본체 폭 4 m가 X=0 중심 → 본체 끝 열 중심 ±1.5, 판자 첫 열 중심 ±2.5.
            Assert.That(StructureGridLogic.ColumnCenterWorldX(1, 4, CellSize), Is.EqualTo(-2.5f).Within(0.001f));
            Assert.That(StructureGridLogic.ColumnCenterWorldX(6, 4, CellSize), Is.EqualTo(2.5f).Within(0.001f));

            // 1×1 점유의 셀 중심과 같은 값이어야 한다 — 뷰·프리뷰·설치가 한 지점을 쓴다.
            StructureGridLogic.CellRegionCenterWorld(1, 0, 1, 1,
                CarCenterZ, CarWidth, CarLength, CellSize, out float worldX, out _);
            Assert.That(worldX, Is.EqualTo(-2.5f).Within(0.001f));
        }

        [Test]
        public void 월드_X는_판자_열까지_포함해_열로_환산된다()
        {
            Assert.That(StructureGridLogic.WorldXToColumn(-1.5f, 4, CellSize), Is.EqualTo(2), "본체 첫 열");
            Assert.That(StructureGridLogic.WorldXToColumn(1.5f, 4, CellSize), Is.EqualTo(5), "본체 끝 열");
            Assert.That(StructureGridLogic.WorldXToColumn(-2.5f, 4, CellSize), Is.EqualTo(1), "좌측 판자 첫 열");
            Assert.That(StructureGridLogic.WorldXToColumn(2.5f, 4, CellSize), Is.EqualTo(6), "우측 판자 첫 열");
        }

        [Test]
        public void 판자_위에는_건축물을_설치할_수_있고_판자가_없으면_기각된다()
        {
            var entries = new StructureEntry[0];
            CarState[] plain = BuildTrain(3);
            CarState[] planked = BuildTrainWithPlanks(3, 1, left: 1, right: 1);

            Assert.That(StructureGridLogic.CanPlace(entries, plain, 1, 1, 0, 0,
                StructureKind.Campfire, 1, 1, true, CarWidth, CarLength, CellSize), Is.False, "판자 없는 열 1");
            Assert.That(StructureGridLogic.CanPlace(entries, planked, 1, 1, 0, 0,
                StructureKind.Campfire, 1, 1, true, CarWidth, CarLength, CellSize), Is.True, "좌측 판자 위");
            Assert.That(StructureGridLogic.CanPlace(entries, planked, 1, 6, 0, 0,
                StructureKind.Campfire, 1, 1, true, CarWidth, CarLength, CellSize), Is.True, "우측 판자 위");
            Assert.That(StructureGridLogic.CanPlace(entries, planked, 1, 0, 0, 0,
                StructureKind.Campfire, 1, 1, true, CarWidth, CarLength, CellSize), Is.False, "판자 2열째는 아직 없다");

            // 2×1 제작대가 본체 끝 열과 판자 열에 걸쳐 놓이는 것도 성립한다.
            Assert.That(StructureGridLogic.CanPlace(entries, planked, 1, 5, 0, 0,
                StructureKind.Workbench, 2, 1, true, CarWidth, CarLength, CellSize), Is.True, "본체 끝 + 판자 걸침");
        }

        [Test]
        public void 판자가_있으면_스냅_클램프_범위가_그만큼_넓어진다()
        {
            // 판자 없음: 좌측 바깥을 겨눠도 본체 첫 열(2)로 클램프.
            Assert.That(StructureGridLogic.TryWorldToPlacementCell(-2.6f, 0f, CarCenterZ,
                CarWidth, CarLength, CellSize, 1, 1, 0, 0, out int plainX, out _), Is.True);
            Assert.That(plainX, Is.EqualTo(2));

            // 좌측 1열: 판자 열(1)까지 스냅된다.
            Assert.That(StructureGridLogic.TryWorldToPlacementCell(-2.6f, 0f, CarCenterZ,
                CarWidth, CarLength, CellSize, 1, 1, 1, 0, out int plankedX, out _), Is.True);
            Assert.That(plankedX, Is.EqualTo(1));

            // 본체(4열)보다 넓은 5칸 점유도 좌우 판자로 유효 열이 6이 되면 통과한다.
            Assert.That(StructureGridLogic.TryWorldToPlacementCell(0f, 0f, CarCenterZ,
                CarWidth, CarLength, CellSize, 5, 1, 0, 0, out _, out _), Is.False, "판자 없이는 기각");
            Assert.That(StructureGridLogic.TryWorldToPlacementCell(0f, 0f, CarCenterZ,
                CarWidth, CarLength, CellSize, 5, 1, 1, 1, out _, out _), Is.True, "좌우 1열씩이면 6열");
        }

        [Test]
        public void 판자_철거_반환량은_건축물과_같은_비율_규칙을_쓴다()
        {
            Assert.That(StructureGridLogic.RefundAmount(3, 0.5f), Is.EqualTo(1), "판자 비용 3 → 1");
            Assert.That(StructureGridLogic.RefundAmount(4, 0.5f), Is.EqualTo(2));
        }
    }
}
