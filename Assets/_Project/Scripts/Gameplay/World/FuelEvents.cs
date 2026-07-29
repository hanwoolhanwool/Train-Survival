namespace Game.Gameplay.World
{
    /// <summary>
    /// 권위 이벤트 — 공유 연료 잔량 변경. 호스트 확정 값의 동기화 수신 시점에 각 피어에서 발행된다.
    /// HUD 연료 게이지가 이를 구독한다. 소모율은 복제하지 않고 각 피어가 같은 입력(설정 + 복제 편성)으로 재계산한다.
    /// </summary>
    public readonly struct FuelChangedEvent
    {
        public readonly float Fuel;

        public readonly float Capacity;

        /// <summary>현재 초당 소모율 — 칸 수 가중치 반영 값. HUD가 트레이드오프를 보여주는 데 쓴다.</summary>
        public readonly float ConsumptionPerSecond;

        public FuelChangedEvent(float fuel, float capacity, float consumptionPerSecond)
        {
            Fuel = fuel;
            Capacity = capacity;
            ConsumptionPerSecond = consumptionPerSecond;
        }
    }
}
