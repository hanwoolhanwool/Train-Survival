using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 열차 레이아웃 좌표 계산 검증 — "이 Z가 그 칸 위인가"가 차폐(체온) 판정의 입력이다.
    /// 이탈 칸은 슬롯에서 뒤로 밀려나므로 판정은 이탈 오프셋을 반영해야 한다(M4 D7).
    /// 기본 편성(칸 3개 · 길이 12 m · 연결부 간격 1.5 m) 기준.
    /// </summary>
    public sealed class TrainLayoutMathTests
    {
        private const float CarLength = 12f;
        private const float CouplingGap = 1.5f;
        private const int CarCount = 3;

        // 총 길이 = 3×12 + 2×1.5 = 39 → 선두 Z = 19.5
        private const float FrontZ = 19.5f;

        private static float CenterZ(int index, float ejectOffset = 0f)
        {
            return TrainLayoutMath.GetCarCenterZ(index, FrontZ, CarLength, CouplingGap, ejectOffset);
        }

        private static bool OnCar(float z, int index, float ejectOffset = 0f)
        {
            return TrainLayoutMath.IsZOnCar(z, index, FrontZ, CarLength, CouplingGap, ejectOffset);
        }

        [Test]
        public void 칸_중심_Z는_선두에서_뒤로_일정_간격으로_배치된다()
        {
            Assert.That(TrainLayoutMath.GetCarCenterZ(0, FrontZ, CarLength, CouplingGap), Is.EqualTo(13.5f).Within(0.001f));
            Assert.That(TrainLayoutMath.GetCarCenterZ(1, FrontZ, CarLength, CouplingGap), Is.EqualTo(0f).Within(0.001f));
            Assert.That(TrainLayoutMath.GetCarCenterZ(2, FrontZ, CarLength, CouplingGap), Is.EqualTo(-13.5f).Within(0.001f));
        }

        [Test]
        public void 이탈_오프셋만큼_실제_중심이_뒤로_밀린다()
        {
            Assert.That(CenterZ(1, ejectOffset: 10f), Is.EqualTo(-10f).Within(0.001f));
            Assert.That(CenterZ(1, ejectOffset: -3f), Is.EqualTo(0f).Within(0.001f), "음수 오프셋은 0으로 클램프");
        }

        [Test]
        public void 이탈_오프셋으로_밀린_칸의_실제_중심에서_그_칸으로_판정된다()
        {
            Assert.That(OnCar(CenterZ(1, 10f), 1, ejectOffset: 10f), Is.True);
        }

        [Test]
        public void 이탈_후_원래_슬롯_위치는_더_이상_그_칸이_아니다()
        {
            // 회귀 방지 — 슬롯 기준 역산이던 시절에는 이 위치가 통과해 버렸다.
            Assert.That(OnCar(CenterZ(1), 1, ejectOffset: 10f), Is.False);
        }

        [Test]
        public void 이탈_칸_가장자리_안쪽도_그_칸이다()
        {
            // 플레이어가 서 있는 위치(중심~가장자리)에 따라 판정 경계가 달라지면 안 된다.
            Assert.That(OnCar(CenterZ(1, 10f) + 5.9f, 1, ejectOffset: 10f), Is.True, "앞쪽 가장자리 안쪽");
            Assert.That(OnCar(CenterZ(1, 10f) - 5.9f, 1, ejectOffset: 10f), Is.True, "뒤쪽 가장자리 안쪽");
        }

        [Test]
        public void 오프셋이_pitch를_넘어도_뒤_칸으로_오인되지_않는다()
        {
            // 연쇄 이탈 규칙상 앞 칸이 이탈하면 뒤 칸도 같이 밀린다 — 뒤 칸 판정도 자기 오프셋을 반영하므로
            // 앞 칸 위의 Z가 뒤 칸의 (밀려난) 갑판 범위에 들어가지 않는다.
            float z = CenterZ(1, 15f);

            Assert.That(OnCar(z, 1, ejectOffset: 15f), Is.True, "자기 칸");
            Assert.That(OnCar(z, 2, ejectOffset: 15f), Is.False, "함께 밀린 뒤 칸");
        }

        [Test]
        public void 오프셋_0이면_기존_판정과_동일하다()
        {
            Assert.That(OnCar(CenterZ(0), 0), Is.True, "칸 중심");
            Assert.That(OnCar(CenterZ(0) + 5.9f, 0), Is.True, "가장자리 안쪽");

            // 0번 칸 후단 7.5 ~ 1번 칸 전단 6.0 사이가 연결부 간격 — 어느 칸에도 속하지 않는다.
            Assert.That(OnCar(6.75f, 0), Is.False, "연결부 간격 위 (앞 칸 기준)");
            Assert.That(OnCar(6.75f, 1), Is.False, "연결부 간격 위 (뒤 칸 기준)");

            for (int i = 0; i < CarCount; i++)
            {
                Assert.That(OnCar(FrontZ + 10f, i), Is.False, "열차 앞");
                Assert.That(OnCar(-30f, i), Is.False, "열차 뒤");
            }
        }

        [Test]
        public void 잘못된_규격은_안전하게_실패한다()
        {
            Assert.That(TrainLayoutMath.IsZOnCar(0f, 1, FrontZ, 0f, CouplingGap, 0f), Is.False, "칸 길이 0");
            Assert.That(TrainLayoutMath.IsZOnCar(0f, -1, FrontZ, CarLength, CouplingGap, 0f), Is.False, "음수 인덱스");
        }
    }
}
