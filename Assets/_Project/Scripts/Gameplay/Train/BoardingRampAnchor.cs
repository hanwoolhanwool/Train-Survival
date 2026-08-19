namespace Game.Gameplay.Train
{
    /// <summary>
    /// 승차 램프를 후미 칸의 어디에 맞출지 — 램프가 내려가는 방향에 따라 기준점이 달라진다.
    /// 씬마다 램프 배치가 달라 컴포넌트가 아니라 에셋 값으로 고른다 (직렬화 기본값 = 기존 동작).
    /// </summary>
    public enum BoardingRampAnchor
    {
        /// <summary>후미 칸 갑판 뒤끝 — 열차 뒤쪽(-Z)으로 내려가는 램프.</summary>
        RearEdge = 0,

        /// <summary>후미 칸 중심 — 칸 옆면(±X)으로 내려가는 램프. 궤도를 피해 지면에 닿는다.</summary>
        RearCenter = 1,
    }
}
