namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 편성 상태의 읽기 계약 (개발 가이드 §6.3 — 호스트 소유 단일 상태 모델).
    /// 방어 UI·칸 표현(CarView)·후속 시스템은 이 인터페이스로만 편성을 조회한다(변이는 호스트 전용 API로 분리).
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainState
    {
        /// <summary>편성 칸 수(파괴·이탈한 칸도 슬롯은 유지되므로 값은 불변).</summary>
        int CarCount { get; }

        /// <summary>연결부 수(= 칸 수 - 1, 초기 편성 기준 불변).</summary>
        int CouplingCount { get; }

        /// <summary>인덱스의 칸 상태를 읽는다. 범위 밖이면 false.</summary>
        bool TryGetCar(int index, out CarState car);

        /// <summary>인덱스의 연결부 상태를 읽는다. 범위 밖이면 false.</summary>
        bool TryGetCoupling(int index, out CouplingState coupling);

        /// <summary>이탈 칸이 슬롯 기준 뒤로 밀려난 거리(m). 붙어 있거나 범위 밖이면 0.</summary>
        float GetEjectOffset(int index);
    }
}
