namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 부위(칸·연결부·건축물) 수리를 상태 모델에 전달하는 호스트 전용 변이 계약 (기획서 §9 — 수리 망치).
    /// 수리 도구는 구체 <see cref="TrainState"/>가 아니라 이 인터페이스에 의존한다(DIP).
    /// 서버 권위이므로 구현은 클라이언트 호출을 무시한다. <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainRepairSink
    {
        /// <summary>
        /// 부위 하나를 수리한다. 파괴·이탈·만피 등 수리 불가면 false — 끊긴 연결부·파괴된 칸의 복구(재결합)는
        /// 미결(기획서 §9.1 설계 노트)이라 수리로는 되살릴 수 없다.
        /// </summary>
        bool ServerApplyRepair(TrainPartKind kind, int index, float amount);
    }
}
