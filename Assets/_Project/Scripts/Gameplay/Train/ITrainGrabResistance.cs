namespace Game.Gameplay.Train
{
    /// <summary>
    /// 손잡이 잡기로 이탈 칸에 저항을 거는 호스트 전용 계약 (손잡이-이탈저항 스펙 §5·§6).
    /// 손잡이 앵커가 그랩 점유/해제 시 해당 칸의 잡은 인원 수를 증감시키고, 호스트 이동 시뮬레이션이 이를 저항으로 반영한다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainGrabResistance
    {
        /// <summary>칸의 손잡이를 잡은 인원 +1 (호스트 전용).</summary>
        void AddGrabber(int carIndex);

        /// <summary>칸의 손잡이를 놓은 인원 -1 (호스트 전용).</summary>
        void RemoveGrabber(int carIndex);

        /// <summary>칸을 현재 잡고 있는 인원 수.</summary>
        int GetGrabberCount(int carIndex);
    }
}
