using Game.Gameplay.Region;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지형 타일이 <b>끝나는 자리를 덮는</b> 지평선 판 한 장.
    ///
    /// <para><b>왜 필요한가.</b> 활성 타일은 앞 <c>TilesAhead × 40 m</c>(현행 200~240 m)까지뿐이라
    /// 그 너머는 절단면이다. 숲·바다는 fog가 짙어(0.0062) 200 m에서 <b>78 %</b>가 지워지므로
    /// 아무도 그 끝을 못 본다. 그런데 원경(산 능선·유적)을 살리려 fog를 얇게 깐 지역은
    /// 사막 0.0015 <b>8.6 %</b> · 북극 0.0017 <b>10.9 %</b> · 대초원 0.0012 <b>5.6 %</b>밖에
    /// 가리지 못해 <b>지형이 뚝 끊긴 것이 그대로 보인다.</b></para>
    ///
    /// <para><b>왜 타일을 늘리지 않았나.</b> 앞뒤 한 칸씩(9 → 11장, +22 %)은 실측 대조로
    /// 드로우콜 약 1,032 → 1,260 · gpu 약 +9~10 %인데(예산 §7 4차: 9 → 7장에서 gpu −9.5 % ·
    /// 드로우콜 −25.9 %), 절단면은 대초원 기준 5.6 % → <b>8.0 %</b>로만 가려진다 —
    /// <b>92 %가 그대로 남는다.</b> 끝을 더 멀리 미는 것으로는 끝이 사라지지 않는다.
    /// 판 한 장은 드로우콜 <b>+1</b>로 절단면을 지평선까지 잇는다.</para>
    ///
    /// <para><b>켜고 끄는 것이 아니라 구간으로 자른다</b>(<see cref="RegionBlendQuery.TryResolveSpanFor"/>) —
    /// 수면과 같은 규약이다. 이 판도 반경 1.5 km라 발밑 한 점보다 훨씬 넓어서, 발밑만 보고
    /// 껐다면 경계에서 <b>지평선이 한 프레임에 사라졌을</b> 것이다. 잘린 끝은 지역 경계에
    /// 정확히 서므로 그 자리에서 다음 지역 지형으로 이어진다.</para>
    ///
    /// <para>지평선을 배선하지 않은 지역에서는 <b>아예 그리지 않는다</b> — 그 지역은 fog가
    /// 짙어 절단면이 보이지 않을뿐더러, 바다(물면 −4)에서는 판이 <b>물 위에 떠서 바다를 덮는다.</b></para>
    ///
    /// <para>지형과 <b>같은 라이팅</b>을 받아야 이어져 보이므로 머티리얼은 지형과 같은 계열을 쓴다.
    /// 그림자는 주지도 받지도 않는다 — 3 km짜리 판이 캐스케이드를 먹으면 그림자 해상도가 통째로 떨어진다.</para>
    ///
    /// <para>Game 씬에 1개 배치한다. <see cref="SeaSurfaceView"/>와 같은 규약으로 런타임에
    /// 스스로 판을 만든다 — 씬에 3 km 오브젝트를 두면 에디터 작업이 불편하고 실수로 옮기기 쉽다.</para>
    /// </summary>
    public sealed class DistantHorizonPlane : MonoBehaviour
    {
        [Tooltip("지평선 판 머티리얼 — 지형과 같은 계열(SimpleLit)을 쓴다. 색은 지역이 정하므로 " +
                 "여기서는 흰색이면 된다. 비우면 이 컴포넌트는 아무것도 하지 않는다.")]
        [SerializeField] private Material _material;

        [Tooltip("한 변 길이 (m). 카메라 far clip(기본 1,000 m)을 넘겨야 잘린 끝이 화면에 들어오지 않는다.")]
        [SerializeField, Min(100f)] private float _size = 3000f;

        [Tooltip("지역이 높이를 지정하지 않았을 때 쓸 판 높이 (m). 타일 지형보다 낮아야 " +
                 "타일이 위에 얹히고 타일 밖에서만 보인다.")]
        [SerializeField] private float _fallbackPlaneY = -0.5f;

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        /// <summary>매 프레임 델리게이트를 새로 만들지 않으려고 캐시해 둔다.</summary>
        private static readonly System.Func<RegionDefinition, bool> OwnsHorizonPredicate = OwnsHorizon;

        /// <summary>판 두께 — 위에서만 보이므로 얇아도 된다(SeaSurfaceView 와 같은 값).</summary>
        private const float PlaneThickness = 0.1f;

        /// <summary>물이 있는 지역에서 수면 판 아래로 내리는 여유 — z-fighting 방지.</summary>
        private const float WaterClearance = 0.1f;

        private Transform _plane;
        private Renderer _renderer;
        private Material _instance;
        private Color _appliedColor;
        private bool _hasAppliedColor;

        private void Start()
        {
            if (_material == null)
            {
                enabled = false;
                return;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "DistantHorizon";
            Destroy(go.GetComponent<Collider>());   // 지평선은 밟는 것이 아니다 — 판정은 타일이 한다

            _plane = go.transform;
            _plane.SetParent(transform, false);
            _plane.localScale = new Vector3(_size, PlaneThickness, _size);
            _plane.localPosition = new Vector3(0f, _fallbackPlaneY, 0f);

            // 원본에 쓰면 그 값이 에디터 세션 내내 남는다 — RegionSkyController 와 같은 이유로 복제본을 쓴다.
            _instance = new Material(_material);

            _renderer = go.GetComponent<Renderer>();
            _renderer.sharedMaterial = _instance;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            _renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_instance == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(_instance);
            }
            else
            {
                DestroyImmediate(_instance);
            }

            _instance = null;
        }

        // 지역 전환이 거리로 진행하므로 매 프레임 조회한다 (SeaSurfaceView 와 같은 규약).
        private void LateUpdate()
        {
            if (_plane == null)
            {
                return;
            }

            bool show = RegionBlendQuery.TryResolveSpanFor(
                OwnsHorizonPredicate, out RegionDefinition region, out float backZ, out float frontZ);

            if (show)
            {
                float half = _size * 0.5f;
                float near = Mathf.Max(backZ, -half);
                float far = Mathf.Min(frontZ, half);

                show = far - near > 0.01f;
                if (show)
                {
                    _plane.localScale = new Vector3(_size, PlaneThickness, far - near);
                    _plane.localPosition = new Vector3(0f, ResolvePlaneY(region), (near + far) * 0.5f);
                    ApplyColor(region.HorizonColor);
                }
            }

            if (_renderer.enabled != show)
            {
                _renderer.enabled = show;
            }
        }

        /// <summary>
        /// 판의 높이 — <b>물이 있는 지역에서는 물면까지 내린다.</b>
        ///
        /// <para>내리지 않으면 판이 <b>물 위에 떠서 바다를 덮는다</b> — 바다는 물면이 −4,
        /// 북극은 −1.5 인데 판 기본값은 −0.5 다. 지평선을 소유하지 않는 지역에서는 판을
        /// 아예 그리지 않으므로(구간 밖) 이 보정이 필요한 곳은 <b>북극</b>처럼
        /// 지평선과 물을 함께 갖는 지역이다.</para>
        /// </summary>
        private float ResolvePlaneY(RegionDefinition region)
        {
            // 물면과 정확히 같은 높이에 두면 수면 판(SeaSurfaceView)과 겹쳐 깜빡인다 —
            // 두 판의 두께 합보다 크게 내려 둔다. 0.1 m 차이는 200 m 밖에서 보이지 않는다.
            return region.HasWater ? region.WaterSurfaceY - WaterClearance : _fallbackPlaneY;
        }

        private void ApplyColor(Color color)
        {
            if (_hasAppliedColor && _appliedColor == color)
            {
                return;
            }

            _appliedColor = color;
            _hasAppliedColor = true;
            _instance.SetColor(BaseColorId, color);
            _instance.SetColor(ColorId, color);
        }

        /// <summary>
        /// 이 지역이 지평선 판을 소유하는가 — 소유하지 않으면 <b>그리지 않는다.</b>
        /// 안개가 짙은 지역(숲·바다 0.0062)은 200 m 에서 78 %가 지워져 절단면이 애초에
        /// 보이지 않으므로, 판을 그려 봐야 드로우콜만 쓰고 <b>물 위에 뜨기까지 한다.</b>
        /// </summary>
        private static bool OwnsHorizon(RegionDefinition region)
        {
            return region.OverridesHorizon;
        }
    }
}
