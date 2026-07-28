namespace Game.Gameplay.World
{
    /// <summary>
    /// 엔진이 끌고 있는 부하(칸 수)의 읽기 계약 — 연료 소모 가중치의 입력 (기획서 §7.1: 칸이 늘수록 연료 소모 증가).
    /// 연료 시스템(World)이 열차 도메인을 직접 참조하지 않도록 소비자 쪽에 계약을 둔다(DIP,
    /// <see cref="IWorldScrollSpeedControl"/>과 같은 ISP 분리 패턴). 열차 상태 모델이 구현·등록한다.
    /// </summary>
    public interface IFuelLoadProvider
    {
        /// <summary>기관차가 끌고 있는(연결·생존) 화물칸 수 — 기관차 자신은 세지 않는다.</summary>
        int AttachedCarCount { get; }
    }
}
