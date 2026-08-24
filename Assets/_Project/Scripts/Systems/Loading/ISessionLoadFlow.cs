using System;

namespace Game.Systems.Loading
{
    /// <summary>
    /// 인게임 진입 로딩 흐름의 계약 —
    /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §6.3 · §6.4.
    ///
    /// <para><b>화면이 흐름을 밀지 않는다</b>(§6.3). 로딩 화면은 여기서 (단계, 진행률, 문구)를
    /// 읽어 그리기만 하고, 단계를 넘기는 일은 전적으로 구현이 한다.</para>
    ///
    /// <para><b>대기실과의 접점은 <see cref="Begin"/> 한 줄이다</b>(§6.4). 준비 화면은
    /// "게임 시작"에서 세션을 직접 부르는 대신 이 흐름에 넘긴다.</para>
    /// </summary>
    public interface ISessionLoadFlow
    {
        /// <summary>지금 어느 단계인가.</summary>
        LoadingStage Stage { get; }

        /// <summary>0~1 전체 진행률. <b>단조 증가한다</b>(§4.3).</summary>
        float Progress { get; }

        /// <summary>지금 무엇을 기다리는지 한 줄(<see cref="LoadingStageText"/>).</summary>
        string Status { get; }

        /// <summary>로딩 화면이 떠 있어야 하는가. <see cref="LoadingStage.Idle"/>이면 거짓이다.</summary>
        bool IsActive { get; }

        /// <summary>
        /// 출발한다 — 호스트만 부른다. 이미 로딩 중이면 <c>false</c>를 돌려주고 아무것도 하지 않는다.
        /// </summary>
        /// <param name="startSceneLoad">
        /// ② 단계에서 불릴 <b>씬 전환 요청</b>. 계획 §6.4대로 세션 서비스를 거치는 경로
        /// (<c>MenuSessionActions.BeginJourney</c>)를 그대로 넘긴다 — 흐름은 그것이 무엇인지 모른다.
        /// <c>false</c>를 돌려주면 되돌린다(§3.5).
        /// </param>
        /// <param name="onAborted">
        /// 되돌렸을 때 사유와 함께 불린다. 아직 대기실 씬이 살아 있는 시점이므로
        /// 부르는 쪽이 화면을 되살릴 수 있다.
        /// </param>
        bool Begin(Func<bool> startSceneLoad, Action<string> onAborted);
    }
}
