namespace Game.Gameplay.Cycle
{
    /// <summary>
    /// 낮/밤 시각 연출 모드 (M8 2차 — 착수 준비 결정 ⑥). 같은 장면에서 즉시 오가며 비교하는 것이
    /// 이 차수의 검증 방식이므로, 모드는 릴리스 스위치이기 이전에 <b>검증 도구</b>다.
    /// <para>
    /// 수식(<see cref="DayVisualMath"/>)은 모드를 모른다 — 어느 수식을 쓸지 고르는 것은
    /// 적용자(<see cref="DayCycleVisualController"/>)의 몫이다.
    /// </para>
    /// </summary>
    public enum DayVisualMode : byte
    {
        /// <summary>아무것도 쓰지 않는다 — 회귀 기준선(화면이 연출 도입 전과 같아야 한다).</summary>
        Off = 0,

        /// <summary>국면 전환 구간에서만 환경광을 크로스페이드한다.</summary>
        A = 1,

        /// <summary>환경광·태양·하늘을 국면 진행도로 상시 보간한다 (A안을 포함).</summary>
        B = 2,
    }
}
