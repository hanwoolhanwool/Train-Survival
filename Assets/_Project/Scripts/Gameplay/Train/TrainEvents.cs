namespace Game.Gameplay.Train
{
    /// <summary>
    /// 로컬 표현 이벤트 — 자기 플레이어의 엔진 상호작용 범위 진입/이탈. HUD 투입 안내("E — 연료 투입")용.
    /// 범위 상태가 바뀔 때마다 발행된다.
    /// </summary>
    public readonly struct EnginePromptLocalEvent
    {
        public readonly bool InRange;

        public EnginePromptLocalEvent(bool inRange)
        {
            InRange = inRange;
        }
    }
}
