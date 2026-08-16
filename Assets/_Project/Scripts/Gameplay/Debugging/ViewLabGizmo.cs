using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>
    /// ViewLab 선택 피벗 기즈모 — 게임 뷰에서도 보이도록 GL 라인으로 XYZ 축을 그린다
    /// (docs/plans/뷰랩-씬-계획.md §4-②). 씬 뷰 없이도 피벗 축 확인이 목적.
    /// </summary>
    public sealed class ViewLabGizmo : MonoBehaviour
    {
        [SerializeField] private float _axisLength = 0.5f;

        private Material _lineMaterial;

        /// <summary>표시 대상 (null이면 그리지 않음). 컨트롤러가 무기 선택 시 지정.</summary>
        public Transform Target { get; set; }

        private void OnRenderObject()
        {
            if (Target == null)
            {
                return;
            }

            if (_lineMaterial == null)
            {
                _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                // 뷰모델을 뚫고 보여야 축 확인이 된다.
                _lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            }

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            Vector3 origin = Target.position;

            // 부모 스케일에 눌리지 않도록 월드 축 길이 고정.
            DrawLine(origin, origin + Target.right * _axisLength, Color.red);
            DrawLine(origin, origin + Target.up * _axisLength, Color.green);
            DrawLine(origin, origin + Target.forward * _axisLength, Color.blue);

            GL.End();
            GL.PopMatrix();
        }

        private static void DrawLine(Vector3 from, Vector3 to, Color color)
        {
            GL.Color(color);
            GL.Vertex(from);
            GL.Vertex(to);
        }
    }
}
