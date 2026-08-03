namespace Game.Gameplay.World
{
    /// <summary>
    /// 자원 스폰 가중 추첨의 순수 규칙 — 난수는 호출부가 주입한다 (결정론, EditMode 테스트 대상).
    /// <see cref="Game.Gameplay.Monsters"/>의 변종 추첨과 같은 규약: 가중치 비례 구간에 roll을 사상한다.
    /// </summary>
    public static class ResourceSpawnPicker
    {
        /// <summary>
        /// 가중치 배열에서 하나를 뽑는다. roll01은 [0,1) 난수.
        /// 유효 가중치(0 초과)가 없으면 -1 — 호출부가 폴백 프리팹으로 처리한다.
        /// </summary>
        public static int Pick(float[] weights, float roll01)
        {
            if (weights == null || weights.Length == 0)
            {
                return -1;
            }

            float total = 0f;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] > 0f)
                {
                    total += weights[i];
                }
            }

            if (total <= 0f)
            {
                return -1;
            }

            if (roll01 < 0f)
            {
                roll01 = 0f;
            }
            else if (roll01 >= 1f)
            {
                // 1 이상은 마지막 유효 구간으로 취급한다 (경계 안전).
                roll01 = 0.999999f;
            }

            float cursor = roll01 * total;
            for (int i = 0; i < weights.Length; i++)
            {
                if (weights[i] <= 0f)
                {
                    continue;
                }

                cursor -= weights[i];
                if (cursor < 0f)
                {
                    return i;
                }
            }

            // 부동소수 누적 오차 폴백 — 마지막 유효 인덱스.
            for (int i = weights.Length - 1; i >= 0; i--)
            {
                if (weights[i] > 0f)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
