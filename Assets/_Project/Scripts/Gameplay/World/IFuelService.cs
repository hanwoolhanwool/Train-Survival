namespace Game.Gameplay.World
{
    /// <summary>
    /// 공유 연료 상태 계약 (기획서 §2 — "연료는 상시 소모되는 기본 압박 자원").
    /// 잔량은 호스트 권위이며, 변경은 <see cref="FuelChangedEvent"/>로 통지된다.
    /// </summary>
    public interface IFuelService
    {
        /// <summary>현재 연료 잔량. 호스트 권위 값.</summary>
        float Fuel { get; }

        /// <summary>연료 최대 저장량.</summary>
        float Capacity { get; }

        /// <summary>연료를 충전한다. 서버 전용 — 클라이언트 호출은 무시된다.</summary>
        void AddFuel(float amount);
    }
}
