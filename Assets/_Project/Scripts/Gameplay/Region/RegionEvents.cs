namespace Game.Gameplay.Region
{
    /// <summary>
    /// 지역 전환 권위 이벤트 — 호스트 권위 Day 번호에서 각 피어가 동일하게 파생해 발행한다
    /// (낮/밤 국면 전환과 같은 규약: 시간의 함수이므로 전용 RPC 없이 전 피어가 같은 전환을 본다).
    /// 지역 진입 시 1회 발행되며, 지역 안에서 날짜만 바뀔 때는 발행되지 않는다.
    /// </summary>
    public readonly struct RegionChangedEvent
    {
        /// <summary>진입한 지역의 배열 인덱스.</summary>
        public readonly int RegionIndex;

        /// <summary>지역 순환 횟수 (0 = 첫 바퀴).</summary>
        public readonly int CycleNumber;

        /// <summary>진입 시점의 지역 내 일차 (보통 1).</summary>
        public readonly int DayInRegion;

        /// <summary>진입한 지역 정의 — 지형·자원 프리팹 교체 구독자가 바로 쓴다. 없으면 null.</summary>
        public readonly RegionDefinition Region;

        public RegionChangedEvent(int regionIndex, int cycleNumber, int dayInRegion, RegionDefinition region)
        {
            RegionIndex = regionIndex;
            CycleNumber = cycleNumber;
            DayInRegion = dayInRegion;
            Region = region;
        }
    }
}
