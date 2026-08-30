namespace Game.Gameplay.Train
{
    /// <summary>
    /// 건축물이 그리드에서 <b>실제로 막는 셀의 모양</b> (천막 계획 결정 ⑥).
    /// 발자국 사각형이 곧 점유였던 규약을 데이터로 갈라, 지붕처럼 <b>덮되 막지 않는</b>
    /// 건축물이 성립하게 한다. 효과 범위(<see cref="ShelterScope"/>)·비용과는 별개 축이다.
    /// </summary>
    public enum StructureOccupancy : byte
    {
        /// <summary>발자국 사각형 전체를 막는다 — 기존 건축물 전부의 규약이자 기본값.</summary>
        Solid = 0,

        /// <summary>
        /// 네 모서리 셀만 막는다 (천막의 기둥). 안쪽은 빈 자리라 다른 건축물이 들어가고,
        /// 이미 선 건축물 위로 덮을 수도 있다. 발자국이 2×2면 네 셀이 곧 네 모서리라
        /// <see cref="Solid"/>와 같아진다.
        /// </summary>
        Corners = 1,
    }
}
