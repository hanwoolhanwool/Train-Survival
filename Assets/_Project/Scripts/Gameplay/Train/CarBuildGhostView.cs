using Game.Core.Events;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 건설·재결합 프리뷰 (M3 피드백 — 건설 포트의 망치 통합, 손잡이-이탈저항 스펙 §4.1).
    /// 망치로 건설 지점을 겨누면 지어질 칸 부피를, 이탈 칸의 재결합 지점을 겨누면 이어질 연결부 자리를
    /// 초록 테두리(와이어 박스)로 보여줘 무엇이 일어날지 즉시 알 수 있게 한다.
    /// 건설 테두리 안은 곧 자리 점유 판정 영역이라, 붉은 테두리 안에 사람·몬스터가 서 있으면 왜 막혔는지 바로 보인다.
    /// 로컬 표현 전용 — 상태를 소유하지 않고 두 조준 이벤트(<see cref="CarBuildAimLocalEvent"/>,
    /// <see cref="CarRecoupleAimLocalEvent"/>) 구독으로만 그린다.
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

        // 두 조준은 상호 배타지만 이벤트는 각자 바뀔 때만 오므로, 마지막 상태를 각각 들고 함께 판단한다
        // (한쪽의 '조준 해제'가 다른 쪽의 프리뷰를 지우지 않게).
        private CarBuildAimLocalEvent _buildAim;
        private CarRecoupleAimLocalEvent _recoupleAim;

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
            EventBus<CarRecoupleAimLocalEvent>.Subscribe(OnRecoupleAim);
        }

        private void OnDisable()
        {
            EventBus<CarBuildAimLocalEvent>.Unsubscribe(OnBuildAim);
            EventBus<CarRecoupleAimLocalEvent>.Unsubscribe(OnRecoupleAim);
            _line.enabled = false;
        }

        private void OnBuildAim(CarBuildAimLocalEvent evt)
        {
            _buildAim = evt;
            Redraw();
        }

        private void OnRecoupleAim(CarRecoupleAimLocalEvent evt)
        {
            _recoupleAim = evt;
            Redraw();
        }

        /// <summary>재결합 조준이 먼저다 — 망치의 우클릭 우선순위와 같은 순서로 그린다.</summary>
        private void Redraw()
        {
            if (_recoupleAim.Aiming)
            {
                DrawBox(_recoupleAim.GhostCenter, _recoupleAim.GhostSize, _recoupleAim.CanRecouple);
                return;
            }

            if (_buildAim.Aiming)
            {
                DrawBox(_buildAim.GhostCenter, _buildAim.GhostSize, _buildAim.CanBuild);
                return;
            }

            _line.enabled = false;
        }

        private void DrawBox(Vector3 center, Vector3 size, bool allowed)
        {
            FillBox(center, size * 0.5f);
            _line.SetPositions(_points);
            _propertyBlock.SetColor(BaseColorId, allowed ? _buildableColor : _blockedColor);
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
