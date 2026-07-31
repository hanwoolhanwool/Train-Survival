using Game.Core.Events;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 건설 프리뷰 (M3 피드백 — 건설 포트의 망치 통합). 망치로 건설 지점을 겨누면
    /// 지어질 칸 부피를 초록 테두리(와이어 박스)로 보여줘 무엇이 지어질지 즉시 알 수 있게 한다.
    /// 테두리 안이 곧 자리 점유 판정 영역이라, 붉은 테두리 안에 사람·몬스터가 서 있으면 왜 막혔는지 바로 보인다.
    /// 로컬 표현 전용 — 상태를 소유하지 않고 <see cref="CarBuildAimLocalEvent"/> 구독으로만 그린다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class CarBuildGhostView : MonoBehaviour
    {
        // 와이어 박스 한붓그리기 — 선 하나로 12개 모서리를 전부 덮는다(일부 모서리는 되그린다).
        // b = 바닥 네 귀퉁이, t = 천장 네 귀퉁이. 인덱스는 (x부호, z부호) 시계 방향.
        private static readonly int[] PathCorners =
        {
            0, 1, 2, 3, 0, 4, 5, 6, 7, 4, 5, 1, 2, 6, 7, 3,
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        [SerializeField] private Color _buildableColor = new Color(0.25f, 1f, 0.35f, 0.9f);

        [Tooltip("자원 부족·자리 점유로 지금은 못 짓는 상태의 테두리 색.")]
        [SerializeField] private Color _blockedColor = new Color(1f, 0.35f, 0.25f, 0.9f);

        [SerializeField, Min(0.01f)] private float _lineWidth = 0.08f;

        private LineRenderer _line;
        private MaterialPropertyBlock _propertyBlock;
        private readonly Vector3[] _corners = new Vector3[8];
        private readonly Vector3[] _points = new Vector3[16];

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = false;
            _line.positionCount = _points.Length;
            _line.widthMultiplier = _lineWidth;
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.enabled = false;
            _propertyBlock = new MaterialPropertyBlock();
        }

        private void OnEnable()
        {
            EventBus<CarBuildAimLocalEvent>.Subscribe(OnBuildAim);
        }

        private void OnDisable()
        {
            EventBus<CarBuildAimLocalEvent>.Unsubscribe(OnBuildAim);
            _line.enabled = false;
        }

        private void OnBuildAim(CarBuildAimLocalEvent evt)
        {
            if (!evt.Aiming)
            {
                _line.enabled = false;
                return;
            }

            FillBox(evt.GhostCenter, evt.GhostSize * 0.5f);
            _line.SetPositions(_points);
            _propertyBlock.SetColor(BaseColorId, evt.CanBuild ? _buildableColor : _blockedColor);
            _line.SetPropertyBlock(_propertyBlock);
            _line.enabled = true;
        }

        private void FillBox(Vector3 center, Vector3 extents)
        {
            for (int i = 0; i < _corners.Length; i++)
            {
                bool top = i >= 4;
                int corner = i % 4;
                _corners[i] = center + new Vector3(
                    (corner == 0 || corner == 3 ? -1f : 1f) * extents.x,
                    (top ? 1f : -1f) * extents.y,
                    (corner < 2 ? -1f : 1f) * extents.z);
            }

            for (int i = 0; i < PathCorners.Length; i++)
            {
                _points[i] = _corners[PathCorners[i]];
            }
        }
    }
}
