using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 몬스터 커스텀 조향의 순수 계산 로직 (네트워크 문서 §4.3 — NavMesh 불사용 확정).
    /// 지상 = 목표 향 조향 + 국소 회피 + 컨베이어 변위 가산, 갑판 위 = 목표 향 조향 + 국소 회피.
    /// 셋 다 단순 벡터 합성이라 호스트 단독 시뮬레이션에 부담이 없다.
    /// </summary>
    public static class MonsterSteering
    {
        /// <summary>
        /// "이동속도 > 스크롤 속도" 제약을 강제한 유효 추격 속도 (§4.3 — 추격 성립 조건).
        /// 데이터의 이동속도가 스크롤보다 느려도 최소 여유분만큼은 앞선다.
        /// </summary>
        public static float EnforceChaseSpeed(float moveSpeed, float scrollSpeed, float chaseSpeedMargin)
        {
            return Mathf.Max(moveSpeed, scrollSpeed + chaseSpeedMargin);
        }

        /// <summary>
        /// 지상 이동 속도 벡터 — 목표 향 수평 조향에 국소 회피를 섞고, 컨베이어 변위(-Z 스크롤)를 가산한다.
        /// 회피 법선은 호출자가 레이캐스트로 얻는다 (장애물 없으면 hasObstacle = false).
        /// </summary>
        public static Vector3 ComputeGroundVelocity(
            Vector3 position, Vector3 targetPosition,
            bool hasObstacle, Vector3 obstacleNormal,
            float chaseSpeed, float scrollSpeed)
        {
            Vector3 seekDirection = ComputeSeekDirection(position, targetPosition, hasObstacle, obstacleNormal);
            return seekDirection * chaseSpeed + Vector3.back * scrollSpeed;
        }

        /// <summary>
        /// 갑판 위 이동 속도 벡터 — 열차 프레임 소속이므로 컨베이어 가산이 없다.
        /// 국소 회피는 지상과 같은 법선 합산 (갑판 건축물에 막힌 채 제자리걸음하지 않게).
        /// </summary>
        public static Vector3 ComputeDeckVelocity(
            Vector3 position, Vector3 targetPosition,
            bool hasObstacle, Vector3 obstacleNormal, float moveSpeed)
        {
            return ComputeSeekDirection(position, targetPosition, hasObstacle, obstacleNormal) * moveSpeed;
        }

        private static Vector3 ComputeSeekDirection(
            Vector3 position, Vector3 targetPosition, bool hasObstacle, Vector3 obstacleNormal)
        {
            Vector3 toTarget = targetPosition - position;
            toTarget.y = 0f;

            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            Vector3 direction = toTarget.normalized;

            if (hasObstacle)
            {
                Vector3 avoid = obstacleNormal;
                avoid.y = 0f;
                Vector3 blended = direction + avoid;
                if (blended.sqrMagnitude > 0.0001f)
                {
                    direction = blended.normalized;
                }
            }

            return direction;
        }
    }
}
