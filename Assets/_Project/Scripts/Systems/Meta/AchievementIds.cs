namespace Game.Systems.Meta
{
    /// <summary>
    /// 업적 id 최소 집합 (M6 3차 §2.5 — 파이프 검증용. 명칭·추가는 기획 협의로 확장,
    /// 저장 구조는 string id 집합이라 목록 변경이 무마찰이다).
    /// </summary>
    public static class AchievementIds
    {
        /// <summary>첫 런 결말 — 첫 게임오버.</summary>
        public const string FirstGameOver = "first_game_over";

        /// <summary>Day 3 도달 (게임오버 시점 기준).</summary>
        public const string ReachDay3 = "reach_day_3";
    }
}
