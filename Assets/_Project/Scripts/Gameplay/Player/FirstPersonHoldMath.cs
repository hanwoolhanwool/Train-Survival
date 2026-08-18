using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 통합 1인칭 파지 자세의 <b>판정 계기</b> (1인칭 통합 시점 전환 계획 §1.4 · 기술 확정 ⑪) —
    /// 홀드 타깃이 <b>화면 안에 있는가</b>와 <b>팔이 닿는가</b>를 수치로 낸다. 판정 무관 표현 층위이며
    /// 순수 함수라 EditMode로 검증한다.
    ///
    /// <para>이 계산이 필요한 이유: FP 뷰모델은 카메라의 자식이라 화면 어디에나 놓을 수 있었지만,
    /// 통합 1인칭의 손은 <b>어깨에서 팔 길이 안</b>에 있어야 한다. 눈(1.6)과 어깨(1.09)의 0.51 m
    /// 낙차 때문에 "화면에 넣기"와 "팔 펴지 않기"가 서로를 밀어내고, 그 균형점을 눈대중으로
    /// 찾으면 무기마다 다시 헤맨다. 뷰랩 계기와 테스트가 같은 함수를 본다 (§3.7).</para>
    /// </summary>
    public static class FirstPersonHoldMath
    {
        /// <summary>홀드 타깃의 루트 로컬 위치 — 조준 피벗을 중심으로 피치를 태운다.</summary>
        /// <param name="pitchDegrees">표시 피치 (도, +가 내려다봄) — <see cref="WeaponHoldMath"/>와 같은 규약.</param>
        public static Vector3 HoldTargetRootLocal(
            Vector3 aimPivotLocal, float pitchDegrees, Vector3 handLocal)
        {
            return aimPivotLocal + Quaternion.Euler(pitchDegrees, 0f, 0f) * handLocal;
        }

        /// <summary>카메라의 루트 로컬 위치 — 카메라 피벗에서 피치를 태운 로컬 오프셋만큼 나아간다.</summary>
        public static Vector3 CameraRootLocal(
            Vector3 cameraPivotLocal, float pitchDegrees, Vector3 cameraLocalOffset)
        {
            return cameraPivotLocal + Quaternion.Euler(pitchDegrees, 0f, 0f) * cameraLocalOffset;
        }

        /// <summary>
        /// 루트 로컬 좌표를 <b>카메라가 보는 좌표</b>로 옮긴다 — z가 화면 안쪽, y가 위, x가 오른쪽.
        /// 카메라도 같은 피치로 돌기 때문에 회전을 되돌려야 화면 기준 각도가 나온다.
        /// </summary>
        public static Vector3 ToCameraLocal(
            Vector3 targetRootLocal, Vector3 cameraRootLocal, float pitchDegrees)
        {
            return Quaternion.Inverse(Quaternion.Euler(pitchDegrees, 0f, 0f))
                * (targetRootLocal - cameraRootLocal);
        }

        /// <summary>화면 중심에서 내려간 각 (도) — <b>+가 아래</b>. 계획 §1.3 표와 같은 부호다.</summary>
        public static float VerticalDownDegrees(Vector3 cameraLocal)
        {
            float horizontal = new Vector2(cameraLocal.x, cameraLocal.z).magnitude;
            return Mathf.Atan2(-cameraLocal.y, horizontal) * Mathf.Rad2Deg;
        }

        /// <summary>화면 중심에서 벗어난 좌우 각 (도) — <b>+가 오른쪽</b>.</summary>
        public static float HorizontalDegrees(Vector3 cameraLocal)
        {
            return Mathf.Atan2(cameraLocal.x, cameraLocal.z) * Mathf.Rad2Deg;
        }

        /// <summary>수직 반각 (도) — Unity의 <c>fieldOfView</c>는 수직 전체 각이다.</summary>
        public static float VerticalHalfFovDegrees(float fieldOfView)
        {
            return fieldOfView * 0.5f;
        }

        /// <summary>수평 반각 (도) — 화면 비율만큼 넓어진다 (16:9에서 FOV 60이면 약 45.8°).</summary>
        public static float HorizontalHalfFovDegrees(float fieldOfView, float aspect)
        {
            float halfVertical = VerticalHalfFovDegrees(fieldOfView) * Mathf.Deg2Rad;
            return Mathf.Atan(Mathf.Tan(halfVertical) * Mathf.Max(0.0001f, aspect)) * Mathf.Rad2Deg;
        }

        /// <summary>
        /// 화면 안에 들어오는가 — 카메라 뒤(z ≤ 0)는 각도와 무관하게 밖이다.
        /// 경계에 걸친 것은 "안"으로 본다 (손이 하단 경계에 오고 총열이 안으로 뻗는 배치가 §1.4의 해다).
        /// </summary>
        public static bool IsWithinFov(Vector3 cameraLocal, float fieldOfView, float aspect)
        {
            if (cameraLocal.z <= 0f)
            {
                return false;
            }

            return Mathf.Abs(VerticalDownDegrees(cameraLocal)) <= VerticalHalfFovDegrees(fieldOfView)
                && Mathf.Abs(HorizontalDegrees(cameraLocal)) <= HorizontalHalfFovDegrees(fieldOfView, aspect);
        }

        /// <summary>
        /// 팔 사용률 — 어깨에서 손까지 거리 ÷ 팔 길이. 1을 넘으면 <b>닿지 않아</b> IK가 팔을 뻗은 채
        /// 끌려간다. 0.85 이하를 권장한다 (팔꿈치를 펴지 않고 남기는 여유 — §1.4).
        /// </summary>
        public static float ReachRatio(Vector3 shoulderRootLocal, Vector3 handRootLocal, float armLength)
        {
            if (armLength <= 0.0001f)
            {
                return 0f;
            }

            return Vector3.Distance(shoulderRootLocal, handRootLocal) / armLength;
        }
    }
}
