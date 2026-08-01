using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// Day에 따라 등장 가능한 몬스터 변종을 가중치로 추첨하는 순수 로직
    /// (기획서 §5 — "Day가 진행될수록 새로운 행동을 추가").
    /// 난수는 호출부가 넘겨 결정론을 유지한다 — 호스트가 뽑은 결과만 복제된다.
    /// </summary>
    public static class MonsterVariantPicker
    {
        /// <summary>
        /// 등장 조건을 만족하는 변종 중 하나를 가중치 비례로 고른다.
        /// </summary>
        /// <param name="dayNumber">현재 Day 번호.</param>
        /// <param name="minDays">변종별 등장 시작 Day.</param>
        /// <param name="weights">변종별 추첨 가중치.</param>
        /// <param name="roll01">0~1 난수 (호출부가 제공).</param>
        /// <returns>고른 변종 인덱스. 등장 가능한 변종이 없으면 −1.</returns>
        public static int Pick(int dayNumber, int[] minDays, float[] weights, float roll01)
        {
            if (minDays == null || weights == null || minDays.Length == 0 || weights.Length != minDays.Length)
            {
                return -1;
            }

            int day = Mathf.Max(1, dayNumber);

            float totalWeight = 0f;
            for (int i = 0; i < minDays.Length; i++)
            {
                if (day >= minDays[i] && weights[i] > 0f)
                {
                    totalWeight += weights[i];
                }
            }

            if (totalWeight <= 0f)
            {
                return -1;
            }

            float target = Mathf.Clamp01(roll01) * totalWeight;
            float accumulated = 0f;

            for (int i = 0; i < minDays.Length; i++)
            {
                if (day < minDays[i] || weights[i] <= 0f)
                {
                    continue;
                }

                accumulated += weights[i];

                // roll01 == 1 인 경계에서도 마지막 후보가 선택되도록 <= 로 비교한다.
                if (target <= accumulated)
                {
                    return i;
                }
            }

            // 부동소수 누적 오차로 어느 구간에도 걸리지 않은 경우 — 마지막 등장 가능 변종으로 되돌린다.
            for (int i = minDays.Length - 1; i >= 0; i--)
            {
                if (day >= minDays[i] && weights[i] > 0f)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
