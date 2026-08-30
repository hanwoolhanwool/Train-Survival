using System.Collections;
using System.Collections.Generic;
using Game.Core.Logging;
using Game.Gameplay.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Gameplay.Debugging
{
    /// <summary>
    /// 에셋랩 컨트롤러 — 맵에 배치되는 에셋을 한 씬에서 하나씩 세워 놓고 360° 돌려 보며
    /// 스케일·피벗·예산 문제를 찾는다 (docs/plans/features/에셋랩-씬-계획.md).
    ///
    /// <para>조작은 마우스만 — ViewLab과 같은 정책이다. 좌드래그 = 대상 수동 회전,
    /// 우드래그 = 카메라 궤도, 휠 = 줌.</para>
    ///
    /// <para>에디터 전용 씬(빌드 미포함) 소속이라 스폰을 <c>PoolManager</c>로 우회하지 않는다 —
    /// 한 번에 한 개만 세우고 즉시 버리는 구조라 풀링 이득이 없고, 풀을 태우면
    /// 프리팹 원본이 아니라 풀이 손본 상태를 보게 돼 검수 목적과 어긋난다.</para>
    /// </summary>
    public sealed class AssetLabController : MonoBehaviour
    {
        [SerializeField] private ViewLabOrbitCamera _orbitCamera;
        [SerializeField] private AssetLabBoundsView _boundsView;

        [Tooltip("스폰 대상이 매달릴 회전 노드 — 이 노드를 돌려 360°를 만든다")]
        [SerializeField] private Transform _turntable;

        [Tooltip("뒷면·역광을 확인하려면 태양을 돌려야 한다")]
        [SerializeField] private Light _sun;

        [Header("턴테이블")]
        [SerializeField] private float _spinSpeed = 25f;
        [SerializeField] private float _dragSensitivity = 0.4f;

        [Header("패널")]
        [SerializeField] private float _listWidth = 260f;
        [SerializeField] private float _detailWidth = 330f;

        [Tooltip("일괄 검수에서 한 프레임에 재는 개수 — 크게 잡으면 에디터가 길게 멈춘다")]
        [SerializeField] private int _scanBatchSize = 8;

        private readonly List<AssetLabEntry> _entries = new List<AssetLabEntry>();

        private GameObject _instance;
        private AssetLabEntry _current;

        /// <summary>강제할 LOD 단계 — 음수면 자동(화면 점유 비율로 Unity가 고른다).</summary>
        private int _forcedLod = -1;

        /// <summary>선택 대상의 단계별 삼각형 수 — 감축이 얼마나 됐는지는 이것으로만 보인다.</summary>
        private int[] _lodCounts = { 0 };
        private AssetLabCategory _tab = AssetLabCategory.Environment;
        private string _filter = string.Empty;
        private bool _spin = true;
        private bool _forceShowScatter = true;
        private bool _onlyIssues;
        private float _sunYaw = 40f;
        private Vector2 _listScroll;
        private Vector2 _detailScroll;
        private string _status = string.Empty;
        private bool _scanning;
        private int _scanProgress;

        private GUIStyle _hudStyle;
        private GUIStyle _issueStyle;

        private void Awake()
        {
            // 포커스가 빠져도 회전이 멈추면 다른 창에서 스크린샷을 볼 수 없다 (ViewLab §7과 같은 함정).
            Application.runInBackground = true;
        }

        private void Start()
        {
            Reload();
        }

        private void Update()
        {
            if (_turntable == null)
            {
                return;
            }

            Mouse mouse = Mouse.current;
            bool dragging = mouse != null && mouse.leftButton.isPressed && !IsPointerOverPanel(mouse);

            if (dragging)
            {
                _turntable.Rotate(Vector3.up, -mouse.delta.ReadValue().x * _dragSensitivity, Space.World);
            }
            else if (_spin)
            {
                _turntable.Rotate(Vector3.up, _spinSpeed * Time.deltaTime, Space.World);
            }

            if (_boundsView != null && _instance != null)
            {
                // 돌아가는 중에도 상자가 따라붙어야 한다 — 매 프레임 다시 잰다.
                _boundsView.Target = WorldBoundsOf(_instance);
            }
        }

        // ── 카탈로그 ────────────────────────────────────────────

        private void Reload()
        {
            _entries.Clear();
            _entries.AddRange(AssetLabCatalog.Collect());
            _status = $"에셋 {_entries.Count}종 수집";
            GameLog.Info(LogCategory.ViewLab, $"[에셋랩] {_status}");

            if (_entries.Count > 0 && _current == null)
            {
                Select(FirstOfTab());
            }
        }

        private AssetLabEntry FirstOfTab()
        {
            foreach (AssetLabEntry e in _entries)
            {
                if (e.Category == _tab && AssetLabCatalog.Matches(e, _filter))
                {
                    return e;
                }
            }

            return _entries.Count > 0 ? _entries[0] : null;
        }

        private void Select(AssetLabEntry entry)
        {
            if (entry == null)
            {
                return;
            }

            Despawn();
            _current = entry;

            if (_turntable == null)
            {
                return;
            }

            // 회전은 원본 그대로 둔다 — FBX 루트에 실린 축 보정(Z-up→Y-up)을 지우면
            // 서 있어야 할 것이 눕고, 그 상태를 "에셋 문제"로 오판한다.
            _turntable.rotation = Quaternion.identity;
            _instance = Instantiate(entry.Asset, _turntable, false);
            _instance.transform.localPosition = Vector3.zero;

            if (_forceShowScatter)
            {
                ForceShowScatterSlots(_instance);
            }

            Measure(entry);
            FrameCamera(entry);

            _lodCounts = AssetLabProbe.LodTriangleCounts(_instance);
            AssetLabProbe.ForceLod(_instance, _forcedLod);   // 대상이 바뀌어도 보던 단계를 유지한다
        }

        private void Despawn()
        {
            if (_instance != null)
            {
                Destroy(_instance);
                _instance = null;
            }

            if (_boundsView != null)
            {
                _boundsView.Target = default;
            }
        }

        /// <summary>
        /// 변주 슬롯을 전부 켠다 — 검수는 "이번에 뽑힌 것"이 아니라 저작된 전부를 봐야 한다.
        /// 난수를 주입하는 공개 경로(<see cref="ScatterSlot.Apply"/>)를 그대로 쓴다.
        /// </summary>
        private static void ForceShowScatterSlots(GameObject root)
        {
            foreach (ScatterSlot slot in root.GetComponentsInChildren<ScatterSlot>(true))
            {
                slot.Apply(0f, 0.5f, 0.5f);
            }
        }

        private void Measure(AssetLabEntry entry)
        {
            if (_instance == null)
            {
                return;
            }

            AssetMeasurement m = AssetLabProbe.Measure(_instance);
            entry.Measurement = m;
            entry.Issues = AssetLabDiagnostics.Inspect(entry.Category, m);
            entry.Measured = true;

            if (_boundsView != null)
            {
                _boundsView.Target = WorldBoundsOf(_instance);
            }
        }

        private void FrameCamera(AssetLabEntry entry)
        {
            if (_orbitCamera == null)
            {
                return;
            }

            Vector3 size = entry.Measurement.Size;
            var cam = _orbitCamera.GetComponent<Camera>();
            float fov = cam != null ? cam.fieldOfView : 60f;
            float distance = AssetLabProbe.FramingDistance(size, fov);
            _orbitCamera.Frame(_turntable, distance, Mathf.Max(size.y * 0.5f, 0.3f));
        }

        private static Bounds WorldBoundsOf(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(false);
            Bounds b = default;
            bool set = false;
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled)
                {
                    continue;
                }

                if (!set)
                {
                    b = renderers[i].bounds;
                    set = true;
                }
                else
                {
                    b.Encapsulate(renderers[i].bounds);
                }
            }

            return b;
        }

        // ── 일괄 검수 ───────────────────────────────────────────

        /// <summary>
        /// 전 항목을 한 번씩 세워 재고 버린다. 목록을 손으로 넘기며 보는 것과 결과는 같지만,
        /// "어디에 문제가 있는지"를 먼저 알고 들어가야 순회가 의미를 갖는다.
        /// </summary>
        private IEnumerator ScanAll()
        {
            _scanning = true;
            _scanProgress = 0;
            AssetLabEntry restore = _current;

            for (int i = 0; i < _entries.Count; i++)
            {
                AssetLabEntry entry = _entries[i];
                var probe = Instantiate(entry.Asset, _turntable, false);
                probe.transform.localPosition = Vector3.zero;

                if (_forceShowScatter)
                {
                    ForceShowScatterSlots(probe);
                }

                AssetMeasurement m = AssetLabProbe.Measure(probe);
                entry.Measurement = m;
                entry.Issues = AssetLabDiagnostics.Inspect(entry.Category, m);
                entry.Measured = true;
                Destroy(probe);

                _scanProgress = i + 1;
                if ((i + 1) % Mathf.Max(1, _scanBatchSize) == 0)
                {
                    yield return null;
                }
            }

            _scanning = false;
            _status = SummarizeScan();
            GameLog.Info(LogCategory.ViewLab, $"[에셋랩] 일괄 검수 완료 — {_status}");

            // 검수 중 대상이 사라져 있었으니 원래 보던 것을 되돌린다.
            _current = null;
            Select(restore);
        }

        private string SummarizeScan()
        {
            int errors = 0;
            int warnings = 0;
            foreach (AssetLabEntry e in _entries)
            {
                if (e.Issues == null)
                {
                    continue;
                }

                AssetIssueSeverity worst = AssetLabDiagnostics.WorstOf(e.Issues);
                if (worst == AssetIssueSeverity.Error)
                {
                    errors++;
                }
                else if (worst == AssetIssueSeverity.Warning)
                {
                    warnings++;
                }
            }

            return $"검수 {_entries.Count}종 — 오류 {errors} · 경고 {warnings}";
        }

        // ── 패널 ────────────────────────────────────────────────

        private bool IsPointerOverPanel(Mouse mouse)
        {
            float x = mouse.position.ReadValue().x;
            return x < _listWidth + 20f || x > Screen.width - _detailWidth - 20f;
        }

        private void OnGUI()
        {
            EnsureStyles();
            DrawHud();
            DrawListPanel();
            DrawDetailPanel();
        }

        private void EnsureStyles()
        {
            if (_hudStyle != null)
            {
                return;
            }

            _hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, padding = new RectOffset(10, 10, 6, 6) };
            _hudStyle.normal.textColor = Color.white;
            _issueStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
            _issueStyle.normal.textColor = Color.white;
        }

        private void DrawHud()
        {
            string name = _current != null ? _current.DisplayName : "(선택 없음)";
            string scan = _scanning ? $"  ·  검수 중 {_scanProgress}/{_entries.Count}" : string.Empty;
            var rect = new Rect(_listWidth + 32f, 12f, Screen.width - _listWidth - _detailWidth - 64f, 66f);

            // 하늘을 배경으로 흰 글자를 얹으면 읽히지 않는다 — 어두운 판을 깔고 쓴다.
            Color old = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.45f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;

            GUI.Label(rect,
                $"[에셋랩]  {name}{scan}\n"
                + $"{_status}\n"
                + "좌드래그 = 대상 회전  ·  우드래그 = 카메라 궤도  ·  휠 = 줌  ·  휠클릭 = 팬",
                _hudStyle);
        }

        /// <summary>패널 뒤에 어두운 판을 깐다 — 밝은 하늘 위에서는 기본 박스로 글자가 안 읽힌다.</summary>
        private static void DrawPanelBackdrop(Rect rect)
        {
            Color old = GUI.color;
            GUI.color = new Color(0.05f, 0.06f, 0.08f, 0.82f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = old;
        }

        private void DrawListPanel()
        {
            var panel = new Rect(12f, 12f, _listWidth, Screen.height - 24f);
            DrawPanelBackdrop(panel);
            GUILayout.BeginArea(panel, GUI.skin.box);

            GUILayout.Label($"에셋 {_entries.Count}종");

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("새로고침"))
            {
                Reload();
            }

            GUI.enabled = !_scanning;
            if (GUILayout.Button("전체 검수"))
            {
                StartCoroutine(ScanAll());
            }

            GUI.enabled = true;
            GUILayout.EndHorizontal();

            GUILayout.Label("검색");
            string next = GUILayout.TextField(_filter);
            if (next != _filter)
            {
                _filter = next;
            }

            _onlyIssues = GUILayout.Toggle(_onlyIssues, " 문제 있는 것만");

            DrawCategoryTabs();
            GUILayout.Space(4f);

            _listScroll = GUILayout.BeginScrollView(_listScroll);
            foreach (AssetLabEntry entry in _entries)
            {
                if (entry.Category != _tab || !AssetLabCatalog.Matches(entry, _filter))
                {
                    continue;
                }

                if (_onlyIssues && (!entry.Measured
                    || AssetLabDiagnostics.WorstOf(entry.Issues) == AssetIssueSeverity.Info))
                {
                    continue;
                }

                DrawEntryButton(entry);
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawCategoryTabs()
        {
            var values = (AssetLabCategory[])System.Enum.GetValues(typeof(AssetLabCategory));
            int perRow = 4;
            for (int i = 0; i < values.Length; i++)
            {
                if (i % perRow == 0)
                {
                    GUILayout.BeginHorizontal();
                }

                AssetLabCategory c = values[i];
                Color old = GUI.backgroundColor;
                GUI.backgroundColor = _tab == c ? new Color(0.5f, 1f, 0.5f) : old;
                if (GUILayout.Button(AssetLabCatalog.LabelOf(c)))
                {
                    _tab = c;
                    _listScroll = Vector2.zero;
                }

                GUI.backgroundColor = old;

                if (i % perRow == perRow - 1 || i == values.Length - 1)
                {
                    GUILayout.EndHorizontal();
                }
            }
        }

        private void DrawEntryButton(AssetLabEntry entry)
        {
            Color old = GUI.backgroundColor;
            if (entry == _current)
            {
                GUI.backgroundColor = new Color(0.5f, 0.85f, 1f);
            }
            else if (entry.Measured)
            {
                GUI.backgroundColor = ColorFor(AssetLabDiagnostics.WorstOf(entry.Issues));
            }

            string suffix = entry.Measured ? $"  ({entry.Measurement.Triangles:N0})" : string.Empty;
            if (GUILayout.Button(entry.DisplayName + suffix))
            {
                Select(entry);
            }

            GUI.backgroundColor = old;
        }

        private static Color ColorFor(AssetIssueSeverity severity)
        {
            switch (severity)
            {
                case AssetIssueSeverity.Error:
                    return new Color(1f, 0.5f, 0.45f);
                case AssetIssueSeverity.Warning:
                    return new Color(1f, 0.85f, 0.4f);
                default:
                    return new Color(0.6f, 0.95f, 0.6f);
            }
        }

        private void DrawDetailPanel()
        {
            var area = new Rect(Screen.width - _detailWidth - 12f, 12f, _detailWidth, Screen.height - 24f);
            DrawPanelBackdrop(area);
            GUILayout.BeginArea(area, GUI.skin.box);
            _detailScroll = GUILayout.BeginScrollView(_detailScroll);

            // 기본 라벨색은 반투명 패널 위에서 잘 안 읽힌다 — 이 패널 안에서만 흰색으로 올린다.
            Color oldContent = GUI.contentColor;
            GUI.contentColor = Color.white;

            DrawViewSection();
            GUILayout.Space(8f);
            DrawLodSection();
            GUILayout.Space(8f);
            DrawMeasurementSection();
            GUILayout.Space(8f);
            DrawIssueSection();

            GUI.contentColor = oldContent;
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void DrawViewSection()
        {
            GUILayout.Label("■ 보기");
            _spin = GUILayout.Toggle(_spin, " 자동 회전");
            GUILayout.Label($"속도 {_spinSpeed:0}°/s");
            _spinSpeed = GUILayout.HorizontalSlider(_spinSpeed, 0f, 120f);

            if (_boundsView != null)
            {
                _boundsView.ShowBounds = GUILayout.Toggle(_boundsView.ShowBounds, " 바운즈 상자");
                _boundsView.ShowRuler = GUILayout.Toggle(_boundsView.ShowRuler, " 사람 키(1.8 m) 자");
            }

            bool nextForce = GUILayout.Toggle(_forceShowScatter, " 변주 슬롯 전부 표시");
            if (nextForce != _forceShowScatter)
            {
                _forceShowScatter = nextForce;
                AssetLabEntry again = _current;
                _current = null;
                Select(again);
            }

            if (_sun != null)
            {
                GUILayout.Label($"태양 방위 {_sunYaw:0}°");
                float nextYaw = GUILayout.HorizontalSlider(_sunYaw, 0f, 360f);
                if (!Mathf.Approximately(nextYaw, _sunYaw))
                {
                    _sunYaw = nextYaw;
                    Vector3 e = _sun.transform.eulerAngles;
                    _sun.transform.rotation = Quaternion.Euler(e.x, _sunYaw, e.z);
                }
            }
        }

        /// <summary>
        /// LOD 단계를 손으로 골라 눈으로 비교한다. Mesh LOD는 화면 점유 비율로 단계를 고르므로
        /// 가만히 보고 있으면 LOD1·2가 어떻게 생겼는지 볼 수 없다 — 감축 품질은 수치가 아니라
        /// 눈이 판정한다 (에셋랩-씬-계획.md §4.1-B).
        /// </summary>
        private void DrawLodSection()
        {
            GUILayout.Label("■ LOD");

            if (_current == null || _instance == null)
            {
                GUILayout.Label("(선택 없음)");
                return;
            }

            if (_lodCounts.Length <= 1)
            {
                GUILayout.Label($"이 에셋은 LOD가 없다 ({_lodCounts[0]:N0} tris 고정).");
                return;
            }

            GUILayout.BeginHorizontal();
            if (ToggleButton("자동", _forcedLod < 0))
            {
                SetForcedLod(-1);
            }

            for (int lod = 0; lod < _lodCounts.Length; lod++)
            {
                if (ToggleButton($"LOD{lod}", _forcedLod == lod))
                {
                    SetForcedLod(lod);
                }
            }

            GUILayout.EndHorizontal();

            int full = Mathf.Max(1, _lodCounts[0]);
            for (int lod = 0; lod < _lodCounts.Length; lod++)
            {
                string mark = _forcedLod == lod ? "▶ " : "   ";
                float ratio = (float)_lodCounts[lod] / full * 100f;
                GUILayout.Label($"{mark}LOD{lod}   {_lodCounts[lod]:N0} tris   ({ratio:F0} %)");
            }

            if (_forcedLod >= 0)
            {
                GUILayout.Label("강제 표시 중 — 실제 게임에서는 거리에 따라 자동 전환된다.");
            }
        }

        private void SetForcedLod(int lod)
        {
            _forcedLod = lod;
            AssetLabProbe.ForceLod(_instance, lod);
        }

        /// <summary>선택 상태 버튼 — 켜져 있으면 초록 틴트 (뷰랩 패널과 같은 표시).</summary>
        private static bool ToggleButton(string label, bool active)
        {
            Color old = GUI.backgroundColor;
            GUI.backgroundColor = active ? new Color(0.5f, 1f, 0.5f) : old;
            bool pressed = GUILayout.Button(label);
            GUI.backgroundColor = old;
            return pressed;
        }

        private void DrawMeasurementSection()
        {
            GUILayout.Label("■ 계측");
            if (_current == null || !_current.Measured)
            {
                GUILayout.Label("(선택 없음)");
                return;
            }

            AssetMeasurement m = _current.Measurement;
            int budget = AssetLabDiagnostics.TriBudgetFor(_current.Category);

            GUILayout.Label($"분류    {AssetLabCatalog.LabelOf(_current.Category)}");
            GUILayout.Label($"삼각형  {m.Triangles:N0}  / 예산 {budget:N0}");
            GUILayout.Label($"크기    {m.Size.x:F2} × {m.Size.y:F2} × {m.Size.z:F2} m");
            GUILayout.Label($"사람 대비  {m.Size.y / AssetLabBoundsView.HumanHeight:F2}배");
            GUILayout.Label($"지면 오프셋  {m.GroundOffset:+0.000;-0.000;0} m");
            GUILayout.Label($"렌더러  {m.RendererCount}  ·  머티리얼 {m.MaterialCount}");
            string lod = m.HasLod
                ? $"{m.LodLevels}단 (최하위 {m.LowestLodTriangles:N0} tris)"
                : "없음";
            GUILayout.Label($"콜라이더 {(m.HasCollider ? "있음" : "없음")}"
                + $"  ·  그림자 {(m.CastsShadow ? "켜짐" : "꺼짐")}");
            GUILayout.Label($"LOD     {lod}");
            GUILayout.Label($"경로  {_current.AssetPath}", _issueStyle);

#if UNITY_EDITOR
            if (GUILayout.Button("프로젝트에서 이 에셋 선택"))
            {
                var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<Object>(_current.AssetPath);
                UnityEditor.Selection.activeObject = asset;
                UnityEditor.EditorGUIUtility.PingObject(asset);
            }
#endif
        }

        private void DrawIssueSection()
        {
            GUILayout.Label("■ 검수");
            if (_current == null || _current.Issues == null || _current.Issues.Count == 0)
            {
                GUILayout.Label(_current != null && _current.Measured ? "문제 없음" : "(미검수)");
                return;
            }

            foreach (AssetIssue issue in _current.Issues)
            {
                Color old = GUI.color;
                GUI.color = ColorFor(issue.Severity);
                GUILayout.Label($"[{issue.Code}] {issue.Message}", _issueStyle);
                GUI.color = old;
            }
        }
    }
}
