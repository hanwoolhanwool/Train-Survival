using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 통합 1인칭에서 <b>머리만 치운다</b> (1인칭 통합 시점 전환 계획 결정 ③ ⓒ) — 표현 전용.
    /// 카메라가 눈높이에 있어 머리 메시 안에 들어가므로, 머리 본을 축소해 시야에서 뺀다.
    /// Girl·Man은 단일 SkinnedMesh라 부위별 렌더러 분리가 불가능해 <b>본 스케일</b>이 유일한 수단이다.
    ///
    /// <para>이 조작은 <b>로컬 Transform 변경</b>이라 다른 피어의 화면에는 영향이 없다.
    /// 대가는 자기 화면의 자기 그림자에서 머리가 사라지는 것이다 (R4 — 검증 V8).</para>
    ///
    /// <para><see cref="IPlayerViewMode.Mode"/>만 보고 소유자 여부는 따로 확인하지 않는다 —
    /// 원격 프록시의 모드는 분리에 머물러 있어(<see cref="PlayerViewModeController.CanDrive"/>)
    /// 통합 모드라는 것 자체가 자기 화면임을 뜻한다.</para>
    ///
    /// <para>원복값을 기억했다가 되돌리므로 전환이 멱등이다 (§4.1). 모델 교대(Girl↔Man)에는
    /// 이전 본을 먼저 되돌린 뒤 새 본을 잡는다 — 축소된 값을 원복값으로 기억하면 머리가 영영 사라진다.</para>
    /// </summary>
    public sealed class FirstPersonHeadHider : MonoBehaviour
    {
        [Tooltip("머리 은닉 사용 여부와 축소 배율 — PlayerViewModeController와 같은 에셋을 물린다.")]
        [SerializeField] private PlayerViewSettings _settings;

        private PlayerCharacterView _view;
        private IPlayerViewMode _viewMode;

        private Animator _cachedAnimator;
        private Transform _headBone;
        private Vector3 _headBaseScale = Vector3.one;
        private bool _hidden;

        private void Awake()
        {
            _view = GetComponent<PlayerCharacterView>();
            _viewMode = GetComponent<IPlayerViewMode>();
        }

        private void OnDisable()
        {
            Restore();
        }

        // Animator가 본을 쓴 뒤에 겹쳐 써야 하므로 LateUpdate다 (PlayerAimView와 같은 규약).
        private void LateUpdate()
        {
            Animator animator = _view != null ? _view.ActiveAnimator : null;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            if (animator != _cachedAnimator)
            {
                Restore();
                _cachedAnimator = animator;
                _headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                _headBaseScale = _headBone != null ? _headBone.localScale : Vector3.one;
            }

            if (_headBone == null)
            {
                return;
            }

            if (ShouldHide())
            {
                // 매 프레임 쓴다 — 리타게팅이 본 스케일을 되돌려 놓을 수 있다.
                _headBone.localScale = _headBaseScale * _settings.HeadHiddenScale;
                _hidden = true;
            }
            else if (_hidden)
            {
                Restore();
            }
        }

        private bool ShouldHide()
        {
            return _settings != null
                && _settings.HideHeadBone
                && _viewMode != null
                && _viewMode.Mode == PlayerViewMode.UnifiedFirstPerson;
        }

        private void Restore()
        {
            if (_hidden && _headBone != null)
            {
                _headBone.localScale = _headBaseScale;
            }

            _hidden = false;
        }
    }
}
