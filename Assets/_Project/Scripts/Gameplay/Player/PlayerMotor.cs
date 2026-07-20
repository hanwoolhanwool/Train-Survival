using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>1인칭 이동 계산 순수 로직 — EditMode 테스트 대상.</summary>
    public static class PlayerMotor
    {
        /// <summary>목표 점프 높이에 도달하는 초기 상승 속도 (v = √(2gh)).</summary>
        public static float GetJumpSpeed(float jumpHeight, float gravity)
        {
            return Mathf.Sqrt(2f * Mathf.Max(0f, gravity) * Mathf.Max(0f, jumpHeight));
        }

        /// <summary>
        /// 수평 속도 갱신. 접지 시 즉시 목표 속도, 공중에서는 이동 입력의 유효 비율(공중 제어)만큼
        /// 가속으로 목표에 접근한다 (슬라이스 스펙 §4.1 — 공중 제어 50 %).
        /// </summary>
        public static Vector3 ComputeHorizontalVelocity(
            Vector3 currentVelocity, Vector3 desiredVelocity,
            bool isGrounded, float airControlRatio, float airAcceleration, float deltaTime)
        {
            if (isGrounded)
            {
                return desiredVelocity;
            }

            float maxDelta = airAcceleration * Mathf.Clamp01(airControlRatio) * deltaTime;
            return Vector3.MoveTowards(currentVelocity, desiredVelocity, maxDelta);
        }
    }
}
