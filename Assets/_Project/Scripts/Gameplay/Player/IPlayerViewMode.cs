namespace Game.Gameplay.Player
{
    /// <summary>
    /// 시점 모드의 <b>읽기 계약</b> (1인칭 통합 시점 전환 계획 §3.1) — 표현 컴포넌트는 이것만 안다.
    /// 멤버가 하나뿐인 이유는 소비자가 실제로 그것만 쓰기 때문이다 (ISP):
    /// 설정 에셋이 필요한 컴포넌트는 자기 <c>SerializeField</c>로 직접 물린다.
    ///
    /// <para>구현체를 못 찾으면 소비자는 <see cref="PlayerViewMode.SplitFpTp"/>(현행)으로 동작해야 한다 —
    /// 원격 프록시·뷰랩처럼 컨트롤러가 없거나 조작 권한이 없는 곳에서 표현이 깨지지 않게 하는 계약이다 (LSP).</para>
    /// </summary>
    public interface IPlayerViewMode
    {
        /// <summary>현재 시점 모드 — 매 프레임 읽어도 되는 값이다.</summary>
        PlayerViewMode Mode { get; }
    }
}
