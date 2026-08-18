namespace Game.Gameplay.Player
{
    /// <summary>
    /// 로컬 표현 이벤트 — 자기 플레이어의 시점 모드가 정해지거나 바뀐 시점에 발행된다
    /// (1인칭 통합 시점 전환 계획 기술 확정 ⑦). 스폰 직후 1회 + 이후 전환 때마다.
    ///
    /// <para>발행 주체는 <see cref="PlayerViewModeController"/>이며, <b>조작 권한이 있는
    /// 인스턴스만</b> 발행한다 — 원격 프록시의 컨트롤러는 값을 바꾸지도 발행하지도 않으므로
    /// 이 이벤트는 언제나 "내 화면"에 대한 것이다.</para>
    ///
    /// <para>구독은 <b>변경 알림</b> 용도다. 초기 상태가 필요한 표현 컴포넌트는 자기 초기화에서
    /// 컨트롤러의 <see cref="PlayerViewModeController.Mode"/>를 직접 읽는다 — 구독 순서에
    /// 의존하지 않기 위해서다.</para>
    /// </summary>
    public readonly struct PlayerViewModeChangedLocalEvent
    {
        /// <summary>적용된 시점 모드.</summary>
        public readonly PlayerViewMode Mode;

        public PlayerViewModeChangedLocalEvent(PlayerViewMode mode)
        {
            Mode = mode;
        }
    }
}
