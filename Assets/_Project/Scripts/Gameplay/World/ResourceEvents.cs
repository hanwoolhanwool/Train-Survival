namespace Game.Gameplay.World
{
    /// <summary>
    /// 권위 이벤트 — 호스트가 자원 획득을 확정한 뒤, 각 피어의 동기화 수신 시점에 발행된다.
    /// 팀 공유 카운터 누계를 전달한다. UI·연출은 이 값을 그대로 표시하면 된다.
    /// </summary>
    public readonly struct ResourceAcquiredEvent
    {
        public readonly int TeamTotal;

        public ResourceAcquiredEvent(int teamTotal)
        {
            TeamTotal = teamTotal;
        }
    }
}
