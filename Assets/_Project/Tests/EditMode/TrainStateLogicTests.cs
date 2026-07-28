using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 열차 편성 상태 순수 로직 검증 (개발 가이드 §M3 — 칸 파괴·후방 연쇄 이탈·기관차 불변식).
    /// 편성 규약: 인덱스 0 = 기관차(선두, 파괴 불가), 값이 클수록 후방.
    /// </summary>
    public sealed class TrainStateLogicTests
    {
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

        [Test]
        public void 기관차만_파괴_불가다()
        {
            Assert.That(TrainStateLogic.IsDestructible(CarType.Locomotive), Is.False);
            Assert.That(TrainStateLogic.IsDestructible(CarType.Standard), Is.True);
            Assert.That(TrainStateLogic.IsDestructible(CarType.Greenhouse), Is.True);
        }

        [Test]
        public void 초기_편성은_선두_기관차_전부_최대체력_연결_상태다()
        {
            CarState[] cars = BuildTrain(3);

            Assert.That(cars.Length, Is.EqualTo(3));
            Assert.That(cars[0].Type, Is.EqualTo(CarType.Locomotive));
            for (int i = 0; i < cars.Length; i++)
            {
                Assert.That(cars[i].Attached, Is.True);
                Assert.That(cars[i].Health, Is.EqualTo(cars[i].MaxHealth));
            }
        }

        [Test]
        public void 데미지는_체력을_줄이고_0_미만으로_내려가지_않는다()
        {
            CarState[] cars = BuildTrain(3);

            Assert.That(TrainStateLogic.ApplyDamage(cars, 1, 30f), Is.EqualTo(CarDamageResult.Damaged));
            Assert.That(cars[1].Health, Is.EqualTo(70f).Within(0.001f));

            Assert.That(TrainStateLogic.ApplyDamage(cars, 1, 999f), Is.EqualTo(CarDamageResult.Destroyed));
            Assert.That(cars[1].Health, Is.EqualTo(0f));
        }

        [Test]
        public void 기관차_데미지와_잘못된_인덱스는_무시된다()
        {
            CarState[] cars = BuildTrain(3);

            Assert.That(TrainStateLogic.ApplyDamage(cars, 0, 999f), Is.EqualTo(CarDamageResult.Ignored));
            Assert.That(cars[0].Health, Is.EqualTo(float.PositiveInfinity));
            Assert.That(TrainStateLogic.ApplyDamage(cars, 5, 10f), Is.EqualTo(CarDamageResult.Ignored));
            Assert.That(TrainStateLogic.ApplyDamage(cars, 1, 0f), Is.EqualTo(CarDamageResult.Ignored));
        }

        [Test]
        public void 이미_파괴되거나_이탈한_칸에는_데미지가_들어가지_않는다()
        {
            CarState[] cars = BuildTrain(4);

            TrainStateLogic.ApplyDamage(cars, 1, 999f); // 파괴됨
            Assert.That(TrainStateLogic.ApplyDamage(cars, 1, 10f), Is.EqualTo(CarDamageResult.Ignored));

            TrainStateLogic.DetachFrom(cars, 3); // 3번 이탈
            Assert.That(TrainStateLogic.ApplyDamage(cars, 3, 10f), Is.EqualTo(CarDamageResult.Ignored));
        }

        [Test]
        public void 이탈은_시작점부터_후방_전부를_끊고_새로_이탈한_인덱스를_돌려준다()
        {
            CarState[] cars = BuildTrain(5);

            int[] detached = TrainStateLogic.DetachFrom(cars, 2);

            Assert.That(detached, Is.EqualTo(new[] { 2, 3, 4 }));
            Assert.That(cars[0].Attached, Is.True);
            Assert.That(cars[1].Attached, Is.True);
            Assert.That(cars[2].Attached, Is.False);
            Assert.That(cars[3].Attached, Is.False);
            Assert.That(cars[4].Attached, Is.False);
        }

        [Test]
        public void 기관차는_이탈_시작점이_0이하여도_절대_이탈하지_않는다()
        {
            CarState[] cars = BuildTrain(3);

            int[] detached = TrainStateLogic.DetachFrom(cars, 0);

            Assert.That(detached, Is.EqualTo(new[] { 1, 2 }));
            Assert.That(cars[0].Attached, Is.True, "기관차(0)는 어떤 경우에도 이탈하지 않는다");
        }

        [Test]
        public void 이미_이탈한_칸은_다시_이탈_목록에_포함되지_않는다()
        {
            CarState[] cars = BuildTrain(5);

            TrainStateLogic.DetachFrom(cars, 4); // 4번 먼저 이탈
            int[] detached = TrainStateLogic.DetachFrom(cars, 2);

            Assert.That(detached, Is.EqualTo(new[] { 2, 3 }), "이미 이탈한 4번은 제외");
        }

        [Test]
        public void 칸_파괴는_그_뒤_칸부터_후방_연쇄_이탈시킨다()
        {
            CarState[] cars = BuildTrain(5);

            int[] detached = TrainStateLogic.DestroyAndDetach(cars, 2);

            Assert.That(cars[2].Health, Is.EqualTo(0f));
            Assert.That(cars[2].Attached, Is.False, "파괴된 칸 자체도 편성에서 빠진다");
            Assert.That(detached, Is.EqualTo(new[] { 3, 4 }), "파괴 칸 뒤의 칸이 연쇄 이탈");
            Assert.That(cars[0].Attached, Is.True);
            Assert.That(cars[1].Attached, Is.True);
        }

        [Test]
        public void 기관차_파괴_시도는_상태를_바꾸지_않는다()
        {
            CarState[] cars = BuildTrain(3);

            int[] detached = TrainStateLogic.DestroyAndDetach(cars, 0);

            Assert.That(detached, Is.Empty);
            Assert.That(cars[0].Attached, Is.True);
            Assert.That(cars[1].Attached, Is.True, "기관차 파괴가 무시되므로 후방도 그대로");
        }

        // ── 연결부 (기획서 §9 — 밤 방어전 핵심 방어 목표) ──────────────────

        [Test]
        public void 초기_연결부는_칸수_빼기1개_전부_최대체력_연결_상태다()
        {
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(3, 60f);

            Assert.That(couplings.Length, Is.EqualTo(2));
            foreach (CouplingState c in couplings)
            {
                Assert.That(c.Broken, Is.False);
                Assert.That(c.Health, Is.EqualTo(60f));
                Assert.That(c.MaxHealth, Is.EqualTo(60f));
            }

            Assert.That(TrainStateLogic.BuildInitialCouplings(1, 60f), Is.Empty, "칸이 1개면 연결부 없음");
        }

        [Test]
        public void 연결부는_잇는_두_칸이_모두_살아있을_때만_공격_대상이다()
        {
            CarState[] cars = BuildTrain(3);
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(3, 60f);

            Assert.That(TrainStateLogic.IsCouplingLive(couplings, cars, 0), Is.True);
            Assert.That(TrainStateLogic.IsCouplingLive(couplings, cars, 1), Is.True);
            Assert.That(TrainStateLogic.IsCouplingLive(couplings, cars, 2), Is.False, "연결부 2는 없음");

            TrainStateLogic.DetachFrom(cars, 2); // 후미 칸 이탈
            Assert.That(TrainStateLogic.IsCouplingLive(couplings, cars, 1), Is.False, "뒤 칸이 이탈하면 연결부도 죽음");
        }

        [Test]
        public void 연결부_데미지는_체력을_줄이고_끊기면_Broken이_된다()
        {
            CarState[] cars = BuildTrain(3);
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(3, 60f);

            Assert.That(TrainStateLogic.ApplyCouplingDamage(couplings, cars, 1, 20f),
                Is.EqualTo(CouplingDamageResult.Damaged));
            Assert.That(couplings[1].Health, Is.EqualTo(40f).Within(0.001f));
            Assert.That(couplings[1].Broken, Is.False);

            Assert.That(TrainStateLogic.ApplyCouplingDamage(couplings, cars, 1, 999f),
                Is.EqualTo(CouplingDamageResult.Broken));
            Assert.That(couplings[1].Broken, Is.True);

            Assert.That(TrainStateLogic.ApplyCouplingDamage(couplings, cars, 1, 10f),
                Is.EqualTo(CouplingDamageResult.Ignored), "이미 끊긴 연결부는 무시");
        }

        [Test]
        public void 연결부_파괴는_그_뒤_칸부터_후방_연쇄_이탈로_이어진다()
        {
            // 후미 연결부 2(칸2-칸3 사이) 파괴 → 칸3 이탈. 칸들은 멀쩡(체력 유지).
            CarState[] cars = BuildTrain(4);
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(4, 60f);

            TrainStateLogic.ApplyCouplingDamage(couplings, cars, 2, 999f);
            int[] detached = TrainStateLogic.DetachFrom(cars, 2 + 1);

            Assert.That(detached, Is.EqualTo(new[] { 3 }));
            Assert.That(cars[2].Attached, Is.True, "연결부 앞 칸은 유지");
            Assert.That(cars[3].Attached, Is.False);
            Assert.That(cars[3].Health, Is.GreaterThan(0f), "이탈 칸은 파괴가 아니라 멀쩡히 떨어져 나감");
        }

        [Test]
        public void 연결부는_살아있는_것_중_가장_후미만_표적이다()
        {
            CarState[] cars = BuildTrain(3);
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(3, 60f);

            Assert.That(TrainStateLogic.IsCouplingTargetable(couplings, cars, 1), Is.True, "후미 연결부는 표적");
            Assert.That(TrainStateLogic.IsCouplingTargetable(couplings, cars, 0), Is.False,
                "뒤 연결부가 살아 있는 동안 앞(기관차-다음 칸) 연결부는 표적이 아니다");

            TrainStateLogic.ApplyCouplingDamage(couplings, cars, 1, 999f);
            TrainStateLogic.DetachFrom(cars, 2);

            Assert.That(TrainStateLogic.IsCouplingTargetable(couplings, cars, 0), Is.True,
                "후미가 끊기면 그 앞 연결부가 새 표적이 된다");
        }

        [Test]
        public void 앞쪽_연결부_데미지는_후미가_살아있는_동안_무시된다()
        {
            CarState[] cars = BuildTrain(3);
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(3, 60f);

            Assert.That(TrainStateLogic.ApplyCouplingDamage(couplings, cars, 0, 20f),
                Is.EqualTo(CouplingDamageResult.Ignored));
            Assert.That(couplings[0].Health, Is.EqualTo(60f), "표적이 아니므로 체력 변화 없음");
        }

        [Test]
        public void 후방_칸_파괴로_뒤_연결부가_죽으면_앞_연결부가_표적이_된다()
        {
            // 칸2 파괴 → 연결부 1·2 절단 + 칸3 이탈 → 연결부 0이 유일하게 살아 있는 연결부 = 표적.
            CarState[] cars = BuildTrain(4);
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(4, 60f);

            TrainStateLogic.DestroyAndDetach(cars, 2);
            TrainStateLogic.BreakCoupling(couplings, 1);
            TrainStateLogic.BreakCoupling(couplings, 2);

            Assert.That(TrainStateLogic.IsCouplingTargetable(couplings, cars, 0), Is.True);
        }

        [Test]
        public void 단일_연결부_절단은_범위밖_이미끊김을_방어한다()
        {
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(3, 60f);

            Assert.That(TrainStateLogic.BreakCoupling(couplings, 0), Is.True);
            Assert.That(couplings[0].Broken, Is.True);
            Assert.That(TrainStateLogic.BreakCoupling(couplings, 0), Is.False, "이미 끊김");
            Assert.That(TrainStateLogic.BreakCoupling(couplings, 5), Is.False, "범위 밖");
        }

        [Test]
        public void 칸_파괴는_인접_앞뒤_연결부를_함께_끊는다()
        {
            // 칸2 파괴 → 연결부1(칸1-칸2)·연결부2(칸2-칸3) 절단. 연결부0(기관차-칸1)은 유지.
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(4, 60f);

            bool frontBroke = TrainStateLogic.BreakCoupling(couplings, 2 - 1);
            bool rearBroke = TrainStateLogic.BreakCoupling(couplings, 2);

            Assert.That(frontBroke, Is.True);
            Assert.That(rearBroke, Is.True);
            Assert.That(couplings[0].Broken, Is.False, "떨어진 칸과 무관한 앞 연결부는 유지");
            Assert.That(couplings[1].Broken, Is.True);
            Assert.That(couplings[2].Broken, Is.True);
        }

        // ── 칸 위 건축물 (§M3 — 건축물 개별 파괴) ──────────────────

        private static CarType[] OrderWithGreenhouse()
        {
            return new[] { CarType.Locomotive, CarType.Standard, CarType.Greenhouse };
        }

        [Test]
        public void 초기_건축물은_온실칸에만_있고_최대체력이다()
        {
            StructureState[] structures = TrainStateLogic.BuildInitialStructures(OrderWithGreenhouse(), 50f);

            Assert.That(structures.Length, Is.EqualTo(3));
            Assert.That(structures[0].Present, Is.False, "기관차에는 건축물이 없다");
            Assert.That(structures[1].Present, Is.False, "일반 화물칸에는 붙박이 건축물이 없다");
            Assert.That(structures[2].Present, Is.True);
            Assert.That(structures[2].Health, Is.EqualTo(50f));
            Assert.That(structures[2].MaxHealth, Is.EqualTo(50f));
        }

        [Test]
        public void 건축물_데미지는_체력을_줄이고_0에서_파괴된다()
        {
            CarState[] cars = TrainStateLogic.BuildInitialCars(OrderWithGreenhouse(), MaxHealthFor);
            StructureState[] structures = TrainStateLogic.BuildInitialStructures(OrderWithGreenhouse(), 50f);

            Assert.That(TrainStateLogic.ApplyStructureDamage(structures, cars, 2, 20f),
                Is.EqualTo(CarDamageResult.Damaged));
            Assert.That(structures[2].Health, Is.EqualTo(30f).Within(0.001f));

            Assert.That(TrainStateLogic.ApplyStructureDamage(structures, cars, 2, 999f),
                Is.EqualTo(CarDamageResult.Destroyed));
            Assert.That(structures[2].Health, Is.EqualTo(0f));
            Assert.That(TrainStateLogic.IsStructureAlive(structures[2]), Is.False);
        }

        [Test]
        public void 건축물_없는_칸과_이탈_칸의_건축물_데미지는_무시된다()
        {
            CarState[] cars = TrainStateLogic.BuildInitialCars(OrderWithGreenhouse(), MaxHealthFor);
            StructureState[] structures = TrainStateLogic.BuildInitialStructures(OrderWithGreenhouse(), 50f);

            Assert.That(TrainStateLogic.ApplyStructureDamage(structures, cars, 1, 10f),
                Is.EqualTo(CarDamageResult.Ignored), "건축물 없는 칸");

            TrainStateLogic.DetachFrom(cars, 2);
            Assert.That(TrainStateLogic.ApplyStructureDamage(structures, cars, 2, 10f),
                Is.EqualTo(CarDamageResult.Ignored), "이탈 칸 위 건축물은 별도 표적이 아니다");
        }

        // ── 수리 (§M3 — 수리 망치. 파괴·이탈된 부위는 되살릴 수 없다) ──────────────────

        [Test]
        public void 칸_수리는_손상분만_회복하고_만피를_넘지_않는다()
        {
            CarState[] cars = BuildTrain(3);
            TrainStateLogic.ApplyDamage(cars, 1, 30f);

            Assert.That(TrainStateLogic.RepairCar(cars, 1, 10f), Is.True);
            Assert.That(cars[1].Health, Is.EqualTo(80f).Within(0.001f));

            Assert.That(TrainStateLogic.RepairCar(cars, 1, 999f), Is.True);
            Assert.That(cars[1].Health, Is.EqualTo(cars[1].MaxHealth));

            Assert.That(TrainStateLogic.RepairCar(cars, 1, 10f), Is.False, "만피는 수리 불가");
        }

        [Test]
        public void 기관차_이탈_칸_파괴_칸은_수리_대상이_아니다()
        {
            CarState[] cars = BuildTrain(4);

            Assert.That(TrainStateLogic.RepairCar(cars, 0, 10f), Is.False, "기관차는 체력 무한 — 수리 무의미");

            TrainStateLogic.ApplyDamage(cars, 3, 30f);
            TrainStateLogic.DetachFrom(cars, 3);
            Assert.That(TrainStateLogic.RepairCar(cars, 3, 10f), Is.False, "이탈 칸");

            TrainStateLogic.ApplyDamage(cars, 2, 30f);
            TrainStateLogic.DestroyAndDetach(cars, 2);
            Assert.That(TrainStateLogic.RepairCar(cars, 2, 10f), Is.False, "파괴 칸");
        }

        [Test]
        public void 연결부_수리는_살아있는_연결부만_가능하고_끊긴_것은_불가다()
        {
            CarState[] cars = BuildTrain(3);
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(3, 60f);
            TrainStateLogic.ApplyCouplingDamage(couplings, cars, 1, 20f);

            Assert.That(TrainStateLogic.RepairCoupling(couplings, cars, 1, 10f), Is.True);
            Assert.That(couplings[1].Health, Is.EqualTo(50f).Within(0.001f));

            TrainStateLogic.ApplyCouplingDamage(couplings, cars, 1, 999f);
            Assert.That(couplings[1].Broken, Is.True);
            Assert.That(TrainStateLogic.RepairCoupling(couplings, cars, 1, 10f), Is.False, "끊긴 연결부는 재결합 미결 — 수리 불가");
        }

        [Test]
        public void 건축물_수리는_손상분만_회복하고_파괴된_건축물은_불가다()
        {
            CarState[] cars = TrainStateLogic.BuildInitialCars(OrderWithGreenhouse(), MaxHealthFor);
            StructureState[] structures = TrainStateLogic.BuildInitialStructures(OrderWithGreenhouse(), 50f);
            TrainStateLogic.ApplyStructureDamage(structures, cars, 2, 20f);

            Assert.That(TrainStateLogic.RepairStructure(structures, cars, 2, 5f), Is.True);
            Assert.That(structures[2].Health, Is.EqualTo(35f).Within(0.001f));

            Assert.That(TrainStateLogic.RepairStructure(structures, cars, 2, 999f), Is.True);
            Assert.That(structures[2].Health, Is.EqualTo(50f), "만피 초과 금지");

            TrainStateLogic.ApplyStructureDamage(structures, cars, 2, 999f);
            Assert.That(TrainStateLogic.RepairStructure(structures, cars, 2, 10f), Is.False, "파괴된 건축물 재건은 M5 범위");
        }

        // ── 칸 증설 (§M3 — 칸 증설/연결) ──────────────────

        [Test]
        public void 증설은_상한_미만이고_전_슬롯이_건재할_때만_가능하다()
        {
            CarState[] cars = BuildTrain(3);

            Assert.That(TrainStateLogic.CanAppendCar(cars, CarType.Greenhouse, 5), Is.True);
            Assert.That(TrainStateLogic.CanAppendCar(cars, CarType.Standard, 5), Is.True);
            Assert.That(TrainStateLogic.CanAppendCar(cars, CarType.Greenhouse, 3), Is.False, "상한 도달");
            Assert.That(TrainStateLogic.CanAppendCar(cars, CarType.Locomotive, 5), Is.False, "기관차는 증설 불가");
        }

        [Test]
        public void 이탈_또는_파괴_슬롯이_있으면_증설_불가다()
        {
            CarState[] detachedTrain = BuildTrain(3);
            TrainStateLogic.DetachFrom(detachedTrain, 2);
            Assert.That(TrainStateLogic.CanAppendCar(detachedTrain, CarType.Greenhouse, 5), Is.False, "이탈 슬롯 뒤에는 못 잇는다");

            CarState[] destroyedTrain = BuildTrain(3);
            TrainStateLogic.DestroyAndDetach(destroyedTrain, 2);
            Assert.That(TrainStateLogic.CanAppendCar(destroyedTrain, CarType.Greenhouse, 5), Is.False, "파괴 슬롯 뒤에는 못 잇는다");
        }

        [Test]
        public void 온실칸_슬롯_생성은_건축물을_포함하고_일반칸은_빈_슬롯이다()
        {
            StructureState greenhouse = TrainStateLogic.MakeStructureFor(CarType.Greenhouse, 50f);
            Assert.That(greenhouse.Present, Is.True);
            Assert.That(greenhouse.Health, Is.EqualTo(50f));

            StructureState standard = TrainStateLogic.MakeStructureFor(CarType.Standard, 50f);
            Assert.That(standard.Present, Is.False);
            Assert.That(standard.Health, Is.EqualTo(0f));
        }
    }
}
