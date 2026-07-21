using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 조준 보정 순수 계산 (§11 명중 판정 시차 해소).
    /// 총구는 카메라에서 어긋난 위치에 있으므로, 카메라 평행 발사 대신 조준점을 향해 수렴 발사한다.
    /// 엔진 상태 무의존 — EditMode 테스트 대상.
    /// </summary>
    public static class HarpoonAimMath
    {
        /// <summary>
        /// 조준점 수렴 발사 방향 — 총구에서 조준점(카메라 레이 명중점 또는 최대 사거리점)을 향한다.
        /// 조준점이 총구 뒤쪽이거나 사실상 총구와 겹치면(초근접 장애물) 카메라 전방으로 폴백한다.
        /// </summary>
        public static Vector3 ResolveFireDirection(Vector3 muzzle, Vector3 aimPoint, Vector3 aimForward)
        {
            Vector3 toAim = aimPoint - muzzle;
            if (toAim.sqrMagnitude < 0.0001f || Vector3.Dot(toAim, aimForward) <= 0f)
            {
                return aimForward.normalized;
            }

            return toAim.normalized;
        }
    }
}
