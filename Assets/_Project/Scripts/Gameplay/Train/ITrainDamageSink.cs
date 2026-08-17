namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 부위(칸·연결부·건축물) 피격을 상태 모델에 전달하는 호스트 전용 변이 계약.
    /// 데미지 리시버(CarView·CouplingPart·StructureView)는 구체 <see cref="TrainState"/>가 아니라 이 인터페이스에 의존한다(DIP).
    /// 서버 권위이므로 구현은 클라이언트 호출을 무시한다. <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainDamageSink
    {
        /// <summary>칸에 데미지를 적용한다(파괴 시 인접 연결부 절단 + 후방 연쇄 이탈).</summary>
        void ApplyCarDamage(int carIndex, float amount);

        /// <summary>연결부에 데미지를 적용한다(끊기면 후방 연쇄 이탈).</summary>
        void ApplyCouplingDamage(int couplingIndex, float amount);

        /// <summary>칸 위 건축물에 데미지를 적용한다 — 대상은 그리드 항목의 서버 발급 Id
        /// (건축 개편 1차 — 리스트 인덱스가 아닌 안정 참조. 기획서 §9 — 건축물 개별 파괴).</summary>
        void ApplyStructureDamage(int structureId, float amount);
    }
}
