namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 증설 계약 (개발 가이드 §M3 — 칸 증설/연결, 기획서 §7.1). 증설은 항상 후미에 1칸씩 잇는다.
    /// 판정(<see cref="CanAppendCar"/>)은 복제 상태 기반이라 전 피어에서 동일하고, 확정은 호스트 전용이다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainExpansion
    {
        /// <summary>편성 상한 — 씬에 미리 확보된 예비 슬롯 수까지만 증설할 수 있다.</summary>
        int MaxCarCount { get; }

        /// <summary>
        /// 지금 후미에 이 종류의 칸을 이을 수 있는지 — 상한 미만이고 기존 전 슬롯이 살아 붙어 있어야 한다
        /// (이탈·파괴된 슬롯 뒤에는 잇지 못한다. 회수 불가 전제와 정합 — 기획서 §9.1).
        /// </summary>
        bool CanAppendCar(CarType type);

        /// <summary>후미에 새 칸 1개를 원자적으로 잇는다(칸+연결부+건축물 슬롯). 서버 전용 — 클라이언트 호출은 항상 false.</summary>
        bool ServerTryAppendCar(CarType type);
    }
}
