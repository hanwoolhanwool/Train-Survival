namespace Game.Systems.Loading
{
    /// <summary>
    /// 인게임 진입 로딩의 단계 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §3.2 · §6.1.
    ///
    /// <para><b>순서가 값이다.</b> 뒤로 갈수록 큰 값을 갖고, 진행률 가중치
    /// (<see cref="LoadingProgressMath"/>)가 이 순서 위에 쌓인다. 중간에 단계를 끼우려면
    /// 가중치 표도 함께 고쳐야 한다.</para>
    ///
    /// <para><b>씬 로드 전후로 갈린다</b>(계획 §3.1). <see cref="Prepare"/>는 아직 대기실 씬이고,
    /// <see cref="Settle"/>부터는 인게임 씬이 이미 서 있다 — 무엇을 미리 만들 수 있는지가
    /// 이 경계로 결정된다.</para>
    /// </summary>
    public enum LoadingStage
    {
        /// <summary>로딩 중이 아니다. 로딩 화면은 꺼져 있다.</summary>
        Idle = 0,

        /// <summary>① 예고 — 아직 대기실 씬이다. 씬 로드 전에만 할 수 있는 것을 미리 만든다.</summary>
        Prepare = 1,

        /// <summary>① 전원 대기 — 느린 PC가 예고를 마칠 때까지. 4차 전까지는 즉시 통과한다.</summary>
        WaitPrepare = 2,

        /// <summary>② 로드 — 대기실 씬이 사라지고 인게임 씬이 들어온다.</summary>
        LoadScene = 3,

        /// <summary>③ 정착 — 인게임 씬이 섰지만 아직 로딩 화면이 덮고 있다.</summary>
        Settle = 4,

        /// <summary>③ 전원 대기 — <b>진짜 출발 게이트</b>. 4차 전까지는 즉시 통과한다.</summary>
        WaitSettle = 5,

        /// <summary>④ 출발 — 최소 표시 시간을 채우고 화면을 걷는다(5차).</summary>
        Depart = 6,

        /// <summary>끝났다. 다음 프레임에 <see cref="Idle"/>로 돌아간다.</summary>
        Done = 7,
    }
}
