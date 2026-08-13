using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>낮 하나에 유효한 스탬피드 유입 계획 (M7 1차 — 통과형 무리).</summary>
    public readonly struct StampedePlan
    {
        /// <summary>이 이벤트에서 유입될 총 마릿수.</summary>
        public readonly int TotalCount;

        /// <summary>유입 간격 (초) — 무리를 "연속 유입 열(列)"로 표현해 동시 수를 억제한다.</summary>
        public readonly float SpawnInterval;

        /// <summary>동시 존재 상한 — 밤 웨이브 상한과 독립인 대역폭 방어선.</summary>
        public readonly int MaxAlive;

        public StampedePlan(int totalCount, float spawnInterval, int maxAlive)
        {
            TotalCount = totalCount;
            SpawnInterval = spawnInterval;
            MaxAlive = maxAlive;
        }
    }

    /// <summary>
    /// 스탬피드 이벤트의 순수 계산 로직 (M7 1차, 기획서 §4.3 — <see cref="WaveMath"/>와 나란한 축).
    /// 발생 추첨·유입 계획·통과 속도를 담당한다. 난수는 호출부가 넘겨 결정론을 유지한다 —
    /// 호스트가 뽑은 결과만 세션에 반영된다 (날씨와 같은 규약).
    /// </summary>
    public static class StampedeMath
    {
        /// <summary>
        /// 낮 시작 발생 추첨 — 날씨 규약 준수: 지역 첫날은 발생하지 않는다
        /// (지형조차 아직 도착하지 않은 시점이라 전환 연출과 겹쳐 읽힌다).
        /// </summary>
        /// <param name="chancePerDay">지역 데이터의 발생 확률 (0~1). 0 = 이 지역 미발생.</param>
        /// <param name="dayInRegion">지역 안에서의 일차 (1부터).</param>
        /// <param name="roll01">0~1 난수 (호출부가 제공).</param>
        public static bool ShouldTrigger(float chancePerDay, int dayInRegion, float roll01)
        {
            if (chancePerDay <= 0f || dayInRegion <= 1)
            {
                return false;
            }

            return Mathf.Clamp01(roll01) < chancePerDay;
        }

        /// <summary>
        /// 유입 계획 확정 — 설정값을 유효 범위로 강제한다 (총량 ≥ 1, 간격 ≥ 0.1s, 동시 상한 1~cap).
        /// 동시 상한은 대역폭 방어선이므로 총량이 커도 절대 넘지 않는다.
        /// </summary>
        public static StampedePlan Plan(int totalCount, float spawnInterval, int maxAlive, int maxAliveCap)
        {
            int cap = Mathf.Max(1, maxAliveCap);

            return new StampedePlan(
                Mathf.Max(1, totalCount),
                Mathf.Max(0.1f, spawnInterval),
                Mathf.Clamp(maxAlive, 1, cap));
        }

        /// <summary>
        /// 통과 주행 속도 벡터 — 열차와 평행한 직선 주행. 전방에서 나타나 후방으로 스쳐 지나가도록
        /// 지상 자체 주행(-Z)에 컨베이어 변위(-Z 스크롤)를 가산한다 (조향·추격 없음).
        /// </summary>
        public static Vector3 ComputePassVelocity(float moveSpeed, float scrollSpeed)
        {
            return Vector3.back * (Mathf.Max(0f, moveSpeed) + Mathf.Max(0f, scrollSpeed));
        }
    }
}
