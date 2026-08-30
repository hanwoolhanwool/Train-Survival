using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>에셋랩 카탈로그 분류 — 예산·피벗 규칙이 갈리는 단위 (에셋랩-씬-계획.md §3).</summary>
    public enum AssetLabCategory
    {
        /// <summary>지형 세그먼트 프리팹 — 타일 전체를 한 덩어리로 본다.</summary>
        Terrain,

        /// <summary>타일 위에 흩뿌려지는 배경 프롭 — 개수가 많아 개당 예산이 가장 빡빡하다.</summary>
        Environment,

        /// <summary>채집 자원 노드.</summary>
        Resource,

        /// <summary>열차 위 건축물.</summary>
        Structure,

        /// <summary>열차 본체·부품.</summary>
        Train,

        /// <summary>플레이어·몬스터.</summary>
        Character,

        /// <summary>손에 드는 무기.</summary>
        Weapon,
    }

    /// <summary>검수 항목의 심각도 — 패널 색과 정렬 순서를 정한다.</summary>
    public enum AssetIssueSeverity
    {
        /// <summary>기록만 — 판단 재료.</summary>
        Info,

        /// <summary>고치는 편이 좋다 — 예산 초과·규약 미준수.</summary>
        Warning,

        /// <summary>화면에 바로 티가 난다 — 묻힘·뜸·머티리얼 누락.</summary>
        Error,
    }

    /// <summary>검수 결과 한 줄.</summary>
    public readonly struct AssetIssue
    {
        public AssetIssue(AssetIssueSeverity severity, string code, string message)
        {
            Severity = severity;
            Code = code;
            Message = message;
        }

        public AssetIssueSeverity Severity { get; }

        /// <summary>규칙 식별자 — 테스트가 문구가 아니라 이 코드로 못을 박는다.</summary>
        public string Code { get; }

        public string Message { get; }
    }

    /// <summary>
    /// 에셋 하나에서 뽑아낸 계측값 — 씬을 만지지 않고 값만 담는다.
    /// 씬 오브젝트에서 채우는 일은 <see cref="AssetLabProbe"/>가 한다.
    /// </summary>
    public readonly struct AssetMeasurement
    {
        public AssetMeasurement(
            int triangles,
            int materialCount,
            int missingMaterialCount,
            int rendererCount,
            Vector3 size,
            float groundOffset,
            bool hasCollider,
            int lodLevels,
            int lowestLodTriangles,
            bool castsShadow)
        {
            Triangles = triangles;
            MaterialCount = materialCount;
            MissingMaterialCount = missingMaterialCount;
            RendererCount = rendererCount;
            Size = size;
            GroundOffset = groundOffset;
            HasCollider = hasCollider;
            LodLevels = lodLevels;
            LowestLodTriangles = lowestLodTriangles;
            CastsShadow = castsShadow;
        }

        public int Triangles { get; }

        public int MaterialCount { get; }

        /// <summary>머티리얼 슬롯이 비어 핑크로 뜨는 렌더러 수.</summary>
        public int MissingMaterialCount { get; }

        public int RendererCount { get; }

        /// <summary>월드 바운즈 크기(m).</summary>
        public Vector3 Size { get; }

        /// <summary>
        /// 바운즈 밑면의 Y — 프리팹 원점을 지면(y=0)에 놓았을 때 얼마나 묻히거나(음수)
        /// 뜨는가(양수). 접지물은 0이어야 배치할 때 보정 오프셋이 필요 없다.
        /// </summary>
        public float GroundOffset { get; }

        public bool HasCollider { get; }

        /// <summary>
        /// LOD 단계 수 — 1이면 없다. <b>Unity 6에서는 LOD가 두 갈래다</b>:
        /// <see cref="LODGroup"/> 컴포넌트(오브젝트를 통째로 교체)와 <b>Mesh LOD</b>
        /// (<see cref="Mesh.lodCount"/> — 메시 자체가 단계를 품는다). 이 프로젝트는 후자를 쓴다.
        /// </summary>
        public int LodLevels { get; }

        /// <summary>최하위 LOD의 삼각형 수 — 원거리에서 실제로 그려지는 비용.</summary>
        public int LowestLodTriangles { get; }

        public bool CastsShadow { get; }

        /// <summary>LOD가 걸려 있는가.</summary>
        public bool HasLod => LodLevels > 1;
    }

    /// <summary>
    /// 에셋랩 판정 규칙 — 씬·컴포넌트를 만지지 않는 순수 함수만 둔다 (EditMode 테스트 대상).
    /// 임계값 출처는 <c>docs/design/Train-Survival-아트-렌더링-예산.md</c>다.
    /// </summary>
    public static class AssetLabDiagnostics
    {
        /// <summary>배경 프롭 개당 목표치 — 예산 §6의 가정값.</summary>
        public const int EnvironmentTriBudget = 1500;

        /// <summary>세그먼트 한 장 목표치 — 예산 §6 "타일당 60,000".</summary>
        public const int TerrainTriBudget = 60000;

        /// <summary>
        /// 접지 판정 허용 오차 — 높이에 비례한다. 5 cm짜리 자갈과 12 m 나무에
        /// 같은 절대 오차를 적용하면 한쪽만 계속 걸린다.
        /// </summary>
        public const float GroundOffsetRatio = 0.02f;

        /// <summary>비율만 쓰면 납작한 것이 통과해 버리므로 바닥값을 둔다.</summary>
        public const float GroundOffsetFloor = 0.02f;

        /// <summary>LOD 없이 넘어가는 크기 상한(m) — 이보다 크면 원거리까지 풀 메시로 남는다.</summary>
        public const float LodRequiredHeight = 4f;

        /// <summary>
        /// 이 이하로 가벼우면 크더라도 LOD를 묻지 않는다 — 지역 팔레트의 절차 메시는
        /// 15~200 tris짜리 블록아웃이라 단계를 나눌 것이 없다(대초원 풍차 124 tris).
        /// </summary>
        public const int LodExemptTriangles = 500;

        /// <summary>카테고리별 개당 삼각형 예산.</summary>
        public static int TriBudgetFor(AssetLabCategory category)
        {
            switch (category)
            {
                case AssetLabCategory.Terrain:
                    return TerrainTriBudget;
                case AssetLabCategory.Environment:
                    return EnvironmentTriBudget;
                case AssetLabCategory.Resource:
                    return 800;
                case AssetLabCategory.Structure:
                    return 5000;
                case AssetLabCategory.Train:
                    return 15000;
                case AssetLabCategory.Character:
                    return 12000;
                case AssetLabCategory.Weapon:
                    return 3000;
                default:
                    return EnvironmentTriBudget;
            }
        }

        /// <summary>
        /// 이 카테고리는 지면에 발을 붙이는가 — 타일은 지면 자체이고, 무기는 손에,
        /// 열차 부품은 차체에 붙는다. 셋 다 원점이 지면일 이유가 없어 접지 판정에서 뺀다
        /// (열차는 실측에서 <c>Train_CabLamp</c>·<c>Train_Coupler</c>·<c>HandrailAnchor</c> 3종이
        /// 전부 오판정이었다 — 에셋랩-씬-계획.md §7 결정 ①).
        /// </summary>
        public static bool IsGrounded(AssetLabCategory category)
        {
            return category != AssetLabCategory.Terrain
                && category != AssetLabCategory.Weapon
                && category != AssetLabCategory.Train;
        }

        /// <summary>주어진 높이에서 접지로 인정할 오차(m).</summary>
        public static float GroundOffsetToleranceFor(float height)
        {
            return Mathf.Max(GroundOffsetFloor, Mathf.Abs(height) * GroundOffsetRatio);
        }

        /// <summary>경로에서 카테고리를 뽑는다 — 폴더가 아니라 파일명 접두어가 기준이다.</summary>
        public static AssetLabCategory CategoryFor(string assetName)
        {
            if (string.IsNullOrEmpty(assetName))
            {
                return AssetLabCategory.Environment;
            }

            if (assetName.StartsWith("TerrainTile", System.StringComparison.Ordinal))
            {
                return AssetLabCategory.Terrain;
            }

            if (assetName.StartsWith("Res_", System.StringComparison.Ordinal)
                || assetName.StartsWith("ResourceNode", System.StringComparison.Ordinal))
            {
                return AssetLabCategory.Resource;
            }

            if (assetName.StartsWith("Structure_", System.StringComparison.Ordinal)
                || assetName.StartsWith("Plank_", System.StringComparison.Ordinal))
            {
                return AssetLabCategory.Structure;
            }

            if (assetName.StartsWith("Train_", System.StringComparison.Ordinal)
                || assetName.StartsWith("Firebox", System.StringComparison.Ordinal)
                || assetName.StartsWith("Handrail", System.StringComparison.Ordinal))
            {
                return AssetLabCategory.Train;
            }

            if (assetName.StartsWith("Character_", System.StringComparison.Ordinal)
                || assetName.StartsWith("Monster", System.StringComparison.Ordinal)
                || assetName.StartsWith("Boss_", System.StringComparison.Ordinal)
                || assetName.StartsWith("Player", System.StringComparison.Ordinal))
            {
                return AssetLabCategory.Character;
            }

            if (assetName.StartsWith("Weapon_", System.StringComparison.Ordinal))
            {
                return AssetLabCategory.Weapon;
            }

            return AssetLabCategory.Environment;
        }

        /// <summary>
        /// 계측값을 규칙에 통과시켜 검수 항목을 뽑는다. 심각도 내림차순으로 정렬해 돌려준다.
        /// </summary>
        public static List<AssetIssue> Inspect(AssetLabCategory category, AssetMeasurement m)
        {
            var issues = new List<AssetIssue>();

            if (m.RendererCount == 0)
            {
                issues.Add(new AssetIssue(AssetIssueSeverity.Error, "NO_RENDERER",
                    "렌더러가 없다 — 배치해도 아무것도 보이지 않는다."));
                return issues;
            }

            if (m.MissingMaterialCount > 0)
            {
                issues.Add(new AssetIssue(AssetIssueSeverity.Error, "MISSING_MATERIAL",
                    $"머티리얼이 빈 슬롯 {m.MissingMaterialCount}개 — 핑크로 렌더된다."));
            }

            if (IsGrounded(category))
            {
                float tolerance = GroundOffsetToleranceFor(m.Size.y);
                if (m.GroundOffset < -tolerance)
                {
                    issues.Add(new AssetIssue(AssetIssueSeverity.Error, "PIVOT_SUNK",
                        $"피벗이 바운즈 중심에 있다 — 원점을 지면에 놓으면 {-m.GroundOffset:F2} m 묻힌다"
                        + $" (높이 {m.Size.y:F2} m의 {-m.GroundOffset / Mathf.Max(m.Size.y, 0.001f) * 100f:F0} %)."));
                }
                else if (m.GroundOffset > tolerance)
                {
                    issues.Add(new AssetIssue(AssetIssueSeverity.Error, "PIVOT_FLOAT",
                        $"피벗이 바운즈 아래에 있다 — 원점을 지면에 놓으면 {m.GroundOffset:F2} m 뜬다."));
                }
            }

            int budget = TriBudgetFor(category);
            if (m.Triangles > budget * 2)
            {
                issues.Add(new AssetIssue(AssetIssueSeverity.Error, "TRI_OVER_2X",
                    $"삼각형 {m.Triangles:N0} — 예산 {budget:N0}의 {(float)m.Triangles / budget:F1}배."));
            }
            else if (m.Triangles > budget)
            {
                issues.Add(new AssetIssue(AssetIssueSeverity.Warning, "TRI_OVER",
                    $"삼각형 {m.Triangles:N0} — 예산 {budget:N0} 초과."));
            }

            if (!m.HasLod && m.Size.y >= LodRequiredHeight && m.Triangles > LodExemptTriangles)
            {
                issues.Add(new AssetIssue(AssetIssueSeverity.Warning, "NO_LOD",
                    $"높이 {m.Size.y:F1} m · {m.Triangles:N0} tris인데 LOD가 없다"
                    + " (LODGroup도 Mesh LOD도 없음) — 원거리까지 풀 메시로 남는다."));
            }

            if (m.MaterialCount > 2 && category != AssetLabCategory.Terrain)
            {
                issues.Add(new AssetIssue(AssetIssueSeverity.Warning, "MATERIAL_SPLIT",
                    $"머티리얼 {m.MaterialCount}개 — 파츠 분리는 tris보다 비싸다 (예산 §2)."));
            }

            if (category == AssetLabCategory.Environment && m.HasCollider)
            {
                issues.Add(new AssetIssue(AssetIssueSeverity.Info, "PROP_COLLIDER",
                    "콜라이더가 있다 — 변주(ScatterSlot)를 걸면 피어마다 다른 벽이 생긴다."));
            }

            return issues;
        }

        /// <summary>항목 목록에서 가장 높은 심각도 — 목록 색을 칠할 때 쓴다.</summary>
        public static AssetIssueSeverity WorstOf(IReadOnlyList<AssetIssue> issues)
        {
            var worst = AssetIssueSeverity.Info;
            if (issues == null)
            {
                return worst;
            }

            for (int i = 0; i < issues.Count; i++)
            {
                if (issues[i].Severity > worst)
                {
                    worst = issues[i].Severity;
                }
            }

            return worst;
        }
    }
}
