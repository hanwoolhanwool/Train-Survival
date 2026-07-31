using Game.Gameplay.Train;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 이탈 칸 재결합 조준 순수 계산 검증 (손잡이-이탈저항 스펙 §4.1).
    /// 겨눌 칸 선택·연결부 자리 프리뷰 부피·안내 문구 우선순위를 확인한다.
    /// </summary>
    public sealed class CarRecoupleAimLogicTests
    {
        private const float CarLength = 12f;
        private const float CouplingGap = 1.5f;
        private const float CarWidth = 3f;
        private const float DeckHeight = 3f;
        private const float LostDistance = 45f;

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
        public void 이탈_칸이_없으면_겨눌_대상이_없다()
        {
            CarState[] cars = BuildTrain(3);

            Assert.That(CarRecoupleAimLogic.FindRecoupleTarget(cars, new float[3], LostDistance),
                Is.EqualTo(-1));
        }

        [Test]
        public void 겨눌_대상은_선두부터_첫_이탈_칸이다()
        {
            CarState[] cars = BuildTrain(4);
            TrainStateLogic.DetachFrom(cars, 2);

            // 칸2·칸3이 함께 이탈 — 앞에서부터 순차로 붙이므로 겨눌 대상은 칸2다.
            Assert.That(CarRecoupleAimLogic.FindRecoupleTarget(cars, new float[4], LostDistance),
                Is.EqualTo(2));
        }

        [Test]
        public void 소실_거리를_넘긴_칸은_건너뛴다()
        {
            CarState[] cars = BuildTrain(4);
            TrainStateLogic.DetachFrom(cars, 2);

            // 칸2는 이미 소실 거리 밖(그 자리는 칸 건설 후보가 된다) — 다음 이탈 칸인 칸3이 대상이다.
            var offsets = new[] { 0f, 0f, 50f, 12f };
            Assert.That(CarRecoupleAimLogic.FindRecoupleTarget(cars, offsets, LostDistance),
                Is.EqualTo(3));
        }

        [Test]
        public void 파괴된_칸은_회수_대상이_아니다()
        {
            CarState[] cars = BuildTrain(4);
            TrainStateLogic.DestroyAndDetach(cars, 2);

            // 칸2는 파괴(재건 대상), 칸3은 이탈(회수 대상).
            Assert.That(CarRecoupleAimLogic.FindRecoupleTarget(cars, new float[4], LostDistance),
                Is.EqualTo(3));
        }

        [Test]
        public void 프리뷰_부피는_이어질_연결부_자리다()
        {
            CarRecoupleAimLogic.CouplingVolume(-27f, CarLength, CouplingGap, CarWidth, DeckHeight,
                out Vector3 center, out Vector3 size);

            // 중심은 칸 건설과 같은 조준 지점(앞 연결부 중앙), 깊이는 연결 간격 — 칸이 아니라 간극을 강조한다.
            Assert.That(center.z, Is.EqualTo(CarBuildAimLogic.AnchorZ(-27f, CarLength, CouplingGap)).Within(0.001f));
            Assert.That(center, Is.EqualTo(new Vector3(0f, DeckHeight * 0.5f, -20.25f)));
            Assert.That(size, Is.EqualTo(new Vector3(CarWidth, DeckHeight, CouplingGap)));
        }

        [Test]
        public void 앞_칸이_없으면_다른_이유보다_먼저_알린다()
        {
            // 구조적 순서가 최우선 — 자원이 있든 없든, 끌어왔든 아니든 앞 칸부터 채워야 한다.
            Assert.That(CarRecoupleAimLogic.ResolvePrompt(false, 0f, true),
                Is.EqualTo(RecouplePrompt.FrontCarMissing));
            Assert.That(CarRecoupleAimLogic.ResolvePrompt(false, 12f, false),
                Is.EqualTo(RecouplePrompt.FrontCarMissing));
        }

        [Test]
        public void 아직_끌어오는_중이면_남은_거리를_먼저_알린다()
        {
            Assert.That(CarRecoupleAimLogic.ResolvePrompt(true, 12f, false),
                Is.EqualTo(RecouplePrompt.NotAtSlot), "자원 부족보다 진행 상황이 먼저");
            Assert.That(CarRecoupleAimLogic.ResolvePrompt(true, 12f, true),
                Is.EqualTo(RecouplePrompt.NotAtSlot));
        }

        [Test]
        public void 슬롯에_닿으면_자원_충족_여부로_갈린다()
        {
            Assert.That(CarRecoupleAimLogic.ResolvePrompt(true, 0f, false),
                Is.EqualTo(RecouplePrompt.InsufficientResources));
            Assert.That(CarRecoupleAimLogic.ResolvePrompt(true, 0f, true),
                Is.EqualTo(RecouplePrompt.Ready));
        }

        [Test]
        public void 표시_보간_잔차는_슬롯_도달로_본다()
        {
            // 클라이언트 표시 오프셋은 재시뮬 보간 값이라 정확히 0으로 떨어지지 않는다 —
            // 허용치 안이면 도달로 보지 않으면 "붙일 수 있는데 안내는 못 붙는다"가 된다.
            Assert.That(CarRecoupleAimLogic.ResolvePrompt(true, CarRecoupleAimLogic.SlotArrivalEpsilon, true),
                Is.EqualTo(RecouplePrompt.Ready));
            Assert.That(CarRecoupleAimLogic.ResolvePrompt(true, CarRecoupleAimLogic.SlotArrivalEpsilon + 0.01f, true),
                Is.EqualTo(RecouplePrompt.NotAtSlot));
        }
    }
}
