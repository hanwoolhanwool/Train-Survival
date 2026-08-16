using Game.Gameplay.Inventory;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 시점(피치)·파지 무기 공유 표현 (M8 1차 검증 개선 — 2026-08-16 사용자 요청).
    /// 요(yaw)는 <see cref="OwnerNetworkTransform"/>이 이미 복제하므로 <b>피치와 든 슬롯만</b>
    /// 소유자 기록 NetworkVariable로 얹는다 — "상시 대역폭 증분 0" 원칙(확장 계획 §3.1)의
    /// 의도된 예외이며, 값이 임계 이상 변할 때만 기록해 증분을 최소로 유지한다.
    /// 적용은 전 피어 로컬이다:
    /// - <b>AimPivot</b>(무기 피벗들의 부모)을 피치로 회전 — 소유자 화면에서는 무기가
    ///   카메라와 함께 돌아 화면에 고정되고, 원격 화면에서는 무기가 시선을 따라간다.
    /// - <b>가슴·머리 본</b>을 Animator 갱신 뒤(LateUpdate) 피치 비율만큼 젖힌다 — 조준
    ///   애니메이션 에셋이 없으므로(판정 결정 E) 상체 절차 회전으로 근사한다.
    /// 판정 무관 — 사격 판정은 기존대로 발사자가 실어 보낸 조준값을 쓴다.
    /// </summary>
    public sealed class PlayerAimView : NetworkBehaviour
    {
        [SerializeField] private PlayerAnimationSettings _settings;

        [Tooltip("소유자 시점 피치의 원본 — CameraRig/CameraPivot.")]
        [SerializeField] private Transform _cameraPivot;

        [Tooltip("무기 피벗들의 공통 부모 — 눈높이(y 1.6)에서 피치로 회전한다.")]
        [SerializeField] private Transform _aimPivot;

        /// <summary>시점 피치 (도, +가 내려다봄) — 소유자 기록, 임계(≈1°) 이상 변할 때만 갱신.</summary>
        private readonly NetworkVariable<float> _pitch = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        /// <summary>들고 있는 슬롯의 아이템 종류 — 소유자 기록. UI 열림·사망 중엔 None.</summary>
        private readonly NetworkVariable<int> _heldItem = new NetworkVariable<int>(
            (int)HotbarItemType.None,
            NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private PlayerCharacterView _view;
        private PlayerHealth _health;
        private HotbarController _hotbar;
        private float _currentPitch;
        private Animator _cachedAnimator;
        private Transform _chestBone;
        private Transform _headBone;

        /// <summary>
        /// 이 플레이어가 들고 있는 아이템 (복제 값) — 원격 피어의 무기 뷰모델 표시 조건.
        /// 소유자 게이트(<see cref="GunController.InputEnabled"/> 등)와 같은 의미가 되도록
        /// UI 열림·사망 중에는 None으로 기록된다.
        /// </summary>
        public HotbarItemType HeldItem => (HotbarItemType)_heldItem.Value;

        private void Awake()
        {
            _view = GetComponent<PlayerCharacterView>();
            _health = GetComponent<PlayerHealth>();
            _hotbar = GetComponent<HotbarController>();
        }

        // Animator가 본을 쓴 뒤에 겹쳐 써야 하므로 LateUpdate다 (MonsterGrabTarget과 같은 규약).
        private void LateUpdate()
        {
            if (!IsSpawned || _settings == null)
            {
                return;
            }

            bool alive = _health == null || _health.IsAlive;
            if (IsOwner)
            {
                OwnerPublish(alive);
            }
            else
            {
                _currentPitch = PlayerAnimationMath.SmoothTowards(
                    _currentPitch, _pitch.Value, _settings.PitchSmoothingHalfLifeSeconds, Time.deltaTime);
            }

            if (_aimPivot != null)
            {
                _aimPivot.localRotation = Quaternion.Euler(_currentPitch, 0f, 0f);
            }

            if (alive)
            {
                ApplyBodyPitch();
            }
        }

        /// <summary>소유자 — 자기 값은 지연 없이 쓰고, 복제는 임계 이상 변할 때만 기록한다.</summary>
        private void OwnerPublish(bool alive)
        {
            if (_cameraPivot != null)
            {
                _currentPitch = PlayerAnimationMath.SignedPitch(_cameraPivot.localEulerAngles.x);
            }

            if (Mathf.Abs(_currentPitch - _pitch.Value) > _settings.PitchReplicationThresholdDegrees)
            {
                _pitch.Value = _currentPitch;
            }

            bool holding = alive && _hotbar != null && !_hotbar.IsPanelOpen;
            int held = (int)(holding ? _hotbar.SelectedItemType : HotbarItemType.None);
            if (_heldItem.Value != held)
            {
                _heldItem.Value = held;
            }
        }

        /// <summary>
        /// 상체 절차 회전 — 가슴·머리를 피치 비율만큼 캐릭터 오른쪽 축으로 젖힌다.
        /// 사망 중에는 건너뛴다(쓰러진 몸의 목이 꺾인다). 소유자도 적용한다 — 그림자에 보인다.
        /// </summary>
        private void ApplyBodyPitch()
        {
            Animator animator = _view != null ? _view.ActiveAnimator : null;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            if (animator != _cachedAnimator)
            {
                _cachedAnimator = animator;
                Transform chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                _chestBone = chest != null ? chest : animator.GetBoneTransform(HumanBodyBones.Spine);
                _headBone = animator.GetBoneTransform(HumanBodyBones.Head);
            }

            // 기본 보정 각을 더한다 — 클립이 리타게팅 편향으로 항상 고개를 숙이고 있어(피치 0에서
            // 바닥을 봄), 보정 없이는 시선 공유의 기준점 자체가 어긋난다.
            Vector3 right = transform.right;
            if (_chestBone != null)
            {
                float chestAngle = _settings.ChestPitchOffsetDegrees + _currentPitch * _settings.ChestPitchWeight;
                _chestBone.rotation = Quaternion.AngleAxis(chestAngle, right) * _chestBone.rotation;
            }

            if (_headBone != null)
            {
                float headAngle = _settings.HeadPitchOffsetDegrees + _currentPitch * _settings.HeadPitchWeight;
                _headBone.rotation = Quaternion.AngleAxis(headAngle, right) * _headBone.rotation;
            }
        }
    }
}
