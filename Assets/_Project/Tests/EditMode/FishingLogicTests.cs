using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 낚시 계산 검증 (바다 지역 구현 계획 §7.3).
    /// 명제는 <b>"빨리 달릴수록 잘 물린다"</b> — 끌낚시라 속도가 곧 미끼다.
    /// </summary>
    public sealed class FishingLogicTests
    {
        private const float Reference = 6f;      // 기본 스크롤 속도
        private const float MinDelay = 2.5f;
        private const float MaxDelay = 12f;
        private const float Influence = 0.8f;
        private const float Window = 1.2f;

        private static float Delay(float roll, float speed)
        {
            return FishingLogic.BiteDelaySeconds(roll, speed, Reference, MinDelay, MaxDelay, Influence);
        }

        // ── 끌낚시의 명제 ──

        [Test]
        public void 빠를수록_입질이_빨라진다()
        {
            Assert.Less(Delay(0.5f, Reference), Delay(0.5f, Reference * 0.5f));
            Assert.Less(Delay(0.5f, Reference * 0.5f), Delay(0.5f, 0f));
        }

        [Test]
        public void 정지_중에는_거의_안_물린다()
        {
            // 속도 0이면 상한이 그대로다 — 최악의 경우 maxDelay 전부를 기다린다.
            Assert.AreEqual(MaxDelay, Delay(1f, 0f), 1e-3f);
        }

        [Test]
        public void 속도가_기준을_넘어도_최소_대기보다_짧아지지_않는다()
        {
            Assert.GreaterOrEqual(Delay(0f, Reference * 10f), MinDelay - 1e-4f);
            Assert.GreaterOrEqual(Delay(1f, Reference * 10f), MinDelay - 1e-4f);
        }

        [Test]
        public void 속도에_대해_단조_감소한다()
        {
            float previous = Delay(0.5f, 0f);
            for (float s = 0.5f; s <= 12f; s += 0.5f)
            {
                float current = Delay(0.5f, s);
                Assert.LessOrEqual(current, previous + 1e-5f, $"속도 {s}에서 대기가 늘었다");
                previous = current;
            }
        }

        // ── 난수 대응 ──

        [Test]
        public void 난수_0이면_최소_대기다()
        {
            Assert.AreEqual(MinDelay, Delay(0f, Reference), 1e-3f);
            Assert.AreEqual(MinDelay, Delay(0f, 0f), 1e-3f);
        }

        [Test]
        public void 난수가_클수록_오래_기다린다()
        {
            Assert.Less(Delay(0.1f, Reference), Delay(0.9f, Reference));
        }

        [Test]
        public void 난수는_0_1_밖이어도_잘린다()
        {
            Assert.AreEqual(Delay(0f, Reference), Delay(-3f, Reference), 1e-4f);
            Assert.AreEqual(Delay(1f, Reference), Delay(7f, Reference), 1e-4f);
        }

        // ── 방어 ──

        [Test]
        public void 최소가_최대보다_크면_최소로_눌린다()
        {
            float d = FishingLogic.BiteDelaySeconds(1f, Reference, Reference, 8f, 3f, Influence);
            Assert.AreEqual(8f, d, 1e-3f);
        }

        [Test]
        public void 기준_속도가_0이면_속도_효과가_없다()
        {
            float fast = FishingLogic.BiteDelaySeconds(1f, 100f, 0f, MinDelay, MaxDelay, Influence);
            Assert.AreEqual(MaxDelay, fast, 1e-3f);
        }

        // ── 챔질 창 ──

        [Test]
        public void 입질_직후는_챔질_성공이다()
        {
            Assert.IsTrue(FishingLogic.IsWithinHookWindow(0f, Window));
            Assert.IsTrue(FishingLogic.IsWithinHookWindow(Window * 0.5f, Window));
        }

        [Test]
        public void 창_경계는_포함이다()
        {
            Assert.IsTrue(FishingLogic.IsWithinHookWindow(Window, Window));
        }

        [Test]
        public void 창을_넘기면_놓친다()
        {
            Assert.IsFalse(FishingLogic.IsWithinHookWindow(Window + 0.01f, Window));
        }

        [Test]
        public void 입질_전_음수_시간은_실패다()
        {
            Assert.IsFalse(FishingLogic.IsWithinHookWindow(-0.5f, Window));
        }

        // ── 마릿수 ──

        [Test]
        public void 기본은_한_마리다()
        {
            Assert.AreEqual(1, FishingLogic.CatchCount(0.9f, 0.15f));
        }

        [Test]
        public void 낮은_난수는_두_마리다()
        {
            Assert.AreEqual(2, FishingLogic.CatchCount(0.05f, 0.15f));
        }

        [Test]
        public void 확률_0이면_항상_한_마리다()
        {
            Assert.AreEqual(1, FishingLogic.CatchCount(0f, 0f));
        }

        // ── 던질 자리 (조준선 × 물면) ──

        [Test]
        public void 아래를_보면_물면에_닿는다()
        {
            // 갑판 눈높이 5.17 에서 물면 -4 까지 = 9.17 m 아래.
            float d = FishingLogic.DistanceToWaterPlane(new Vector3(0f, 5.17f, 0f), Vector3.down, -4f);
            Assert.AreEqual(9.17f, d, 1e-3f);
        }

        [Test]
        public void 위를_보면_닿지_않는다()
        {
            Assert.AreEqual(-1f, FishingLogic.DistanceToWaterPlane(new Vector3(0f, 5f, 0f), Vector3.up, -4f));
        }

        [Test]
        public void 수평이면_닿지_않는다()
        {
            Assert.AreEqual(-1f, FishingLogic.DistanceToWaterPlane(new Vector3(0f, 5f, 0f), Vector3.forward, -4f));
        }

        [Test]
        public void 비스듬히_보면_더_멀리_떨어진다()
        {
            var origin = new Vector3(0f, 5.17f, 0f);
            float steep = FishingLogic.DistanceToWaterPlane(origin, Vector3.down, -4f);
            float shallow = FishingLogic.DistanceToWaterPlane(origin, new Vector3(0f, -1f, 1f).normalized, -4f);
            Assert.Greater(shallow, steep);
        }

        [Test]
        public void 사거리를_넘으면_못_던진다()
        {
            var origin = new Vector3(0f, 5.17f, 0f);
            var almostFlat = new Vector3(0f, -0.05f, 1f).normalized;
            Assert.IsFalse(FishingLogic.CanCast(origin, almostFlat, -4f, 25f));
            Assert.IsTrue(FishingLogic.CanCast(origin, Vector3.down, -4f, 25f));
        }

        [Test]
        public void 물속에서_위를_봐도_던질_수_없다()
        {
            // 잠수 중 낚시는 4차 이후다 (작살).
            Assert.IsFalse(FishingLogic.CanCast(new Vector3(0f, -6f, 0f), Vector3.up, -4f, 25f));
        }

        // ── 밸런싱 기준점 ──

        [Test]
        public void 전속_기대_어획이_정지보다_많다()
        {
            float running = FishingLogic.ExpectedCatchesPerMinute(Reference, Reference, MinDelay, MaxDelay, Influence);
            float stopped = FishingLogic.ExpectedCatchesPerMinute(0f, Reference, MinDelay, MaxDelay, Influence);
            Assert.Greater(running, stopped);
        }

        [Test]
        public void 전속_기대_어획은_분당_약_17마리다()
        {
            // roll 0.5 · 속도 6 → 상한이 4.4초로 당겨지고 대기 = 3.45초 → 분당 17.4마리.
            // 밸런싱이 바뀌면 이 수치가 먼저 흔들린다. **상한이라 실제보다 후하다** —
            // 챔질 실패와 다른 할 일(방어·건축·연료)이 실제 어획을 깎는다.
            float perMinute = FishingLogic.ExpectedCatchesPerMinute(Reference, Reference, MinDelay, MaxDelay, Influence);
            Assert.AreEqual(17.4f, perMinute, 0.3f);
        }

        [Test]
        public void 정지_기대_어획은_전속의_절반_아래다()
        {
            float running = FishingLogic.ExpectedCatchesPerMinute(Reference, Reference, MinDelay, MaxDelay, Influence);
            float stopped = FishingLogic.ExpectedCatchesPerMinute(0f, Reference, MinDelay, MaxDelay, Influence);
            Assert.Less(stopped, running * 0.5f, "속도가 낚시 전략이 되려면 차이가 뚜렷해야 한다");
        }
    }
}
