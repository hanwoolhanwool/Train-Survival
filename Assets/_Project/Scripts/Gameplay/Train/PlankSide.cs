namespace Game.Gameplay.Train
{
    /// <summary>
    /// 판자 증축이 붙는 칸의 옆면 (건축 개편 3차 — 계획서 §2.9, 결정 ⑥: 셀 열 단위 증축).
    /// 열차는 X=0 고정 주행·+Z 전방이므로 좌측 = -X, 우측 = +X다. 그리드 고정 예약 좌표계에서
    /// 좌측 판자는 칸 본체보다 작은 열(<see cref="StructureGridLogic.FirstBodyColumn"/> 미만),
    /// 우측 판자는 큰 열을 쓴다 — 좌측을 나중에 지어도 기존 항목의 CellX 재색인이 필요 없다.
    /// RPC 페이로드로 실리므로 값은 고정한다.
    /// </summary>
    public enum PlankSide : byte
    {
        /// <summary>좌측(-X) — 그리드 열이 칸 본체보다 작아지는 쪽.</summary>
        Left = 0,

        /// <summary>우측(+X) — 그리드 열이 칸 본체보다 커지는 쪽.</summary>
        Right = 1,
    }
}
