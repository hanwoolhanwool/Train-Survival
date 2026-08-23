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
    /// <para><b>슬롯 틴트는 현재 꺼져 있다</b> (<c>PlayerAnimationSettings.TintPlayersBySlot</c>) —
    /// 늦게 들어온 사람만 몸이 물들어 보였다. 색표와 이 경로는 그대로 남아 있어 설정 하나로 되살아난다.</para>
    ///
    /// <para><b>소유자 자신의 몸은 시점 모드와 무관하게 언제나 그림자만 남긴다</b>
    /// (1인칭 통합 시점 전환 계획 §3.2 — 2026-08-19 사용자 확정). 화면에 보이는 것은 무기뿐이고,
    /// 몸은 그림자로만 존재한다. 메시를 켜지 않으므로 <b>그림자가 머리까지 온전</b>하다.</para>
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

        /// <summary>현재 켜져 있는 모델의 Animator — <see cref="PlayerAnimationDriver"/>가 구동한다.</summary>
        public Animator ActiveAnimator { get; private set; }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _visualSlot.Value = ResolveVisualSlot();
            }

            _visualSlot.OnValueChanged += OnVisualSlotChanged;
            Apply(_visualSlot.Value);
        }

        public override void OnNetworkDespawn()
        {
            _visualSlot.OnValueChanged -= OnVisualSlotChanged;
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
            if (renderer != null)
            {
                // 슬롯 틴트가 꺼져 있으면 _BaseColor 를 <b>아예 건드리지 않는다</b> — 흰색으로 덮어쓰면
                // 머티리얼이 원래 흰색이 아닐 때 색이 달라진다. 안 칠하는 것과 흰색으로 칠하는 것은 다르다.
                if (_settings != null && _settings.TintPlayersBySlot)
                {
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor(BaseColorId, _settings.GetPlayerColor(slot));
                    renderer.SetPropertyBlock(block);
                }

                // 소유자 몸은 카메라(y 1.6)를 가리므로 메시는 숨기고 그림자만 남긴다.
                // 시점 모드와 무관하다 — 통합 1인칭에서도 화면에 보이는 것은 손에 쥔 무기뿐이고,
                // 메시를 켜지 않으므로 그림자가 머리까지 온전하다.
                renderer.shadowCastingMode = IsOwner
                    ? UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly
                    : UnityEngine.Rendering.ShadowCastingMode.On;
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
