using UnityEngine;

namespace Game.Gameplay.Harpoon
{
    /// <summary>
    /// 집게 로프 순수 연출 — 시작점(총구)과 끝점(투사체/대상)을 잇는 LineRenderer.
    /// 물리 시뮬레이션 대상이 아니며, 탄성(처짐) 곡선으로 호스트 확정 지연 30~80 ms를 시각적으로 흡수한다 (§2.4).
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class HarpoonRopeRenderer : MonoBehaviour
    {
        private const int SegmentCount = 12;
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField, Min(0f)] private float _maxSlack = 0.6f;
        [SerializeField] private Color _normalColor = new Color(0.15f, 0.12f, 0.1f, 1f);
        [SerializeField] private Color _failColor = new Color(0.85f, 0.2f, 0.15f, 1f);

        private LineRenderer _lineRenderer;
        private MaterialPropertyBlock _propertyBlock;
        private readonly Vector3[] _points = new Vector3[SegmentCount + 1];

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _lineRenderer.positionCount = SegmentCount + 1;
            _lineRenderer.enabled = false;
            _propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// 로프를 표시한다. slack01: 0 = 팽팽함, 1 = 최대 처짐(승인 대기 탄성 구간).
        /// isFail: true면 실패(빗나감·거부·되감기) 연출 색상으로 표시한다.
        /// </summary>
        public void Show(Vector3 start, Vector3 end, float slack01, bool isFail = false)
        {
            _lineRenderer.enabled = true;
            float sag = Mathf.Clamp01(slack01) * _maxSlack;

            for (int i = 0; i <= SegmentCount; i++)
            {
                float t = (float)i / SegmentCount;
                Vector3 point = Vector3.Lerp(start, end, t);
                point.y -= Mathf.Sin(t * Mathf.PI) * sag;
                _points[i] = point;
            }

            _lineRenderer.SetPositions(_points);

            _propertyBlock.SetColor(BaseColorId, isFail ? _failColor : _normalColor);
            _lineRenderer.SetPropertyBlock(_propertyBlock);
        }

        public void Hide()
        {
            _lineRenderer.enabled = false;
        }
    }
}
