using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>연료 소모·감속의 순수 계산 로직.</summary>
    public static class FuelMath
    {
        /// <summary>연료 잔량을 소모율만큼 줄인다 (0 미만 방지).</summary>
        public static float ConsumeFuel(float fuel, float consumptionPerSecond, float deltaTime)
        {
            return Mathf.Max(0f, fuel - consumptionPerSecond * deltaTime);
        }

        /// <summary>
        /// 끌고 있는 칸 수를 반영한 초당 소모율 — 기본 소모 + 화물칸 수 × 칸당 소모
        /// (기획서 §7.1 — 칸이 늘수록 연료 소모 증가, 칸이 이탈하면 그만큼 가벼워진다).
        /// </summary>
        public static float ComputeConsumptionPerSecond(float basePerSecond, float perCarPerSecond, int attachedCarCount)
        {
            return Mathf.Max(0f, basePerSecond)
                + Mathf.Max(0f, perCarPerSecond) * Mathf.Max(0, attachedCarCount);
        }

        /// <summary>연료 잔량을 충전한다 (최대 저장량 초과 방지).</summary>
        public static float AddFuel(float fuel, float amount, float capacity)
        {
            return Mathf.Clamp(fuel + Mathf.Max(0f, amount), 0f, capacity);
        }

        /// <summary>연료 잔량이 결정하는 목표 스크롤 속도 — 잔량 있음 = 기본 속도, 고갈 = 최저 유지 속도.</summary>
        public static float ComputeTargetScrollSpeed(float baseScrollSpeed, float fuel, float depletedSpeedRatio)
        {
            return fuel > 0f ? baseScrollSpeed : baseScrollSpeed * Mathf.Clamp01(depletedSpeedRatio);
        }

        /// <summary>현재 속도를 목표 속도로 가감속률에 따라 수렴시킨다.</summary>
        public static float StepScrollSpeed(float currentSpeed, float targetSpeed, float changeRate, float deltaTime)
        {
            return Mathf.MoveTowards(currentSpeed, targetSpeed, changeRate * deltaTime);
        }
    }
}
