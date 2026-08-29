using Game.Gameplay.Monsters;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 몬스터 도약 검증 (바다 지역 구현 계획 §8.2 · §12.2).
    /// 바다 규격 — 물면 −4 · 상판 0 · 갑판 3.566 · 중력 25 · 결정 ⑨ 정점 +1.5.
    /// </summary>
    public sealed class MonsterLeapMathTests
    {
        private const float Gravity = 25f;
        private const float SurfaceY = -4f;
        private const float DeckTopY = 0f;        // 교량 상판
        private const float TrainDeckY = 3.566f;  // 열차 갑판
        private const float ApexY = 1.5f;         // 결정 ⑨
        private const float Range = 8f;
        private const float EmergeMargin = 1f;

        private static float ApexOf(float fromY, float speed)
        {
            // v² = 2gh → h = v²/2g
            return fromY + speed * speed / (2f * Gravity);
        }

        // ── 초기 속도 — 어디서 뛰는지를 본다 ──

        [Test]
        public void 출발_높이가_낮을수록_더_세게_뛴다()
        {
            float fromGround = MonsterLeapMath.LeapSpeed(0f, TrainDeckY + 1f, Gravity);
            float fromWater = MonsterLeapMath.LeapSpeed(SurfaceY, TrainDeckY + 1f, Gravity);

            Assert.Greater(fromWater, fromGround, "물면은 4 m 아래라 더 세게 뛰어야 한다");
        }

        [Test]
        public void 물에서_뛰어도_갑판에_닿는다()
        {
            // 예전 식(출발 높이를 무시)은 물면에서 정점이 0.57에 그쳐 갑판에 4 m 모자랐다.
            float speed = MonsterLeapMath.LeapSpeed(SurfaceY, TrainDeckY + 1f, Gravity);

            Assert.That(ApexOf(SurfaceY, speed), Is.EqualTo(TrainDeckY + 1f).Within(0.01f));
        }

        [Test]
        public void 출발_높이를_무시하면_갑판에_못_닿는다()
        {
            // 회귀 방지 — 고치기 전 식이 왜 틀렸는지를 수치로 남긴다.
            float wrong = Mathf.Sqrt(2f * Gravity * (TrainDeckY + 1f));

            Assert.Less(ApexOf(SurfaceY, wrong), TrainDeckY, "정점이 갑판에 못 미친다");
        }

        [Test]
        public void 이미_정점_위면_뛰지_않는다()
        {
            Assert.That(MonsterLeapMath.LeapSpeed(2f, ApexY, Gravity), Is.EqualTo(0f));
            Assert.That(MonsterLeapMath.LeapSpeed(ApexY, ApexY, Gravity), Is.EqualTo(0f));
        }

        // ── 결정 ⑨ — 상판은 닿고 갑판은 닿지 않는다 ──

        [Test]
        public void 물고기_점프는_상판에_닿는다()
        {
            float speed = MonsterLeapMath.LeapSpeed(SurfaceY, ApexY, Gravity);

            Assert.That(ApexOf(SurfaceY, speed), Is.EqualTo(ApexY).Within(0.01f));
            Assert.IsTrue(MonsterLeapMath.ReachesHeight(SurfaceY, ApexY, DeckTopY), "상판");
        }

        [Test]
        public void 물고기_점프는_갑판에_닿지_않는다()
        {
            // 이것이 3단 위험 구배의 핵심이다 — 갑판이 안전하지 않으면 내려갈 이유가 없다.
            Assert.IsFalse(MonsterLeapMath.ReachesHeight(SurfaceY, ApexY, TrainDeckY));
        }

        [Test]
        public void 상판_위_플레이어_머리까지_닿는다()
        {
            // 상판 0에 선 플레이어의 머리는 약 1.7 — 정점 1.5면 몸통 높이에서 만난다.
            Assert.IsTrue(MonsterLeapMath.ReachesHeight(SurfaceY, ApexY, 1.4f));
        }

        // ── 언제 튀어오르는가 ──

        private static Vector3 Fish(float x)
        {
            return new Vector3(x, SurfaceY, 0f);
        }

        [Test]
        public void 물_밖_표적에_사거리_안이면_뛴다()
        {
            Assert.IsTrue(MonsterLeapMath.ShouldSurfaceLeap(
                Fish(8f), new Vector3(4f, DeckTopY, 0f), SurfaceY, Range, EmergeMargin));
        }

        [Test]
        public void 물속_표적에는_뛰지_않는다()
        {
            // 잠수 중인 플레이어에게는 도약 없는 근접 위협이다 — 그것이 49초 창을 빠듯하게 만든다.
            Assert.IsFalse(MonsterLeapMath.ShouldSurfaceLeap(
                Fish(6f), new Vector3(4f, SurfaceY - 2f, 0f), SurfaceY, Range, EmergeMargin));
        }

        [Test]
        public void 수면에_걸친_표적에도_뛰지_않는다()
        {
            // 수영 중인 플레이어는 수면에 걸쳐 있다 — 여유가 없으면 헤엄치는 사람에게 튀어오른다.
            Assert.IsFalse(MonsterLeapMath.ShouldSurfaceLeap(
                Fish(6f), new Vector3(4f, SurfaceY + 0.5f, 0f), SurfaceY, Range, EmergeMargin));
        }

        [Test]
        public void 사거리_밖이면_뛰지_않는다()
        {
            Assert.IsFalse(MonsterLeapMath.ShouldSurfaceLeap(
                Fish(20f), new Vector3(4f, DeckTopY, 0f), SurfaceY, Range, EmergeMargin));
        }

        [Test]
        public void 이미_공중이면_다시_뛰지_않는다()
        {
            var airborne = new Vector3(6f, SurfaceY + 2f, 0f);

            Assert.IsFalse(MonsterLeapMath.ShouldSurfaceLeap(
                airborne, new Vector3(4f, DeckTopY, 0f), SurfaceY, Range, EmergeMargin));
        }

        [Test]
        public void 사거리는_수평_거리로만_잰다()
        {
            // 높이 차(4 m)가 사거리를 깎으면 물에서 상판을 칠 수 없다.
            var target = new Vector3(4f, DeckTopY, 6f);

            Assert.IsTrue(MonsterLeapMath.ShouldSurfaceLeap(
                Fish(4f), target, SurfaceY, Range, EmergeMargin));
        }
    }
}
