namespace Game.Gameplay.World
{
    /// <summary>
    /// 월드 스크롤 상태 조회 계약. 열차 원점 고정 좌표계(네트워크 문서 §4.1)에서
    /// "체감 열차 속도"와 누적 주행 거리의 유일한 출처다.
    /// 속도·거리 모두 호스트 권위이며, 클라이언트 구현은 수신 값을 외삽·스무딩한 결과를 노출한다.
    /// </summary>
    public interface IWorldScrollService
    {
        /// <summary>현재 스크롤 속도 (m/s). 호스트 권위 값.</summary>
        float ScrollSpeed { get; }

        /// <summary>누적 주행 거리 (m). 지형 타일·지상 오브젝트 위치의 공용 기준값.</summary>
        float TraveledDistance { get; }
    }
}
