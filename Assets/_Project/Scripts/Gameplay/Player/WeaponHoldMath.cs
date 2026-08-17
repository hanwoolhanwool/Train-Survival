using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 무기 손 파지 순수 로직 — 피치→홀드 타깃 산출·가중치 블렌드 스텝 (파지 계획 §2.2).
    /// Unity API 무의존(Vector3/Quaternion 연산만) — EditMode 테스트 대상 (§4 순수 로직 분리).
    /// </summary>
    public static class WeaponHoldMath
    {
        /// <summary>
        /// 손 IK 목표 가중치 — 조준 자세 무기(총기)를 들 때만 켠다 (파지 계획 §2.3 가중치 규칙).
        /// 엔트리 없는 아이템(자원·None)·소켓 전용 무기(망치·근접)는 0.
        /// </summary>
        public static float TargetWeight(bool held, bool aimPose, float aimHandWeight)
        {
            return held && aimPose ? Mathf.Clamp01(aimHandWeight) : 0f;
        }

        /// <summary>
        /// 가중치 블렌드 스텝 — 프레임레이트 무관 지수 수렴 (오버슈트 없음).
        /// <see cref="PlayerAnimationMath.SmoothTowards"/>와 같은 반감기 규약을 재사용한다.
        /// </summary>
        public static float StepWeight(float current, float target, float halfLifeSeconds, float deltaTime)
        {
            return PlayerAnimationMath.SmoothTowards(current, target, halfLifeSeconds, deltaTime);
        }

        /// <summary>
        /// Hold 레이어 목표 가중치 (품질 업그레이드 계획 C축) — 파지 포즈가 있는 무기를 든
        /// 동안만 올린다. 클립 반입 전에는 <paramref name="layerWeight"/>가 0이라 레이어가
        /// 상체를 건드리지 않는다 (A안과 동일 동작).
        /// </summary>
        public static float TargetLayerWeight(bool held, WeaponHoldPose pose, float layerWeight)
        {
            return held && pose != WeaponHoldPose.None ? Mathf.Clamp01(layerWeight) : 0f;
        }

        /// <summary>
        /// 포즈 클립과 IK의 합성 가중치 — 클립이 자세를 맡는 만큼 IK를 비워 준다
        /// (업그레이드 계획 §2.1). 두 값의 곱이라 <b>합성 결과가 각 항의 상한을 넘지 않는다</b>.
        /// </summary>
        public static float BlendIkWithPose(float ikWeight, float ikResidualWeight)
        {
            return Mathf.Clamp01(ikWeight) * Mathf.Clamp01(ikResidualWeight);
        }

        /// <summary>
        /// 홀드 타깃 월드 자세 산출 — 루트 자식의 조준 피벗(가슴 높이)이 피치로 회전하고,
        /// 그 로컬 좌표에 배치된 홀드 타깃을 월드로 편다 (파지 계획 §2.3).
        /// 실제 Transform 계층 없이 데이터(<see cref="WeaponHoldSettings"/>)만으로 계산한다.
        /// </summary>
        public static void ComputeHoldPose(
            Vector3 rootPosition, Quaternion rootRotation, Vector3 pivotLocalPosition,
            float pitchDegrees, Vector3 handLocalPosition, Quaternion handLocalRotation,
            out Vector3 worldPosition, out Quaternion worldRotation)
        {
            Quaternion pivotRotation = rootRotation * Quaternion.Euler(pitchDegrees, 0f, 0f);
            worldPosition = rootPosition + rootRotation * pivotLocalPosition + pivotRotation * handLocalPosition;
            worldRotation = pivotRotation * handLocalRotation;
        }
    }
}
