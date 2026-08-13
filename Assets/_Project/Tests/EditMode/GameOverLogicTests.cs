using Game.Gameplay.Session;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>전멸(게임오버) 판정 검증 (M6 3차 결정 ② — 기획서 §9.1 "전원 사망 시 게임오버").</summary>
    public sealed class GameOverLogicTests
    {
        private static GameOverLogic.PlayerLifeState Alive()
        {
            return new GameOverLogic.PlayerLifeState(hasPlayerObject: true, isAlive: true);
        }

        private static GameOverLogic.PlayerLifeState Dead()
        {
            return new GameOverLogic.PlayerLifeState(hasPlayerObject: true, isAlive: false);
        }

        private static GameOverLogic.PlayerLifeState NotSpawned()
        {
            return new GameOverLogic.PlayerLifeState(hasPlayerObject: false, isAlive: false);
        }

        [Test]
        public void 접속_중_전원이_죽어_있으면_전멸이다()
        {
            Assert.That(GameOverLogic.IsWipe(new[] { Dead(), Dead(), Dead() }), Is.True);
        }

        [Test]
        public void 한_명이라도_살아_있으면_전멸이_아니다()
        {
            Assert.That(GameOverLogic.IsWipe(new[] { Dead(), Alive(), Dead() }), Is.False);
        }

        [Test]
        public void 호스트_혼자_남아_죽어도_전멸이다()
        {
            // 마지막 생존자(클라)가 끊긴 경우 — 끊긴 자는 목록에 없고(§2.3 제외),
            // 남은 접속자 전원이 사망 상태면 게임오버다 (결정 ②).
            Assert.That(GameOverLogic.IsWipe(new[] { Dead() }), Is.True);
        }

        [Test]
        public void 스폰_전_접속자가_있으면_판정을_보류한다()
        {
            // 접속 승인~스폰 사이·재접속 복원 직전 — 생사를 알 수 없어 전멸 오탐을 막는다.
            // 스폰·복원 완료 시점의 재평가가 놓치지 않는다 (GameOverMonitor 트리거 ⓓ).
            Assert.That(GameOverLogic.IsWipe(new[] { Dead(), NotSpawned() }), Is.False);
        }

        [Test]
        public void 빈_목록은_전멸이_아니다()
        {
            Assert.That(GameOverLogic.IsWipe(new GameOverLogic.PlayerLifeState[0]), Is.False);
            Assert.That(GameOverLogic.IsWipe(null), Is.False);
        }
    }
}
