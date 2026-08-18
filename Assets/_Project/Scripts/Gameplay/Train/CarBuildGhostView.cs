using Game.Core.Events;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 건설·재결합·건축물 설치·판자 증축 프리뷰 (M3 피드백 — 건설 포트의 망치 통합,
    /// 손잡이-이탈저항 스펙 §4.1, 건축 개편 1차 §2.4·3차 §2.9). 망치로 건설 지점을 겨누면 지어질 칸
    /// 부피를, 이탈 칸의 재결합 지점을 겨누면 이어질 연결부 자리를
    /// <b>부피 테두리</b>로 보여주고, 칸 갑판을 겨누면 건축물이 점유할 셀 영역을, 칸 옆 빈 판자
    /// 자리를 겨누면 그 열을 <b>바닥 사각형</b>으로 보여준다 — 그리드 위 설치는 차지하는 면적이
    /// 전부라 바닥 표시가 읽기 쉽고, 실물의 방향·부피는 반투명 프리뷰가 이미 보여주기 때문이다.
    /// 색은 초록(가능)/빨강(불가)으로 무엇이 일어날지 즉시 알 수 있게 한다.
    /// 로컬 표현 전용 — 상태를 소유하지 않고 조준 이벤트(<see cref="CarBuildAimLocalEvent"/>,
    /// <see cref="CarRecoupleAimLocalEvent"/>, <see cref="StructurePlaceAimLocalEvent"/>,
    /// <see cref="PlankAimLocalEvent"/>) 구독으로만 그린다.
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

        [Tooltip("바닥 사각형을 갑판면 위로 띄우는 높이(m) — 갑판 메시와 같은 높이면 선이 깜빡인다.")]
        [SerializeField, Min(0f)] private float _groundLift = 0.02f;

        private LineRenderer _line;
        private MaterialPropertyBlock _propertyBlock;
        private readonly Vector3[] _corners = new Vector3[8];
        private readonly Vector3[] _points = new Vector3[16];

        // 바닥 사각형 — 네 귀퉁이 + 시작점으로 닫는다 (건축물 설치·판자 증축 프리뷰).
        private readonly Vector3[] _footprintPoints = new Vector3[5];

        // 두 조준은 상호 배타지만 이벤트는 각자 바뀔 때만 오므로, 마지막 상태를 각각 들고 함께 판단한다
        // (한쪽의 '조준 해제'가 다른 쪽의 프리뷰를 지우지 않게).
        private CarBuildAimLocalEvent _buildAim;
        private CarRecoupleAimLocalEvent _recoupleAim;
        private StructurePlaceAimLocalEvent _structureAim;
        private PlankAimLocalEvent _plankAim;

        private void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = false;

            // 점 개수는 모양마다 다르다 (부피 16 · 바닥 5) — 그리는 쪽에서 정한다.
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
            EventBus<StructurePlaceAimLocalEvent>.Subscribe(OnStructureAim);
            EventBus<PlankAimLocalEvent>.Subscribe(OnPlankAim);
        }

        private void OnDisable()
        {
            EventBus<CarBuildAimLocalEvent>.Unsubscribe(OnBuildAim);
            EventBus<CarRecoupleAimLocalEvent>.Unsubscribe(OnRecoupleAim);
            EventBus<StructurePlaceAimLocalEvent>.Unsubscribe(OnStructureAim);
            EventBus<PlankAimLocalEvent>.Unsubscribe(OnPlankAim);
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

        private void OnStructureAim(StructurePlaceAimLocalEvent evt)
        {
            _structureAim = evt;
            Redraw();
        }

        private void OnPlankAim(PlankAimLocalEvent evt)
        {
            _plankAim = evt;
            Redraw();
        }

        /// <summary>
        /// 재결합 &gt; 칸 건설 &gt; 판자 증축 &gt; 건축물 설치 — 망치의 우클릭 우선순위와 같은 순서로 그린다.
        /// 각 분기는 <b>자기 이벤트만</b> 본다: 무엇이 이겼는지는 컨트롤러가 이미 정해 보냈고
        /// (이미 깔린 판자 열은 그 위 건축물 설치가 우선이라 EmptySlot이 거짓으로 온다), 여기서는
        /// 두 이벤트를 상관시켜 다시 판단하지 않는다 (건축 개편 마무리 패스).
        /// </summary>
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

            if (_plankAim.Aiming && _plankAim.EmptySlot)
            {
                DrawFootprint(_plankAim.GhostCenter, _plankAim.GhostSize, _plankAim.CanBuild);
                return;
            }

            if (_structureAim.Aiming)
            {
                DrawFootprint(_structureAim.GhostCenter, _structureAim.GhostSize, _structureAim.CanBuild);
                return;
            }

            _line.enabled = false;
        }

        /// <summary>부피 테두리 — 지어질 공간이 통째로 생기는 칸 건설·재결합용.</summary>
        private void DrawBox(Vector3 center, Vector3 size, bool allowed)
        {
            FillBox(center, size * 0.5f);
            _line.positionCount = _points.Length;
            _line.SetPositions(_points);
            ApplyTint(allowed);
        }

        /// <summary>
        /// 바닥 사각형 — 그리드 위 설치는 "어느 칸을 차지하는가"가 전부라, 높이 있는 상자보다
        /// 점유 면적을 바닥에 그리는 쪽이 읽기 쉽다 (실물의 방향·부피는 반투명
        /// <see cref="StructurePlacementGhostView"/>가 이미 보여준다).
        /// </summary>
        private void DrawFootprint(Vector3 center, Vector3 size, bool allowed)
        {
            float y = center.y - (size.y * 0.5f) + _groundLift;
            float extentX = size.x * 0.5f;
            float extentZ = size.z * 0.5f;

            _footprintPoints[0] = new Vector3(center.x - extentX, y, center.z - extentZ);
            _footprintPoints[1] = new Vector3(center.x + extentX, y, center.z - extentZ);
            _footprintPoints[2] = new Vector3(center.x + extentX, y, center.z + extentZ);
            _footprintPoints[3] = new Vector3(center.x - extentX, y, center.z + extentZ);
            _footprintPoints[4] = _footprintPoints[0];

            _line.positionCount = _footprintPoints.Length;
            _line.SetPositions(_footprintPoints);
            ApplyTint(allowed);
        }

        private void ApplyTint(bool allowed)
        {
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
