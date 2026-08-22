namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차·궤도 높이 단계가 바뀜 — 호스트가 확정한 단계가 복제된 뒤 <b>모든 피어에서</b> 발행된다.
    /// 높이를 따라가야 하는 표현(<see cref="TrainElevationFollower"/>)이 이를 구독한다.
    /// 갑판 기준선(<see cref="TrainLayoutSettings.DeckHeight"/>)은 발행 <b>전에</b> 이미 갱신되므로,
    /// 구독자는 이 이벤트를 받은 시점에 새 갑판 높이를 그대로 읽어도 된다.
    /// </summary>
    public readonly struct TrainElevationChangedEvent
    {
        public readonly int StepIndex;

        public readonly float Offset;

        public TrainElevationChangedEvent(int stepIndex, float offset)
        {
            StepIndex = stepIndex;
            Offset = offset;
        }
    }
}
