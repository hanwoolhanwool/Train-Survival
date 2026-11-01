using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// Hold 레이어 구동 (무기 파지 품질 업그레이드 계획 C축 §2.1) — 복제된 파지 슬롯
    /// (<see cref="PlayerAimView.HeldItem"/>)에서 파지 포즈 카테고리를 <b>로컬 유도</b>해
    /// Animator 파라미터와 상체 레이어 가중치를 구동한다 (동기화 채널 0 — 기술 확정 ④,
    /// <see cref="PlayerAnimationDriver"/>의 Speed 로컬 유도와 같은 규약).
    /// Animator와 같은 GameObject(Girl·Man 모델 루트)에 붙는다.
    /// 판정 무관 표현 전용이며 <see cref="WeaponHoldIk"/>와 서로 참조하지 않는다 (ISP) —
    /// 각자 <see cref="HeldItem"/>과 <see cref="WeaponHoldSettings"/>만 조회한다.
    /// </summary>
    public sealed class WeaponHoldPoseDriver : MonoBehaviour
    {
        private static readonly int HoldPoseParam = Animator.StringToHash("HoldPose");
        private static readonly int SwingParam = Animator.StringToHash("Swing");

        [SerializeField] private WeaponHoldSettings _settings;

        [Tooltip("상체 파지 레이어 이름 — AC_Player의 Hold 레이어.")]
        [SerializeField] private string _holdLayerName = "Hold";

        private Animator _animator;
        private PlayerAimView _aim;
        private int _holdLayerIndex = -1;
        private float _layerWeight;
        private RaccoonEquipmentProfile _raccoon;
        private float _runStyleWeight;
        private static readonly int RunStyleParam = Animator.StringToHash("RaccoonShotgunRun");

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _aim = GetComponentInParent<PlayerAimView>();
            _raccoon=GetComponent<RaccoonEquipmentPose>()?.Profile;
        }

        private void OnEnable()
        {
            // 레이어 인덱스는 컨트롤러가 붙은 뒤에만 해석된다 — 모델 교대마다 다시 찾는다.
            _holdLayerIndex = _animator != null ? _animator.GetLayerIndex(_holdLayerName) : -1;
            _layerWeight = 0f;
            _runStyleWeight=0f;
            if(_raccoon!=null&&_raccoon.HasAuthoredShotgunRun)_animator.SetFloat(RunStyleParam,0);
            if (_holdLayerIndex >= 0)
            {
                _animator.SetLayerWeight(_holdLayerIndex, 0f);
            }
        }

        private void Update()
        {
            if (_animator == null || !_animator.isActiveAndEnabled
                || _settings == null || _aim == null || !_aim.IsSpawned)
            {
                return;
            }

            WeaponHoldSettings.Entry entry;
            bool held = _settings.TryGetEntry(_aim.HeldItem, out entry);
            WeaponHoldPose pose = held ? entry.Pose : WeaponHoldPose.None;

            _animator.SetInteger(HoldPoseParam, (int)pose);
            if(_raccoon!=null&&_raccoon.HasAuthoredShotgunRun)
            {
                float targetStyle=_aim.HeldItem==Game.Gameplay.Inventory.HotbarItemType.Shotgun?1f:0f;
                _runStyleWeight=Mathf.MoveTowards(_runStyleWeight,targetStyle,Time.deltaTime/.16f);
                _animator.SetFloat(RunStyleParam,_runStyleWeight);
            }

            if (_holdLayerIndex < 0)
            {
                return;
            }

            float target = WeaponHoldMath.TargetLayerWeight(held, pose, _settings.HoldLayerWeight);
            if(_raccoon!=null)target*=1-_raccoon.AuthoredRunWeight(_animator);
            _layerWeight = WeaponHoldMath.StepWeight(
                _layerWeight, target, _settings.HoldLayerBlendHalfLifeSeconds, Time.deltaTime);
            _animator.SetLayerWeight(_holdLayerIndex, _layerWeight);
        }

        /// <summary>
        /// TP 스윙 1발 재생 (C축 C4) — 근접·망치의 기존 연출 RPC 수신 지점에서 호출한다.
        /// 신규 RPC 없이 유지해 둔 채널(<c>PlayRemoteSwingRpc</c>)에 표현만 얹는 형태다.
        /// </summary>
        public void PlaySwing()
        {
            if (_animator != null && _animator.isActiveAndEnabled && _holdLayerIndex >= 0)
            {
                _animator.SetTrigger(SwingParam);
            }
        }
    }
}
