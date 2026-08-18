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
            worldPosition = ComputeHoldPosition(
                rootPosition, rootRotation, pivotLocalPosition, pitchDegrees, handLocalPosition);
            worldRotation = rootRotation * Quaternion.Euler(pitchDegrees, 0f, 0f) * handLocalRotation;
        }

        /// <summary>
        /// <b>어깨 추종 오프셋</b> (포즈 편집 계획 E6) — 홀드 타깃은 조준 피벗(루트 로컬) 기준이라
        /// 로코모션이 상체를 움직이면 어깨만 이동해 팔 자세가 달라진다. 달리기 클립은 어깨를
        /// 정지 대비 최대 21 cm 끌어내려, 어깨–손 거리가 절반 가까이 줄고 팔이 접힌다(실측).
        /// 어깨가 움직인 만큼 타깃도 옮겨 <b>정지든 달리기든 같은 팔 자세</b>를 유지한다.
        /// </summary>
        /// <param name="shoulderLocalPosition">현재 어깨의 루트 로컬 위치.</param>
        /// <param name="restLocalPosition">정지 자세 기준 어깨 위치 — 여기서는 오프셋이 0이다.</param>
        /// <param name="follow">추종 비율. 0이면 종전대로 루트에 고정된다.</param>
        public static Vector3 ShoulderFollowOffset(
            Vector3 shoulderLocalPosition, Vector3 restLocalPosition, float follow)
        {
            return (shoulderLocalPosition - restLocalPosition) * Mathf.Clamp01(follow);
        }

        /// <summary>
        /// <b>손목을 편 손 자세</b> — 손 본의 손끝 축을 실제 전완 방향에 맞추고, 남는 자유도(축 둘레
        /// 롤)만 데이터로 준다 (포즈 편집 계획 E5). 손 회전을 절대각으로 고정하면 팔이 조금만
        /// 달리 서도 그 차이가 손목 꺾임으로 남지만, 이 방식은 팔이 어떻게 서든 손목이 펴진다.
        /// <para>손 본 로컬 축은 실측 규약을 따른다 — <b>+Y = 손끝 · +Z = 손등 · −X = 엄지</b>
        /// (왼손은 미러라 <paramref name="mirrored"/>로 롤 부호를 뒤집는다).</para>
        /// </summary>
        /// <param name="forearmDirection">팔꿈치 → 손목 월드 방향.</param>
        /// <param name="rollDegrees">전완 축 둘레 회전. 0이면 손등이 하늘을 본다.</param>
        /// <param name="bendDegrees">손목 굽힘 보정 — +면 손등 쪽으로 젖힌다. 0이 곧게 편 상태.</param>
        /// <param name="mirrored">왼손이면 true.</param>
        public static Quaternion StraightWristRotation(
            Vector3 forearmDirection, float rollDegrees, float bendDegrees, bool mirrored)
        {
            Vector3 fingers = forearmDirection.sqrMagnitude > 1e-8f
                ? forearmDirection.normalized
                : Vector3.forward;

            // 롤 0 기준 = 손등이 하늘. 전완이 수직에 가까우면 기준이 무너지므로 옆 축으로 갈아탄다.
            Vector3 reference = Vector3.up - Vector3.Dot(Vector3.up, fingers) * fingers;
            if (reference.sqrMagnitude < 1e-6f)
            {
                reference = Vector3.right - Vector3.Dot(Vector3.right, fingers) * fingers;
            }

            float roll = mirrored ? -rollDegrees : rollDegrees;
            Vector3 back = Quaternion.AngleAxis(roll, fingers) * reference.normalized;

            // 굽힘은 손등 축 둘레 회전 — 손끝을 손바닥/손등 쪽으로 젖힌다.
            Vector3 bendAxis = Vector3.Cross(fingers, back);
            Quaternion bend = Quaternion.AngleAxis(mirrored ? -bendDegrees : bendDegrees, bendAxis);
            return Quaternion.LookRotation(bend * back, bend * fingers);
        }

        /// <summary>
        /// 조준 피벗 로컬 좌표 → 월드 위치 — 손 목표와 <b>팔꿈치 힌트</b>가 공유하는 변환
        /// (포즈 편집 계획 E3). 둘이 같은 좌표계를 써야 피치 구간에서 팔꿈치만 따로 놀지 않는다.
        /// </summary>
        public static Vector3 ComputeHoldPosition(
            Vector3 rootPosition, Quaternion rootRotation, Vector3 pivotLocalPosition,
            float pitchDegrees, Vector3 localPosition)
        {
            Quaternion pivotRotation = rootRotation * Quaternion.Euler(pitchDegrees, 0f, 0f);
            return rootPosition + rootRotation * pivotLocalPosition + pivotRotation * localPosition;
        }
    }
}
