namespace Game.Gameplay.Crafting
{
    /// <summary>
    /// 레시피가 요구하는 제작 지점의 종류 (M5 4차 — 화덕 요리).
    /// 레시피에 byte로 직렬화되므로 한 번 배정한 값은 바꾸지 않는다.
    /// </summary>
    public enum CraftStationKind : byte
    {
        /// <summary>제작대·기관차 고정 지점 — 기본값 0이라 기존 레시피가 무변경으로 동작한다.</summary>
        Workbench = 0,

        /// <summary>화덕 건축물 — 요리 레시피 전용. 기관차 지점으로는 만들 수 없다.</summary>
        Campfire = 1,

        /// <summary>정수기 건축물 — 얼음 정화 전용 (M7 3차 결정 ①). 화덕과 같은 규약.</summary>
        Purifier = 2,
    }
}
