using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 산탄 확산의 순수 수학 (샷건 — 기획서 §6.2). 난수(u, v)는 호출부가 주입한다
    /// (엔진 무의존·EditMode 테스트 대상, 몬스터 변종 추첨과 같은 규약).
    /// </summary>
    public static class WeaponSpreadMath
    {
        /// <summary>
        /// 전방 벡터를 원뿔(반각 spreadAngleDeg) 안의 방향으로 기울인다.
        /// u ∈ [0,1] = 기울기 표본(√u로 원판 균일 분포), v ∈ [0,1] = 방위각 표본.
        /// u = 0이거나 확산각이 0이면 전방 그대로다.
        /// </summary>
        public static Vector3 ApplySpread(Vector3 forward, float spreadAngleDeg, float u, float v)
        {
            if (spreadAngleDeg <= 0f || forward.sqrMagnitude < 0.0001f)
            {
                return forward;
            }

            forward = forward.normalized;

            // 원뿔 단면(원판)에서 균일하게 뽑으려면 반지름 방향 표본에 √를 씌운다.
            float tiltDeg = spreadAngleDeg * Mathf.Sqrt(Mathf.Clamp01(u));
            float azimuthDeg = Mathf.Clamp01(v) * 360f;

            // 전방과 평행하지 않은 기준축으로 수직 벡터를 만든다.
            Vector3 reference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.99f
                ? Vector3.right
                : Vector3.up;
            Vector3 ortho = Vector3.Cross(forward, reference).normalized;

            Vector3 tiltAxis = Quaternion.AngleAxis(azimuthDeg, forward) * ortho;
            return Quaternion.AngleAxis(tiltDeg, tiltAxis) * forward;
        }
    }
}
