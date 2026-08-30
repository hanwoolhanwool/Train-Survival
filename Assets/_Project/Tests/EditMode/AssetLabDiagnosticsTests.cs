using Game.Gameplay.Debugging;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 에셋랩 판정 규칙 — 실측으로 확인한 문제가 규칙에 걸리는지 못 박는다
    /// (docs/plans/features/에셋랩-씬-계획.md §6).
    /// </summary>
    public sealed class AssetLabDiagnosticsTests
    {
        /// <summary>정상 배경 프롭 하나 — 각 테스트가 여기서 한 항목만 흔들어 본다.</summary>
        private static AssetMeasurement Healthy(
            int triangles = 400,
            float height = 1f,
            float groundOffset = 0f,
            int lodLevels = 1,
            int materials = 1,
            int missingMaterials = 0,
            int renderers = 1)
        {
            return new AssetMeasurement(
                triangles,
                materials,
                missingMaterials,
                renderers,
                new Vector3(1f, height, 1f),
                groundOffset,
                hasCollider: false,
                lodLevels: lodLevels,
                lowestLodTriangles: lodLevels > 1 ? triangles / 4 : triangles,
                castsShadow: true);
        }

        private static bool Has(System.Collections.Generic.List<AssetIssue> issues, string code)
        {
            return issues.Exists(i => i.Code == code);
        }

        // ── 분류 ──

        [Test]
        public void 지형_타일은_지형으로_분류된다()
        {
            Assert.That(AssetLabDiagnostics.CategoryFor("TerrainTile_Forest_A"),
                Is.EqualTo(AssetLabCategory.Terrain));
        }

        [Test]
        public void 자원_접두어는_자원으로_분류된다()
        {
            Assert.That(AssetLabDiagnostics.CategoryFor("Res_Timber"),
                Is.EqualTo(AssetLabCategory.Resource));
        }

        [Test]
        public void 아는_접두어가_없으면_배경으로_분류된다()
        {
            Assert.That(AssetLabDiagnostics.CategoryFor("Env_Tree_Conifer_A"),
                Is.EqualTo(AssetLabCategory.Environment));
        }

        [Test]
        public void 지형과_무기와_열차는_접지_판정에서_빠진다()
        {
            // 열차 부품은 차체에 붙는다 — CabLamp·Coupler·HandrailAnchor 3종이 전부 오판정이었다.
            Assert.That(AssetLabDiagnostics.IsGrounded(AssetLabCategory.Terrain), Is.False);
            Assert.That(AssetLabDiagnostics.IsGrounded(AssetLabCategory.Weapon), Is.False);
            Assert.That(AssetLabDiagnostics.IsGrounded(AssetLabCategory.Train), Is.False);
            Assert.That(AssetLabDiagnostics.IsGrounded(AssetLabCategory.Environment), Is.True);
            Assert.That(AssetLabDiagnostics.IsGrounded(AssetLabCategory.Character), Is.True);
        }

        // ── 예산 ──

        [Test]
        public void 배경_예산은_아트_예산_문서의_1500이다()
        {
            Assert.That(AssetLabDiagnostics.TriBudgetFor(AssetLabCategory.Environment), Is.EqualTo(1500));
        }

        [Test]
        public void 예산을_넘으면_경고_두_배를_넘으면_오류다()
        {
            var warn = AssetLabDiagnostics.Inspect(AssetLabCategory.Environment, Healthy(triangles: 2000));
            var error = AssetLabDiagnostics.Inspect(AssetLabCategory.Environment, Healthy(triangles: 3001));

            Assert.That(Has(warn, "TRI_OVER"), Is.True);
            Assert.That(Has(warn, "TRI_OVER_2X"), Is.False);
            Assert.That(Has(error, "TRI_OVER_2X"), Is.True);
        }

        [Test]
        public void 예산_안이면_삼각형_지적이_없다()
        {
            var issues = AssetLabDiagnostics.Inspect(AssetLabCategory.Environment, Healthy(triangles: 1500));
            Assert.That(Has(issues, "TRI_OVER"), Is.False);
        }

        // ── 피벗 ──

        [Test]
        public void 바운즈_중심_피벗은_묻힘으로_잡힌다()
        {
            // Env_Signal_Rusty 실측: 높이 3.5 m · 지면 오프셋 -1.753 m (절반이 땅속).
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Environment, Healthy(height: 3.5f, groundOffset: -1.753f));

            Assert.That(Has(issues, "PIVOT_SUNK"), Is.True);
        }

        [Test]
        public void 바운즈_아래_피벗은_뜸으로_잡힌다()
        {
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Environment, Healthy(height: 2f, groundOffset: 0.5f));

            Assert.That(Has(issues, "PIVOT_FLOAT"), Is.True);
        }

        [Test]
        public void 허용_오차는_높이에_비례하고_바닥값을_지킨다()
        {
            // 12 m 나무는 24 cm 까지, 5 cm 자갈은 바닥값 2 cm 까지 봐준다.
            Assert.That(AssetLabDiagnostics.GroundOffsetToleranceFor(12f), Is.EqualTo(0.24f).Within(1e-4f));
            Assert.That(AssetLabDiagnostics.GroundOffsetToleranceFor(0.05f), Is.EqualTo(0.02f).Within(1e-4f));
        }

        [Test]
        public void 오차_안의_어긋남은_지적하지_않는다()
        {
            // Env_Stump_A 실측: 높이 0.23 m · 오프셋 0 — 통과해야 한다.
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Environment, Healthy(height: 0.23f, groundOffset: -0.01f));

            Assert.That(Has(issues, "PIVOT_SUNK"), Is.False);
        }

        [Test]
        public void 지형_타일은_묻혀_있어도_피벗을_지적하지_않는다()
        {
            // 타일은 지면 자체라 바운즈가 y=0 아래로 내려가는 것이 정상이다.
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Terrain, Healthy(triangles: 1000, height: 12f, groundOffset: -12f));

            Assert.That(Has(issues, "PIVOT_SUNK"), Is.False);
        }

        // ── LOD·머티리얼 ──

        [Test]
        public void 키가_크고_무거운_에셋에_LOD가_없으면_경고한다()
        {
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Environment, Healthy(triangles: 1000, height: 10f));

            Assert.That(Has(issues, "NO_LOD"), Is.True);
        }

        [Test]
        public void 작은_에셋은_LOD가_없어도_넘어간다()
        {
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Environment, Healthy(height: 0.6f));

            Assert.That(Has(issues, "NO_LOD"), Is.False);
        }

        [Test]
        public void 메시_LOD가_걸려_있으면_LODGroup이_없어도_통과한다()
        {
            // Unity 6은 LOD가 두 갈래다 — 배경 에셋 58종은 LODGroup 없이 Mesh LOD 3단을 쓴다.
            // LODGroup만 찾으면 이들이 전부 "LOD 없음"으로 잘못 잡힌다.
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Environment, Healthy(triangles: 1000, height: 12f, lodLevels: 3));

            Assert.That(Has(issues, "NO_LOD"), Is.False);
        }

        [Test]
        public void 가벼운_절차_메시는_커도_LOD를_묻지_않는다()
        {
            // 지역 팔레트의 블록아웃 메시는 15~200 tris라 단계를 나눌 것이 없다.
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Environment, Healthy(triangles: 124, height: 20f));

            Assert.That(Has(issues, "NO_LOD"), Is.False);
        }

        [Test]
        public void LOD_단계가_1이면_없는_것으로_본다()
        {
            Assert.That(Healthy(lodLevels: 1).HasLod, Is.False);
            Assert.That(Healthy(lodLevels: 3).HasLod, Is.True);
        }

        [Test]
        public void 빈_머티리얼_슬롯은_오류다()
        {
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Environment, Healthy(missingMaterials: 1));

            Assert.That(Has(issues, "MISSING_MATERIAL"), Is.True);
        }

        [Test]
        public void 렌더러가_없으면_다른_판정을_하지_않는다()
        {
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Environment, Healthy(triangles: 999999, renderers: 0));

            Assert.That(issues.Count, Is.EqualTo(1));
            Assert.That(issues[0].Code, Is.EqualTo("NO_RENDERER"));
        }

        // ── 심각도 ──

        [Test]
        public void 가장_높은_심각도를_고른다()
        {
            var issues = AssetLabDiagnostics.Inspect(
                AssetLabCategory.Environment, Healthy(triangles: 2000, groundOffset: -0.5f));

            Assert.That(AssetLabDiagnostics.WorstOf(issues), Is.EqualTo(AssetIssueSeverity.Error));
        }

        [Test]
        public void 정상_에셋은_지적이_없다()
        {
            var issues = AssetLabDiagnostics.Inspect(AssetLabCategory.Environment, Healthy());

            Assert.That(issues, Is.Empty);
        }

        // ── 카탈로그 규칙 ──

        [Test]
        public void 투사체와_이펙트는_목록에서_뺀다()
        {
            Assert.That(AssetLabCatalog.IsExcludedName("BossProjectile"), Is.True);
            Assert.That(AssetLabCatalog.IsExcludedName("MonsterDeathEffect"), Is.True);
            Assert.That(AssetLabCatalog.IsExcludedName("Env_Rock_S"), Is.False);
        }

        [Test]
        public void 검색은_대소문자를_가리지_않는다()
        {
            var entry = new AssetLabEntry("Env_Tree_Conifer_A", "p", AssetLabCategory.Environment, null);

            Assert.That(AssetLabCatalog.Matches(entry, "conifer"), Is.True);
            Assert.That(AssetLabCatalog.Matches(entry, "cactus"), Is.False);
            Assert.That(AssetLabCatalog.Matches(entry, string.Empty), Is.True);
        }

        [Test]
        public void 목록은_분류_먼저_이름_나중으로_정렬된다()
        {
            var terrain = new AssetLabEntry("Z", "p", AssetLabCategory.Terrain, null);
            var environment = new AssetLabEntry("A", "p", AssetLabCategory.Environment, null);

            Assert.That(AssetLabCatalog.CompareEntries(terrain, environment), Is.LessThan(0));
        }

        // ── 프레이밍 ──

        [Test]
        public void 큰_대상일수록_카메라가_멀어진다()
        {
            float far = AssetLabProbe.FramingDistance(new Vector3(10f, 12f, 10f), 60f);
            float near = AssetLabProbe.FramingDistance(new Vector3(0.6f, 0.3f, 0.3f), 60f);

            Assert.That(far, Is.GreaterThan(near));
        }

        [Test]
        public void 크기가_0이어도_거리가_0이_되지_않는다()
        {
            Assert.That(AssetLabProbe.FramingDistance(Vector3.zero, 60f), Is.GreaterThan(0f));
        }
    }
}
