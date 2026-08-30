namespace Game.Gameplay.Train
{
    /// <summary>
    /// 건축물의 체온 효과가 닿는 범위 (천막 계획 결정 ③).
    /// 건축 개편 §5가 "칸 단위 boolean"을 의식적으로 골랐고, 천막이 그 선택을 자기 몫만
    /// 뒤집는다 — 어느 종류가 어느 범위인지는 <b>코드가 아니라 카탈로그가</b> 안다.
    /// </summary>
    public enum ShelterScope : byte
    {
        /// <summary>그 칸 위 어디서든 효과가 든다 — 난방기·강화 난방로의 규약이자 기본값.</summary>
        Car = 0,

        /// <summary>발자국 사각형 안에 있어야 효과가 든다 — 천막은 지붕 아래여야 그늘이다.</summary>
        Footprint = 1,
    }
}
