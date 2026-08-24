using UnityEngine;

namespace Game.Gameplay.Combat
{
    /// <summary>
    /// 거치 무기 조준의 순수 수학 (M7 4차 §2.3·§2.4 — EditMode 대상).
    /// <para>
    /// <b>각도 규약</b>: yaw는 거치대 정면 기준 좌우(오른쪽 +), pitch는 <b>앙각</b>이다(위 +).
    /// 카메라 피벗의 Euler X는 아래가 +라 부호가 반대이므로, 화면에 옮길 때만 부호를 뒤집는다 —
    /// 데이터(사각 한계)는 사람이 읽는 대로 "−15°까지 내려보고 +40°까지 올려본다"로 적힌다.
    /// </para>
    /// 사각 제한은 <b>아군 오사와 포신이 칸을 뚫는 그림을 데이터로 막는 축</b>이고,
    /// 사람 조작과 자동 터렛이 <b>같은 제한</b>을 받는다 (§2.6).
    /// </summary>
    public static class MountedAimMath
    {
        /// <summary>각도를 [-180, 180) 범위로 접는다 — 누적 회전이 몇 바퀴를 돌아도 클램프가 성립하게 한다.</summary>
        public static float NormalizeAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees >= 180f)
            {
                degrees -= 360f;
            }
            else if (degrees < -180f)
            {
                degrees += 360f;
            }

            return degrees;
        }

        /// <summary>
        /// 사각 안으로 조준각을 접는다 — 조작 계층은 클램프된 값만 갖는다(밖으로 나갈 수가 없다).
        /// yaw 한계가 180° 이상이면 좌우 제한이 없다.
        /// </summary>
        public static void Clamp(
            float yawDeg, float pitchDeg, float yawLimitDeg, float pitchMinDeg, float pitchMaxDeg,
            out float clampedYaw, out float clampedPitch)
        {
            float limit = Mathf.Abs(yawLimitDeg);
            float yaw = NormalizeAngle(yawDeg);
            clampedYaw = limit >= 180f ? yaw : Mathf.Clamp(yaw, -limit, limit);
            clampedPitch = Mathf.Clamp(NormalizeAngle(pitchDeg), pitchMinDeg, pitchMaxDeg);
        }

        /// <summary>
        /// 사각 안인가 — 서버가 <b>보고된 발사 방향</b>을 되돌려 검증하는 면이다 (§2.4).
        /// 조작 계층이 클램프를 지키면 항상 참이므로, 거짓은 곧 조작된 보고다.
        /// </summary>
        public static bool IsWithinArc(
            float yawDeg, float pitchDeg, float yawLimitDeg, float pitchMinDeg, float pitchMaxDeg,
            float toleranceDeg = 1f)
        {
            float limit = Mathf.Abs(yawLimitDeg) + toleranceDeg;
            float yaw = NormalizeAngle(yawDeg);
            float pitch = NormalizeAngle(pitchDeg);

            bool yawOk = limit >= 180f || (yaw >= -limit && yaw <= limit);
            return yawOk && pitch >= pitchMinDeg - toleranceDeg && pitch <= pitchMaxDeg + toleranceDeg;
        }

        /// <summary>거치대 회전 + 조준각 → 월드 전방. 포신 회전·자동 사격이 같은 함수를 쓴다.</summary>
        public static Vector3 ResolveForward(Quaternion mountRotation, float yawDeg, float pitchDeg)
        {
            // Euler X는 아래가 +다 — 앙각을 화면 좌표계로 옮길 때만 부호를 뒤집는다.
            return mountRotation * Quaternion.Euler(-pitchDeg, yawDeg, 0f) * Vector3.forward;
        }

        /// <summary>
        /// 월드 방향 → 거치대 기준 조준각 (<see cref="ResolveForward"/>의 역). 서버가 보고된 방향을
        /// 사각으로 되돌려 보는 데 쓴다. 길이가 0인 방향은 판정할 수 없으므로 false.
        /// </summary>
        public static bool TryResolveAim(
            Quaternion mountRotation, Vector3 worldForward, out float yawDeg, out float pitchDeg)
        {
            if (worldForward.sqrMagnitude < 0.000001f)
            {
                yawDeg = 0f;
                pitchDeg = 0f;
                return false;
            }

            Vector3 local = Quaternion.Inverse(mountRotation) * worldForward.normalized;
            yawDeg = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            pitchDeg = Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;
            return true;
        }

        /// <summary>
        /// 거치대의 월드 회전 — 설치 회전(0~3 × 90°)만이 진실이다. 뷰 트랜스폼을 읽지 않으므로
        /// 뷰가 아직 스폰되지 않은 피어와 서버가 <b>같은 값</b>을 얻는다 (좌석 기준점과 같은 규약).
        /// </summary>
        public static Quaternion ResolveMountRotation(int entryRotation)
        {
            return Quaternion.Euler(0f, (entryRotation & 3) * 90f, 0f);
        }
    }
}
