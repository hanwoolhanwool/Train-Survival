using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 환경 바람 세기의 순수 계산 (천막 계획 3차 §6.8의 대안).
    ///
    /// 바람은 <b>날씨 × 국면</b>이다 — 모래폭풍이 불면 세지고 밤이면 잦아든다.
    /// 플레이어 위치를 보지 않으므로 피어마다 값이 갈리지 않고, 결과는 전역 셰이더 값 하나라
    /// 복제도 물리도 늘지 않는다(§6.8이 상호작용 대신 이 길을 고른 이유다).
    /// </summary>
    public static class EnvironmentWindMath
    {
        /// <summary>날씨가 없을 때(맑음)의 기준 배율 — 천이 아주 멈추면 죽은 물건으로 보인다.</summary>
        public const float CalmScale = 1f;

        /// <summary>
        /// 목표 배율 = 날씨 배율 × 국면 배율.
        /// <paramref name="weatherWindScale"/>이 0 이하면(미배선 날씨 포함) 맑음과 같이 본다 —
        /// 기존 날씨 에셋이 값을 갖지 않아도 화면이 바뀌지 않게 하는 소급 규약이다.
        /// </summary>
        public static float ResolveTargetScale(float weatherWindScale, bool isNight, float nightScale)
        {
            float weather = weatherWindScale > 0f ? weatherWindScale : CalmScale;
            float phase = isNight ? Mathf.Max(0f, nightScale) : 1f;
            return weather * phase;
        }

        /// <summary>
        /// 목표로 초당 <paramref name="ratePerSecond"/>만큼 다가간다 — 날씨가 바뀌는 순간
        /// 천이 튀지 않게 한다(지역 fog가 6초 크로스페이드를 쓰는 것과 같은 결).
        /// </summary>
        public static float Step(float current, float target, float ratePerSecond, float deltaTime)
        {
            if (ratePerSecond <= 0f || deltaTime <= 0f)
            {
                return target;
            }

            return Mathf.MoveTowards(current, target, ratePerSecond * deltaTime);
        }
    }
}
