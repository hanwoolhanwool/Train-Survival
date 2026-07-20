using Game.Gameplay.World;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>월드 스크롤 좌표 유도·스무딩 보정 검증 (네트워크 문서 §4.1, §8 해소 항목).</summary>
    public sealed class WorldScrollMathTests
    {
        [Test]
        public void 스폰_바인딩으로_위치를_유도하면_주행한_만큼_뒤로_밀린다()
        {
            var spawnPosition = new Vector3(5f, 0.4f, 60f);

            Vector3 result = WorldScrollMath.GetScrolledPosition(spawnPosition, spawnDistance: 100f, currentDistance: 130f);

            Assert.That(result.x, Is.EqualTo(5f));
            Assert.That(result.y, Is.EqualTo(0.4f));
            Assert.That(result.z, Is.EqualTo(30f), "30 m 주행 → 30 m 후방 이동");
        }

        [Test]
        public void 같은_누적_거리라면_스폰_시점과_무관하게_같은_위치가_나온다()
        {
            // 결정론 조건: 위치는 (스폰 위치, 스폰 거리, 현재 거리)만의 함수여야 클라 간 위상이 일치한다.
            Vector3 a = WorldScrollMath.GetScrolledPosition(new Vector3(0f, 0f, 50f), 10f, 70f);
            Vector3 b = WorldScrollMath.GetScrolledPosition(new Vector3(0f, 0f, 20f), 40f, 70f);

            Assert.That(a.z, Is.EqualTo(-10f));
            Assert.That(b.z, Is.EqualTo(-10f));
        }

        [Test]
        public void 스무딩_보정은_목표를_향해_수렴한다()
        {
            float current = 0f;
            const float target = 10f;

            for (int i = 0; i < 200; i++)
            {
                current = WorldScrollMath.SmoothToward(current, target, scrollSpeed: 0f, deltaTime: 0.02f, correctionRate: 5f);
            }

            Assert.That(current, Is.EqualTo(target).Within(0.01f));
        }

        [Test]
        public void 오차가_없으면_속도_적분과_같다()
        {
            float result = WorldScrollMath.SmoothToward(
                current: 100f, target: 100.12f, scrollSpeed: 6f, deltaTime: 0.02f, correctionRate: 5f);

            Assert.That(result, Is.EqualTo(100.12f).Within(0.001f), "current + 6×0.02 = target이면 보정량이 0에 수렴");
        }
    }
}
