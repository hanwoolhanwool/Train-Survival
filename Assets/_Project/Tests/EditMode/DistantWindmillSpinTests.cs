using Game.Gameplay.World;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 원경 풍차 회전 (대초원 지역 구현 계획 §4.6 · 결정 ⑥).
    /// 날개가 도는 것이 눈에 읽히는지는 렌더가 정하지만, <b>각도가 누적으로 무너지지 않는 것</b>과
    /// <b>회전이 시차보다 압도적으로 빠르다는 결정 근거</b>는 여기서 고정한다.
    /// </summary>
    public sealed class DistantWindmillSpinTests
    {
        [Test]
        public void 시작_위상이_0이면_처음_각도도_0이다()
        {
            Assert.AreEqual(0f, DistantWindmillSpin.ResolveAngle(0f, 60f, 0f), 1e-4f);
        }

        [Test]
        public void 십_rpm은_초당_육십도다()
        {
            // 10 rpm = 600 °/min = 60 °/s — 계획 §4.6의 기준값.
            Assert.AreEqual(60f, DistantWindmillSpin.ReferenceDegreesPerSecond, 1e-4f);
            Assert.AreEqual(30f, DistantWindmillSpin.ResolveAngle(0.5f, 60f, 0f), 1e-3f);
        }

        [Test]
        public void 한_바퀴에서_정확히_0으로_돌아온다()
        {
            // 60 °/s 로 6초면 360° — 되감기가 있어야 오래 돌아도 정밀도가 안 무너진다.
            Assert.AreEqual(0f, DistantWindmillSpin.ResolveAngle(6f, 60f, 0f), 1e-3f);
        }

        [Test]
        public void 각도는_항상_0에서_360_사이다()
        {
            for (int i = 0; i < 400; i++)
            {
                float elapsed = i * 3.7f;
                float angle = DistantWindmillSpin.ResolveAngle(elapsed, 63.5f, 41f);
                Assert.GreaterOrEqual(angle, 0f);
                Assert.LessOrEqual(angle, 360f);
            }
        }

        [Test]
        public void 시작_위상이_다르면_같은_시각에_다른_각도다()
        {
            // 군락 6기가 한 몸처럼 돌면 "회전하는 하나의 물체"로 읽힌다 — 위상이 갈려야 한다.
            float a = DistantWindmillSpin.ResolveAngle(12f, 60f, 0f);
            float b = DistantWindmillSpin.ResolveAngle(12f, 60f, 47f);
            Assert.Greater(UnityEngine.Mathf.Abs(a - b), 1f);
        }

        [Test]
        public void 속도를_십_퍼센트_흔들면_한_바퀴_안에_벌어진다()
        {
            // ±10 % 면 10 초 만에 120° 벌어진다 — 두 바퀴 안에 군락이 흩어진다.
            float slow = DistantWindmillSpin.ResolveAngle(10f, 54f, 0f);
            float fast = DistantWindmillSpin.ResolveAngle(10f, 66f, 0f);
            Assert.AreEqual(180f, slow, 1e-2f);
            Assert.AreEqual(300f, fast, 1e-2f);
        }

        [Test]
        public void 회전이_시차_이동보다_사천배_넘게_빠르다()
        {
            // 결정 ⑥의 근거 — 800 m 에서 시차(0.18 m/s)는 0.013 °/s 라 정지 사진과 구분되지 않는다.
            float ratio = DistantWindmillSpin.AngularSpeedRatioOverParallax(60f, 0.18f, 800f);
            Assert.Greater(ratio, 4000f);
            Assert.Less(ratio, 5500f);
        }

        [Test]
        public void 거리가_0이면_비율이_무한이다()
        {
            Assert.IsTrue(float.IsPositiveInfinity(
                DistantWindmillSpin.AngularSpeedRatioOverParallax(60f, 0.18f, 0f)));
        }
    }
}
