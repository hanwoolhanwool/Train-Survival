using Game.Gameplay.Region;
using NUnit.Framework;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 지평선 판 <b>에셋 배선</b>의 건전성.
    ///
    /// <para>원경 게이팅과 같은 이유로 있다 — 어긋나도 <b>예외도 로그도 남지 않고</b>,
    /// 그저 지형이 끊긴 자리가 다시 보이거나(판이 빠짐) 엉뚱한 색 띠가 지평선에 서 있게 된다
    /// (색이 지면과 다름). 둘 다 눈으로만 잡히는데, 눈은 매번 보지 않는다.</para>
    /// </summary>
    public sealed class HorizonPlaneAssetTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/Game_ArtTest.unity";
        private const string PlaneScriptPath =
            "Assets/_Project/Scripts/Gameplay/World/DistantHorizonPlane.cs";
        private const string HorizonMaterialPath = "Assets/_Project/Art/Materials/M_Env_Horizon.mat";

        private static RegionDefinition LoadRegion(string name)
        {
            string path = $"Assets/_Project/Data/Region_{name}.asset";
            var region = AssetDatabase.LoadAssetAtPath<RegionDefinition>(path);
            Assert.IsNotNull(region, $"지역 에셋이 없다: {path}");
            return region;
        }

        private static Color GroundColor(string materialName)
        {
            string path = $"Assets/_Project/Art/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Assert.IsNotNull(material, $"지면 머티리얼이 없다: {path}");
            return material.GetColor("_BaseColor");
        }

        /// <summary>
        /// 판이 필요한 지역은 <b>안개가 얇아 절단면이 보이는</b> 셋뿐이다.
        /// 숲·바다는 0.0062라 200 m 에서 78 %가 지워져 애초에 끝이 안 보인다 —
        /// 거기까지 켜면 보이지도 않는 판을 그리느라 드로우콜만 쓴다.
        /// </summary>
        [Test]
        public void 지평선을_소유하는_지역은_안개가_얇은_셋뿐이다()
        {
            Assert.IsTrue(LoadRegion("Desert").OverridesHorizon, "사막(0.0015)은 절단면이 보인다");
            Assert.IsTrue(LoadRegion("Grassland").OverridesHorizon, "대초원(0.0012)은 절단면이 보인다");
            Assert.IsTrue(LoadRegion("Arctic").OverridesHorizon, "북극(0.0017)은 절단면이 보인다");

            Assert.IsFalse(LoadRegion("Forest").OverridesHorizon, "숲(0.0062)은 안개가 절단면을 지운다");
            Assert.IsFalse(LoadRegion("Sea").OverridesHorizon, "바다(0.0062)는 안개가 절단면을 지운다");
        }

        /// <summary>
        /// 판 색이 그 지역 <b>지면색과 같아야</b> 타일 끝에서 이어져 보인다.
        /// 다르면 절단면을 가리는 대신 <b>지평선에 색 띠</b>를 하나 더 그리는 셈이 된다.
        /// </summary>
        [Test]
        public void 지평선_색은_그_지역_지면색과_같다()
        {
            AssertColorMatches(LoadRegion("Desert"), GroundColor("M_GroundDesert"));
            AssertColorMatches(LoadRegion("Grassland"), GroundColor("M_GroundGrassland"));
            AssertColorMatches(LoadRegion("Arctic"), GroundColor("M_GroundArctic"));
        }

        private static void AssertColorMatches(RegionDefinition region, Color ground)
        {
            Color horizon = region.HorizonColor;
            Assert.AreEqual(ground.r, horizon.r, 0.01f, $"{region.DisplayName} R");
            Assert.AreEqual(ground.g, horizon.g, 0.01f, $"{region.DisplayName} G");
            Assert.AreEqual(ground.b, horizon.b, 0.01f, $"{region.DisplayName} B");
        }

        [Test]
        public void 지평선_머티리얼은_그림자를_받지_않는다()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(HorizonMaterialPath);
            Assert.IsNotNull(material, $"지평선 머티리얼이 없다: {HorizonMaterialPath}");

            // 3 km 판이 그림자를 받으면 캐스케이드를 통째로 먹는다 (예산 §7 — 캐스케이드가 4위 비용).
            Assert.AreEqual(0f, material.GetFloat("_ReceiveShadows"), 1e-4f);
        }

        /// <summary>
        /// 씬에 판이 <b>배선돼 있는가</b>. 씬을 열지 않고 텍스트로 확인한다 — 이 프로젝트의
        /// EditMode 테스트는 활성 씬을 바꾸지 않는다는 규약을 지키기 위해서다.
        /// 스크립트·머티리얼 GUID 는 에셋 쪽에서 받아오므로, 참조가 어긋나면 여기서 걸린다.
        /// </summary>
        [Test]
        public void 인게임_씬에_지평선판이_배선돼_있다()
        {
            string scriptGuid = AssetDatabase.AssetPathToGUID(PlaneScriptPath);
            string materialGuid = AssetDatabase.AssetPathToGUID(HorizonMaterialPath);

            Assert.IsNotEmpty(scriptGuid, $"스크립트를 찾을 수 없다: {PlaneScriptPath}");
            Assert.IsNotEmpty(materialGuid, $"머티리얼을 찾을 수 없다: {HorizonMaterialPath}");

            string scene = File.ReadAllText(ScenePath);

            Assert.IsTrue(scene.Contains(scriptGuid), "씬에 DistantHorizonPlane 이 붙어 있지 않다");
            Assert.IsTrue(scene.Contains(materialGuid), "지평선판에 머티리얼이 물려 있지 않다");
        }
    }
}
