using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>Isolated static art comparison. Does not modify character or gameplay prefabs.</summary>
    public sealed class OtterStyleReviewV3 : MonoBehaviour
    {
        public Transform Before;
        public Transform Candidate;
        public Camera ReviewCamera;
        private const float PanelWidth = 350f;
        private float yaw;
        private float zoom = 1f;
        private int mode;
        private GUIStyle label;
        private GUIStyle button;
        private GUIStyle heading;
        private Font font;

        private void Update()
        {
            if (!Before || !Candidate || !ReviewCamera) return;
            Before.gameObject.SetActive(mode != 2);
            Candidate.gameObject.SetActive(mode != 1);
            Before.localPosition = new Vector3(mode == 0 ? -1.05f : 0f, 0f, 0f);
            Candidate.localPosition = new Vector3(mode == 0 ? 1.05f : 0f, 0f, 0f);
            Before.localRotation = Candidate.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float panel = Mathf.Min(PanelWidth, Screen.width * 0.36f);
            ReviewCamera.rect = new Rect(panel / Screen.width, 0f, 1f - panel / Screen.width, 1f);
            float aspect = (Screen.width - panel) / Mathf.Max(1f, Screen.height);
            ReviewCamera.orthographicSize = Mathf.Max(1.2f, (mode == 0 ? 2.02f : 1.05f) / aspect) / zoom;
        }

        private void OnGUI()
        {
            GUI.depth = -100;
            if (label == null)
            {
                font = Font.CreateDynamicFontFromOSFont("Malgun Gothic", 16);
                label = new GUIStyle(GUI.skin.label) { font = font, fontSize = 15, wordWrap = true };
                button = new GUIStyle(GUI.skin.button) { font = font, fontSize = 15 };
                heading = new GUIStyle(label) { fontSize = 19, fontStyle = FontStyle.Bold };
            }
            float panel = Mathf.Min(PanelWidth, Screen.width * 0.36f);
            var previousColor = GUI.color;
            GUI.color = new Color(.055f, .075f, .09f, 1f);
            GUI.DrawTexture(new Rect(0, 0, panel, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUILayout.BeginArea(new Rect(14, 18, panel - 28, Screen.height - 36));
            GUILayout.Label("수달 · A안 비교", heading);
            GUILayout.Label("기존 개성 유지 / 표면 정리", label);
            GUILayout.Space(14);
            if (GUILayout.Button("나란히 비교", button, GUILayout.Height(34))) { mode = 0; zoom = 1f; }
            if (GUILayout.Button("수정 전 v2 · 단독", button, GUILayout.Height(34))) { mode = 1; zoom = 1f; }
            if (GUILayout.Button("A안 v3 · 단독", button, GUILayout.Height(34))) { mode = 2; zoom = 1f; }
            GUILayout.Space(16);
            GUILayout.Label("같은 각도 · 같은 조명", label);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("정면", button, GUILayout.Height(32))) yaw = 0;
            if (GUILayout.Button("사선", button, GUILayout.Height(32))) yaw = -35;
            if (GUILayout.Button("후면", button, GUILayout.Height(32))) yaw = 180;
            GUILayout.EndHorizontal();
            GUILayout.Label("회전", label);
            yaw = GUILayout.HorizontalSlider(yaw, -180f, 180f);
            GUILayout.Label("확대", label);
            zoom = GUILayout.HorizontalSlider(zoom, .8f, 2f);
            if (GUILayout.Button("시점 초기화", button, GUILayout.Height(32))) { yaw = 0f; zoom = 1f; }
            GUILayout.Space(20);
            GUILayout.Label("A안 변경\n· 옷감의 잔무늬·가는 봉제선 완화\n· 털과 옷의 잘게 나뉜 명암 정리\n· 부드러운 표면과 무광 재질", label);
            GUILayout.Space(12);
            GUILayout.Label("눈·입 / 의상 색 / 큰 주름 / 대칭 꼬리 유지", label);
            GUILayout.Space(12);
            GUILayout.Label("정적 아트 후보입니다.\n리깅·본게임에는 적용하지 않았습니다.", label);
            GUILayout.EndArea();
            DrawCaption(Before, "수정 전 v2");
            DrawCaption(Candidate, "A안 v3");
            var e = Event.current;
            if (e.mousePosition.x > panel)
            {
                if (e.type == EventType.MouseDrag && e.button == 1)
                {
                    yaw = Mathf.Repeat(yaw - e.delta.x * .6f + 180f, 360f) - 180f;
                    e.Use();
                }
                if (e.type == EventType.ScrollWheel)
                {
                    zoom = Mathf.Clamp(zoom - e.delta.y * .06f, .8f, 2f);
                    e.Use();
                }
            }
        }

        private void DrawCaption(Transform target, string text)
        {
            if (!target || !target.gameObject.activeSelf || !ReviewCamera) return;
            var point = ReviewCamera.WorldToScreenPoint(target.position + Vector3.up * -.13f);
            var style = new GUIStyle(heading) { alignment = TextAnchor.MiddleCenter };
            GUI.Label(new Rect(point.x - 140f, Screen.height - point.y, 280f, 30f), text, style);
        }

    }
}
