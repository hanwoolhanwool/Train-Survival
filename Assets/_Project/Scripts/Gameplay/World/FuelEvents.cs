namespace Game.Gameplay.World
{
    /// <summary>
    /// 권위 이벤트 — 공유 연료 잔량 변경. 호스트 확정 값의 동기화 수신 시점에 각 피어에서 발행된다.
    /// HUD 연료 게이지가 이를 구독한다.
    /// </summary>
    public readonly struct FuelChangedEvent
    {
        public readonly float Fuel;

        public readonly float Capacity;

        public FuelChangedEvent(float fuel, float capacity)
        {
            Fuel = fuel;
            Capacity = capacity;
        }
    }
}
