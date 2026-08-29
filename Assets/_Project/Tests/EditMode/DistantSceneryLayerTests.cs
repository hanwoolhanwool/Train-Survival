using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 원경 시차 레이어의 되감기 계산 (사막 지역 구현 계획 §4.3).
    /// 계수가 눈에 맞는지는 렌더가 정하지만, <b>되감기가 튀지 않는지</b>는 여기서 고정한다 —
    /// 되감는 순간 화면이 어긋나면 대자연이 아니라 미끄러짐으로 읽힌다.
    /// </summary>
    public sealed class DistantSceneryLayerTests
    {
        [Test]
        public void 시작점에서는_밀리지_않는다()
        {
            Assert.AreEqual(0f, DistantSceneryLayer.ResolveOffsetZ(0f, 0.35f, 300f), 1e-4f);
        }

        [Test]
        public void 시차계수만큼만_뒤로_민다()
        {
            // 근경이 100 m 흐르는 동안 중경(0.35)은 35 m만 흐른다 — 이 차이가 깊이를 만든다.
            Assert.AreEqual(-35f, DistantSceneryLayer.ResolveOffsetZ(100f, 0.35f, 300f), 1e-3f);
        }

        [Test]
        public void 되감기_간격에_도달하면_처음으로_돌아온다()
        {
            // 300 m 되감기 × 시차 0.35 → 주행 857.14 m 에서 한 바퀴.
            float oneLap = 300f / 0.35f;
            Assert.AreEqual(0f, DistantSceneryLayer.ResolveOffsetZ(oneLap, 0.35f, 300f), 1e-2f);
            Assert.AreEqual(0f, DistantSceneryLayer.ResolveOffsetZ(oneLap * 3f, 0.35f, 300f), 1e-2f);
        }

        [Test]
        public void 되감기_구간을_벗어나지_않는다()
        {
            // 벗어나면 자식이 카메라 뒤로 사라진 채 돌아오지 않는다.
            for (int i = 0; i < 200; i++)
            {
                float z = DistantSceneryLayer.ResolveOffsetZ(i * 37f, 0.35f, 300f);
                Assert.LessOrEqual(z, 0f);
                Assert.Greater(z, -300f);
            }
        }

        [Test]
        public void 되감기가_없으면_계속_밀린다()
        {
            // 원경 지면판처럼 되감을 필요가 없는 층은 간격 0으로 둔다 (계수도 0이라 실제로는 정지).
            Assert.AreEqual(-500f, DistantSceneryLayer.ResolveOffsetZ(1000f, 0.5f, 0f), 1e-3f);
        }

        [Test]
        public void 시차계수_0은_정지다()
        {
            Assert.AreEqual(0f, DistantSceneryLayer.ResolveOffsetZ(9999f, 0f, 0f), 1e-4f);
        }

        [Test]
        public void 사막_4층의_흐름_속도가_설계값과_같다()
        {
            // §4.1 속도비 6 : 2.1 : 0.6 : 0.18 : 0 — 근경과 산 능선 사이가 33배다.
            Assert.AreEqual(2.1f, DistantSceneryLayer.EffectiveSpeed(6f, 0.35f), 1e-3f);
            Assert.AreEqual(0.6f, DistantSceneryLayer.EffectiveSpeed(6f, 0.10f), 1e-3f);
            Assert.AreEqual(0.18f, DistantSceneryLayer.EffectiveSpeed(6f, 0.03f), 1e-3f);
            Assert.AreEqual(0f, DistantSceneryLayer.EffectiveSpeed(6f, 0f), 1e-4f);
        }

        [Test]
        public void 중경_반복_주기는_팔레트_주기와_어긋난다()
        {
            // 중경 143초 vs 팔레트 재등장 133초 — 두 주기가 어긋나야 합성 화면의 주기가 길어진다.
            float period = DistantSceneryLayer.WrapPeriodSeconds(6f, 0.35f, 300f);
            Assert.AreEqual(142.86f, period, 0.1f);
            Assert.Greater(Mathf.Abs(period - 133f), 5f);
        }

        [Test]
        public void 산_능선은_사막_4일_동안_한_번도_반복되지_않는다()
        {
            // 사막 총 주행 시간 1,560초 (4일). 이 안에 되감기가 오면 "판때기 두 장"으로 읽힌다.
            float period = DistantSceneryLayer.WrapPeriodSeconds(6f, 0.03f, 1200f);
            Assert.AreEqual(6666.67f, period, 1f);
            Assert.Greater(period, 1560f);
        }

        [Test]
        public void 흐르지_않는_층은_되감기_주기가_없다()
        {
            Assert.AreEqual(
                float.PositiveInfinity, DistantSceneryLayer.WrapPeriodSeconds(6f, 0f, 1600f));
            Assert.AreEqual(
                float.PositiveInfinity, DistantSceneryLayer.WrapPeriodSeconds(0f, 0.35f, 300f));
        }
    }
}
