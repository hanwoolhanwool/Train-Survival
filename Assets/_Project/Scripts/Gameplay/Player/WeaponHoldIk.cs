using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 총기 파지 조준 자세 — Humanoid 내장 IK로 양손을 홀드 타깃에 부착한다
    /// (무기 손 파지 계획 §2.3). Animator와 같은 GameObject(Girl·Man 모델 루트)에 붙어야
    /// <see cref="OnAnimatorIK"/>가 돈다 — Base Layer IK Pass 필요.
    /// 총기(조준 자세 엔트리)는 드는 동안 항상 겨누고(결정 ③), 총구는 복제 피치를 따라
    /// 오르내린다. 판정 무관 표현 전용 — <see cref="PlayerAimView"/>의 복제 값과
    /// <see cref="WeaponHoldSettings"/> 데이터만 읽는다 (§4 MVP·DIP).
    /// <see cref="HeldWeaponSocket"/>과는 서로 모른다 (ISP) — 무기는 오른손 본 자식이라
    /// IK로 팔이 들리면 총도 자연히 조준 위치로 올라간다.
    /// </summary>
    public sealed class WeaponHoldIk : MonoBehaviour
    {
        [SerializeField] private WeaponHoldSettings _settings;

        private Animator _animator;
        private PlayerAimView _aim;
        private WeaponHoldSettings.Entry _lastEntry;
        private float _rightWeight;
        private float _leftWeight;

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _aim = GetComponentInParent<PlayerAimView>();
        }

        // Girl↔Man 전환으로 모델이 꺼졌다 켜지면 이전 블렌드 잔량이 남지 않게 한다.
        private void OnDisable()
        {
            _rightWeight = 0f;
            _leftWeight = 0f;
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0 || _animator == null || _settings == null
                || _aim == null || !_aim.IsSpawned)
            {
                return;
            }

            bool held = _settings.TryGetEntry(_aim.HeldItem, out WeaponHoldSettings.Entry entry);
            if (held)
            {
                _lastEntry = entry;
            }

            float rightTarget = WeaponHoldMath.TargetWeight(
                held, held && entry.AimPose, _settings.AimHandWeight);
            float leftTarget = held && entry.TwoHanded ? rightTarget : 0f;
            float deltaTime = Time.deltaTime;
            _rightWeight = WeaponHoldMath.StepWeight(
                _rightWeight, rightTarget, _settings.IkBlendHalfLifeSeconds, deltaTime);
            _leftWeight = WeaponHoldMath.StepWeight(
                _leftWeight, leftTarget, _settings.IkBlendHalfLifeSeconds, deltaTime);

            // 블렌드 아웃 중에는 마지막 파지 엔트리의 타깃으로 팔을 되돌린다 — 타깃 없이
            // 가중치만 남으면 손이 원점으로 튄다.
            WeaponHoldSettings.Entry pose = held ? entry : _lastEntry;
            if (pose == null)
            {
                return;
            }

            // 포즈 클립이 자세를 맡는 만큼 IK를 비운다 (업그레이드 계획 C축 §2.1) —
            // 클립 반입 전에는 잔여 가중치가 1이라 A안과 동일하게 IK 단독으로 동작한다.
            ApplyHand(AvatarIKGoal.RightHand,
                WeaponHoldMath.BlendIkWithPose(_rightWeight, pose.IkResidualWeight),
                pose.RightHandLocalPosition, pose.RightHandLocalRotation, pose.StraightenWrist);
            ApplyHand(AvatarIKGoal.LeftHand,
                WeaponHoldMath.BlendIkWithPose(_leftWeight, pose.LeftIkResidualWeight),
                pose.LeftHandLocalPosition, pose.LeftHandLocalRotation, pose.StraightenWrist);

            ApplyElbowHint(AvatarIKHint.RightElbow,
                WeaponHoldMath.BlendIkWithPose(_rightWeight, pose.RightElbowHintWeight),
                pose.RightElbowHintLocalPosition);
            ApplyElbowHint(AvatarIKHint.LeftElbow,
                WeaponHoldMath.BlendIkWithPose(_leftWeight, pose.LeftElbowHintWeight),
                pose.LeftElbowHintLocalPosition);
        }

        private void ApplyHand(
            AvatarIKGoal goal, float weight, Vector3 localPosition, Quaternion localRotation,
            bool straightenWrist)
        {
            if (weight <= 0.001f)
            {
                return;
            }

            // 스무딩된 표시 피치를 쓴다 — 가슴 절차 회전(ApplyBodyPitch)과 같은 값을 본다
            // (기술 확정 ⑥). OnAnimatorIK가 1프레임 이전 값을 읽는 지연은 수용.
            Transform root = _aim.transform;
            WeaponHoldMath.ComputeHoldPose(
                root.position, root.rotation, _settings.AimPivotLocalPosition,
                _aim.DisplayPitchDegrees, localPosition, localRotation,
                out Vector3 worldPosition, out Quaternion worldRotation);

            _animator.SetIKPositionWeight(goal, weight);
            _animator.SetIKPosition(goal, worldPosition);

            // 손목 펴기를 쓰면 회전은 IK가 아니라 LateUpdate가 잡는다 (E5) — 여기서 절대각을
            // 걸면 IK 뒤 실제 전완과 어긋난 채로 굳는다.
            _animator.SetIKRotationWeight(goal, straightenWrist ? 0f : weight);
            if (!straightenWrist)
            {
                _animator.SetIKRotation(goal, worldRotation);
            }
        }

        /// <summary>
        /// IK가 팔을 다 움직인 뒤 <b>손목만 편다</b> (E5). 전완 방향을 실측해 쓰므로 팔이 어떻게
        /// 서든 손목이 꺾이지 않는다. 무기는 손 본 자식이라 손을 돌리면 함께 따라온다.
        /// </summary>
        private void LateUpdate()
        {
            if (_animator == null || _settings == null || _aim == null || !_aim.IsSpawned
                || _lastEntry == null || !_lastEntry.StraightenWrist)
            {
                return;
            }

            StraightenWrist(HumanBodyBones.RightLowerArm, HumanBodyBones.RightHand, _rightWeight,
                _lastEntry.RightWristRollDegrees, _lastEntry.RightWristBendDegrees, mirrored: false);
            StraightenWrist(HumanBodyBones.LeftLowerArm, HumanBodyBones.LeftHand, _leftWeight,
                _lastEntry.LeftWristRollDegrees, _lastEntry.LeftWristBendDegrees, mirrored: true);
        }

        private void StraightenWrist(
            HumanBodyBones lowerArmBone, HumanBodyBones handBone, float weight,
            float rollDegrees, float bendDegrees, bool mirrored)
        {
            if (weight <= 0.001f)
            {
                return;
            }

            Transform lowerArm = _animator.GetBoneTransform(lowerArmBone);
            Transform hand = _animator.GetBoneTransform(handBone);
            if (lowerArm == null || hand == null)
            {
                return;
            }

            Quaternion straight = WeaponHoldMath.StraightWristRotation(
                hand.position - lowerArm.position, rollDegrees, bendDegrees, mirrored);

            // 파지 블렌드 중에는 클립 자세에서 서서히 옮겨간다 — 들자마자 손목이 튀지 않게.
            hand.rotation = Quaternion.Slerp(hand.rotation, straight, Mathf.Clamp01(weight));
        }

        /// <summary>
        /// 팔꿈치 스윙 방향 — 손 목표만으로는 팔꿈치가 어느 쪽으로 굽을지 정해지지 않아
        /// (2본 IK의 남는 자유도 1) 힌트로 잡는다 (포즈 편집 계획 §2.3 · E3).
        /// 가중치 0이면 아무것도 세팅하지 않아 내장 IK 기본 스윙이 그대로 남는다.
        /// </summary>
        private void ApplyElbowHint(AvatarIKHint hint, float weight, Vector3 localPosition)
        {
            if (weight <= 0.001f)
            {
                return;
            }

            Transform root = _aim.transform;
            Vector3 worldPosition = WeaponHoldMath.ComputeHoldPosition(
                root.position, root.rotation, _settings.AimPivotLocalPosition,
                _aim.DisplayPitchDegrees, localPosition);

            _animator.SetIKHintPositionWeight(hint, weight);
            _animator.SetIKHintPosition(hint, worldPosition);
        }
    }
}
