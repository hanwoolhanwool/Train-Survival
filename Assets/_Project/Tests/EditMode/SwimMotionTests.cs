using Game.Gameplay.Player;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 수영·잠수 계산 검증 (바다 지역 구현 계획 §6.1).
    /// 이 도메인의 명제는 하나다 — <b>수면에서는 못 돌아오고, 잠수해야 돌아온다.</b>
    /// </summary>
    public sealed class SwimMotionTests
    {
        // 바다 규격 (계획 §3.1) — 물면 −4, 해저 −12
        private const float WaterY = -4f;
        private const float ScrollSpeed = 6f;      // WorldScrollSettings._baseScrollSpeed

        private const float EnterDepth = 1f;
        private const float ExitDepth = 0.2f;
        private const float StartDepth = 1.8f;     // 머리가 잠기는 깊이
        private const float FullDepth = 3.5f;
        private const float SubmergedFactor = 0.4f;

        private const float SwimSpeed = 3.5f;
        private const float SwimVertical = 2f;
        private const float Buoyancy = 0.6f;

        private static float Factor(float depth)
        {
            return SwimMotion.ScrollFactor(depth, StartDepth, FullDepth, SubmergedFactor);
        }

        // ── 잠김 깊이 ──

        [Test]
        public void 발이_물_위면_깊이가_음수다()
        {
            Assert.Less(SwimMotion.SubmergeDepth(-2f, WaterY), 0f);
        }

        [Test]
        public void 발이_물_아래면_깊이가_양수다()
        {
            Assert.AreEqual(3f, SwimMotion.SubmergeDepth(-7f, WaterY), 1e-4f);
        }

        // ── 수영 판정 ──

        [Test]
        public void 물_위에서는_수영이_아니다()
        {
            Assert.IsFalse(SwimMotion.IsSwimming(0f, WaterY, false, EnterDepth, ExitDepth));
        }

        [Test]
        public void 진입_깊이를_넘으면_수영이_시작된다()
        {
            Assert.IsTrue(SwimMotion.IsSwimming(WaterY - EnterDepth, WaterY, false, EnterDepth, ExitDepth));
        }

        [Test]
        public void 진입_직전_깊이에서는_아직_걷는다()
        {
            Assert.IsFalse(SwimMotion.IsSwimming(WaterY - 0.9f, WaterY, false, EnterDepth, ExitDepth));
        }

        [Test]
        public void 히스테리시스_수영_중_얕아져도_이탈_깊이까지는_유지된다()
        {
            // 진입(1.0)과 이탈(0.2) 사이 — 걷는 상태였다면 수영이 아니지만, 수영 중이었다면 유지된다.
            float footY = WaterY - 0.5f;
            Assert.IsTrue(SwimMotion.IsSwimming(footY, WaterY, true, EnterDepth, ExitDepth));
            Assert.IsFalse(SwimMotion.IsSwimming(footY, WaterY, false, EnterDepth, ExitDepth));
        }

        [Test]
        public void 이탈_깊이보다_얕아지면_수영이_끝난다()
        {
            Assert.IsFalse(SwimMotion.IsSwimming(WaterY - 0.1f, WaterY, true, EnterDepth, ExitDepth));
        }

        // ── 물살 배율 ──

        [Test]
        public void 수면_근처에서는_물살이_감쇠하지_않는다()
        {
            Assert.AreEqual(1f, Factor(0.5f), 1e-4f);
            Assert.AreEqual(1f, Factor(StartDepth), 1e-4f);
        }

        [Test]
        public void 충분히_깊으면_물살이_최소_배율까지_준다()
        {
            Assert.AreEqual(SubmergedFactor, Factor(FullDepth), 1e-4f);
            Assert.AreEqual(SubmergedFactor, Factor(FullDepth + 10f), 1e-4f);
        }

        [Test]
        public void 물살_배율은_깊어질수록_단조_감소한다()
        {
            float previous = Factor(0f);
            for (float d = 0.25f; d <= 6f; d += 0.25f)
            {
                float current = Factor(d);
                Assert.LessOrEqual(current, previous + 1e-5f, $"깊이 {d}에서 배율이 증가했다");
                previous = current;
            }
        }

        [Test]
        public void 시작_깊이와_완전_깊이가_같으면_즉시_최소_배율이다()
        {
            Assert.AreEqual(SubmergedFactor, SwimMotion.ScrollFactor(2f, 1.8f, 1.8f, SubmergedFactor), 1e-4f);
        }

        // ── 이 도메인의 핵심 명제 ──

        [Test]
        public void 수면_수영은_복귀_불가다()
        {
            float net = SwimMotion.NetForwardSpeed(SwimSpeed, ScrollSpeed, Factor(0.5f));
            Assert.Less(net, 0f, "수면에서 앞으로 갈 수 있으면 잠수할 이유가 사라진다");
        }

        [Test]
        public void 잠수하면_복귀_가능하다()
        {
            float net = SwimMotion.NetForwardSpeed(SwimSpeed, ScrollSpeed, Factor(FullDepth));
            Assert.Greater(net, 0f, "잠수해도 못 돌아오면 물에 들어가는 것이 곧 사망이다");
        }

        [Test]
        public void 잠수가_수면보다_항상_빠르다()
        {
            float surface = SwimMotion.NetForwardSpeed(SwimSpeed, ScrollSpeed, Factor(0.5f));
            float deep = SwimMotion.NetForwardSpeed(SwimSpeed, ScrollSpeed, Factor(FullDepth));
            Assert.Greater(deep, surface);
        }

        // ── 체류 창 (§6.1 표) ──

        [Test]
        public void 감쇠_없는_물살의_체류_창은_약_19_6초다()
        {
            // 기관차 앞(열차 77.7) + 사망선 40 = 117.7 m
            float seconds = SwimMotion.SecondsUntilFallBehind(117.7f, ScrollSpeed, 1f);
            Assert.AreEqual(19.6f, seconds, 0.1f);
        }

        [Test]
        public void 감쇠된_물살의_체류_창은_약_49초다()
        {
            float seconds = SwimMotion.SecondsUntilFallBehind(117.7f, ScrollSpeed, SubmergedFactor);
            Assert.AreEqual(49.0f, seconds, 0.1f);
        }

        [Test]
        public void 물살이_없으면_체류_창은_무한이다()
        {
            Assert.AreEqual(float.PositiveInfinity, SwimMotion.SecondsUntilFallBehind(50f, 0f, 1f));
        }

        [Test]
        public void 이미_한계를_넘었으면_남은_시간은_0이다()
        {
            Assert.AreEqual(0f, SwimMotion.SecondsUntilFallBehind(-5f, ScrollSpeed, 1f), 1e-4f);
        }

        // ── 수직 이동 ──

        [Test]
        public void 입력이_없으면_부력으로_떠오른다()
        {
            Assert.AreEqual(Buoyancy, SwimMotion.ComputeVerticalSpeed(3f, 0, SwimVertical, Buoyancy, EnterDepth), 1e-4f);
        }

        [Test]
        public void 수면에_닿으면_부력이_멈춘다()
        {
            Assert.AreEqual(0f, SwimMotion.ComputeVerticalSpeed(EnterDepth, 0, SwimVertical, Buoyancy, EnterDepth), 1e-4f);
        }

        [Test]
        public void 하강_입력은_깊이와_무관하게_내려간다()
        {
            Assert.AreEqual(-SwimVertical, SwimMotion.ComputeVerticalSpeed(0.5f, -1, SwimVertical, Buoyancy, EnterDepth), 1e-4f);
            Assert.AreEqual(-SwimVertical, SwimMotion.ComputeVerticalSpeed(7f, -1, SwimVertical, Buoyancy, EnterDepth), 1e-4f);
        }

        [Test]
        public void 상승_입력은_수면에서_멈춘다()
        {
            Assert.AreEqual(SwimVertical, SwimMotion.ComputeVerticalSpeed(5f, 1, SwimVertical, Buoyancy, EnterDepth), 1e-4f);
            Assert.AreEqual(0f, SwimMotion.ComputeVerticalSpeed(EnterDepth - 0.1f, 1, SwimVertical, Buoyancy, EnterDepth), 1e-4f);
        }
    }
}
