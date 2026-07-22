using UnityEngine;

namespace Game.Gameplay.Cycle
{
    /// <summary>타임라인 평가 결과 — 누적 시간이 가리키는 (Day 번호, 국면, 국면 내 경과).</summary>
    public readonly struct DayTimelineState
    {
        /// <summary>1부터 시작하는 Day 번호.</summary>
        public readonly int DayNumber;

        public readonly DayPhase Phase;

        /// <summary>현재 국면 내 경과 시간 (초).</summary>
        public readonly float PhaseElapsed;

        /// <summary>현재 국면의 전체 길이 (초).</summary>
        public readonly float PhaseDuration;

        public float PhaseRemaining => Mathf.Max(0f, PhaseDuration - PhaseElapsed);

        public DayTimelineState(int dayNumber, DayPhase phase, float phaseElapsed, float phaseDuration)
        {
            DayNumber = dayNumber;
            Phase = phase;
            PhaseElapsed = phaseElapsed;
            PhaseDuration = phaseDuration;
        }
    }

    /// <summary>
    /// 낮/밤 타임라인의 순수 평가 로직 — 호스트 권위 누적 시간(초) 하나에서 전체 상태를 유도한다.
    /// 모든 피어가 같은 누적 시간으로 같은 결과를 얻으므로 국면 전환 판단이 갈라지지 않는다.
    /// </summary>
    public static class DayTimelineMath
    {
        /// <summary>누적 시간(초)을 (Day 번호, 국면, 국면 내 경과)로 평가한다. 낮 → 밤 → Day+1 순환.</summary>
        public static DayTimelineState Evaluate(float totalSeconds, float dayDuration, float nightDuration)
        {
            float cycleDuration = dayDuration + nightDuration;
            float clamped = Mathf.Max(0f, totalSeconds);

            int cycleIndex = (int)(clamped / cycleDuration);
            float timeInCycle = clamped - cycleIndex * cycleDuration;

            return timeInCycle < dayDuration
                ? new DayTimelineState(cycleIndex + 1, DayPhase.Day, timeInCycle, dayDuration)
                : new DayTimelineState(cycleIndex + 1, DayPhase.Night, timeInCycle - dayDuration, nightDuration);
        }
    }
}
