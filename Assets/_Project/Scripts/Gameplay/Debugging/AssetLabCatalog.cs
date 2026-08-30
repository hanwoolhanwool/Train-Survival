using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>카탈로그 한 줄 — 목록에 뜨는 에셋 하나.</summary>
    public sealed class AssetLabEntry
    {
        public AssetLabEntry(string displayName, string assetPath, AssetLabCategory category, GameObject asset)
        {
            DisplayName = displayName;
            AssetPath = assetPath;
            Category = category;
            Asset = asset;
        }

        public string DisplayName { get; }

        public string AssetPath { get; }

        public AssetLabCategory Category { get; }

        /// <summary>프리팹/모델 원본 — 스폰할 때 이것을 복제한다.</summary>
        public GameObject Asset { get; }

        /// <summary>마지막으로 스폰했을 때의 계측값 — 목록 색·정렬에 쓴다.</summary>
        public AssetMeasurement Measurement { get; set; }

        /// <summary>마지막 검수 결과.</summary>
        public List<AssetIssue> Issues { get; set; }

        /// <summary>한 번이라도 재 봤는가 — 안 재 본 항목은 회색으로 둔다.</summary>
        public bool Measured { get; set; }
    }

    /// <summary>
    /// 에셋랩 카탈로그 수집 — 어떤 에셋이 맵에 올라가는지를 프로젝트에서 직접 읽는다.
    /// 목록을 손으로 배선하지 않는 이유는, 지역이 늘 때마다 씬을 다시 만져야 하기 때문이다
    /// (에셋랩-씬-계획.md §4-①).
    /// </summary>
    public static class AssetLabCatalog
    {
        /// <summary>수집 대상 폴더 — 프리팹이 먼저다(배치되는 실체가 프리팹이므로).</summary>
        private static readonly string[] SearchFolders =
        {
            "Assets/_Project/Prefabs",
            "Assets/_Project/Art/Models",
        };

        /// <summary>목록에서 뺄 경로 조각 — 화면에 놓이지 않는 것들.</summary>
        private static readonly string[] ExcludedPathParts =
        {
            "/Network/",
            "/UI/",
        };

        /// <summary>
        /// 이름에 이 조각이 들어가면 뺀다 — 날아가고 터지는 것은 "맵에 배치된 에셋"이 아니라
        /// 예산·피벗 규칙이 다르다. 목록에 두면 오판정만 늘어난다.
        /// </summary>
        private static readonly string[] ExcludedNameParts =
        {
            "Projectile",
            "Effect",
            "Tracer",
        };

        /// <summary>
        /// 프로젝트에서 배치 가능한 에셋을 모아 카테고리·이름순으로 돌려준다.
        /// 에디터 밖에서는 빈 목록 — 이 씬 자체가 빌드에 들어가지 않는다.
        /// </summary>
        public static List<AssetLabEntry> Collect()
        {
            var entries = new List<AssetLabEntry>();

#if UNITY_EDITOR
            var seenPaths = new HashSet<string>();

            foreach (string folder in SearchFolders)
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder(folder))
                {
                    continue;
                }

                string[] guids = UnityEditor.AssetDatabase.FindAssets("t:Prefab t:Model", new[] { folder });
                foreach (string guid in guids)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(path) || !seenPaths.Add(path) || IsExcluded(path))
                    {
                        continue;
                    }

                    var asset = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                    if (asset == null || asset.GetComponentInChildren<Renderer>(true) == null)
                    {
                        continue;   // 렌더러가 하나도 없으면 볼 것이 없다 (VFX·앵커 프리팹)
                    }

                    string name = System.IO.Path.GetFileNameWithoutExtension(path);
                    if (IsExcludedName(name))
                    {
                        continue;
                    }

                    entries.Add(new AssetLabEntry(name, path, AssetLabDiagnostics.CategoryFor(name), asset));
                }
            }

            entries.Sort(CompareEntries);
#endif

            return entries;
        }

        /// <summary>카테고리 순 → 이름 순. 사람이 목록에서 위치를 외울 수 있어야 한다.</summary>
        internal static int CompareEntries(AssetLabEntry a, AssetLabEntry b)
        {
            int byCategory = a.Category.CompareTo(b.Category);
            return byCategory != 0
                ? byCategory
                : string.Compare(a.DisplayName, b.DisplayName, System.StringComparison.Ordinal);
        }

        private static bool IsExcluded(string path)
        {
            for (int i = 0; i < ExcludedPathParts.Length; i++)
            {
                if (path.Contains(ExcludedPathParts[i]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>이름 기준 제외 — 테스트가 목록 규칙을 직접 확인할 수 있게 열어 둔다.</summary>
        internal static bool IsExcludedName(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return true;
            }

            for (int i = 0; i < ExcludedNameParts.Length; i++)
            {
                if (assetName.IndexOf(ExcludedNameParts[i], System.StringComparison.Ordinal) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>검색어 필터 — 대소문자를 무시한 부분 일치.</summary>
        public static bool Matches(AssetLabEntry entry, string filter)
        {
            if (string.IsNullOrEmpty(filter))
            {
                return true;
            }

            return entry.DisplayName.IndexOf(filter, System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>카테고리 한글 이름 — 패널 탭 라벨.</summary>
        public static string LabelOf(AssetLabCategory category)
        {
            switch (category)
            {
                case AssetLabCategory.Terrain:
                    return "지형";
                case AssetLabCategory.Environment:
                    return "배경";
                case AssetLabCategory.Resource:
                    return "자원";
                case AssetLabCategory.Structure:
                    return "건축";
                case AssetLabCategory.Train:
                    return "열차";
                case AssetLabCategory.Character:
                    return "캐릭터";
                case AssetLabCategory.Weapon:
                    return "무기";
                default:
                    return category.ToString();
            }
        }
    }
}
