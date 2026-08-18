using Game.Core.Events;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 캐릭터 외형 선택 + 4인 색 구분 — 표현 전용 (M8 1차 §0-8 · §2.4).
    /// 호스트가 스폰 순번을 확정·복제하면 각 피어가 같은 규칙으로 모델(Girl/Man 교대)과
    /// 틴트 색을 로컬 적용한다. 색은 머티리얼 인스턴스 분기 대신
    /// <see cref="MaterialPropertyBlock"/>으로 칠한다 (SRP Batcher 규약 — M8 1차 §2.4).
    ///
    /// <para>소유자 자신의 몸을 어떻게 보여줄지는 <see cref="PlayerViewMode"/>가 정한다
    /// (1인칭 통합 시점 전환 계획 §3.2):
    /// <b>분리 모드</b>는 그림자만 남기고(현행), <b>통합 1인칭</b>은 메시를 그대로 그리되
    /// 카메라를 가리는 <b>머리 본만 축소</b>해 치운다 (결정 ③ ⓒ). 원격 피어의 표현은
    /// 두 모드에서 완전히 같다 — 모드는 복제되지 않는다.</para>
    /// </summary>
    public sealed class PlayerCharacterView : NetworkBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private PlayerAnimationSettings _settings;
        [SerializeField] private GameObject _girlModel;
        [SerializeField] private GameObject _manModel;
        [SerializeField] private Animator _girlAnimator;
        [SerializeField] private Animator _manAnimator;
        [SerializeField] private SkinnedMeshRenderer _girlRenderer;
        [SerializeField] private SkinnedMeshRenderer _manRenderer;

        /// <summary>
        /// 시각 슬롯 = 접속자 목록 내 순번 — 호스트 확정, 전 피어 복제.
        /// clientId를 그대로 쓰면 재접속마다 커져 색·모델이 계속 바뀐다
        /// (<see cref="NetworkPlayerController"/>의 스폰 순번과 같은 근거 — M6 1차 §0 소규모 5).
        /// </summary>
        private readonly NetworkVariable<int> _visualSlot = new NetworkVariable<int>();

        private PlayerViewModeController _viewMode;
        private SkinnedMeshRenderer _activeRenderer;

        // 머리 은닉용 캐시 — 모델 교대(Girl↔Man)마다 무효화한다.
        private Animator _cachedHeadAnimator;
        private Transform _headBone;
        private Vector3 _headBaseScale = Vector3.one;
        private bool _headHidden;

        /// <summary>현재 켜져 있는 모델의 Animator — <see cref="PlayerAnimationDriver"/>가 구동한다.</summary>
        public Animator ActiveAnimator { get; private set; }

        /// <summary>현재 시점 모드 — 컨트롤러가 없거나 원격 프록시면 현행(분리)이다.</summary>
        private PlayerViewMode CurrentMode =>
            _viewMode != null ? _viewMode.Mode : PlayerViewMode.SplitFpTp;

        private void Awake()
        {
            _viewMode = GetComponent<PlayerViewModeController>();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _visualSlot.Value = ResolveVisualSlot();
            }

            _visualSlot.OnValueChanged += OnVisualSlotChanged;
            EventBus<PlayerViewModeChangedLocalEvent>.Subscribe(OnViewModeChanged);
            Apply(_visualSlot.Value);
        }

        public override void OnNetworkDespawn()
        {
            _visualSlot.OnValueChanged -= OnVisualSlotChanged;
            EventBus<PlayerViewModeChangedLocalEvent>.Unsubscribe(OnViewModeChanged);
            RestoreHeadBone();
        }

        /// <summary>
        /// 시점 모드 전환 — 몸 렌더 모드만 다시 적용한다. 머리 은닉은 <see cref="LateUpdate"/>가
        /// 매 프레임 판정하므로 여기서 건드리지 않는다 (Animator가 본을 덮어쓸 수 있다).
        /// </summary>
        private void OnViewModeChanged(PlayerViewModeChangedLocalEvent evt)
        {
            ApplyBodyVisibility();
        }

        private void OnVisualSlotChanged(int previous, int current)
        {
            Apply(current);
        }

        private void Apply(int slot)
        {
            bool useGirl = slot % 2 == 0;
            if (_girlModel != null)
            {
                _girlModel.SetActive(useGirl);
            }

            if (_manModel != null)
            {
                _manModel.SetActive(!useGirl);
            }

            ActiveAnimator = useGirl ? _girlAnimator : _manAnimator;

            SkinnedMeshRenderer renderer = useGirl ? _girlRenderer : _manRenderer;
            _activeRenderer = renderer;
            if (renderer != null)
            {
                if (_settings != null)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, _settings.GetPlayerColor(slot));
                    renderer.SetPropertyBlock(block);
                }

                ApplyBodyVisibility();
            }

            // 소유자는 상시 갱신한다 — ShadowsOnly 렌더러는 화면 밖 판정이 나기 쉬워
            // CullUpdateTransforms(프리팹 기본)와 겹치면 본 갱신이 멈춰 그림자가 끊긴다
            // (1회차 검증 버벅임 원인 ②). 원격은 실제로 보일 때만 갱신하면 된다.
            Animator animator = ActiveAnimator;
            if (animator != null)
            {
                animator.cullingMode = IsOwner
                    ? AnimatorCullingMode.AlwaysAnimate
                    : AnimatorCullingMode.CullUpdateTransforms;
            }
        }

        /// <summary>
        /// 소유자 몸의 렌더 모드 — <b>분리 모드</b>는 자기 몸이 카메라(y 1.6)를 가리므로 그림자만
        /// 남기고, <b>통합 1인칭</b>은 메시를 그대로 그린다 (내려다보면 자기 몸통·다리가 보인다).
        /// 원격 피어는 모드와 무관하게 항상 온전히 그린다 — 모드는 복제되지 않는다 (§4.2).
        /// </summary>
        private void ApplyBodyVisibility()
        {
            if (_activeRenderer == null)
            {
                return;
            }

            bool shadowsOnly = IsOwner && CurrentMode == PlayerViewMode.SplitFpTp;
            _activeRenderer.shadowCastingMode = shadowsOnly
                ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                : UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // Animator가 본을 쓴 뒤에 겹쳐 써야 하므로 LateUpdate다 (PlayerAimView와 같은 규약).
        private void LateUpdate()
        {
            if (IsSpawned)
            {
                UpdateHeadHiding();
            }
        }

        /// <summary>
        /// 머리 은닉 (결정 ③ ⓒ) — 통합 1인칭에서 카메라가 머리 메시 안에 들어가므로 머리 본을
        /// 축소해 치운다. Girl·Man은 단일 SkinnedMesh라 부위별 렌더러 분리가 불가능해
        /// <b>본 스케일</b>이 유일한 수단이다. 원복값을 기억했다가 되돌리므로 전환이 멱등이다 (§4.1).
        ///
        /// <para>이 조작은 <b>로컬 Transform 변경</b>이라 다른 피어의 화면에는 영향이 없다.
        /// 대가는 자기 화면의 자기 그림자에서 머리가 사라지는 것이다 (R4 — 검증 V8).</para>
        /// </summary>
        private void UpdateHeadHiding()
        {
            Animator animator = ActiveAnimator;
            if (animator == null || !animator.isActiveAndEnabled)
            {
                return;
            }

            if (animator != _cachedHeadAnimator)
            {
                // 모델 교대 — 이전 본을 먼저 되돌려야 축소된 값이 원복값으로 굳지 않는다.
                RestoreHeadBone();
                _cachedHeadAnimator = animator;
                _headBone = animator.GetBoneTransform(HumanBodyBones.Head);
                _headBaseScale = _headBone != null ? _headBone.localScale : Vector3.one;
            }

            if (_headBone == null)
            {
                return;
            }

            PlayerViewSettings settings = _viewMode != null ? _viewMode.Settings : null;
            bool hide = IsOwner && CurrentMode == PlayerViewMode.UnifiedFirstPerson
                && settings != null && settings.HideHeadBone;

            if (hide)
            {
                // 매 프레임 쓴다 — 리타게팅이 본 스케일을 되돌려 놓을 수 있다.
                _headBone.localScale = _headBaseScale * settings.HeadHiddenScale;
                _headHidden = true;
            }
            else if (_headHidden)
            {
                RestoreHeadBone();
            }
        }

        private void RestoreHeadBone()
        {
            if (_headHidden && _headBone != null)
            {
                _headBone.localScale = _headBaseScale;
            }

            _headHidden = false;
        }

        /// <summary>현재 접속자 목록에서의 위치 — 스폰 승인 직전 AddClient가 끝나 목록에 있다.</summary>
        private int ResolveVisualSlot()
        {
            var ids = NetworkManager.ConnectedClientsIds;
            for (int i = 0; i < ids.Count; i++)
            {
                if (ids[i] == OwnerClientId)
                {
                    return i;
                }
            }

            return ids.Count;
        }
    }
}
