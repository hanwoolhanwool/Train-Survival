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

        [Tooltip("한 변 길이 (m). 카메라 far clip 안에서 수평선까지 덮을 만큼 커야 한다. " +
                 "지역 경계가 이 안에 들어오면 그 자리에서 잘린다 — 잘린 끝이 해안선이다.")]
        [SerializeField, Min(50f)] private float _size = 600f;

        /// <summary>수면 판의 두께 — 위에서만 보이므로 얇아도 된다.</summary>
        private const float PlaneThickness = 0.1f;

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
            _plane.localScale = new Vector3(_size, PlaneThickness, _size);

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

            bool show = TryResolvePlane(out float waterY, out float near, out float far);
            if (show)
            {
                _plane.localScale = new Vector3(_size, PlaneThickness, far - near);
                _plane.localPosition = new Vector3(0f, waterY, (near + far) * 0.5f);
            }

            if (_renderer.enabled != show)
            {
                _renderer.enabled = show;
            }
        }

        /// <summary>
        /// 이번 프레임에 그릴 수면 조각 — 물인 구간을 <see cref="_size"/> 안으로 자른 [near, far].
        ///
        /// <para><b>켜고 끄는 것이 아니라 자른다.</b> 종전에는 발밑이 물인지로 렌더러를 껐는데,
        /// 이 평면은 <b>반경 300 m</b>라 발밑 한 점보다 훨씬 넓다. 그래서 바다를 벗어나는 순간
        /// 뒤에 남은 바다까지 <b>한 프레임에 통째로 말랐고</b>, 들어가기 전에는 앞의 밀밭이
        /// 물에 잠긴 채 다가왔다 — 사용자가 "바다가 갑자기 밀밭으로 변한다"고 본 것이 이것이다.
        /// 지금은 지역 경계에서 잘리므로 <b>그 잘린 끝이 해안선</b>으로 지나간다.</para>
        ///
        /// <para>판정 자체는 <see cref="WaterSurfaceQuery"/>가 소유한다 — 수영·몬스터 지지면이
        /// 같은 함수를 보기 때문에 여기서 규칙을 다시 쓰면 두 벌이 조용히 어긋난다.</para>
        /// </summary>
        private bool TryResolvePlane(out float waterY, out float near, out float far)
        {
            near = 0f;
            far = 0f;

            if (!WaterSurfaceQuery.TryGetWaterSpan(out waterY, out float backZ, out float frontZ))
            {
                return false;
            }

            float half = _size * 0.5f;
            near = Mathf.Max(backZ, -half);
            far = Mathf.Min(frontZ, half);

            // 물 구간이 통째로 평면 밖으로 나갔다 — 그릴 것이 없다.
            return far - near > 0.01f;
        }
    }
}
