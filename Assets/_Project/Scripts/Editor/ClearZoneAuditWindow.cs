using System;
using System.Collections.Generic;
using System.Text;
using Game.Gameplay.World;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// 지형 세그먼트가 클리어 존 규격(레벨 디자인 가이드 §4.2·§4.7)을 지키는지 일괄 판정하는 에디터 도구.
    /// 팔레트가 지역당 10종 = 40장으로 늘어나므로 <b>사람 눈으로 검수하지 않는다</b>
    /// (레벨 디자인 구현 계획 리스크 6·7).
    ///
    /// <para>판정 자체는 <see cref="ClearZoneRules"/>(순수 함수)가 소유하고, 이 창은
    /// 콜라이더·렌더러를 타일 로컬 AABB로 환산해 넘기는 일만 한다.</para>
    /// </summary>
    public sealed class ClearZoneAuditWindow : EditorWindow
    {
        private const string DefaultFolder = "Assets/_Project/Prefabs";
        private const string DefaultPrefix = "TerrainTile";

        // 배경에 NetworkObject가 붙으면 대역폭이 장식 개수에 비례하기 시작한다(계획 불변 지침).
        private const string NetworkObjectTypeName = "NetworkObject";

        private string _folder = DefaultFolder;
        private string _prefix = DefaultPrefix;
        private bool _showWarnings = true;
        private Vector2 _scroll;
        private readonly List<TileReport> _reports = new List<TileReport>();
        private string _summary = "아직 검사하지 않았다.";

        // 리눅스 에디터(= CI)에서는 등록하지 않는다 — 메뉴 항목 수가 임계를 넘으면
        // PlayMode 진입에서 세그폴트가 난다 (자동화 1차 구현 계획 §1.8).
#if !UNITY_EDITOR_LINUX
        [MenuItem("Game/QA/Clear Zone Audit")]
#endif
        private static void Open()
        {
            var window = GetWindow<ClearZoneAuditWindow>("클리어 존 검사기");
            window.minSize = new Vector2(560f, 360f);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "세그먼트 프리팹이 클리어 존 규격을 지키는지 판정한다.\n" +
                "오류는 게임플레이가 깨지는 것(열차 파묻힘·웨이브 갇힘), 경고는 검토 대상이다.",
                MessageType.None);

            using (new EditorGUILayout.HorizontalScope())
            {
                _folder = EditorGUILayout.TextField("대상 폴더", _folder);
                if (GUILayout.Button("...", GUILayout.Width(28f)))
                {
                    string picked = EditorUtility.OpenFolderPanel("검사할 프리팹 폴더", _folder, string.Empty);
                    if (!string.IsNullOrEmpty(picked) && picked.StartsWith(Application.dataPath))
                    {
                        _folder = "Assets" + picked.Substring(Application.dataPath.Length);
                    }
                }
            }

            _prefix = EditorGUILayout.TextField("이름 접두어", _prefix);
            _showWarnings = EditorGUILayout.ToggleLeft("경고도 표시", _showWarnings);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("폴더 검사"))
                {
                    ScanFolder();
                }

                using (new EditorGUI.DisabledScope(Selection.gameObjects.Length == 0))
                {
                    if (GUILayout.Button("선택 항목 검사"))
                    {
                        ScanSelection();
                    }
                }

                using (new EditorGUI.DisabledScope(_reports.Count == 0))
                {
                    if (GUILayout.Button("콘솔로 내보내기"))
                    {
                        LogToConsole();
                    }
                }
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField(_summary, EditorStyles.boldLabel);

            using (var scroll = new EditorGUILayout.ScrollViewScope(_scroll))
            {
                _scroll = scroll.scrollPosition;
                for (int i = 0; i < _reports.Count; i++)
                {
                    DrawReport(_reports[i]);
                }
            }
        }

        private void DrawReport(TileReport report)
        {
            int shown = _showWarnings ? report.Findings.Count : report.ErrorCount;
            string badge = report.ErrorCount > 0
                ? $"오류 {report.ErrorCount}"
                : report.WarningCount > 0 ? $"경고 {report.WarningCount}" : "통과";

            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    report.Expanded = EditorGUILayout.Foldout(
                        report.Expanded, $"{report.Label}   [{badge}]  앵커 {report.AnchorCount}", true);

                    if (!string.IsNullOrEmpty(report.AssetPath) && GUILayout.Button("선택", GUILayout.Width(48f)))
                    {
                        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(report.AssetPath);
                        Selection.activeObject = asset;
                        EditorGUIUtility.PingObject(asset);
                    }
                }

                if (!report.Expanded || shown == 0)
                {
                    return;
                }

                for (int i = 0; i < report.Findings.Count; i++)
                {
                    Finding finding = report.Findings[i];
                    if (!finding.IsError && !_showWarnings)
                    {
                        continue;
                    }

                    using (new EditorGUILayout.HorizontalScope())
                    {
                        GUILayout.Space(14f);
                        GUILayout.Label(finding.IsError ? "✖" : "▲", GUILayout.Width(16f));
                        EditorGUILayout.LabelField($"{finding.Path} — {finding.Message}");

                        if (finding.Target != null && GUILayout.Button("핑", GUILayout.Width(32f)))
                        {
                            EditorGUIUtility.PingObject(finding.Target);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 창을 열지 않고 기본 폴더를 통째로 판정해 콘솔에 남긴다 — 배치 검수·회귀 확인용.
        /// </summary>
        // 리눅스 에디터(= CI)에서는 등록하지 않는다 — 메뉴 항목 수가 임계를 넘으면
        // PlayMode 진입에서 세그폴트가 난다 (자동화 1차 구현 계획 §1.8).
#if !UNITY_EDITOR_LINUX
        [MenuItem("Game/QA/Clear Zone Audit (Log)")]
#endif
        private static void AuditAllToConsole()
        {
            var reports = new List<TileReport>();
            ScanFolderInto(DefaultFolder, DefaultPrefix, reports);
            Debug.Log(BuildLog(Summarize($"{DefaultFolder} · 접두어 \"{DefaultPrefix}\"", reports), reports));
        }

        private void ScanFolder()
        {
            _reports.Clear();

            if (!AssetDatabase.IsValidFolder(_folder))
            {
                _summary = $"폴더가 없다: {_folder}";
                return;
            }

            ScanFolderInto(_folder, _prefix, _reports);
            UpdateSummary($"{_folder} · 접두어 \"{_prefix}\"");
        }

        private static void ScanFolderInto(string folder, string prefix, List<TileReport> reports)
        {
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { folder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string fileName = System.IO.Path.GetFileNameWithoutExtension(path);
                if (!string.IsNullOrEmpty(prefix) && !fileName.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                // 프리팹 컨텐츠를 실제 계층으로 펼쳐야 중첩 프리팹의 트랜스폼이 합성된 값으로 나온다.
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    reports.Add(Inspect(root, fileName, path, pingable: false));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private void ScanSelection()
        {
            _reports.Clear();

            GameObject[] selection = Selection.gameObjects;
            for (int i = 0; i < selection.Length; i++)
            {
                _reports.Add(Inspect(selection[i], selection[i].name, string.Empty, pingable: true));
            }

            UpdateSummary("선택 항목");
        }

        private void UpdateSummary(string scope)
        {
            _summary = Summarize(scope, _reports);
        }

        private static string Summarize(string scope, List<TileReport> reports)
        {
            int errors = 0;
            int warnings = 0;
            for (int i = 0; i < reports.Count; i++)
            {
                errors += reports[i].ErrorCount;
                warnings += reports[i].WarningCount;
            }

            return reports.Count == 0
                ? $"{scope} — 검사 대상이 없다."
                : $"{scope} — 타일 {reports.Count}장 · 오류 {errors} · 경고 {warnings}";
        }

        private void LogToConsole()
        {
            Debug.Log(BuildLog(_summary, _reports));
        }

        private static string BuildLog(string summary, List<TileReport> reports)
        {
            var text = new StringBuilder();
            text.AppendLine($"[클리어 존 검사] {summary}");

            for (int i = 0; i < reports.Count; i++)
            {
                TileReport report = reports[i];
                if (report.Findings.Count == 0)
                {
                    continue;
                }

                text.AppendLine(
                    $"── {report.Label} (앵커 {report.AnchorCount} · " +
                    $"오류 {report.ErrorCount} · 경고 {report.WarningCount})");

                for (int f = 0; f < report.Findings.Count; f++)
                {
                    Finding finding = report.Findings[f];
                    text.AppendLine($"   {(finding.IsError ? "오류" : "경고")} · {finding.Path} — {finding.Message}");
                }
            }

            return text.ToString();
        }

        /// <summary>타일 하나를 규격 전체에 대해 판정한다. 좌표는 전부 타일 루트 로컬로 환산한다.</summary>
        private static TileReport Inspect(GameObject root, string label, string assetPath, bool pingable)
        {
            var report = new TileReport { Label = label, AssetPath = assetPath };
            Transform rootTransform = root.transform;
            Matrix4x4 toTile = rootTransform.worldToLocalMatrix;

            InspectColliders(root, rootTransform, toTile, report, pingable);
            InspectRenderers(root, rootTransform, toTile, report, pingable);
            InspectAnchors(root, rootTransform, report, pingable);
            InspectComponents(root, rootTransform, report, pingable);

            return report;
        }

        private static void InspectColliders(
            GameObject root, Transform rootTransform, Matrix4x4 toTile, TileReport report, bool pingable)
        {
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                Collider collider = colliders[i];
                if (!TryLocalBounds(collider, out Bounds local))
                {
                    continue;
                }

                Bounds bounds = ClearZoneRules.TransformAabb(
                    local, toTile * collider.transform.localToWorldMatrix);

                var probe = new ColliderProbe(
                    bounds,
                    collider.isTrigger,
                    collider is MeshCollider,
                    HasComponentUpTo<WorldFrameSurface>(collider.transform, rootTransform),
                    IsUnderTrackStructure(collider.transform, rootTransform),
                    HasComponentUpTo<ScatterSlot>(collider.transform, rootTransform));

                ClearZoneIssue issues = ClearZoneRules.Evaluate(probe);
                if (issues == ClearZoneIssue.None)
                {
                    continue;
                }

                string path = HierarchyPath(collider.transform, rootTransform);
                UnityEngine.Object pingTarget = pingable ? collider.gameObject : null;

                foreach (ClearZoneIssue flag in AllIssues)
                {
                    if ((issues & flag) == 0)
                    {
                        continue;
                    }

                    report.Add(path, ClearZoneRules.Describe(flag), IsError(flag), pingTarget);
                }
            }
        }

        private static void InspectRenderers(
            GameObject root, Transform rootTransform, Matrix4x4 toTile, TileReport report, bool pingable)
        {
            // 콜라이더가 없어도 궤도 통로에 걸친 메시는 열차와 겹쳐 보인다 — 물리와 별개로 잡는다.
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer renderer = renderers[i];
                if (IsUnderTrackStructure(renderer.transform, rootTransform))
                {
                    continue;
                }

                if (!TryLocalBounds(renderer, out Bounds local))
                {
                    continue;
                }

                Bounds bounds = ClearZoneRules.TransformAabb(
                    local, toTile * renderer.transform.localToWorldMatrix);

                if (!ClearZoneRules.RisesAboveGround(bounds)
                    || !ClearZoneRules.OverlapsBandX(bounds, 0f, ClearZoneRules.TrackCorridorHalfWidth))
                {
                    continue;
                }

                report.Add(
                    HierarchyPath(renderer.transform, rootTransform),
                    "궤도 통로에 걸치는 메시 — 콜라이더가 없어도 열차와 겹쳐 보인다",
                    isError: false,
                    target: pingable ? renderer.gameObject : null);
            }
        }

        private static void InspectAnchors(
            GameObject root, Transform rootTransform, TileReport report, bool pingable)
        {
            ResourceAnchor[] anchors = root.GetComponentsInChildren<ResourceAnchor>(true);
            report.AnchorCount = anchors.Length;

            for (int i = 0; i < anchors.Length; i++)
            {
                ResourceAnchor anchor = anchors[i];
                Vector3 local = rootTransform.InverseTransformPoint(anchor.transform.position);
                AnchorIssue issues = ClearZoneRules.EvaluateAnchor(local);
                if (issues == AnchorIssue.None)
                {
                    continue;
                }

                string path = HierarchyPath(anchor.transform, rootTransform);
                UnityEngine.Object pingTarget = pingable ? anchor.gameObject : null;

                foreach (AnchorIssue flag in AllAnchorIssues)
                {
                    if ((issues & flag) == 0)
                    {
                        continue;
                    }

                    report.Add(path, ClearZoneRules.Describe(flag), isError: true, target: pingTarget);
                }
            }

            if (!ClearZoneRules.IsAnchorCountValid(anchors.Length))
            {
                report.Add(
                    ".",
                    $"자원 앵커 {anchors.Length}개 — 기준은 타일당 " +
                    $"{ClearZoneRules.MinAnchorsPerTile}~{ClearZoneRules.MaxAnchorsPerTile}개다",
                    isError: false,
                    target: null);
            }

            // 역 소품 앵커 (기차역 2차) — 배치 규격은 자원 앵커와 같다(|x| 4~16 · |z| ≤ 20).
            // 개수 기준은 없다(역 타일마다 다르다). 16 m를 넘으면 1단계 집게로 영영 닿지 않아
            // "보이는데 못 가져가는 물건"이 되므로 그것만 오류로 잡는다.
            StationPropAnchor[] props = root.GetComponentsInChildren<StationPropAnchor>(true);
            for (int i = 0; i < props.Length; i++)
            {
                StationPropAnchor prop = props[i];
                Vector3 local = rootTransform.InverseTransformPoint(prop.transform.position);
                AnchorIssue issues = ClearZoneRules.EvaluateAnchor(local);
                if (issues == AnchorIssue.None)
                {
                    continue;
                }

                string propPath = HierarchyPath(prop.transform, rootTransform);
                UnityEngine.Object propTarget = pingable ? prop.gameObject : null;

                foreach (AnchorIssue flag in AllAnchorIssues)
                {
                    if ((issues & flag) == 0)
                    {
                        continue;
                    }

                    report.Add(
                        propPath,
                        $"역 소품({prop.Kind}) — {ClearZoneRules.Describe(flag)}",
                        isError: true,
                        target: propTarget);
                }
            }

            // 랜드마크·스캐터 슬롯 (가이드 §4.4) — 개수만 본다. 자리의 적절함은 사람이 정한다.
            int landmarks = root.GetComponentsInChildren<LandmarkSlot>(true).Length;
            if (!ClearZoneRules.IsLandmarkSlotCountValid(landmarks))
            {
                report.Add(
                    ".",
                    $"랜드마크 슬롯 {landmarks}개 — 타일당 " +
                    $"{ClearZoneRules.MaxLandmarkSlotsPerTile}개까지다",
                    isError: false,
                    target: null);
            }

            int scatters = root.GetComponentsInChildren<ScatterSlot>(true).Length;
            if (!ClearZoneRules.IsScatterSlotCountValid(scatters))
            {
                report.Add(
                    ".",
                    $"스캐터 슬롯 {scatters}개 — 기준은 타일당 " +
                    $"{ClearZoneRules.MinScatterSlotsPerTile}~{ClearZoneRules.MaxScatterSlotsPerTile}개다" +
                    " (반복 인지를 줄이는 주 장치라 비워 두면 팔레트로만 버티게 된다)",
                    isError: false,
                    target: null);
            }
        }

        private static void InspectComponents(
            GameObject root, Transform rootTransform, TileReport report, bool pingable)
        {
            Component[] components = root.GetComponentsInChildren<Component>(true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                if (component.GetType().Name == NetworkObjectTypeName)
                {
                    report.Add(
                        HierarchyPath(component.transform, rootTransform),
                        "NetworkObject — 배경은 전부 로컬 표현이다(대역폭이 장식 개수에 비례한다)",
                        isError: true,
                        target: pingable ? component.gameObject : null);
                    continue;
                }

                // 포인트·스팟 라이트 그림자는 캐스터 비용이 6배다(아트 예산 §4.2).
                var light = component as Light;
                if (light != null && light.type != LightType.Directional && light.shadows != LightShadows.None)
                {
                    report.Add(
                        HierarchyPath(light.transform, rootTransform),
                        $"{light.type} 라이트 그림자 — 캐스터 비용 6배",
                        isError: false,
                        target: pingable ? light.gameObject : null);
                }
            }

            // 타일 루트가 이동하므로 Static Batching·라이트맵 대상이 될 수 없다(가이드 §4.1).
            Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < transforms.Length; i++)
            {
                GameObject target = transforms[i].gameObject;
                if (GameObjectUtility.GetStaticEditorFlags(target) == 0)
                {
                    continue;
                }

                report.Add(
                    HierarchyPath(transforms[i], rootTransform),
                    "정적 플래그가 켜져 있다 — 타일 루트는 매 프레임 이동한다",
                    isError: false,
                    target: pingable ? target : null);
            }
        }

        /// <summary>
        /// 콜라이더의 <b>로컬</b> AABB. <c>Collider.bounds</c>는 프리팹 컨텐츠(프리뷰 씬)에서 신뢰할 수 없어
        /// 모양 값에서 직접 만든다.
        /// </summary>
        private static bool TryLocalBounds(Collider collider, out Bounds bounds)
        {
            switch (collider)
            {
                case BoxCollider box:
                    bounds = new Bounds(box.center, box.size);
                    return true;

                case SphereCollider sphere:
                    bounds = new Bounds(sphere.center, Vector3.one * (sphere.radius * 2f));
                    return true;

                case CapsuleCollider capsule:
                {
                    float diameter = capsule.radius * 2f;
                    float height = Mathf.Max(capsule.height, diameter);
                    Vector3 size = capsule.direction == 0 ? new Vector3(height, diameter, diameter)
                        : capsule.direction == 1 ? new Vector3(diameter, height, diameter)
                        : new Vector3(diameter, diameter, height);
                    bounds = new Bounds(capsule.center, size);
                    return true;
                }

                case MeshCollider mesh when mesh.sharedMesh != null:
                    bounds = mesh.sharedMesh.bounds;
                    return true;

                default:
                    bounds = default;
                    return false;
            }
        }

        private static bool TryLocalBounds(Renderer renderer, out Bounds bounds)
        {
            var skinned = renderer as SkinnedMeshRenderer;
            if (skinned != null)
            {
                bounds = skinned.localBounds;
                return true;
            }

            var filter = renderer.GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                bounds = filter.sharedMesh.bounds;
                return true;
            }

            bounds = default;
            return false;
        }

        /// <summary>루트까지 거슬러 올라가며 컴포넌트를 찾는다 — 루트 위로는 넘어가지 않는다.</summary>
        private static bool HasComponentUpTo<T>(Transform from, Transform root) where T : Component
        {
            for (Transform current = from; current != null; current = current.parent)
            {
                if (current.GetComponent<T>() != null)
                {
                    return true;
                }

                if (current == root)
                {
                    break;
                }
            }

            return false;
        }

        private static bool IsUnderTrackStructure(Transform from, Transform root)
        {
            for (Transform current = from; current != null; current = current.parent)
            {
                if (ClearZoneRules.IsTrackStructureName(current.name))
                {
                    return true;
                }

                if (current == root)
                {
                    break;
                }
            }

            return false;
        }

        private static string HierarchyPath(Transform target, Transform root)
        {
            if (target == root)
            {
                return ".";
            }

            var builder = new StringBuilder(target.name);
            for (Transform current = target.parent; current != null && current != root; current = current.parent)
            {
                builder.Insert(0, "/").Insert(0, current.name);
            }

            return builder.ToString();
        }

        private static bool IsError(ClearZoneIssue issue)
        {
            switch (issue)
            {
                // 게임플레이가 즉시 깨지는 것들 — 열차가 파묻히고, 웨이브가 갇히고,
                // 매 교체마다 콜라이더를 굽고, 피어마다 다른 벽이 생긴다(계획 리스크 3).
                case ClearZoneIssue.TrackCorridor:
                case ClearZoneIssue.DropZone:
                case ClearZoneIssue.LongWall:
                case ClearZoneIssue.MeshColliderUsed:
                case ClearZoneIssue.ColliderUnderScatterSlot:
                    return true;

                default:
                    return false;
            }
        }

        private static readonly ClearZoneIssue[] AllIssues =
        {
            ClearZoneIssue.TrackCorridor,
            ClearZoneIssue.DropZone,
            ClearZoneIssue.LongWall,
            ClearZoneIssue.MeshColliderUsed,
            ClearZoneIssue.MissingWorldFrameSurface,
            ClearZoneIssue.OutsideTileFootprint,
            ClearZoneIssue.ColliderUnderScatterSlot,
        };

        private static readonly AnchorIssue[] AllAnchorIssues =
        {
            AnchorIssue.TooCloseToTrack,
            AnchorIssue.BeyondGrabberReach,
            AnchorIssue.OutsideTileFootprint,
        };

        private sealed class TileReport
        {
            public string Label;
            public string AssetPath;
            public int AnchorCount;
            public int ErrorCount;
            public int WarningCount;
            public bool Expanded = true;
            public readonly List<Finding> Findings = new List<Finding>();

            public void Add(string path, string message, bool isError, UnityEngine.Object target)
            {
                Findings.Add(new Finding(path, message, isError, target));
                if (isError)
                {
                    ErrorCount++;
                }
                else
                {
                    WarningCount++;
                }
            }
        }

        private readonly struct Finding
        {
            public readonly string Path;
            public readonly string Message;
            public readonly bool IsError;
            public readonly UnityEngine.Object Target;

            public Finding(string path, string message, bool isError, UnityEngine.Object target)
            {
                Path = path;
                Message = message;
                IsError = isError;
                Target = target;
            }
        }
    }
}
