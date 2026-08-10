using Game.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>몬스터 커스텀 조향 검증 (네트워크 문서 §4.3 — NavMesh 불사용 확정).</summary>
    public sealed class MonsterSteeringTests
    {
        [Test]
        public void 추격_속도는_스크롤_속도보다_항상_빠르다()
        {
            // §4.3 — "몬스터 이동속도 > 스크롤 속도" 제약: 추격 성립 조건.
            Assert.That(MonsterSteering.EnforceChaseSpeed(8.5f, 6f, 1.5f), Is.EqualTo(8.5f));
            Assert.That(MonsterSteering.EnforceChaseSpeed(5f, 6f, 1.5f), Is.EqualTo(7.5f), "느린 데이터도 하한 강제");
        }

        [Test]
        public void 지상_이동은_목표_방향_속도에_컨베이어_변위가_가산된다()
        {
            Vector3 velocity = MonsterSteering.ComputeGroundVelocity(
                position: new Vector3(0f, 0f, 0f), targetPosition: new Vector3(0f, 0f, 10f),
                hasObstacle: false, obstacleNormal: Vector3.zero,
                chaseSpeed: 8f, scrollSpeed: 6f);

            Assert.That(velocity.x, Is.EqualTo(0f).Within(0.001f));
            Assert.That(velocity.z, Is.EqualTo(2f).Within(0.001f), "전진 8 − 스크롤 6 = 순전진 2 m/s");
        }

        [Test]
        public void 목표의_높이_차는_수평_조향에_영향을_주지_않는다()
        {
            Vector3 velocity = MonsterSteering.ComputeGroundVelocity(
                position: Vector3.zero, targetPosition: new Vector3(0f, 4f, 10f),
                hasObstacle: false, obstacleNormal: Vector3.zero,
                chaseSpeed: 8f, scrollSpeed: 0f);

            Assert.That(velocity.y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(velocity.z, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void 장애물이_있으면_법선_방향으로_회피가_섞인다()
        {
            Vector3 velocity = MonsterSteering.ComputeGroundVelocity(
                position: Vector3.zero, targetPosition: new Vector3(0f, 0f, 10f),
                hasObstacle: true, obstacleNormal: Vector3.right,
                chaseSpeed: 8f, scrollSpeed: 0f);

            Assert.That(velocity.x, Is.GreaterThan(0f), "법선(+X) 쪽으로 비켜간다");
            Assert.That(velocity.z, Is.GreaterThan(0f), "목표 방향 전진은 유지된다");
            Assert.That(velocity.magnitude, Is.EqualTo(8f).Within(0.001f), "회피가 속도 크기를 바꾸지 않는다");
        }

        [Test]
        public void 갑판_위에서는_컨베이어_가산이_없다()
        {
            Vector3 velocity = MonsterSteering.ComputeDeckVelocity(
                position: new Vector3(0f, 3f, 0f), targetPosition: new Vector3(0f, 3f, 5f),
                hasObstacle: false, obstacleNormal: Vector3.zero, moveSpeed: 8f);

            Assert.That(velocity.z, Is.EqualTo(8f).Within(0.001f), "열차 프레임 소속 — 스크롤 미가산");
        }

        [Test]
        public void 갑판_장애물이_있으면_법선_방향으로_회피가_섞인다()
        {
            // M5 8차 — 갑판 건축물(돔·난방기) 회피: 지상과 같은 법선 합산, 컨베이어 항만 없다.
            Vector3 velocity = MonsterSteering.ComputeDeckVelocity(
                position: new Vector3(0f, 3f, 0f), targetPosition: new Vector3(0f, 3f, 10f),
                hasObstacle: true, obstacleNormal: Vector3.right, moveSpeed: 8f);

            Assert.That(velocity.x, Is.GreaterThan(0f), "법선(+X) 쪽으로 비켜간다");
            Assert.That(velocity.z, Is.GreaterThan(0f), "목표 방향 전진은 유지된다");
            Assert.That(velocity.magnitude, Is.EqualTo(8f).Within(0.001f), "회피가 속도 크기를 바꾸지 않는다");
        }

        [Test]
        public void 목표와_같은_위치면_정지한다()
        {
            Vector3 velocity = MonsterSteering.ComputeDeckVelocity(
                Vector3.zero, Vector3.zero, hasObstacle: false, obstacleNormal: Vector3.zero, moveSpeed: 8f);

            Assert.That(velocity, Is.EqualTo(Vector3.zero));
        }
    }
}
