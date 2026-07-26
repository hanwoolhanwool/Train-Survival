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
            // 연결부 1(칸1-칸2 사이) 파괴 → 칸2 이하 이탈. 칸들은 멀쩡(체력 유지).
            CarState[] cars = BuildTrain(4);
            CouplingState[] couplings = TrainStateLogic.BuildInitialCouplings(4, 60f);

            TrainStateLogic.ApplyCouplingDamage(couplings, cars, 1, 999f);
            int[] detached = TrainStateLogic.DetachFrom(cars, 1 + 1);

            Assert.That(detached, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(cars[1].Attached, Is.True, "연결부 앞 칸은 유지");
            Assert.That(cars[2].Attached, Is.False);
            Assert.That(cars[2].Health, Is.GreaterThan(0f), "이탈 칸은 파괴가 아니라 멀쩡히 떨어져 나감");
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
    }
}
