using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 자동 터렛의 대상 후보 하나 (M7 4차 §2.6) — 물리 조회 결과를 순수 판정에 넘기기 위한 값이다.
    /// 어떤 컴포넌트였는지는 담지 않는다: <b>선정 규칙은 위치와 생사만 보면 된다.</b>
    /// </summary>
    public struct TurretCandidate
    {
        /// <summary>대상의 조준점 (월드).</summary>
        public Vector3 Position;

        /// <summary>지금 살아 있는가 — 죽은 대상은 후보에서 빠진다.</summary>
        public bool IsAlive;
    }

    /// <summary>
    /// 자동 터렛의 대상 선정 (M7 4차 §2.6 — EditMode 대상).
    /// <b>물리 조회는 호출부가 하고, 선정 규칙은 이 함수가 소유한다</b>
    /// (레벨 검사기·점유 판정에서 세운 것과 같은 규약).
    /// <para>
    /// 규칙은 셋이다: <b>살아 있고</b>, <b>탐색 반경 안이며</b>, <b>사각 안</b>. 그중 가장 가까운 것.
    /// 사각은 사람이 조작할 때와 <b>같은 제한</b>이다 — 자동이라고 뒤로 쏘지 않는다.
    /// </para>
    /// </summary>
    public static class TurretTargetingMath
    {
        /// <summary>
        /// 후보 중 하나를 고른다 — 없으면 -1.
        /// </summary>
        /// <param name="candidates">후보 배열 (재사용 버퍼여도 된다).</param>
        /// <param name="count">배열 앞쪽에서 실제로 채워진 개수.</param>
        /// <param name="aimOrigin">거치대의 조준 원점 (거리·방향의 기준).</param>
        /// <param name="mountRotation">거치대의 월드 회전 — 사각의 기준.</param>
        /// <param name="searchRadius">탐색 반경(m). 사거리와 별개다.</param>
        /// <param name="yawLimitDeg">좌우 사각 한계.</param>
        /// <param name="pitchMinDeg">내려다보기 한계(음수).</param>
        /// <param name="pitchMaxDeg">올려다보기 한계.</param>
        public static int SelectTarget(
            TurretCandidate[] candidates, int count, Vector3 aimOrigin, Quaternion mountRotation,
            float searchRadius, float yawLimitDeg, float pitchMinDeg, float pitchMaxDeg)
        {
            if (candidates == null || count <= 0)
            {
                return -1;
            }

            int limit = Mathf.Min(count, candidates.Length);
            float radiusSq = searchRadius * searchRadius;
            float bestSq = float.PositiveInfinity;
            int best = -1;

            for (int i = 0; i < limit; i++)
            {
                if (!candidates[i].IsAlive)
                {
                    continue;
                }

                Vector3 toTarget = candidates[i].Position - aimOrigin;
                float distanceSq = toTarget.sqrMagnitude;

                // 동률은 낮은 인덱스가 이긴다 — 같은 입력이면 같은 답이 나와야 한다.
                if (distanceSq > radiusSq || distanceSq >= bestSq)
                {
                    continue;
                }

                if (!MountedAimMath.TryResolveAim(mountRotation, toTarget, out float yaw, out float pitch)
                    || !MountedAimMath.IsWithinArc(yaw, pitch, yawLimitDeg, pitchMinDeg, pitchMaxDeg, 0f))
                {
                    continue;
                }

                bestSq = distanceSq;
                best = i;
            }

            return best;
        }
    }
}
