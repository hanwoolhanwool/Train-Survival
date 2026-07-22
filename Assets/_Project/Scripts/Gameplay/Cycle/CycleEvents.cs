namespace Game.Gameplay.Cycle
{
    /// <summary>
    /// 권위 이벤트 — 낮/밤 국면 전환. 호스트 권위 누적 시간의 동기화 수신 시점에 각 피어에서 발행된다.
    /// 밤 웨이브 시작/종료(호스트)와 HUD 국면 표시가 이를 구독한다.
    /// </summary>
    public readonly struct DayPhaseChangedEvent
    {
        /// <summary>1부터 시작하는 Day 번호.</summary>
        public readonly int DayNumber;

        public readonly DayPhase Phase;

        public DayPhaseChangedEvent(int dayNumber, DayPhase phase)
        {
            DayNumber = dayNumber;
            Phase = phase;
        }
    }
}
