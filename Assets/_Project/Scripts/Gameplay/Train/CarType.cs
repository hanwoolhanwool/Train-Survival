namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 칸 종류 (개발 가이드 §M3). 기관차는 항상 편성의 선두(인덱스 0)이며 불변식으로 파괴 불가다.
    /// 기관차 외에는 모두 동일한 확장 칸이다 — 칸의 개성(온실 등)은 종류가 아니라
    /// 칸 위에 설치하는 건축물(<see cref="StructureState"/>)이 만든다 (자유 설치 방향).
    /// NetworkList 직렬화 대상이므로 바이트 값이 안정적이도록 명시적으로 번호를 고정한다.
    /// </summary>
    public enum CarType : byte
    {
        /// <summary>기관차 — 파괴 불가(불변식). 편성 선두 고정.</summary>
        Locomotive = 0,

        /// <summary>확장 칸 — 건축물을 얹어 용도를 정한다.</summary>
        Standard = 1,
    }
}
