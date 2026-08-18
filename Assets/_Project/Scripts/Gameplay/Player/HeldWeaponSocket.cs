using System;
using Game.Gameplay.Inventory;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// TP 월드모델 손 소켓 (무기 손 파지 계획 §2.2) — 활성 모델(Girl/Man)의 오른손 본을
    /// 해석해 소켓을 만들고, 무기 월드모델 1벌을 붙인 뒤 복제된 파지 슬롯
    /// (<see cref="PlayerAimView.HeldItem"/>)에 맞춰 가시성을 토글한다.
    /// 판정 무관 표현 전용 — 복제 값 읽기만 하고 상태를 소유하지 않는다 (§4 MVP).
    /// 소유자 자신은 몸체와 같은 규약으로 ShadowsOnly — 그림자에 무기가 들려 보인다
    /// (기술 확정 ④). 본 해석은 <see cref="HumanBodyBones"/> 공통 경로만 쓴다 (LSP).
    /// </summary>
    public sealed class HeldWeaponSocket : MonoBehaviour
    {
        /// <summary>무기 종류 ↔ TP 월드모델 루트 — 오프셋·자세 수치는 전부 설정 에셋이 든다.</summary>
        [Serializable]
        private struct ModelEntry
        {
            public HotbarItemType ItemType;
            public Transform Model;
        }

        [SerializeField] private WeaponHoldSettings _settings;

        [Tooltip("TP 월드모델 목록 — 프리팹의 TpWeaponHolder 아래 1벌.")]
        [SerializeField] private ModelEntry[] _models;

        private PlayerAimView _aim;
        private PlayerCharacterView _view;
        private PlayerViewModeController _viewMode;
        private Animator _cachedAnimator;
        private Transform _socket;
        private Renderer[][] _renderers;
        private bool[] _visible;
        private bool[] _shadowOnly;
        private Vector3[] _baseScales;

        private void Awake()
        {
            _aim = GetComponent<PlayerAimView>();
            _view = GetComponent<PlayerCharacterView>();
            _viewMode = GetComponent<PlayerViewModeController>();

            int count = _models != null ? _models.Length : 0;
            _renderers = new Renderer[count][];
            _visible = new bool[count];
            _shadowOnly = new bool[count];
            _baseScales = new Vector3[count];
            for (int i = 0; i < count; i++)
            {
                Transform model = _models[i].Model;
                _renderers[i] = model != null
                    ? model.GetComponentsInChildren<Renderer>(includeInactive: true)
                    : Array.Empty<Renderer>();

                // 무기 모델의 임포트 스케일(FBX 0.01 보정)은 프리팹 배치가 이미 갖고 있다 —
                // 설정의 스케일은 그 위에 얹는 배율이므로 원본을 기억해 둔다.
                _baseScales[i] = model != null ? model.localScale : Vector3.one;
                _visible[i] = true;
                SetVisible(i, visible: false, ownerShadowOnly: false);
            }
        }

        // 본 재해석·가시성 폴링 — Animator 갱신 뒤가 안전한 시점이다 (PlayerAimView와 같은 규약).
        private void LateUpdate()
        {
            Animator animator = _view != null ? _view.ActiveAnimator : null;
            bool ready = animator != null && animator.isActiveAndEnabled
                && _aim != null && _aim.IsSpawned;
            if (!ready)
            {
                HideAll();
                return;
            }

            if (animator != _cachedAnimator)
            {
                // Girl↔Man 전환 — 새 모델의 오른손 본으로 소켓을 옮겨 단다 (기술 확정 ⑤).
                if (!TryAttachSocket(animator))
                {
                    HideAll();
                    return;
                }

                _cachedAnimator = animator;
            }

            ApplyVisibility(_aim.HeldItem);
        }

        /// <summary>오른손 본 아래 소켓을 (재)부착하고 월드모델 전부를 그 자식으로 옮긴다.</summary>
        private bool TryAttachSocket(Animator animator)
        {
            Transform hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (hand == null)
            {
                return false;
            }

            if (_socket == null)
            {
                _socket = new GameObject("HeldWeaponSocket").transform;
            }

            _socket.SetParent(hand, worldPositionStays: false);
            _socket.localPosition = Vector3.zero;
            _socket.localRotation = Quaternion.identity;
            _socket.localScale = NormalizeToRootScale(hand);

            for (int i = 0; i < _models.Length; i++)
            {
                Transform model = _models[i].Model;
                if (model == null)
                {
                    continue;
                }

                model.SetParent(_socket, worldPositionStays: false);
                if (_settings != null && _settings.TryGetEntry(_models[i].ItemType, out WeaponHoldSettings.Entry entry))
                {
                    model.localPosition = entry.SocketLocalPosition;
                    model.localRotation = entry.SocketLocalRotation;
                    model.localScale = Vector3.Scale(_baseScales[i], entry.SocketLocalScale);
                }
            }

            return true;
        }

        /// <summary>
        /// 손 본은 FBX 임포트 스케일(0.01)의 역수를 물고 있어 <c>lossyScale</c>이 100이다
        /// (Girl·Man 공통 실측). 소켓을 플레이어 루트와 같은 배율로 되돌려, 설정의 소켓
        /// 오프셋이 <b>미터 단위</b>로 읽히고 무기 크기가 100배로 튀지 않게 한다.
        /// </summary>
        private Vector3 NormalizeToRootScale(Transform hand)
        {
            Vector3 boneScale = hand.lossyScale;
            Vector3 rootScale = transform.lossyScale;
            return new Vector3(
                SafeRatio(rootScale.x, boneScale.x),
                SafeRatio(rootScale.y, boneScale.y),
                SafeRatio(rootScale.z, boneScale.z));
        }

        private static float SafeRatio(float numerator, float denominator)
        {
            return Mathf.Approximately(denominator, 0f) ? 1f : numerator / denominator;
        }

        /// <summary>
        /// 든 무기만 보인다 — 원격 피어는 온전한 표시, 소유자는 시점 모드가 정한다 (기술 확정 ④):
        /// <b>분리 모드</b>는 화면 전용 FP 뷰모델이 그 자리를 맡으므로 그림자만 남기고,
        /// <b>통합 1인칭</b>은 이 손 무기가 곧 화면에 보이는 무기다
        /// (1인칭 통합 시점 전환 계획 §3.2).
        /// </summary>
        private void ApplyVisibility(HotbarItemType held)
        {
            bool unified = _viewMode != null
                && _viewMode.Mode == PlayerViewMode.UnifiedFirstPerson;
            bool ownerShadowOnly = _aim.IsOwner && !unified;
            for (int i = 0; i < _models.Length; i++)
            {
                SetVisible(i, visible: held != HotbarItemType.None && _models[i].ItemType == held, ownerShadowOnly);
            }
        }

        private void HideAll()
        {
            for (int i = 0; i < _visible.Length; i++)
            {
                SetVisible(i, visible: false, ownerShadowOnly: false);
            }
        }

        /// <summary>
        /// 표시 상태 적용 — <b>그림자 모드도 함께 비교</b>한다. 무기를 든 채 시점 모드를 바꾸면
        /// 표시 여부는 그대로이고 그림자 모드만 달라지는데, 표시 여부만 보고 조기 반환하면
        /// 그 전환이 화면에 반영되지 않는다 (1인칭 통합 계획 §4.1 — 전환 멱등성).
        /// </summary>
        private void SetVisible(int index, bool visible, bool ownerShadowOnly)
        {
            if (_visible[index] == visible && _shadowOnly[index] == ownerShadowOnly)
            {
                return;
            }

            _visible[index] = visible;
            _shadowOnly[index] = ownerShadowOnly;
            Renderer[] renderers = _renderers[index];
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].enabled = visible;
                renderers[i].shadowCastingMode = ownerShadowOnly
                    ? ShadowCastingMode.ShadowsOnly
                    : ShadowCastingMode.On;
            }
        }
    }
}
