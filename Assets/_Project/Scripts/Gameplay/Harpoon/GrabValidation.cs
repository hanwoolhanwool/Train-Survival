using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>호스트 그랩 검증 결과 (슬라이스 스펙 §2.4 — 거부 사유 3종 + M5 5차 등급 게이트).</summary>
    public enum GrabVerdict
    {
        Approved,

        /// <summary>① 대상이 이미 소멸·획득됨.</summary>
        TargetGone,

        /// <summary>② 다른 플레이어의 그랩에 점유됨.</summary>
        TargetClaimed,

        /// <summary>③ 발사 시점 플레이어 위치 ↔ 명중 지점 거리가 사거리 상한(+여유) 초과.</summary>
        OutOfRange,

        /// <summary>④ 집게 등급이 대상의 요구 등급에 못 미침 (M5 5차 — 상위 자원·동료 등 상위 대상 잠금).</summary>
        InsufficientTier
    }

    /// <summary>
    /// 호스트 그랩 검증 순수 로직 — 위치 재검증 없는 상태 검증만 (협동 PvE 치팅 수용 방침).
    /// 거리 기준점은 발사 시점 플레이어 위치다 (비행 중 이동·리드샷의 오거부 방지, §2.4).
    /// </summary>
    public static class GrabValidation
    {
        /// <summary>
        /// 등급 게이트 (M5 5차) — 집게 등급이 대상의 요구 등급 이상이어야 낚아챌 수 있다.
        /// 자원·몬스터·동료가 이 한 규칙을 공유한다 (대상이 늘어도 로직은 그대로다).
        /// </summary>
        public static bool CanLift(int harpoonTier, int requiredTier)
        {
            return harpoonTier >= requiredTier;
        }

        public static GrabVerdict Validate(
            bool targetExists, bool targetClaimedByOther,
            Vector3 firePosition, Vector3 hitPoint,
            float maxRange, float rangeTolerance,
            int harpoonTier, int requiredTier)
        {
            if (!targetExists)
            {
                return GrabVerdict.TargetGone;
            }

            if (targetClaimedByOther)
            {
                return GrabVerdict.TargetClaimed;
            }

            float limit = maxRange + rangeTolerance;
            if ((hitPoint - firePosition).sqrMagnitude > limit * limit)
            {
                return GrabVerdict.OutOfRange;
            }

            if (!CanLift(harpoonTier, requiredTier))
            {
                return GrabVerdict.InsufficientTier;
            }

            return GrabVerdict.Approved;
        }
    }
}
