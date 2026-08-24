namespace Game.Systems.Loading
{
    /// <summary>
    /// 단계별 상태 문구 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §6.3 · §9(5차).
    ///
    /// <para><b>화면이 아니라 흐름이 문구를 소유한다.</b> 로딩 화면은 코디네이터가 내놓는 문자열을
    /// 그리기만 한다(§6.3) — 같은 흐름을 다른 화면이 그리게 되더라도 문구가 갈라지지 않는다.</para>
    ///
    /// <para><b>지금 무엇을 기다리는지</b>만 말한다. 진행률은 숫자가 이미 말하고 있으므로
    /// 문구까지 "62 % 완료"라고 하면 같은 말을 두 번 하는 셈이다.</para>
    ///
    /// <para>문안 다듬기는 5차 몫이다 — 여기 값은 그때 바뀔 수 있다.</para>
    /// </summary>
    public static class LoadingStageText
    {
        /// <summary>단계에 대응하는 한 줄. <see cref="LoadingStage.Idle"/>은 빈 문자열이다.</summary>
        public static string For(LoadingStage stage)
        {
            switch (stage)
            {
                case LoadingStage.Prepare: return "여정을 준비하는 중...";
                case LoadingStage.WaitPrepare: return "다른 참가자를 기다리는 중...";
                case LoadingStage.LoadScene: return "세계를 여는 중...";
                case LoadingStage.Settle: return "자리를 잡는 중...";
                case LoadingStage.WaitSettle: return "다른 참가자를 기다리는 중...";
                case LoadingStage.Depart: return "열차는 곧 출발합니다";
                case LoadingStage.Done: return "열차는 곧 출발합니다";
                default: return string.Empty;
            }
        }
    }
}
