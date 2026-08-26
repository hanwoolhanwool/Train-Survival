using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 수영·잠수 계산 순수 로직 — EditMode 테스트 대상 (바다 지역 구현 계획 §6.1).
    ///
    /// <para><b>이 도메인의 핵심 명제.</b> 열차는 6 m/s로 달리고 수영은 그보다 느리다.
    /// 그래서 <b>수면에서 헤엄치면 영원히 뒤로 밀리고, 잠수해야 앞으로 갈 수 있다</b> —
    /// 수면 아래는 물살이 느리기 때문이다. 이것이 잠수의 존재 이유이며,
    /// 위험을 감수할 보상이 <b>이동 성능</b>으로 나오게 하는 장치다.</para>
    ///
    /// <para><b>수영은 네트워크 상태가 아니다.</b> 발 높이와 지역 물면 높이 둘 다 이미 모든 피어가
    /// 알고 있으므로, 각 피어가 <see cref="IsSwimming"/>으로 <b>독립 유도</b>한다 —
    /// 지역이 Day의 순수 함수인 것과 같은 규약이다. 전용 RPC도 <c>NetworkVariable</c>도 없다.</para>
    /// </summary>
    public static class SwimMotion
    {
        /// <summary>
        /// 발 기준 잠김 깊이 (m). 양수면 발이 물면 아래, 음수면 물 위다.
        /// 발 높이를 쓰는 이유는 <c>CharacterController</c> 규약상 <c>transform.position.y</c>가 곧 발이기 때문이다.
        /// </summary>
        public static float SubmergeDepth(float footY, float waterSurfaceY)
        {
            return waterSurfaceY - footY;
        }

        /// <summary>
        /// 수영 상태 판정. <b>진입·이탈 깊이를 다르게 둬 경계에서 깜빡이지 않게 한다</b> —
        /// 물결·컨베이어로 발 높이가 미세하게 오르내리기 때문이다.
        /// </summary>
        /// <param name="wasSwimming">직전 프레임의 판정. 히스테리시스의 입력이다.</param>
        public static bool IsSwimming(
            float footY, float waterSurfaceY, bool wasSwimming, float enterDepth, float exitDepth)
        {
            float depth = SubmergeDepth(footY, waterSurfaceY);
            return wasSwimming ? depth > exitDepth : depth >= enterDepth;
        }

        /// <summary>
        /// 물살(월드 컨베이어) 배율. 얕으면 1배, <paramref name="fullDepth"/>보다 깊으면
        /// <paramref name="submergedFactor"/>까지 줄어든다.
        /// <para>0.4배가 기본인 이유는 §6.1의 계산이다 — 감쇠가 없으면 수영 속도가 스크롤을 못 이겨
        /// <b>뛰어드는 순간 복귀가 불가능해진다.</b></para>
        /// </summary>
        public static float ScrollFactor(
            float depth, float startDepth, float fullDepth, float submergedFactor)
        {
            if (depth <= startDepth)
            {
                return 1f;
            }

            if (fullDepth <= startDepth)
            {
                return submergedFactor;
            }

            float t = Mathf.Clamp01((depth - startDepth) / (fullDepth - startDepth));
            return Mathf.Lerp(1f, submergedFactor, t);
        }

        /// <summary>
        /// 수직 속도 (m/s). 입력이 있으면 그 방향으로 헤엄치고, <b>없으면 부력으로 수면까지 떠오른다.</b>
        /// 수면 근처(<paramref name="enterDepth"/> 이내)에서는 부력을 끄고 멈춰 물 위로 튀어나가지 않게 한다.
        /// </summary>
        /// <param name="verticalInput">+1 상승 · −1 하강 · 0 없음.</param>
        public static float ComputeVerticalSpeed(
            float depth, int verticalInput, float swimVerticalSpeed, float buoyancySpeed, float enterDepth)
        {
            if (verticalInput > 0)
            {
                // 수면 위로는 못 올라간다 — 머리가 나오는 지점에서 멈춘다.
                return depth <= enterDepth ? 0f : swimVerticalSpeed;
            }

            if (verticalInput < 0)
            {
                return -swimVerticalSpeed;
            }

            return depth <= enterDepth ? 0f : buoyancySpeed;
        }

        /// <summary>
        /// 물살까지 반영한 <b>순 전진 속도</b> (m/s). 양수면 열차 쪽으로 다가가고, 음수면 뒤처진다.
        /// §6.1 표를 그대로 재현하는 식이라 밸런싱 판단의 기준점이 된다.
        /// </summary>
        public static float NetForwardSpeed(float swimSpeed, float scrollSpeed, float scrollFactor)
        {
            return swimSpeed - scrollSpeed * scrollFactor;
        }

        /// <summary>
        /// 뒤처짐 한계까지 남은 시간 (초). 물살에 밀리지 않으면 <see cref="float.PositiveInfinity"/>.
        /// §6.1의 "체류 창" 계산 그 자체다.
        /// </summary>
        public static float SecondsUntilFallBehind(float metersToLimit, float scrollSpeed, float scrollFactor)
        {
            float drift = scrollSpeed * scrollFactor;
            return drift <= 0f ? float.PositiveInfinity : Mathf.Max(0f, metersToLimit) / drift;
        }
    }
}
