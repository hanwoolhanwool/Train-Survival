using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>
    /// 피벗이 놓인 손 — AimPivot 로컬 X 부호로 판정한다. 손 정보를 따로 저장하지 않는 이유는
    /// 피벗 TRS가 단일 출처이기 때문 (뷰랩-씬-계획.md §8-①).
    /// </summary>
    public enum PivotHand
    {
        /// <summary>X가 0 — 좌우 어느 쪽도 아닌 정중앙 배치.</summary>
        Center,

        Right,
        Left,
    }

    /// <summary>
    /// ViewLab 순수 계산 — EditMode 테스트 대상 (docs/plans/뷰랩-씬-계획.md §6).
    /// 씬·컴포넌트 상태를 만지지 않는 함수만 둔다.
    /// </summary>
    public static class ViewLabMath
    {
        /// <summary>손 판정에서 중앙으로 볼 X 폭 — 넛지 최소 스텝(0.005)보다 작게 둔다.</summary>
        public const float HandCenterEpsilon = 1e-3f;

        /// <summary>dirty 판정 위치·스케일 허용 오차 (제곱 거리) — RigLab과 같은 감도.</summary>
        public const float PositionEpsilonSqr = 1e-5f;

        /// <summary>dirty 판정 각도 허용 오차 (°).</summary>
        public const float AngleEpsilonDegrees = 0.01f;

        /// <summary>목록 순환 인덱스 — 빈 목록은 -1.</summary>
        public static int CycleIndex(int current, int delta, int count)
        {
            if (count <= 0)
            {
                return -1;
            }

            int next = (current + delta) % count;
            return next < 0 ? next + count : next;
        }

        /// <summary>미세 스텝 토글에 따른 넛지 폭.</summary>
        public static float Step(bool fine, float coarse, float fineStep)
        {
            return fine ? fineStep : coarse;
        }

        /// <summary>축별 위치 넛지 (axis 0=X, 1=Y, 2=Z).</summary>
        public static Vector3 NudgePosition(Vector3 position, int axis, float signedStep)
        {
            position[axis] += signedStep;
            return position;
        }

        /// <summary>축별 회전 넛지 — 현재 회전의 로컬 축 기준 우곱 (RigLab 방식).</summary>
        public static Quaternion NudgeRotation(Quaternion rotation, int axis, float signedStep)
        {
            Vector3 euler = Vector3.zero;
            euler[axis] = signedStep;
            return rotation * Quaternion.Euler(euler);
        }

        /// <summary>균일 스케일 넛지 — 0 이하로 내려가 렌더러가 뒤집히지 않게 하한을 둔다.</summary>
        public static Vector3 NudgeUniformScale(Vector3 scale, float signedStep, float min = 0.01f)
        {
            float next = Mathf.Max(scale.x + signedStep, min);
            return new Vector3(next, next, next);
        }

        /// <summary>
        /// 이동 상태 버튼 → Speed 파라미터 값 — AC_Player 블렌드 트리 임계값(0/4.5/7)이
        /// <see cref="PlayerMovementSettings"/>의 걷기·달리기 속도와 같다는 전제를 그대로 쓴다.
        /// </summary>
        public static float SpeedForTier(LocomotionTier tier, float walkSpeed, float runSpeed)
        {
            switch (tier)
            {
                case LocomotionTier.Walk:
                    return walkSpeed;
                case LocomotionTier.Run:
                    return runSpeed;
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// 두 로컬 TRS의 차이 — 미저장 변경(dirty) 판정. 스케일 포함
        /// (HarpoonPivot scale 0.75가 위치·회전만 다루면 유실된다 — 계획 §7).
        /// </summary>
        public static bool TrsDiffers(
            Vector3 positionA, Quaternion rotationA, Vector3 scaleA,
            Vector3 positionB, Quaternion rotationB, Vector3 scaleB)
        {
            return (positionA - positionB).sqrMagnitude > PositionEpsilonSqr
                || Quaternion.Angle(rotationA, rotationB) > AngleEpsilonDegrees
                || (scaleA - scaleB).sqrMagnitude > PositionEpsilonSqr;
        }

        /// <summary>로컬 X 부호로 본 피벗의 손.</summary>
        public static PivotHand ResolveHand(float localX)
        {
            if (localX > HandCenterEpsilon)
            {
                return PivotHand.Right;
            }

            return localX < -HandCenterEpsilon ? PivotHand.Left : PivotHand.Center;
        }

        /// <summary>YZ 평면(로컬 X축) 기준 미러 위치 — 좌우 손 전환.</summary>
        public static Vector3 MirrorPosition(Vector3 position)
        {
            return new Vector3(-position.x, position.y, position.z);
        }

        /// <summary>
        /// YZ 평면 기준 미러 회전. 거울 반사는 방향성을 뒤집으므로 회전축은 반사하고 각도는
        /// 부호를 바꾼다 — 두 부호가 겹쳐 q(x,y,z,w) → (x,-y,-z,w)로 정리된다.
        /// 결과는 M·R·M (M = diag(-1,1,1))과 같다.
        /// </summary>
        public static Quaternion MirrorRotation(Quaternion rotation)
        {
            return new Quaternion(rotation.x, -rotation.y, -rotation.z, rotation.w);
        }

        /// <summary>직속 자식에서 이름 일치 탐색 — 프리팹 저장 시 씬 피벗↔에셋 피벗 매칭 (계획 §4-⑤).</summary>
        public static Transform FindChildByName(Transform parent, string name)
        {
            if (parent == null)
            {
                return null;
            }

            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
