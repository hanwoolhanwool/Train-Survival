using Game.Core.Services;
using Game.Gameplay.Region;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 소유자 시점이 수면 아래로 들어갔을 때 화면에 물빛을 씌운다 (바다 지역 구현 계획 §6.2 2-5).
    ///
    /// <para><b>왜 필요한가.</b> 물에는 콜라이더도 벽도 없어서, 표시가 없으면 <b>지금 물속인지
    /// 물 위인지 알 수 없다.</b> 잠수하면 물살이 약해진다는 §6.1의 설계가 체감되려면
    /// 먼저 "잠겼다"가 보여야 한다.</para>
    ///
    /// <para><b>왜 프리팹이 아니라 런타임 부착인가.</b> 플레이어 프리팹에는 <c>NetworkObject</c>가
    /// 있어 편집이 <c>GlobalObjectIdHash</c>를 흔들 수 있다. 표현 전용이고 소유자에게만 필요하므로
    /// <see cref="NetworkPlayerController"/>가 소유자일 때만 <c>AddComponent</c>한다.</para>
    ///
    /// <para>전역 렌더 설정(<c>RenderSettings.fog</c>)을 건드리지 않는 것도 의도다 —
    /// 날씨 연출이 그 슬롯을 이미 쓰고 있어 서로 덮어쓴다.</para>
    /// </summary>
    public sealed class UnderwaterView : MonoBehaviour
    {
        private static readonly Color WaterColor = new Color(0.09f, 0.34f, 0.42f, 0.62f);

        private Camera _camera;
        private GameObject _overlay;

        private void Start()
        {
            _camera = GetComponentInChildren<Camera>(true);
            if (_camera == null)
            {
                enabled = false;
                return;
            }

            _overlay = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _overlay.name = "UnderwaterOverlay";
            Destroy(_overlay.GetComponent<Collider>());

            Transform t = _overlay.transform;
            t.SetParent(_camera.transform, false);

            // 근평면 바로 앞에 둔다 — 더 멀면 지오메트리가 사이로 끼어든다.
            float z = _camera.nearClipPlane + 0.01f;
            t.localPosition = new Vector3(0f, 0f, z);
            t.localRotation = Quaternion.identity;

            // 그 거리에서 시야를 덮는 크기. 여유를 둬 화면 가장자리가 새지 않게 한다.
            float height = 2f * z * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            t.localScale = new Vector3(height * _camera.aspect * 1.4f, height * 1.4f, 1f);

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader) { color = WaterColor };
            material.SetFloat("_Surface", 1f);          // Transparent
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.renderQueue = 3000;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            var renderer = _overlay.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.enabled = false;
        }

        private void OnDestroy()
        {
            if (_overlay != null)
            {
                Destroy(_overlay);
            }
        }

        // 시선 갱신이 끝난 뒤 판정해야 같은 프레임의 카메라 위치를 본다.
        private void LateUpdate()
        {
            if (_overlay == null)
            {
                return;
            }

            bool submerged = IsCameraSubmerged();
            var renderer = _overlay.GetComponent<Renderer>();
            if (renderer.enabled != submerged)
            {
                renderer.enabled = submerged;
            }
        }

        /// <summary>발이 아니라 <b>눈</b>이 기준이다 — 화면을 덮는 판정이므로.</summary>
        private bool IsCameraSubmerged()
        {
            if (!ServiceLocator.TryGet(out IRegionService region))
            {
                return false;
            }

            RegionDefinition definition = region.CurrentRegion;
            if (definition == null || !definition.HasWater)
            {
                return false;
            }

            return _camera.transform.position.y < definition.WaterSurfaceY;
        }
    }
}
