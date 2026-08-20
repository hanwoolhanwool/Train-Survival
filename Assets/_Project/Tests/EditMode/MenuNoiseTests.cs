using Game.UI.MainMenu;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 로비 연출용 저주파 흔들림 검증 — [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §6.2 · §6.4.
    ///
    /// <para>고정하려는 것 넷 — <b>진폭을 넘지 않는다</b>, <b>프레임 사이에 튀지 않는다</b>,
    /// <b>축끼리 같이 움직이지 않는다</b>, <b>주기가 0이어도 터지지 않는다</b>.
    /// 첫째가 깨지면 카메라가 배경 밖을 비추고, 둘째가 깨지면 흔들림이 아니라 떨림이 된다.</para>
    /// </summary>
    public sealed class MenuNoiseTests
    {
        private const float Sweep = 600f;    // 10분치를 훑는다
        private const float Step = 0.05f;

        [Test]
        public void 파형은_항상_범위_안에_있다()
        {
            for (float t = 0f; t < Sweep; t += Step)
            {
                float v = MenuNoise.Wave(t, 16f, 3f);
                Assert.GreaterOrEqual(v, -1f, $"t={t}");
                Assert.LessOrEqual(v, 1f, $"t={t}");
            }
        }

        [Test]
        public void 파형은_프레임_사이에_튀지_않는다()
        {
            // 60 fps 한 프레임에 전체 진폭의 몇 %만 움직여야 흔들림으로 읽힌다.
            const float frame = 1f / 60f;
            float previous = MenuNoise.Wave(0f, 16f, 3f);
            for (float t = frame; t < 120f; t += frame)
            {
                float v = MenuNoise.Wave(t, 16f, 3f);
                Assert.Less(Mathf.Abs(v - previous), 0.05f, $"t={t}에서 값이 튀었다");
                previous = v;
            }
        }

        [Test]
        public void 파형은_같은_시간에_같은_값을_준다()
        {
            Assert.AreEqual(MenuNoise.Wave(12.5f, 16f, 3f), MenuNoise.Wave(12.5f, 16f, 3f));
            Assert.AreNotEqual(MenuNoise.Wave(12.5f, 16f, 3f), MenuNoise.Wave(40f, 16f, 3f));
        }

        [Test]
        public void 주기가_0이면_흔들리지_않는다()
        {
            Assert.AreEqual(0f, MenuNoise.Wave(10f, 0f, 3f));
            Assert.AreEqual(0f, MenuNoise.Wave(10f, -5f, 3f));
            Assert.AreEqual(Vector3.zero, MenuNoise.Drift(10f, Vector3.one, Vector3.zero, 1f));
        }

        [Test]
        public void 드리프트는_지정_진폭을_넘지_않는다()
        {
            Vector3 amp = new Vector3(0.03f, 0.03f, 0.03f);
            Vector3 periods = new Vector3(14f, 17f, 12f);

            for (float t = 0f; t < Sweep; t += Step)
            {
                Vector3 d = MenuNoise.Drift(t, amp, periods, 5f);
                Assert.LessOrEqual(Mathf.Abs(d.x), amp.x + 0.0001f, $"t={t} x");
                Assert.LessOrEqual(Mathf.Abs(d.y), amp.y + 0.0001f, $"t={t} y");
                Assert.LessOrEqual(Mathf.Abs(d.z), amp.z + 0.0001f, $"t={t} z");
            }
        }

        [Test]
        public void 드리프트_세_축은_따로_논다()
        {
            // 세 축이 같은 위상이면 흔들림이 아니라 한 방향 이동으로 보인다.
            int sameXY = 0, sameXZ = 0, samples = 0;
            Vector3 amp = Vector3.one;
            Vector3 periods = new Vector3(14f, 17f, 12f);

            for (float t = 0f; t < Sweep; t += 0.5f)
            {
                Vector3 d = MenuNoise.Drift(t, amp, periods, 5f);
                if (Mathf.Abs(d.x - d.y) < 0.02f) { sameXY++; }
                if (Mathf.Abs(d.x - d.z) < 0.02f) { sameXZ++; }
                samples++;
            }

            Assert.Less(sameXY / (float)samples, 0.25f, "x·y가 붙어 움직인다");
            Assert.Less(sameXZ / (float)samples, 0.25f, "x·z가 붙어 움직인다");
        }

        [Test]
        public void 명멸은_지정_범위를_벗어나지_않는다()
        {
            for (float t = 0f; t < Sweep; t += Step)
            {
                float v = MenuNoise.Flicker(t, 0.85f, 1.15f, 9f, 2f);
                Assert.GreaterOrEqual(v, 0.85f - 0.0001f, $"t={t}");
                Assert.LessOrEqual(v, 1.15f + 0.0001f, $"t={t}");
            }
        }

        [Test]
        public void 명멸_범위를_거꾸로_줘도_같은_구간을_쓴다()
        {
            for (float t = 0f; t < 60f; t += 0.5f)
            {
                Assert.AreEqual(
                    MenuNoise.Flicker(t, 0.85f, 1.15f, 9f, 2f),
                    MenuNoise.Flicker(t, 1.15f, 0.85f, 9f, 2f),
                    0.0001f);
            }
        }

        [Test]
        public void 명멸은_꺼지지_않는다()
        {
            // 하한이 0보다 크면 어느 시점에도 완전히 꺼지지 않아야 한다 — 창문이 사라지면 안 된다.
            float min = 1f;
            for (float t = 0f; t < Sweep; t += Step)
            {
                min = Mathf.Min(min, MenuNoise.Flicker(t, 0.85f, 1.15f, 9f, 2f));
            }

            Assert.Greater(min, 0.5f, "창문이 꺼지는 순간이 있다");
        }
    }
}
