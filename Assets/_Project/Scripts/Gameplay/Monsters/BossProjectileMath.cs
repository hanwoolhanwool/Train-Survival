using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 보스 원거리 투사체의 순수 탄도 (M7 2차 결정 ②-b — 사막 고유 패턴의 신규 축.
    /// 6차 최종 보스가 같은 수식을 재사용한다).
    /// 탄체를 복제하지 않는 것이 설계의 핵심이다 — 호스트는 발사 파라미터(발사점·낙점·비행 시간)만
    /// 보내고, 각 피어가 같은 수식으로 궤적을 로컬 재생한다. 판정은 낙하 시점에 호스트가 한 번만 한다.
    /// </summary>
    public static class BossProjectileMath
    {
        /// <summary>연출·판정이 공유하는 중력 가속도 (m/s²) — 몬스터 도약과 같은 값.</summary>
        public const float Gravity = 25f;

        /// <summary>
        /// 지정 비행 시간에 낙점에 도달하는 초기 속도를 구한다.
        /// 수평 성분은 등속, 수직 성분은 중력 보정을 더한 포물선이다.
        /// </summary>
        /// <param name="origin">발사 지점.</param>
        /// <param name="impact">낙점.</param>
        /// <param name="flightSeconds">비행 시간 (초). 0 이하는 최소값으로 고정된다.</param>
        /// <param name="gravity">중력 가속도.</param>
        public static Vector3 ComputeLaunchVelocity(Vector3 origin, Vector3 impact, float flightSeconds, float gravity)
        {
            float t = Mathf.Max(0.01f, flightSeconds);
            Vector3 delta = impact - origin;

            var velocity = new Vector3(delta.x / t, 0f, delta.z / t);
            velocity.y = delta.y / t + 0.5f * Mathf.Max(0f, gravity) * t;

            return velocity;
        }

        /// <summary>발사 후 경과 시간의 탄체 위치 — 각 피어가 같은 입력으로 같은 궤적을 그린다.</summary>
        public static Vector3 EvaluatePosition(Vector3 origin, Vector3 launchVelocity, float elapsed, float gravity)
        {
            float t = Mathf.Max(0f, elapsed);

            return origin + launchVelocity * t + Vector3.down * (0.5f * Mathf.Max(0f, gravity) * t * t);
        }

        /// <summary>
        /// 낙점 범위 판정 — <b>수평 거리</b>로만 본다. 지상·갑판의 높이 차이 때문에 대상을
        /// 놓치지 않게 하기 위함이며, 예고 링이 지면에 그려지는 표현과도 일치한다.
        /// </summary>
        public static bool IsWithinImpact(Vector3 impact, Vector3 targetPosition, float radius)
        {
            float dx = targetPosition.x - impact.x;
            float dz = targetPosition.z - impact.z;
            float r = Mathf.Max(0f, radius);

            return dx * dx + dz * dz <= r * r;
        }

        /// <summary>
        /// 낙점 예측 — 표적이 비행 시간 동안 흘러갈 변위를 더한다. 지상에 선 표적은 컨베이어에
        /// 실려 -Z로 밀리므로 보정 없이 쏘면 늘 뒤를 맞히고, 갑판 위 표적은 열차와 함께 정지해
        /// 있으므로 보정이 0이다. 어느 쪽이든 낙점 예고를 보고 피할 여유는 그대로 남는다.
        /// </summary>
        /// <param name="targetPosition">표적의 현재 위치.</param>
        /// <param name="targetDriftVelocity">비행 동안 표적이 실려 갈 속도 (갑판 위면 0).</param>
        /// <param name="flightSeconds">비행 시간 (초).</param>
        public static Vector3 PredictImpactPoint(Vector3 targetPosition, Vector3 targetDriftVelocity, float flightSeconds)
        {
            return targetPosition + targetDriftVelocity * Mathf.Max(0f, flightSeconds);
        }
    }
}
