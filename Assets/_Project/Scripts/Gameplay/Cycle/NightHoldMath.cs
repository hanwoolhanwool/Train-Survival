using UnityEngine;

namespace Game.Gameplay.Cycle
{
    /// <summary>
    /// 새벽 보류의 순수 클램프 로직 (M7 2차 결정 ④ — <see cref="DayTimelineMath"/>와 나란한 축).
    /// 보류는 새 상태를 만들지 않는다 — 호스트가 올리는 누적 시간이 밤 끝 경계를 넘지 못하게
    /// 잘라낼 뿐이라, 각 피어는 평소처럼 복제된 누적 시간에서 같은 밤을 유도한다.
    /// </summary>
    public static class NightHoldMath
    {
        /// <summary>
        /// 밤 끝 경계에서 남겨 두는 여유 (초). 경계에 정확히 도달하면
        /// <see cref="DayTimelineMath.Evaluate"/>가 다음 Day의 낮으로 넘어가므로 그 직전에 세운다.
        /// </summary>
        public const float HoldMarginSeconds = 0.05f;

        /// <summary>
        /// 보류 중이면 누적 시간이 <b>현재 밤의 끝 경계</b>를 넘지 않도록 클램프한다.
        /// 보류가 꺼져 있거나 지금이 낮이면 원래 값을 그대로 돌려준다 — 낮을 붙잡지는 않는다.
        /// </summary>
        /// <param name="previousSeconds">이번 프레임 이전의 누적 시간 (초).</param>
        /// <param name="nextSeconds">가산 후의 누적 시간 (초).</param>
        /// <param name="dayDuration">낮 길이 (초).</param>
        /// <param name="nightDuration">밤 길이 (초).</param>
        /// <param name="holding">보류 중인가 (<see cref="INightHoldGate.IsHoldingNight"/>).</param>
        public static float ClampAccumulation(
            float previousSeconds, float nextSeconds, float dayDuration, float nightDuration, bool holding)
        {
            if (!holding)
            {
                return nextSeconds;
            }

            float cycleDuration = dayDuration + nightDuration;
            if (cycleDuration <= 0f)
            {
                return nextSeconds;
            }

            float clamped = Mathf.Max(0f, previousSeconds);

            // 보류는 밤에만 의미가 있다 — 낮에 게이트가 켜져 있어도 시간은 정상 진행한다.
            if (DayTimelineMath.Evaluate(clamped, dayDuration, nightDuration).Phase != DayPhase.Night)
            {
                return nextSeconds;
            }

            int cycleIndex = (int)(clamped / cycleDuration);
            float nightEnd = (cycleIndex + 1) * cycleDuration;

            return Mathf.Min(nextSeconds, nightEnd - HoldMarginSeconds);
        }
    }
}
