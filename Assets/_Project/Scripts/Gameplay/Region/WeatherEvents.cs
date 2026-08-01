namespace Game.Gameplay.Region
{
    /// <summary>
    /// 날씨 전환 권위 이벤트 — 호스트가 발생·종료를 확정하고, 복제 수신 시점에 각 피어에서 발행된다
    /// (지역 전환과 달리 날씨는 무작위라 시간의 함수로 유도할 수 없어 네트워크 상태를 갖는다).
    /// </summary>
    public readonly struct WeatherChangedEvent
    {
        /// <summary>시작된 날씨. 날씨가 갠 경우 null.</summary>
        public readonly WeatherDefinition Weather;

        public WeatherChangedEvent(WeatherDefinition weather)
        {
            Weather = weather;
        }

        /// <summary>날씨가 진행 중인가 (false면 맑음으로 복귀).</summary>
        public bool IsActive => Weather != null;
    }
}
