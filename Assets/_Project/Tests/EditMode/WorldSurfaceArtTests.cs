using Game.Gameplay.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Game.Tests.EditMode
{
    public sealed class WorldSurfaceArtTests
    {
        [TestCase("Assets/_Project/Art/M_GroundArctic.mat", "T_Env_Ground_Arctic_BaseColor", 6, 4)]
        [TestCase("Assets/_Project/Art/Materials/M_Env_Ice_Arctic.mat", "T_Env_Ground_Arctic_BaseColor", 5.2f, 4)]
        [TestCase("Assets/_Project/Art/Materials/M_Env_Seabed_A.mat", "T_Env_Seabed_BaseColor", 20, 4)]
        public void Ground_materials_use_opaque_repeating_base_colour(string path, string name, float x, float y)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Texture texture = material.GetTexture("_BaseMap");
            Assert.That(texture.name, Is.EqualTo(name));
            Assert.That(texture.width, Is.EqualTo(1024));
            Assert.That(texture.height, Is.EqualTo(1024));
            Assert.That(material.GetTexture("_MainTex"), Is.SameAs(texture));
            Assert.That(material.GetTextureScale("_BaseMap"), Is.EqualTo(new Vector2(x, y)));
            Assert.That(material.GetColor("_BaseColor"), Is.EqualTo(Color.white));
            var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(texture));
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Repeat));
            Assert.That(importer.mipmapEnabled, Is.True);
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.alphaIsTransparency, Is.False);
        }

        [Test]
        public void Every_current_arctic_and_sea_tile_has_density_control()
        {
            foreach (string region in new[] { "Arctic", "Sea" })
            for (char variant = 'A'; variant <= 'J'; variant++)
            {
                string path = $"Assets/_Project/Prefabs/TerrainTile_{region}_{variant}.prefab";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var component = prefab.GetComponent<SurfaceTextureTiling>();
                Assert.That(component, Is.Not.Null, path);
                var data = new SerializedObject(component);
                var material = (Material)data.FindProperty("_surfaceMaterial").objectReferenceValue;
                Assert.That(material.name, Is.EqualTo(region == "Arctic" ? "M_Env_Ice_Arctic" : "M_Env_Seabed_A"), path);
                Assert.That(data.FindProperty("_metresPerRepeat").floatValue, Is.EqualTo(10));
            }
        }

        [Test]
        public void Different_slab_sizes_share_texture_phase_and_scrolling_preserves_it()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_Env_Ice_Arctic.mat");
            Vector2 originalScale = material.mainTextureScale;
            var root = new GameObject("Surface tiling test");
            try
            {
                var left = GameObject.CreatePrimitive(PrimitiveType.Cube);
                var right = GameObject.CreatePrimitive(PrimitiveType.Cube);
                left.transform.SetParent(root.transform); right.transform.SetParent(root.transform);
                left.transform.localScale = new Vector3(16, 1, 40);
                right.transform.localScale = new Vector3(52, 1, 40);
                right.transform.localPosition = new Vector3(34, 0, 0); // Shared edge at x=8.
                Renderer a = left.GetComponent<Renderer>(), b = right.GetComponent<Renderer>();
                a.sharedMaterial = material; b.sharedMaterial = material;
                var properties = new MaterialPropertyBlock();
                properties.SetColor("_BaseColor", Color.red); a.SetPropertyBlock(properties);
                var tiling = root.AddComponent<SurfaceTextureTiling>();
                tiling.Configure(material, 10);
                a.GetPropertyBlock(properties); Vector4 first = properties.GetVector("_BaseMap_ST");
                Assert.That(properties.GetColor("_BaseColor"), Is.EqualTo(Color.red));
                b.GetPropertyBlock(properties); Vector4 second = properties.GetVector("_BaseMap_ST");
                Assert.That(first.x, Is.EqualTo(1.6f).Within(0.001));
                Assert.That(second.x, Is.EqualTo(5.2f).Within(0.001));
                Assert.That(first.z, Is.EqualTo(second.x + second.z).Within(0.001), "Cube top U=0 at the left slab's right edge; U=1 at the right slab's left edge.");
                root.transform.position = new Vector3(300, 0, -1240);
                tiling.Apply(); a.GetPropertyBlock(properties);
                Assert.That(properties.GetVector("_BaseMap_ST"), Is.EqualTo(first));
                Assert.That(material.mainTextureScale, Is.EqualTo(originalScale));
            }
            finally { Object.DestroyImmediate(root); }
        }
    }
}
