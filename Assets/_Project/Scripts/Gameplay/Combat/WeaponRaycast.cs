using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 무기·도구 공통 조준 레이캐스트 — "자기 몸(root) 제외 최근접 히트" 규칙의 단일 구현.
    /// 리볼버·수리 망치의 판정 레이와 집게의 조준점 수렴이 같은 규칙을 공유한다 (M5 2차 공통 리팩터).
    /// 정적 버퍼를 공유하므로 메인 스레드 전용이며, 결과는 호출 즉시 소비해야 한다.
    /// </summary>
    public static class WeaponRaycast
    {
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[16];

        /// <summary>
        /// 최근접 히트를 찾는다 — ignoreRoot(자기 몸)의 콜라이더와 트리거는 제외.
        /// </summary>
        public static bool TryGetClosestHit(
            Vector3 origin, Vector3 direction, float maxRange, Transform ignoreRoot, out RaycastHit hit)
        {
            int count = Physics.RaycastNonAlloc(
                origin, direction, HitBuffer, maxRange, ~0, QueryTriggerInteraction.Ignore);

            float bestDistance = float.MaxValue;
            hit = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                if (HitBuffer[i].distance < bestDistance && HitBuffer[i].transform.root != ignoreRoot)
                {
                    bestDistance = HitBuffer[i].distance;
                    hit = HitBuffer[i];
                    found = true;
                }
            }

            return found;
        }

        /// <summary>
        /// 조준점을 구한다 — 맞은 것이 있으면 그 지점, 없으면 최대 사거리 지점.
        /// 집게의 조준점 수렴 발사(총구→조준점 방향)가 쓴다.
        /// </summary>
        public static Vector3 ResolveAimPoint(
            Vector3 origin, Vector3 direction, float maxRange, Transform ignoreRoot)
        {
            return TryGetClosestHit(origin, direction, maxRange, ignoreRoot, out RaycastHit hit)
                ? hit.point
                : origin + direction * maxRange;
        }
    }
}
