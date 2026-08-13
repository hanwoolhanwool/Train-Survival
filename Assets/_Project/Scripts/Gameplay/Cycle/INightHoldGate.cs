namespace Game.Gameplay.Cycle
{
    /// <summary>
    /// 새벽 보류 게이트 (M7 2차 결정 ④) — "이 밤은 아직 끝나면 안 된다"만 알리는 조회 계약.
    /// 사이클이 소비자이고 구현은 바깥(지역 보스)에 있다 — 의존 역전이라 사이클은 보스를 모른다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    ///
    /// 보류는 <b>호스트가 누적 시간을 밤 끝 경계 직전에 클램프</b>하는 방식으로 성립한다
    /// (<see cref="NightHoldMath"/>). 낮/밤은 이미 복제된 누적 시간의 순수 함수이므로
    /// 전 피어·후발 접속이 같은 밤을 그대로 유도한다 — <b>추가 복제가 0</b>이다.
    /// </summary>
    public interface INightHoldGate
    {
        /// <summary>지금 밤을 붙잡고 있는가 (true = 새벽으로 넘어가지 않는다).</summary>
        bool IsHoldingNight { get; }
    }
}
