using Game.Gameplay.Train;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 칸 건설 조준 순수 계산 검증 (M3 피드백 — 건설 포트의 망치 통합).
    /// 건설 지점 좌표와 시선 조준 성립(사거리·정렬·가림) 규칙을 확인한다.
    /// </summary>
    public sealed class CarBuildAimLogicTests
    {
        private const float CarLength = 12f;
        private const float CouplingGap = 1.5f;
        private const float CarWidth = 3f;
        private const float DeckHeight = 3f;

        [Test]
        public void 건설_지점은_슬롯_앞_연결부_중앙이다()
        {
            // 슬롯 중심에서 칸 절반 + 연결 간격 절반만큼 앞(+Z).
            Assert.That(CarBuildAimLogic.AnchorZ(-20f, CarLength, CouplingGap),
                Is.EqualTo(-20f + 6f + 0.75f).Within(0.001f));
        }

        [Test]
        public void 건설_지점_수식은_기존_포트의_후미_연결_지점과_일치한다()
        {
            // 포트 수식: 후미 칸 중심 − 칸 절반 − 간격 절반. 증설 슬롯 중심 = 후미 칸 중심 − (칸 + 간격).
            float rearCarCenterZ = -13.5f;
            float slotCenterZ = rearCarCenterZ - (CarLength + CouplingGap);
            float portZ = rearCarCenterZ - CarLength * 0.5f - CouplingGap * 0.5f;

            Assert.That(CarBuildAimLogic.AnchorZ(slotCenterZ, CarLength, CouplingGap),
                Is.EqualTo(portZ).Within(0.001f));
        }

        [Test]
        public void 건설_지점은_씬의_연결부_오브젝트_위치와_겹친다()
        {
            // 씬 기준(3칸 편성·칸 12 m·간격 1.5 m): 선두 z=19.5 → 슬롯 3 중심 z=-27 → 건설 지점 z=-20.25.
            // 이 자리에는 아직 짓지 않은 칸의 연결부(Connector_3)가 있다 — 그 표현·콜라이더가 꺼져 있어야
            // 조준 레이가 허깨비 부위를 맞지 않는다(2026-08-01 발견 버그).
            float slot3CenterZ = 19.5f - CarLength * 0.5f - 3f * (CarLength + CouplingGap);

            Assert.That(slot3CenterZ, Is.EqualTo(-27f).Within(0.001f));
            Assert.That(CarBuildAimLogic.AnchorZ(slot3CenterZ, CarLength, CouplingGap),
                Is.EqualTo(-20.25f).Within(0.001f));
        }

        [Test]
        public void 건설_부피는_슬롯_자리의_칸_한_칸_크기다()
        {
            CarBuildAimLogic.BuildVolume(-27f, CarWidth, DeckHeight, CarLength,
                out Vector3 center, out Vector3 size);

            // 열차는 원점 고정(x=0)이고 바닥이 y=0에 닿아 갑판이 DeckHeight에 온다.
            Assert.That(center, Is.EqualTo(new Vector3(0f, DeckHeight * 0.5f, -27f)));
            Assert.That(size, Is.EqualTo(new Vector3(CarWidth, DeckHeight, CarLength)));
        }

        [Test]
        public void 건설_지점은_건설_부피의_앞면_바깥에_있다()
        {
            // 겨누는 지점(연결부)은 지어질 칸 앞면(+Z)보다 연결 간격 절반만큼 더 앞이다 —
            // 조준 지점이 부피 안에 들어가면 자기 몸이 늘 자리를 막는 것처럼 보인다.
            const float slotCenterZ = -27f;
            CarBuildAimLogic.BuildVolume(slotCenterZ, CarWidth, DeckHeight, CarLength, out _, out Vector3 size);
            float frontFaceZ = slotCenterZ + size.z * 0.5f;

            Assert.That(CarBuildAimLogic.AnchorZ(slotCenterZ, CarLength, CouplingGap),
                Is.GreaterThan(frontFaceZ));
        }

        [Test]
        public void 사거리_안에서_정면으로_겨누면_조준이_성립한다()
        {
            var origin = new Vector3(0f, 2.5f, 0f);
            var anchor = new Vector3(0f, 2.5f, -3f);

            Assert.That(CarBuildAimLogic.IsAiming(origin, Vector3.back, anchor,
                4f, 0.85f, float.PositiveInfinity), Is.True);
        }

        [Test]
        public void 사거리를_벗어나면_조준이_성립하지_않는다()
        {
            var origin = new Vector3(0f, 2.5f, 0f);
            var anchor = new Vector3(0f, 2.5f, -5f);

            Assert.That(CarBuildAimLogic.IsAiming(origin, Vector3.back, anchor,
                4f, 0.85f, float.PositiveInfinity), Is.False);
        }

        [Test]
        public void 시선이_빗나가면_조준이_성립하지_않는다()
        {
            var origin = new Vector3(0f, 2.5f, 0f);
            var anchor = new Vector3(0f, 2.5f, -3f);

            Assert.That(CarBuildAimLogic.IsAiming(origin, Vector3.right, anchor,
                4f, 0.85f, float.PositiveInfinity), Is.False);
        }

        [Test]
        public void 더_가까운_물리_표적이_시야를_막으면_조준이_성립하지_않는다()
        {
            var origin = new Vector3(0f, 2.5f, 0f);
            var anchor = new Vector3(0f, 2.5f, -3f);

            Assert.That(CarBuildAimLogic.IsAiming(origin, Vector3.back, anchor,
                4f, 0.85f, 1.5f), Is.False);
        }

        [Test]
        public void 표적이_건설_지점보다_멀면_조준을_막지_않는다()
        {
            var origin = new Vector3(0f, 2.5f, 0f);
            var anchor = new Vector3(0f, 2.5f, -3f);

            Assert.That(CarBuildAimLogic.IsAiming(origin, Vector3.back, anchor,
                4f, 0.85f, 10f), Is.True);
        }
    }
}
