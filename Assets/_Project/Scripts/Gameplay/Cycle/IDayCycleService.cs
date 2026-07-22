namespace Game.Gameplay.Cycle
{
    /// <summary>
    /// 낮/밤 타임라인 조회 계약 — Day 번호·국면·남은 시간의 유일한 출처.
    /// 누적 시간은 호스트 권위이며, 국면 전환은 <see cref="DayPhaseChangedEvent"/>로 통지된다.
    /// </summary>
    public interface IDayCycleService
    {
        /// <summary>1부터 시작하는 현재 Day 번호.</summary>
        int DayNumber { get; }

        DayPhase Phase { get; }

        /// <summary>현재 국면의 남은 시간 (초).</summary>
        float PhaseRemaining { get; }

        /// <summary>현재 국면의 전체 길이 (초).</summary>
        float PhaseDuration { get; }
    }
}
