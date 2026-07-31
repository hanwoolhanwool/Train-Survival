namespace Game.Gameplay.Train
{
    /// <summary>
    /// 이탈 칸 재결합 계약 (손잡이-이탈저항 스펙 §4.1). 손잡이로 슬롯까지 끌어온 칸을 수리 망치 우클릭으로
    /// 편성에 다시 붙인다 — 칸 체력과 칸 위 건축물은 그대로 보존되고, 앞 연결부만 절반 체력으로 되살아난다.
    /// 새로 짓는 <see cref="ITrainExpansion"/>과 목적이 달라(회수 vs 건설) 계약을 나눈다.
    /// 조회(TryGet*)는 복제 상태 기반이라 전 피어 동일하고, 확정(ServerTry*)은 호스트 전용이다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainRecouple
    {
        /// <summary>칸 1칸 재결합에 드는 자원 수 — 신규 건설보다 싸게 잡아 회수에 경제적 유인을 준다.</summary>
        int RecoupleCost { get; }

        /// <summary>
        /// 지금 재결합을 겨눌 칸 — 선두부터 첫 이탈 중(미소실) 칸. 복제 상태 기반이라 전 피어 동일 —
        /// 조준·프리뷰가 지점을 계산하는 데 쓴다. 없으면 false.
        /// </summary>
        bool TryGetRecoupleTarget(out int carIndex);

        /// <summary>
        /// 이탈 칸을 편성에 다시 붙인다 — 슬롯 도달·앞 칸 존재를 권위 상태로 재검증한다.
        /// 서버 전용 — 클라이언트 호출은 항상 false.
        /// </summary>
        bool ServerTryRecouple(int carIndex);
    }
}
