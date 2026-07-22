namespace Game.Gameplay.World
{
    /// <summary>
    /// 월드 스크롤 속도 변경 계약 — 조회(<see cref="IWorldScrollService"/>)와 분리된 호스트 전용 제어면.
    /// 연료 감속·초가속 연출 등 속도를 바꾸는 시스템은 이 인터페이스에만 의존한다 (ISP).
    /// </summary>
    public interface IWorldScrollSpeedControl
    {
        /// <summary>스크롤 속도를 변경한다 (m/s). 호스트 전용 — 클라이언트 호출은 무시된다.</summary>
        void SetScrollSpeed(float speed);
    }
}
