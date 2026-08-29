namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 지역이 몬스터 변종 구성을 덮어쓰는 계산 (바다 지역 구현 계획 §12.3 안 ㉢).
    ///
    /// <para><b>왜 필요한가.</b> <see cref="MonsterVariantPicker"/>는 <b>Day만</b> 본다.
    /// 그래서 바다 전용 위협(물고기 점프)에 등장 Day를 걸어 두면 바다(Day 10~12)를 지나
    /// <b>대초원·북극에서도 계속 나온다</b> — 초원에 물고기가 튀어오른다.</para>
    ///
    /// <para><b>왜 변종 쪽 플래그가 아닌가.</b> "물 지역 전용" 같은 조건을 변종에 붙이면 조건이
    /// <b>변종의 소유</b>가 되어 지역은 구성을 고르지 못한다. 가중치를 지역이 덮어쓰면
    /// <i>"북극은 돌격형이 많다"</i> 같은 지역색도 <b>에셋만으로</b> 낼 수 있다.</para>
    ///
    /// <para><b>덮어쓰기이지 곱이 아니다.</b> 가중치 0이 곧 "이 지역에는 나오지 않는다"여야 하는데,
    /// 곱으로 하면 기본값이 0인 변종을 지역이 <b>되살릴 수 없다</b>(0 × 무엇이든 0).
    /// 스탬피드 전용(가중 0)처럼 기본 추첨에서 빠져 있는 변종을 특정 지역에서만 등장시키는 것이
    /// 이 축의 쓸모이므로 <b>치환</b>이다.</para>
    /// </summary>
    public static class RegionVariantWeights
    {
        /// <summary>
        /// 기본 가중치에 지역 오버라이드를 겹쳐 <paramref name="destination"/>에 쓴다.
        ///
        /// <para>매 스폰 배열을 새로 만들지 않도록 호출부가 버퍼를 넘긴다 —
        /// <see cref="MonsterVariantCatalog"/>가 캐시를 재사용하는 것과 같은 이유다.</para>
        ///
        /// <para>카탈로그에 없는 변종을 가리키는 오버라이드(인덱스 −1)는 <b>조용히 무시</b>한다.
        /// 지역 에셋이 참조하던 변종이 카탈로그에서 빠져도 그 지역의 웨이브가 멈추지 않아야 한다.</para>
        /// </summary>
        /// <param name="baseWeights">카탈로그의 변종별 기본 가중치.</param>
        /// <param name="overrideIndices">덮어쓸 변종의 카탈로그 인덱스. 없으면 그대로 둔다.</param>
        /// <param name="overrideWeights"><paramref name="overrideIndices"/>와 짝을 이루는 가중치.</param>
        /// <param name="destination">결과를 받을 버퍼. 길이가 기본 가중치와 다르면 아무것도 하지 않는다.</param>
        /// <returns>덮어쓴 항목 수. 오버라이드가 하나도 반영되지 않았으면 0.</returns>
        public static int Apply(
            float[] baseWeights, int[] overrideIndices, float[] overrideWeights, float[] destination)
        {
            if (baseWeights == null || destination == null || destination.Length != baseWeights.Length)
            {
                return 0;
            }

            for (int i = 0; i < baseWeights.Length; i++)
            {
                destination[i] = baseWeights[i];
            }

            if (overrideIndices == null || overrideWeights == null
                || overrideIndices.Length != overrideWeights.Length)
            {
                return 0;
            }

            int applied = 0;
            for (int i = 0; i < overrideIndices.Length; i++)
            {
                int index = overrideIndices[i];
                if (index < 0 || index >= destination.Length)
                {
                    continue;
                }

                destination[index] = overrideWeights[i] < 0f ? 0f : overrideWeights[i];
                applied++;
            }

            return applied;
        }
    }
}
