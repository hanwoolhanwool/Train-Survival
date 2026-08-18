using Game.Gameplay.Train;
using NUnit.Framework;
using UnityEngine;

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

        // ── 열차 하부 즉사 존 (M5 6차) — 발자국 안 AND 바퀴 높이 이하 ─────────

        private const float HalfWidth = 1.5f;
        private const float RearZ = -19.5f;
        private const float KillHeight = 1.2f;

        private static bool InKillZone(Vector3 position, float killHeight = KillHeight)
        {
            return TrainLayoutMath.IsInWheelKillZone(position, HalfWidth, RearZ, FrontZ, killHeight);
        }

        [Test]
        public void 발자국_안_바퀴_높이_이하면_즉사_존이다()
        {
            Assert.That(InKillZone(new Vector3(0f, 0f, 0f)), Is.True, "지면 (놓인 기절 몬스터)");
            Assert.That(InKillZone(new Vector3(1.4f, 1.2f, -19f)), Is.True, "경계 안쪽 — 파지 앵커 높이가 걸리는 선");
        }

        [Test]
        public void 발자국_밖이면_즉사_존이_아니다()
        {
            Assert.That(InKillZone(new Vector3(1.6f, 0f, 0f)), Is.False, "열차 옆 — 지상 몬스터의 추격 동선");
            Assert.That(InKillZone(new Vector3(0f, 0f, 20f)), Is.False, "열차 앞");
            Assert.That(InKillZone(new Vector3(0f, 0f, -20f)), Is.False, "열차 뒤");
        }

        [Test]
        public void 바퀴_높이보다_위는_즉사_존이_아니다()
        {
            Assert.That(InKillZone(new Vector3(0f, 1.3f, 0f)), Is.False, "바퀴 위 몸통 높이");
            Assert.That(InKillZone(new Vector3(0f, 3f, 0f)), Is.False, "갑판 위 — 파지한 채 갑판에 서 있어도 안전");
        }

        [Test]
        public void 높이_0이면_존이_비활성이다()
        {
            Assert.That(InKillZone(new Vector3(0f, 0f, 0f), killHeight: 0f), Is.False, "에셋으로 끌 수 있는 축");
        }

        // ── 갑판 낙하의 폭·높이 게이트 (M5 7차 A3) — Z 범위는 IsZOnCar가 칸별 판정 ─────────

        private const float DeckHeight = 3f;
        private const float SurfaceMargin = 0.5f;

        private static bool InAperture(Vector3 position, float halfWidth = HalfWidth)
        {
            return TrainLayoutMath.IsWithinDeckAperture(position, halfWidth, DeckHeight, SurfaceMargin);
        }

        [Test]
        public void 폭_안_갑판_높이_위면_갑판_낙하_게이트를_통과한다()
        {
            Assert.That(InAperture(new Vector3(0f, 4f, 0f)), Is.True, "갑판 위 플레이어 앞 (도착 지점)");
            Assert.That(InAperture(new Vector3(1.5f, 2.6f, 0f)), Is.True, "폭 경계 정확히 위 · 여유 높이 안쪽");
        }

        [Test]
        public void 폭_밖이거나_갑판보다_낮으면_게이트에_걸린다()
        {
            Assert.That(InAperture(new Vector3(2.1f, 4f, 0f)), Is.False, "열차 옆 — 지상 낙하");
            Assert.That(InAperture(new Vector3(0f, 1f, 0f)), Is.False, "지상 높이 — 열차 폭 안이어도 갑판이 아니다");
        }

        // ── 가로 여유 없음 (건축 개편 §7 — C-발견 2) ─────────

        [Test]
        public void 갑판_반폭_밖은_조금만_벗어나도_갑판이_아니다()
        {
            // 판자가 없는 칸의 판자 자리(반폭 바로 밖)에 떨어진 보따리가 갑판 높이에 얹히던 버그.
            Assert.That(InAperture(new Vector3(1.6f, 4f, 0f)), Is.False, "반폭 +0.1 m — 판자 없는 자리");
            Assert.That(InAperture(new Vector3(-1.6f, 4f, 0f)), Is.False, "반대쪽도 같다");
        }

        [Test]
        public void 판자로_넓어진_반폭까지는_갑판이다()
        {
            // 판자 1열(1 m)이 붙으면 그 열 위도 갑판 — 폭 게이트가 실측 반폭을 그대로 따라간다.
            Assert.That(InAperture(new Vector3(1.6f, 4f, 0f), halfWidth: HalfWidth + 1f), Is.True, "판자 열 위");
            Assert.That(InAperture(new Vector3(2.6f, 4f, 0f), halfWidth: HalfWidth + 1f), Is.False, "판자 열 밖");
        }

        // ── 갑판 유효 Z 범위 (건축 개편 §7.2 — 앞뒤 한 행씩 콜라이더 제외) ─────────

        private const float DeckSpanLength = 13f;   // 칸 길이 15 − 앞뒤 1 m

        [Test]
        public void 갑판_유효_길이_안이면_밟는_면이다()
        {
            Assert.That(TrainLayoutMath.IsWithinDeckSpan(0f, 0f, DeckSpanLength), Is.True, "칸 중앙");
            Assert.That(TrainLayoutMath.IsWithinDeckSpan(6.4f, 0f, DeckSpanLength), Is.True, "제외 행 바로 안쪽");
            Assert.That(TrainLayoutMath.IsWithinDeckSpan(-6.5f, 0f, DeckSpanLength), Is.True, "경계 정확히 위");
        }

        [Test]
        public void 칸_안이어도_앞뒤_끝_행은_갑판이_아니다()
        {
            // 칸 귀속 범위(±7.5)에는 들지만 콜라이더가 없는 구간 — 물건이 얹히면 공중에 뜬다.
            Assert.That(TrainLayoutMath.IsWithinDeckSpan(6.6f, 0f, DeckSpanLength), Is.False, "마지막 행");
            Assert.That(TrainLayoutMath.IsWithinDeckSpan(-7.4f, 0f, DeckSpanLength), Is.False, "첫 행");
        }

        [Test]
        public void 갑판_Z_범위는_칸_중심을_따라간다()
        {
            // 이탈로 칸이 뒤로 밀리면 갑판 범위도 함께 밀린다 (중심 인자를 그대로 쓴다).
            const float pushedCenter = -20f;
            Assert.That(TrainLayoutMath.IsWithinDeckSpan(-26f, pushedCenter, DeckSpanLength), Is.True, "밀린 칸 위");
            Assert.That(TrainLayoutMath.IsWithinDeckSpan(-13.4f, pushedCenter, DeckSpanLength), Is.False, "원래 자리");
        }
    }
}
