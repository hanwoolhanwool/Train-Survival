namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 칸 종류 (개발 가이드 §M3). 기관차는 항상 편성의 선두(인덱스 0)이며 불변식으로 파괴 불가다.
    /// 증설 칸은 <see cref="Standard"/>·<see cref="Greenhouse"/> 등으로 확장된다 (온실칸 = §M3 증설 1종).
    /// NetworkList 직렬화 대상이므로 바이트 값이 안정적이도록 명시적으로 번호를 고정한다.
    /// </summary>
    public enum CarType : byte
    {
        /// <summary>기관차 — 파괴 불가(불변식). 편성 선두 고정.</summary>
        Locomotive = 0,

        /// <summary>일반 화물칸.</summary>
        Standard = 1,

        /// <summary>온실칸 — §M3 증설 1종.</summary>
        Greenhouse = 2,
    }
}
