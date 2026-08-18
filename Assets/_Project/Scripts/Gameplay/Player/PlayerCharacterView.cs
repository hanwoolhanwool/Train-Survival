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
    /// <para>소유자 자신의 몸을 그릴지 말지는 <see cref="PlayerViewMode"/>가 정한다
    /// (1인칭 통합 시점 전환 계획 §3.2): <b>분리 모드</b>는 그림자만 남기고(현행),
    /// <b>통합 1인칭</b>은 메시를 그대로 그린다. 원격 피어의 표현은 두 모드에서 완전히 같다 —
    /// 모드는 복제되지 않는다.</para>
    ///
    /// <para>카메라를 가리는 머리를 치우는 일은 <see cref="FirstPersonHeadHider"/>가 맡는다 —
    /// 본을 만지는 것은 "어느 모델을 어떤 색으로 켤 것인가"와 변경 이유가 다르다 (SRP).</para>
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

        private IPlayerViewMode _viewMode;
        private SkinnedMeshRenderer _activeRenderer;

        /// <summary>현재 켜져 있는 모델의 Animator — <see cref="PlayerAnimationDriver"/>가 구동한다.</summary>
        public Animator ActiveAnimator { get; private set; }

        /// <summary>현재 시점 모드 — 구현체가 없거나 원격 프록시면 현행(분리)이다.</summary>
        private PlayerViewMode CurrentMode =>
            _viewMode != null ? _viewMode.Mode : PlayerViewMode.SplitFpTp;

        private void Awake()
        {
            _viewMode = GetComponent<IPlayerViewMode>();
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
        }

        /// <summary>시점 모드 전환 — 몸 렌더 모드를 다시 적용한다.</summary>
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
