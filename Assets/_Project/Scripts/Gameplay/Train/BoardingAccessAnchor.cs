namespace Game.Gameplay.Train
{
    /// <summary>
    /// 승하차 통로(램프·사다리)를 후미 칸의 어디에 맞출지 — 통로가 놓이는 방향에 따라 기준점이 달라진다.
    /// 씬마다 배치가 달라 컴포넌트가 아니라 에셋 값으로 고른다 (직렬화 기본값 = 기존 동작).
    /// </summary>
    public enum BoardingAccessAnchor
    {
        /// <summary>후미 칸 갑판 뒤끝 — 열차 뒤쪽(-Z)으로 내려가는 램프.</summary>
        RearEdge = 0,

        /// <summary>후미 칸 중심 — 칸 옆면(±X)에 붙는 사다리·램프. 궤도를 피한다.</summary>
        RearCenter = 1,
    }
}
