using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>
    /// 에셋랩 검수 표시 — 게임 뷰에서도 보이도록 GL 라인으로 그린다
    /// (에셋랩-씬-계획.md §4-③). 씬 뷰 기즈모는 Play 중 스크린샷에 안 잡혀 쓸 수 없다.
    ///
    /// <para>세 가지를 겹쳐 그린다: 대상 바운즈 상자 · 지면(y=0) 아래로 잠긴 부분 ·
    /// 사람 키 1.8 m 기준자. "이게 큰가 작은가"는 눈이 아니라 자로 판단해야 한다.</para>
    /// </summary>
    public sealed class AssetLabBoundsView : MonoBehaviour
    {
        /// <summary>레벨 가이드가 잡은 플레이어 키 — 크기 감각의 기준자.</summary>
        public const float HumanHeight = 1.8f;

        [SerializeField] private Color _boundsColor = new Color(0.4f, 0.9f, 1f, 0.55f);
        [SerializeField] private Color _sunkColor = new Color(1f, 0.35f, 0.3f, 0.9f);
        [SerializeField] private Color _rulerColor = new Color(1f, 0.95f, 0.5f, 0.5f);

        private Material _lineMaterial;

        /// <summary>표시할 바운즈 — 컨트롤러가 스폰 직후 넣는다. 크기가 0이면 그리지 않는다.</summary>
        public Bounds Target { get; set; }

        /// <summary>바운즈 상자를 그릴지 — 패널 토글.</summary>
        public bool ShowBounds { get; set; } = true;

        /// <summary>사람 키 기준자를 그릴지 — 패널 토글.</summary>
        public bool ShowRuler { get; set; } = true;

        private void OnRenderObject()
        {
            if (Target.size.sqrMagnitude <= 0f)
            {
                return;
            }

            EnsureMaterial();
            _lineMaterial.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.LINES);

            if (ShowBounds)
            {
                DrawBox(Target.min, Target.max, _boundsColor);

                // 지면 아래로 잠긴 부분만 붉게 덧그린다 — 묻힘이 한눈에 보여야 한다.
                if (Target.min.y < -0.001f)
                {
                    Vector3 sunkMax = new Vector3(Target.max.x, 0f, Target.max.z);
                    DrawBox(Target.min, sunkMax, _sunkColor);
                }
            }

            if (ShowRuler)
            {
                DrawRuler();
            }

            GL.End();
            GL.PopMatrix();
        }

        /// <summary>1 m 눈금 기둥 + 사람 키(1.8 m) 가로선.</summary>
        private void DrawRuler()
        {
            float x = Target.min.x - 0.5f;
            float z = Target.center.z;
            float top = Mathf.Max(Target.max.y, HumanHeight) + 1f;

            GL.Color(_rulerColor);
            GL.Vertex3(x, 0f, z);
            GL.Vertex3(x, top, z);

            for (float y = 1f; y <= top; y += 1f)
            {
                // 1 m 마다 짧은 눈금, 사람 키에서는 길게 — 대상 폭 전체를 가로지른다.
                bool human = Mathf.Abs(y - HumanHeight) < 0.001f;
                float len = human ? 0.5f : 0.2f;
                GL.Vertex3(x - len, y, z);
                GL.Vertex3(x + len, y, z);
            }

            GL.Color(new Color(_rulerColor.r, _rulerColor.g, _rulerColor.b, 0.9f));
            GL.Vertex3(x, HumanHeight, z);
            GL.Vertex3(Target.max.x + 0.3f, HumanHeight, z);
        }

        private static void DrawBox(Vector3 min, Vector3 max, Color color)
        {
            GL.Color(color);

            Vector3[] c =
            {
                new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z), new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z), new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z),
            };

            for (int i = 0; i < 4; i++)
            {
                int next = (i + 1) % 4;
                GL.Vertex(c[i]);
                GL.Vertex(c[next]);
                GL.Vertex(c[i + 4]);
                GL.Vertex(c[next + 4]);
                GL.Vertex(c[i]);
                GL.Vertex(c[i + 4]);
            }
        }

        private void EnsureMaterial()
        {
            if (_lineMaterial != null)
            {
                return;
            }

            _lineMaterial = new Material(Shader.Find("Hidden/Internal-Colored"))
            {
                hideFlags = HideFlags.HideAndDontSave,
            };
            _lineMaterial.SetInt("_ZWrite", 0);

            // 메시에 가려지면 묻힘을 볼 수 없다 — 상자는 항상 위에 그린다.
            _lineMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        }
    }
}
