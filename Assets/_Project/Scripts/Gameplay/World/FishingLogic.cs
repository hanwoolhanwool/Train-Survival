using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 낚시 계산 순수 로직 — EditMode 테스트 대상 (바다 지역 구현 계획 §7.3).
    ///
    /// <para><b>왜 끌낚시인가.</b> 열차는 6 m/s로 달린다. 찌를 월드에 두면 <b>3.3초 만에 집게
    /// 사거리를 벗어나</b> 입질을 기다릴 시간이 없다. 그래서 찌는 열차 소속이고,
    /// 달리는 열차에서 하는 낚시는 원래 끌낚시다.</para>
    ///
    /// <para><b>그 결과 속도가 자원이 된다.</b> 빨리 달릴수록 잘 물리므로,
    /// 기관실 속도 레버가 생기면 <b>속도 조절이 낚시 전략</b>이 된다 — 연료를 태워
    /// 물고기를 얻는 교환이다. 정지 중에는 거의 안 물린다.</para>
    /// </summary>
    public static class FishingLogic
    {
        /// <summary>
        /// 입질까지 걸리는 시간 (초). <paramref name="roll01"/>은 서버가 뽑은 0~1 난수다.
        /// <para>스크롤이 빠를수록 대기 상한이 <paramref name="minDelay"/> 쪽으로 당겨진다.
        /// 정지(속도 0)면 상한이 그대로라 <b>거의 안 물린다</b>.</para>
        /// </summary>
        /// <param name="referenceSpeed">이 속도에서 감소 효과가 최대가 된다 (기본 스크롤 6 m/s).</param>
        /// <param name="speedInfluence">속도가 상한을 얼마나 당기는가 (0~1).</param>
        public static float BiteDelaySeconds(
            float roll01, float scrollSpeed, float referenceSpeed,
            float minDelay, float maxDelay, float speedInfluence)
        {
            float lo = Mathf.Max(0f, minDelay);
            float hi = Mathf.Max(lo, maxDelay);

            float speedRatio = referenceSpeed <= 0f ? 0f : Mathf.Clamp01(scrollSpeed / referenceSpeed);
            float pull = speedRatio * Mathf.Clamp01(speedInfluence);

            float effectiveMax = Mathf.Lerp(hi, lo, pull);
            return Mathf.Lerp(lo, effectiveMax, Mathf.Clamp01(roll01));
        }

        /// <summary>
        /// 조준선이 물면(수평면 <paramref name="waterY"/>)과 만나는 지점까지의 거리.
        /// 위를 보거나 수평이면 만나지 않으므로 <b>−1</b>을 돌려준다.
        /// <para>물리 레이캐스트를 쓰지 않는 이유는 물에 콜라이더가 없기 때문이다 —
        /// 수영이 위치로 판정되듯(<c>SwimMotion</c>) 낚시도 평면 교차로 판정한다.</para>
        /// </summary>
        public static float DistanceToWaterPlane(Vector3 origin, Vector3 direction, float waterY)
        {
            if (direction.y >= -0.0001f)
            {
                return -1f;
            }

            float distance = (waterY - origin.y) / direction.y;
            return distance < 0f ? -1f : distance;
        }

        /// <summary>조준선이 사거리 안에서 물면에 닿는가 — 던질 수 있는 자리인지의 판정.</summary>
        public static bool CanCast(Vector3 origin, Vector3 direction, float waterY, float maxDistance)
        {
            float d = DistanceToWaterPlane(origin, direction, waterY);
            return d >= 0f && d <= maxDistance;
        }

        /// <summary>
        /// 지형이 조준선을 막는가 — <b>얼음낚시가 성립하는 지점</b> (북극 계획 §8.3 결정 ⑫).
        ///
        /// <para><b>왜 이것 하나로 두 문제가 풀리는가.</b> 지금까지는 조준선과 물면 평면의 교차만
        /// 봤다. 바다는 사방이 물이라 그것으로 충분했지만, 북극은 <b>얼음이 물을 덮고 있다</b> —
        /// 얼음 위를 겨눠도 그 아래 어딘가에서 평면과 만나므로 <b>찌가 얼음에 박힌 채</b> 던져진다.
        /// 차폐를 보면 결함이 막히는 동시에 *"물길·조각 사이에서만 던질 수 있다"* 는 설계 의도가
        /// <b>저절로</b> 성립한다 — 얼음낚시를 하려면 물가로 나가야 하고, 물가로 나가는 것은
        /// §5.2의 물길을 넘는 일이다.</para>
        ///
        /// <para><paramref name="blockedDistance"/>는 조준선을 막은 것까지의 거리다 —
        /// 물면까지의 거리보다 짧으면 막힌 것이다. 막은 것이 없으면 음수를 넘긴다.</para>
        /// </summary>
        public static bool IsBlockedBeforeWater(float distanceToWater, float blockedDistance)
        {
            if (distanceToWater < 0f)
            {
                return true;
            }

            // 물면 바로 앞의 접촉은 통과시킨다 — 물길 가장자리를 겨눴을 때 판정이 깜빡이지 않게 한다.
            const float Margin = 0.2f;
            return blockedDistance >= 0f && blockedDistance < distanceToWater - Margin;
        }

        /// <summary>
        /// 지역 배율을 반영한 입질 대기 (북극 = ×4 → 2.5~12초가 10~48초가 된다).
        /// 한 마리에 열차 반 칸을 지나갈 시간이 든다.
        /// </summary>
        public static float BiteDelaySeconds(
            float roll01, float scrollSpeed, float referenceSpeed,
            float minDelay, float maxDelay, float speedInfluence, float regionMultiplier)
        {
            float multiplier = Mathf.Max(0.01f, regionMultiplier);
            return BiteDelaySeconds(
                roll01, scrollSpeed, referenceSpeed,
                minDelay * multiplier, maxDelay * multiplier, speedInfluence);
        }

        /// <summary>챔질 창 안인가 — 입질 후 이 시간 안에 당겨야 걸린다.</summary>
        public static bool IsWithinHookWindow(float secondsSinceBite, float windowSeconds)
        {
            return secondsSinceBite >= 0f && secondsSinceBite <= Mathf.Max(0f, windowSeconds);
        }

        /// <summary>
        /// 한 번에 올라오는 마릿수. 대부분 1마리이고 드물게 2마리다 —
        /// <paramref name="doubleChance"/>가 그 확률이다.
        /// </summary>
        public static int CatchCount(float roll01, float doubleChance)
        {
            return Mathf.Clamp01(roll01) < Mathf.Clamp01(doubleChance) ? 2 : 1;
        }

        /// <summary>
        /// 스크롤 속도로 본 <b>시간당 기대 어획량</b> (마리/분) — 밸런싱 판단의 기준점이다.
        /// 입질 대기의 기댓값(roll 평균 0.5)에 챔질을 항상 성공한다고 가정한 상한이다.
        /// </summary>
        public static float ExpectedCatchesPerMinute(
            float scrollSpeed, float referenceSpeed, float minDelay, float maxDelay, float speedInfluence)
        {
            float average = BiteDelaySeconds(0.5f, scrollSpeed, referenceSpeed, minDelay, maxDelay, speedInfluence);
            return average <= 0f ? 0f : 60f / average;
        }
    }
}
