namespace Game.Gameplay.Region
{
    /// <summary>
    /// 날씨 발생 추첨의 순수 판정 (M7 3차 — 지금까지 EditMode 공백이던 지점,
    /// <see href="../../../../docs/specs/region/weather-events.md">weather-events §11</see>).
    /// 북극에서 날씨가 2종이 되며 <b>추첨이 처음으로 의미를 갖는다</b> — 난수를 주입받아
    /// 발생 여부와 종류 선택을 함께 결정하고, 호스트는 결과만 복제한다.
    /// </summary>
    public static class WeatherRoll
    {
        /// <summary>맑음 — 발생하지 않았음을 뜻하는 날씨 인덱스.</summary>
        public const int Clear = -1;

        /// <summary>
        /// 발생할 날씨의 인덱스. 발생하지 않으면 <see cref="Clear"/>.
        /// </summary>
        /// <param name="dayInRegion">지역 진입 후 며칠째인가 (1 = 진입 당일).</param>
        /// <param name="weatherCount">이 지역에 등재된 날씨 수.</param>
        /// <param name="chancePerDay">하루당 발생 확률 (0~1).</param>
        /// <param name="chanceRoll">발생 판정 난수 [0, 1).</param>
        /// <param name="selectRoll">종류 선택 난수 [0, 1).</param>
        public static int Evaluate(
            int dayInRegion, int weatherCount, float chancePerDay, float chanceRoll, float selectRoll)
        {
            // 지역 진입 첫날은 날씨를 걸지 않는다 — 지형조차 아직 도착하지 않은 시점이라
            // 전환 연출과 폭풍이 겹쳐 읽힌다 (2026-08-03 검증 피드백).
            if (dayInRegion <= 1 || weatherCount <= 0 || chancePerDay <= 0f)
            {
                return Clear;
            }

            if (chanceRoll > chancePerDay)
            {
                return Clear;
            }

            // 등재된 날씨를 균등 추첨한다 — 종류별 가중치는 두지 않는다. 지역이 "합산 확률 ×
            // 균등 분배"로 표현되므로(북극 = 0.7 → 폭설·혹한파 각 0.35) 확률 조정면이 하나로 남는다.
            int index = (int)(UnityEngine.Mathf.Clamp01(selectRoll) * weatherCount);

            // selectRoll == 1 이면 인덱스가 배열 밖으로 나간다 — Random.value가 1을 포함하므로 막는다.
            return index >= weatherCount ? weatherCount - 1 : index;
        }
    }
}
