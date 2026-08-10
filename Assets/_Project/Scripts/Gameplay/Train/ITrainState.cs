namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 편성 상태의 읽기 계약 (개발 가이드 §6.3 — 호스트 소유 단일 상태 모델).
    /// 방어 UI·칸 표현(CarView)·후속 시스템은 이 인터페이스로만 편성을 조회한다(변이는 호스트 전용 API로 분리).
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainState
    {
        /// <summary>편성 칸 수 — 파괴·이탈한 칸도 슬롯은 유지되며, 후미 증설 시에만 늘어난다(§M3).</summary>
        int CarCount { get; }

        /// <summary>연결부 수(= 칸 수 - 1, 증설 시 함께 늘어난다).</summary>
        int CouplingCount { get; }

        /// <summary>인덱스의 칸 상태를 읽는다. 범위 밖이면 false.</summary>
        bool TryGetCar(int index, out CarState car);

        /// <summary>인덱스의 연결부 상태를 읽는다. 범위 밖이면 false.</summary>
        bool TryGetCoupling(int index, out CouplingState coupling);

        /// <summary>인덱스 칸 위 건축물 슬롯 상태를 읽는다(인덱스 = 칸 인덱스). 범위 밖이면 false.</summary>
        bool TryGetStructure(int index, out StructureState structure);

        /// <summary>이탈 칸이 슬롯 기준 뒤로 밀려난 거리(m). 붙어 있거나 범위 밖이면 0.
        /// 서버는 권위 시뮬 값을, 클라이언트는 복제 계단을 숨긴 표시 보간 값을 돌려준다(표현·잡기 게이트 용도 —
        /// 잡기 확정 등 권위 판정은 서버에서 다시 검증된다).</summary>
        float GetEjectOffset(int index);

        /// <summary>
        /// 인덱스의 칸 손잡이를 집게로 잡을 수 있는 상태인지 — 이탈 중(미부착·미파괴)이고 아직 소실 전.
        /// 정적 배치된 손잡이 앵커가 잡기 가능 시점을 게이트하는 데 쓴다(복제 데이터 기반이라 전 피어 동일 판정).
        /// </summary>
        bool IsCarGrabbable(int index);

        /// <summary>
        /// 인덱스의 연결부가 지금 공격(타겟팅) 가능한지 — 살아 있는 연결부 중 가장 후미여야 한다
        /// (연결부는 열차 끝에서부터 순차 파괴, 복제 데이터 기반이라 전 피어 동일 판정).
        /// </summary>
        bool IsCouplingTargetable(int index);

        /// <summary>
        /// 위치가 살아 있는 칸의 갑판 위인가 — 맞으면 갑판 상면 높이와 칸 인덱스를 돌려준다
        /// (M5 7차 A3 — 해제 낙하의 프레임 판정. 이탈 오프셋 반영, 복제 데이터 기반 전 피어 동일 판정.
        /// 칸 인덱스는 휴지 노드의 이탈 추종에 쓴다 — 7차 2차 D9).
        /// </summary>
        bool TryGetDeckSurface(UnityEngine.Vector3 position, out float deckHeight, out int carIndex);

        /// <summary>
        /// 인덱스 칸의 갑판이 아직 세상에 존재하는가 — 파괴(체력 0)·소실(이탈 한계 초과)이면 false.
        /// 갑판 위 휴지 노드의 회수 판정에 쓴다 (7차 2차 발견 — 칸이 사라지면 위의 물건도 함께 회수).
        /// </summary>
        bool IsDeckAlive(int carIndex);
    }
}
