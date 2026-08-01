namespace Game.Gameplay.Region
{
    /// <summary>
    /// 진행 중인 날씨의 조회 계약 (기획서 §7.4, 개발 가이드 M4).
    /// 전환은 <see cref="WeatherChangedEvent"/>로 발행하고, 늦게 켜진 구독자를 위해 현재 상태는 이 서비스로 노출한다.
    /// </summary>
    public interface IWeatherService
    {
        /// <summary>진행 중인 날씨. 맑으면 null.</summary>
        WeatherDefinition ActiveWeather { get; }

        /// <summary>날씨가 진행 중인가.</summary>
        bool IsActive { get; }
    }
}
