namespace Game.Gameplay.Session
{
    /// <summary>
    /// 권위 이벤트 — 호스트가 전멸(게임오버)을 확정한 시점에 각 피어에서 발행 (M6 3차).
    /// 게임오버 후 재접속한 피어에서도 스폰 시점에 한 번 발행된다 (NetworkVariable 후발 동기화).
    /// </summary>
    public readonly struct GameOverEvent
    {
        /// <summary>게임오버 시점의 Day 번호 (호스트 권위 값).</summary>
        public readonly int DayReached;

        /// <summary>세션 시작부터의 경과 시간 (초, 서버 시계).</summary>
        public readonly float ElapsedSeconds;

        public GameOverEvent(int dayReached, float elapsedSeconds)
        {
            DayReached = dayReached;
            ElapsedSeconds = elapsedSeconds;
        }
    }
}
