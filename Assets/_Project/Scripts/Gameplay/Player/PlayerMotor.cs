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
        ///
        /// <para><paramref name="groundAcceleration"/>가 0 이하면 <b>접지 마찰이 무한</b>이다 —
        /// 목표 속도를 즉시 돌려주는 종전 동작이고, 표면이 값을 주지 않는 네 지역이 그대로 여기 남는다.
        /// 0보다 크면 공중과 <b>같은 형태</b>로 목표에 접근한다: 미끄러짐은 새 상태가 아니라
        /// <b>이미 있는 경로에 낮은 가속을 넣은 것</b>이다 (북극 계획 §5.5).</para>
        /// </summary>
        public static Vector3 ComputeHorizontalVelocity(
            Vector3 currentVelocity, Vector3 desiredVelocity,
            bool isGrounded, float airControlRatio, float airAcceleration, float deltaTime,
            float groundAcceleration = 0f)
        {
            if (isGrounded)
            {
                if (groundAcceleration <= 0f)
                {
                    return desiredVelocity;
                }

                return Vector3.MoveTowards(currentVelocity, desiredVelocity, groundAcceleration * deltaTime);
            }

            float maxDelta = airAcceleration * Mathf.Clamp01(airControlRatio) * deltaTime;
            return Vector3.MoveTowards(currentVelocity, desiredVelocity, maxDelta);
        }

        /// <summary>
        /// 접지 가속이 정하는 <b>제동 거리</b>(m) — 미끄러짐이 실제로 무엇을 바꾸는지 재는 자다.
        ///
        /// <para>등가속 감속이므로 <c>v² / (2a)</c>. 북극 as-built: 눈 덮인 유빙 12 m/s²에서
        /// 걷기 0.84 m · 달리기 2.04 m, 맨 얼음 3 m/s²에서 걷기 3.38 m · <b>달리기 8.17 m</b>다.
        /// 넓은 물길이 5 m 이므로, 빙판에서 달려 넘으려면 <b>물길 앞에서 못 멈춘다</b> —
        /// 넘든지 빠지든지 둘 중 하나가 되는 지점이 여기서 계산된다(계획 §5.2 ↔ §5.5).</para>
        ///
        /// <para>가속이 0 이하(무한 마찰)면 0이다.</para>
        /// </summary>
        public static float StoppingDistance(float speed, float groundAcceleration)
        {
            if (groundAcceleration <= 0f)
            {
                return 0f;
            }

            float v = Mathf.Max(0f, speed);
            return v * v / (2f * groundAcceleration);
        }
    }
}
