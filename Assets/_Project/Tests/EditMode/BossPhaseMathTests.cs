using Game.Gameplay.Monsters;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 보스 페이즈·고유 패턴 계획 검증 (M7 2차 결정 ② — 체력 비율이 페이즈를, 페이즈가
    /// 이동 속도·패턴 빈도·고유 패턴 해금을 결정한다. 소환 수는 합산 cap이 상한이다).
    /// </summary>
    public sealed class BossPhaseMathTests
    {
        private static readonly float[] TwoThresholds = { 0.7f, 0.35f };

        [Test]
        public void 임계가_없으면_항상_1페이즈다()
        {
            Assert.That(BossPhaseMath.EvaluatePhase(1f, null), Is.EqualTo(0));
            Assert.That(BossPhaseMath.EvaluatePhase(0f, new float[0]), Is.EqualTo(0));
        }

        [Test]
        public void 체력_비율이_임계를_지날_때마다_페이즈가_오른다()
        {
            Assert.That(BossPhaseMath.EvaluatePhase(1f, TwoThresholds), Is.EqualTo(0));
            Assert.That(BossPhaseMath.EvaluatePhase(0.71f, TwoThresholds), Is.EqualTo(0));
            Assert.That(BossPhaseMath.EvaluatePhase(0.5f, TwoThresholds), Is.EqualTo(1));
            Assert.That(BossPhaseMath.EvaluatePhase(0.34f, TwoThresholds), Is.EqualTo(2));
            Assert.That(BossPhaseMath.EvaluatePhase(0f, TwoThresholds), Is.EqualTo(2));
        }

        [Test]
        public void 임계값에_정확히_닿으면_이미_다음_페이즈다()
        {
            // 경계값 포함 규칙 — 50 %에서 "아직 1페이즈"면 한 프레임 차이로 전환이 갈린다.
            Assert.That(BossPhaseMath.EvaluatePhase(0.7f, TwoThresholds), Is.EqualTo(1));
            Assert.That(BossPhaseMath.EvaluatePhase(0.35f, TwoThresholds), Is.EqualTo(2));
        }

        [Test]
        public void 체력_비율은_0에서_1로_클램프된다()
        {
            Assert.That(BossPhaseMath.EvaluatePhase(2f, TwoThresholds), Is.EqualTo(0));
            Assert.That(BossPhaseMath.EvaluatePhase(-1f, TwoThresholds), Is.EqualTo(2));
        }

        [Test]
        public void 페이즈가_오를수록_빨라지고_쿨다운이_짧아진다()
        {
            Assert.That(BossPhaseMath.SpeedMultiplier(0, 0.12f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(BossPhaseMath.SpeedMultiplier(2, 0.12f), Is.EqualTo(1.24f).Within(0.0001f));

            Assert.That(BossPhaseMath.CooldownScale(0, 0.85f), Is.EqualTo(1f).Within(0.0001f));
            Assert.That(BossPhaseMath.CooldownScale(1, 0.85f), Is.EqualTo(0.85f).Within(0.0001f));
            Assert.That(BossPhaseMath.CooldownScale(2, 0.85f), Is.EqualTo(0.7225f).Within(0.0001f));
        }

        [Test]
        public void 쿨다운_배율은_1을_넘거나_0이_되지_않는다()
        {
            Assert.That(BossPhaseMath.CooldownScale(3, 2f), Is.EqualTo(1f).Within(0.0001f), "1 초과 배율은 1로 클램프");
            Assert.That(BossPhaseMath.CooldownScale(3, 0f), Is.GreaterThan(0f), "0 배율은 하한으로 눌린다");
        }

        [Test]
        public void 고유_패턴은_지정_페이즈부터_해금된다()
        {
            Assert.That(BossPhaseMath.IsSignatureUnlocked(0, 1), Is.False);
            Assert.That(BossPhaseMath.IsSignatureUnlocked(1, 1), Is.True);
            Assert.That(BossPhaseMath.IsSignatureUnlocked(2, 1), Is.True);
            Assert.That(BossPhaseMath.IsSignatureUnlocked(0, 0), Is.True, "해금 페이즈 0 = 처음부터");
        }

        [Test]
        public void 소환_수는_보스_자기_상한을_넘지_않는다()
        {
            int count = BossPhaseMath.PlanSignatureSpawnCount(
                requested: 4, ownedAlive: 4, ownedCap: 6, otherAlive: 0, combinedCap: 100);

            Assert.That(count, Is.EqualTo(2), "6 − 4 = 2마리만 더 부를 수 있다");
        }

        [Test]
        public void 소환_수는_웨이브와의_합산_cap에도_눌린다()
        {
            // 대역폭 방어선 — 보스 소속이 자기 상한 안이어도 밤 웨이브와 합쳐 넘길 수 없다.
            int count = BossPhaseMath.PlanSignatureSpawnCount(
                requested: 6, ownedAlive: 2, ownedCap: 8, otherAlive: 11, combinedCap: 16);

            Assert.That(count, Is.EqualTo(3), "16 − (2 + 11) = 3");
        }

        [Test]
        public void 합산_cap이_이미_찼으면_한_마리도_부르지_못한다()
        {
            int count = BossPhaseMath.PlanSignatureSpawnCount(
                requested: 4, ownedAlive: 4, ownedCap: 8, otherAlive: 12, combinedCap: 16);

            Assert.That(count, Is.EqualTo(0));
        }

        [Test]
        public void 요청이_0_이하면_스폰하지_않는다()
        {
            Assert.That(
                BossPhaseMath.PlanSignatureSpawnCount(0, 0, 8, 0, 16), Is.EqualTo(0));
            Assert.That(
                BossPhaseMath.PlanSignatureSpawnCount(-3, 0, 8, 0, 16), Is.EqualTo(0));
        }
    }
}
