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
            float residual = pose.IkResidualWeight;
            ApplyHand(AvatarIKGoal.RightHand, WeaponHoldMath.BlendIkWithPose(_rightWeight, residual),
                pose.RightHandLocalPosition, pose.RightHandLocalRotation);
            ApplyHand(AvatarIKGoal.LeftHand, WeaponHoldMath.BlendIkWithPose(_leftWeight, residual),
                pose.LeftHandLocalPosition, pose.LeftHandLocalRotation);
        }

        private void ApplyHand(AvatarIKGoal goal, float weight, Vector3 localPosition, Quaternion localRotation)
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
            _animator.SetIKRotationWeight(goal, weight);
            _animator.SetIKPosition(goal, worldPosition);
            _animator.SetIKRotation(goal, worldRotation);
        }
    }
}
