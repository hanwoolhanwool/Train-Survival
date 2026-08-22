namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차·궤도 전체 높이의 QA 토글 계약 — 편성·손잡이·설비·궤도 타일·갑판 기준선을
    /// <b>같은 오프셋 하나</b>로 함께 올리고 내린다.
    /// 단계 확정은 호스트가 하고 전 피어에 복제되므로, 어느 피어에서 눌러도 모두 같은 높이를 본다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface ITrainElevation
    {
        /// <summary>순환할 단계 수 (기본 3 — 현재 / 아래 / 더 아래). 0이면 토글이 비활성이다.</summary>
        int StepCount { get; }

        /// <summary>지금 단계 인덱스 — 0이 씬·에셋에 굳어 있는 기준 높이다.</summary>
        int StepIndex { get; }

        /// <summary>지금 적용 중인 높이 오프셋(m) — 0이 기준, 음수가 내려간 상태다.</summary>
        float Offset { get; }

        /// <summary>다음 단계로 넘긴다(마지막 다음은 기준 높이). 서버 전용 — 클라이언트 호출은 무시된다.</summary>
        void ServerCycleStep();
    }
}
