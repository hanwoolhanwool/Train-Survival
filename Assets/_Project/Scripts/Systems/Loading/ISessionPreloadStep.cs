namespace Game.Systems.Loading
{
    /// <summary>
    /// 미리 만드는 일이 <b>씬 로드 전에만</b> 되는지 <b>후에만</b> 되는지 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §3.1 · §5.1.
    /// </summary>
    public enum PreloadPhase
    {
        /// <summary>
        /// A 묶음 — 씬 로드 <b>전</b>(<see cref="LoadingStage.Prepare"/>).
        /// 지형 타일이 여기다: 씬이 활성화되는 순간 스트리머가 바로 돌기 때문에 뒤에 하면 이미 늦고,
        /// NGO는 <c>allowSceneActivation</c>을 열어 주지 않아 활성화를 미룰 수도 없다.
        /// </summary>
        BeforeSceneLoad = 0,

        /// <summary>
        /// B 묶음 — 씬 로드 <b>후</b>(<see cref="LoadingStage.Settle"/>).
        /// 건축물 고스트와 HUD 워밍업이 여기다: 대상 컴포넌트가 인게임 씬에 있다.
        /// </summary>
        AfterSceneLoad = 1,
    }

    /// <summary>
    /// 로딩 중에 미리 만들 것이 있는 쪽의 계약 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §6.1 · §6.2.
    ///
    /// <para><b>선언은 <c>Game.Systems</c>가, 구현은 바깥 계층이 한다</b>(DIP). 지형 지식은
    /// <c>Game.Gameplay</c>에, UI 지식은 <c>Game.UI</c>에 있고 <c>Game.Systems</c>는 둘 다 참조하지
    /// 않는다. 그래서 코디네이터는 <b>등록된 것을 묶음 순서대로 돌릴 뿐 무엇인지 모른다</b> —
    /// "몬스터도 미리 만들자"가 나중에 생겨도 코디네이터를 고치지 않는다(OCP).</para>
    ///
    /// <para><b>한 프레임에 다 하지 않는다</b>(§5.5). <c>PoolManager.Prewarm</c>은 N번의
    /// <c>Instantiate</c>를 한 프레임에 동기로 돌기 때문에, 그대로 부르면 진행바가 그 프레임에
    /// 멈춘다. <see cref="Advance"/>는 <b>한 프레임 몫만</b> 처리하고 돌아와야 한다.</para>
    ///
    /// <para><b>실패해도 흐름을 멈추지 않는다</b>(§3.5). 프리로드 실패는 렉이지 게임 중단 사유가
    /// 아니므로, 구현이 예외를 던지면 코디네이터가 그 스텝만 건너뛰고 진행한다.</para>
    /// </summary>
    public interface ISessionPreloadStep
    {
        /// <summary>씬 로드 전인가 후인가.</summary>
        PreloadPhase Phase { get; }

        /// <summary>
        /// 만들어야 할 총량. 단위는 구현이 정한다(타일 인스턴스 수, 건축물 종류 수 …) —
        /// 진행률은 <see cref="Done"/>/<see cref="Total"/>로만 읽히므로 단위가 무엇인지는
        /// 코디네이터의 관심사가 아니다.
        ///
        /// <para><b>단계가 시작된 시점에 이미 유효해야 한다.</b> 계획 산출이 필요하면 이 속성을
        /// 처음 읽을 때(또는 그보다 앞서) 끝내 둔다 — 코디네이터는 <see cref="Advance"/>보다
        /// 먼저 이 값을 읽는다. 0이면 할 일이 없는 것으로 보고 곧바로 넘어간다.</para>
        /// </summary>
        int Total { get; }

        /// <summary>지금까지 끝난 양. <see cref="Total"/>에 닿으면 이 스텝은 완료다.</summary>
        int Done { get; }

        /// <summary>한 프레임 몫을 처리한다. 이미 끝났으면 아무것도 하지 않는다.</summary>
        void Advance();
    }
}
