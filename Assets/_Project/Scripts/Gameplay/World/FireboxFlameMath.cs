using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 화구 화염 세기의 순수 계산 로직 (화구 연료구 교체 계획 §3.3).
    /// 세기는 언제나 0~1 정규값이며, 실제 방출량·크기·조명 밝기로 옮기는 것은 뷰의 몫이다.
    /// </summary>
    public static class FireboxFlameMath
    {
        /// <summary>
        /// 연료 잔량이 결정하는 기본 세기 — 가득이면 1, 바닥이면 <paramref name="minIntensity"/>(잉걸불).
        /// 잔량이 줄수록 불이 잦아들어 <b>HUD를 안 봐도 압박이 보인다</b> (계획서 결정 ⑤).
        /// 저장량이 0 이하(설정 미배선)면 잔량을 알 수 없으므로 잉걸불로 폴백한다.
        /// </summary>
        public static float ComputeBaseIntensity(float fuel, float capacity, float minIntensity)
        {
            float floor = Mathf.Clamp01(minIntensity);
            if (capacity <= 0f)
            {
                return floor;
            }

            return Mathf.Lerp(floor, 1f, Mathf.Clamp01(fuel / capacity));
        }

        /// <summary>
        /// 투입 버스트의 감쇠 — 투입 순간(<paramref name="elapsed"/> 0)에 <paramref name="peak"/>,
        /// <paramref name="duration"/>에 0이 된다. 확 치솟았다가 부드럽게 잦아들도록 제곱으로 감쇠한다
        /// (선형은 꺼지는 순간이 눈에 띈다).
        /// 지속이 0 이하이거나 구간을 벗어난 경과는 버스트 없음으로 처리한다.
        /// </summary>
        public static float ComputeBurstFactor(float elapsed, float duration, float peak)
        {
            if (duration <= 0f || elapsed < 0f || elapsed >= duration)
            {
                return 0f;
            }

            float remaining = 1f - elapsed / duration;
            return Mathf.Max(0f, peak) * remaining * remaining;
        }

        /// <summary>
        /// 투입한 자원의 발열량이 결정하는 버스트 최대치 — 통나무를 넣었을 때가 넝마보다 크게 타오른다.
        /// 상한을 둬 발열량이 큰 자원을 연타해도 화면이 하얘지지 않는다.
        /// 기준 발열량이 0 이하(카탈로그 미배선)면 종류를 구분할 수 없으므로 최대치로 본다.
        /// </summary>
        public static float ComputeBurstPeak(float fuelValue, float referenceValue, float maxPeak)
        {
            float ceiling = Mathf.Max(0f, maxPeak);
            if (referenceValue <= 0f)
            {
                return ceiling;
            }

            return Mathf.Clamp(ceiling * (fuelValue / referenceValue), 0f, ceiling);
        }

        /// <summary>기본 세기에 버스트를 얹은 최종 세기 — 0~1로 잘린다.</summary>
        public static float ComposeIntensity(float baseIntensity, float burstFactor)
        {
            return Mathf.Clamp01(baseIntensity + burstFactor);
        }
    }
}
