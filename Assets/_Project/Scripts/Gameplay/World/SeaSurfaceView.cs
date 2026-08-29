using Game.Core.Services;
using Game.Gameplay.Region;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 바다 수면을 <b>타일마다가 아니라 한 장으로</b> 그린다 (바다 지역 구현 계획 §5.2 개정).
    ///
    /// <para><b>왜 바꿨나.</b> 처음에는 세그먼트마다 물 평면을 넣었다(A안). 그런데 반투명 평면이
    /// 이어지면 <b>경계에서 알파가 두 번 겹쳐</b> 선이 보인다. 겹침을 없애려 살짝 포개자
    /// 이번엔 그 띠가 <b>직사각형으로 진해졌다</b> — 겹친 만큼 두 번 칠해지기 때문이다.
    /// 맞대도 포개도 경계가 남는 것은 <b>평면을 쪼갠 것 자체가 원인</b>이라서다.</para>
    ///
    /// <para><b>한 장으로 바꿀 수 있게 된 이유</b>는 여울(E)을 <b>해저 둔덕</b>으로 표현하기로
    /// 정했기 때문이다(§3.4). 물 높이가 어디서나 −4로 같아졌으므로 세그먼트별 물면 변화가
    /// 더는 필요 없다. A안을 택했던 근거가 사라졌다.</para>
    ///
    /// <para>물이 없는 지역(숲·사막·대초원·북극)에서는 스스로 꺼진다.</para>
    /// </summary>
    public sealed class SeaSurfaceView : MonoBehaviour
    {
        [Tooltip("수면 머티리얼 — 월드 XZ UV 를 켠 스타일라이즈드 워터.")]
        [SerializeField] private Material _waterMaterial;

        [Tooltip("한 변 길이 (m). 카메라 far clip 안에서 수평선까지 덮을 만큼 커야 한다.")]
        [SerializeField, Min(50f)] private float _size = 600f;

        private Transform _plane;
        private Renderer _renderer;

        private void Start()
        {
            if (_waterMaterial == null)
            {
                enabled = false;
                return;
            }

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "SeaSurface";
            Destroy(go.GetComponent<Collider>());   // 물에는 콜라이더가 없다 — 판정은 위치로 한다

            _plane = go.transform;
            _plane.SetParent(transform, false);
            _plane.localScale = new Vector3(_size, 0.1f, _size);

            _renderer = go.GetComponent<Renderer>();
            _renderer.sharedMaterial = _waterMaterial;
            _renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _renderer.enabled = false;
        }

        // 지역 전환은 Day 단위라 매 프레임 볼 필요가 없지만, 판정이 조회 두 번이라 비용이 없다.
        // 늦은 참여·재접속에서 구독 시점을 놓치는 것보다 이쪽이 안전하다.
        private void LateUpdate()
        {
            if (_plane == null)
            {
                return;
            }

            bool show = TryGetWaterSurfaceY(out float waterY);
            if (_renderer.enabled != show)
            {
                _renderer.enabled = show;
            }

            if (show)
            {
                Vector3 p = _plane.localPosition;
                if (!Mathf.Approximately(p.y, waterY))
                {
                    _plane.localPosition = new Vector3(p.x, waterY, p.z);
                }
            }
        }

        /// <summary>
        /// 발밑 지형 기준으로 판정한다 — "현재 지역"으로 켜고 끄면 Day가 넘어간 순간
        /// <b>물만 먼저 사라지고 교량은 40초 더 남는다</b> (<see cref="WaterSurfaceQuery"/>).
        /// </summary>
        private static bool TryGetWaterSurfaceY(out float waterSurfaceY)
        {
            return WaterSurfaceQuery.TryGetWaterSurfaceY(out waterSurfaceY);
        }
    }
}
