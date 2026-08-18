using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>
    /// ViewLab 바닥 격자 — 거리감 확인용 GL 라인 (docs/plans/뷰랩-씬-계획.md §2).
    /// 에디터 전용 씬이라 런타임 비용은 고려하지 않는다. 1m 간격, 5m마다 밝은 선.
    /// </summary>
    public sealed class ViewLabGrid : MonoBehaviour
    {
        [SerializeField] private int _halfExtent = 20;
        [SerializeField] private Color _minorColor = new Color(1f, 1f, 1f, 0.08f);
        [SerializeField] private Color _majorColor = new Color(1f, 1f, 1f, 0.25f);

        private Material _lineMaterial;

        private void OnRenderObject()
        {
            if (_lineMaterial == null)
            {
                // 내장 컬러 셰이더 — 외부 에셋 없이 GL 라인을 그린다.
                _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                _lineMaterial.SetInt("_ZWrite", 0);
            }

            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            for (int i = -_halfExtent; i <= _halfExtent; i++)
            {
                GL.Color(i % 5 == 0 ? _majorColor : _minorColor);
                GL.Vertex3(i, 0.01f, -_halfExtent);
                GL.Vertex3(i, 0.01f, _halfExtent);
                GL.Vertex3(-_halfExtent, 0.01f, i);
                GL.Vertex3(_halfExtent, 0.01f, i);
            }

            GL.End();
            GL.PopMatrix();
        }
    }
}
