using System.Collections.Generic;
using Game.Core.Events;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 건축물 설치 자리의 반투명 실물 프리뷰 (건축 개편 1차 — 플레이 검증 피드백).
    /// 와이어 박스(<see cref="CarBuildGhostView"/>)는 점유 셀 영역만 보여줘 실물의 방향(회전)을
    /// 알 수 없으므로, 선택 종류의 실물 모델을 반투명 고스트 재질로 겹쳐 보여준다 —
    /// 초록(설치 가능)/빨강(불가) 틴트는 테두리 색 규약과 같다.
    /// 로컬 표현 전용 — 상태를 소유하지 않고 <see cref="StructurePlaceAimLocalEvent"/> 구독으로만 그린다.
    /// CarBuildGhost 오브젝트에 함께 배치한다.
    /// </summary>
    public sealed class StructurePlacementGhostView : MonoBehaviour
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private StructureCatalog _catalog;

        [Tooltip("고스트 공용 반투명 재질 — 프리뷰의 모든 렌더러가 이것 하나로 교체된다.")]
        [SerializeField] private Material _ghostMaterial;

        [Tooltip("건축 그리드 셀 크기 (m) — 가변 크기 프리뷰를 실제 크기로 세우는 데 쓴다. " +
            "TrainLayoutSettings._structureCellSize와 같은 값이어야 한다.")]
        [SerializeField, Min(0.25f)] private float _structureCellSize = 1f;

        [SerializeField] private Color _buildableColor = new Color(0.25f, 1f, 0.35f, 0.4f);

        [Tooltip("셀 점유·자리 점유·자원 부족으로 지금은 못 짓는 상태의 틴트 색.")]
        [SerializeField] private Color _blockedColor = new Color(1f, 0.35f, 0.25f, 0.4f);

        // 종류별 프리뷰 사본 — 재질을 통째로 갈아끼우고 콜라이더·뷰 로직을 떼어낸 로컬 전용 표현이라
        // PoolManager 풀에 되돌릴 수 없다. 종류당 1개만 만들어 세션 내내 재사용한다 (풀링 지양 규칙의 의도적 예외).
        private readonly Dictionary<StructureKind, GameObject> _previews =
            new Dictionary<StructureKind, GameObject>();

        // 종류별 프리뷰의 렌더러 — 틴트는 커서가 셀을 넘을 때마다 다시 칠하므로, 그때마다
        // GetComponentsInChildren로 배열을 새로 만들지 않도록 사본 생성 시 한 번만 모은다.
        private readonly Dictionary<StructureKind, Renderer[]> _previewRenderers =
            new Dictionary<StructureKind, Renderer[]>();

        private MaterialPropertyBlock _propertyBlock;
        private GameObject _active;

        private void Awake()
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            EventBus<StructurePlaceAimLocalEvent>.Subscribe(OnPlaceAim);
        }

        private void OnDisable()
        {
            EventBus<StructurePlaceAimLocalEvent>.Unsubscribe(OnPlaceAim);
            Hide();
        }

        private void OnPlaceAim(StructurePlaceAimLocalEvent evt)
        {
            if (!evt.Aiming || _catalog == null || _ghostMaterial == null)
            {
                Hide();
                return;
            }

            GameObject preview = GetOrCreatePreview(evt.Kind);
            if (preview == null)
            {
                Hide();
                return;
            }

            if (_active != preview)
            {
                Hide();
                _active = preview;
            }

            // 프리팹 피벗은 점유 영역 바닥 중심 — 이벤트의 박스 중심에서 바닥으로 내린다.
            Vector3 position = evt.GhostCenter - new Vector3(0f, evt.GhostSize.y * 0.5f, 0f);
            preview.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, evt.Rotation * 90f, 0f));
            preview.SetActive(true);

            // 가변 크기 종류는 프리뷰도 <b>지금 서게 될 크기</b>로 세운다 — 원본 크기로 띄우면
            // 천막 기둥 넷이 한가운데 모인 덩어리로 보여 어디에 서는지 알 수 없다
            // (천막 계획 Play 2회차 결함 ②). GetComponent를 쓴다 — TryGetComponent는 인터페이스를 못 찾는다.
            var footprintView = preview.GetComponent<IStructureFootprintView>();
            if (footprintView != null)
            {
                footprintView.ApplyFootprint(evt.FootprintWidth, evt.FootprintLength, _structureCellSize);
            }

            Color tint = evt.CanBuild ? _buildableColor : _blockedColor;
            _propertyBlock.SetColor(BaseColorId, tint);
            if (_previewRenderers.TryGetValue(evt.Kind, out Renderer[] renderers))
            {
                for (int i = 0; i < renderers.Length; i++)
                {
                    renderers[i].SetPropertyBlock(_propertyBlock);
                }
            }
        }

        private void Hide()
        {
            if (_active != null)
            {
                _active.SetActive(false);
                _active = null;
            }
        }

        /// <summary>
        /// 한 종류의 프리뷰 사본을 <b>미리</b> 만들어 둔다 —
        /// [인게임 진입 로딩 구현 계획](docs/plans/features/인게임-진입-로딩-구현-계획.md) §5.3.
        ///
        /// <para>이 사본은 원래도 <b>세션 내내 상주</b>하도록 설계됐다(아래 <c>_previews</c> 주석).
        /// 즉 메모리 총량은 그대로이고 <b>만드는 시점만 앞당겨진다</b> — 공짜에 가까운 이동이다.
        /// 그리고 이 <c>Instantiate</c> 하나가 그 종류의 메시와 텍스처를 전부 끌어오므로,
        /// 조준 첫 프레임과 설치 첫 프레임의 렉이 함께 사라진다.</para>
        ///
        /// <para><b>한 종류씩만 받는다.</b> 6종을 한 번에 만드는 <c>PrewarmAll</c>이 아니라 —
        /// 장당 텍스처가 무거워서(§0.3-A) 한 프레임에 몰면 로딩 진행바가 그 프레임에 멈춘다(§5.5).
        /// 몇 종씩 나눠 부를지는 부르는 쪽이 정한다.</para>
        ///
        /// <para>이미 있으면 아무것도 하지 않는다. 만들 수 없으면(프리팹·재질 미배치) <c>false</c>.</para>
        /// </summary>
        public bool Prewarm(StructureKind kind)
        {
            if (_catalog == null || _ghostMaterial == null)
            {
                return false;
            }

            return GetOrCreatePreview(kind) != null;
        }

        /// <summary>
        /// 종류별 프리뷰 사본을 만든다 — 실물 프리팹에서 충돌·상태 바인딩을 떼고
        /// 모든 렌더러를 고스트 재질 하나로 교체한 순수 표현 사본이다.
        /// </summary>
        private GameObject GetOrCreatePreview(StructureKind kind)
        {
            if (_previews.TryGetValue(kind, out GameObject cached) && cached != null)
            {
                return cached;
            }

            GameObject prefab = _catalog.GetViewPrefab(kind);
            if (prefab == null)
            {
                return null;
            }

            GameObject preview = Instantiate(prefab, transform);
            preview.name = "Ghost_" + kind;
            preview.SetActive(false);

            foreach (Collider collider in preview.GetComponentsInChildren<Collider>(includeInactive: true))
            {
                Destroy(collider);
            }

            StripStateBinding(preview);

            Renderer[] renderers = preview.GetComponentsInChildren<Renderer>(includeInactive: true);
            foreach (Renderer renderer in renderers)
            {
                var materials = new Material[renderer.sharedMaterials.Length];
                for (int i = 0; i < materials.Length; i++)
                {
                    materials[i] = _ghostMaterial;
                }

                renderer.sharedMaterials = materials;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _previews[kind] = preview;
            _previewRenderers[kind] = renderers;
            return preview;
        }

        /// <summary>
        /// 사본에서 <b>상태를 물고 있는 컴포넌트</b>를 뗀다 — 고스트는 순수한 그림이어야 한다.
        ///
        /// <para><b>순서가 전부다.</b> <see cref="StructureView"/>를 먼저 지우려 하면
        /// <c>[RequireComponent(typeof(StructureView))]</c>를 단 이웃 때문에 유니티가 거절하고
        /// <b>"Can't remove … because … depends on it"만 남긴 채 둘 다 살아남는다.</b>
        /// 의존하는 쪽을 먼저 지우면 같은 프레임에 둘 다 깨끗이 빠진다.</para>
        ///
        /// <para><b>이웃을 이름으로 나열하지 않는다.</b> 목록은 조용히 뒤처진다 —
        /// 거치 무기(<c>MountedWeaponView</c>)가 실제로 그렇게 늦게 합류했고, 그때 이 자리는
        /// 로딩마다 오류 한 줄을 찍기 시작했다. 대신 <c>RequireComponent</c> 선언을 읽어
        /// <b>의존한다고 스스로 밝힌 것</b>을 찾는다.</para>
        /// </summary>
        private static void StripStateBinding(GameObject preview)
        {
            Component[] components = preview.GetComponents<Component>();

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];

                // 발자국 뷰는 남긴다 — 상태(StructureEntry)를 읽지 않고 <b>모양만</b> 맞추는
                // 순수 표현이라 고스트에서도 그대로 쓴다. 이걸 떼면 가변 크기 프리뷰가
                // 원본 크기 덩어리로 뜬다 (천막 계획 Play 3회차 결함 ②).
                if (component == null || component is StructureView || component is IStructureFootprintView)
                {
                    continue;
                }

                if (DependsOnStructureView(component.GetType()))
                {
                    Destroy(component);
                }
            }

            StructureView view = preview.GetComponent<StructureView>();
            if (view != null)
            {
                Destroy(view);
            }
        }

        /// <summary><see cref="StructureView"/> 없이는 못 산다고 선언한 타입인가.</summary>
        private static bool DependsOnStructureView(System.Type type)
        {
            object[] attributes = type.GetCustomAttributes(typeof(RequireComponent), inherit: true);

            for (int i = 0; i < attributes.Length; i++)
            {
                var require = (RequireComponent)attributes[i];
                if (require.m_Type0 == typeof(StructureView)
                    || require.m_Type1 == typeof(StructureView)
                    || require.m_Type2 == typeof(StructureView))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
