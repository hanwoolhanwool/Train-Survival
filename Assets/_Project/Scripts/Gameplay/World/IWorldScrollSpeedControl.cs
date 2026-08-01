namespace Game.Gameplay.World
{
    /// <summary>
    /// 월드 스크롤 속도 변경 계약 — 조회(<see cref="IWorldScrollService"/>)와 분리된 호스트 전용 제어면.
    /// 연료 감속·초가속 연출 등 속도를 바꾸는 시스템은 이 인터페이스에만 의존한다 (ISP).
    /// </summary>
    public interface IWorldScrollSpeedControl
    {
        /// <summary>
        /// 기본 스크롤 속도를 변경한다 (m/s). 호스트 전용 — 클라이언트 호출은 무시된다.
        /// 연료 상태가 매 프레임 수렴시키는 값이므로, 일시적인 환경 개입은
        /// <see cref="SetEnvironmentSpeedMultiplier"/>를 써야 덮어써지지 않는다.
        /// </summary>
        void SetScrollSpeed(float speed);

        /// <summary>
        /// 기본 속도에 곱해지는 환경 배율을 설정한다 (날씨 감속·부스트 등). 호스트 전용.
        /// 기본 속도와 별개 레이어라 연료 감속과 서로 덮어쓰지 않는다.
        /// </summary>
        void SetEnvironmentSpeedMultiplier(float multiplier);
    }
}
