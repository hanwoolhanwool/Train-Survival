using System.Collections.Generic;
using System.IO;
using Game.Gameplay.World;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class WorldUiArtIntegration
    {
        private const string Art = "Assets/_Project/Art/";

        public static void ExportSurfacePreviews()
        {
            string directory = Path.GetFullPath("output/imagegen/world-structures-v1/reviews");
            Directory.CreateDirectory(directory);
            foreach (string region in new[] { "Arctic", "Sea" })
            {
                var preview = new PreviewRenderUtility();
                try
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/TerrainTile_" + region + "_A.prefab");
                    GameObject root = Object.Instantiate(prefab);
                    preview.AddSingleGO(root);
                    foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                    root.GetComponent<SurfaceTextureTiling>().Apply();
                    Bounds bounds = default;
                    bool found = false;
                    foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>())
                    {
                        string wanted = region == "Arctic" ? "M_Env_Ice_Arctic" : "M_Env_Seabed_A";
                        renderer.enabled = renderer.sharedMaterial != null && renderer.sharedMaterial.name == wanted;
                        if (!renderer.enabled) continue;
                        if (!found) { bounds = renderer.bounds; found = true; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                    preview.camera.orthographic = true;
                    preview.camera.orthographicSize = bounds.size.x / (1280f / 480f) * 0.53f;
                    preview.camera.nearClipPlane = 0.01f;
                    preview.camera.farClipPlane = 500;
                    preview.camera.transform.SetPositionAndRotation(bounds.center + Vector3.up * 150, Quaternion.Euler(90, 0, 0));
                    preview.camera.clearFlags = CameraClearFlags.SolidColor;
                    preview.camera.backgroundColor = new Color(0.12f, 0.15f, 0.17f);
                    preview.ambientColor = new Color(0.65f, 0.65f, 0.65f);
                    preview.lights[0].intensity = 0.8f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(90, 0, 0);
                    preview.lights[1].intensity = 0;
                    preview.BeginStaticPreview(new Rect(0, 0, 1280, 480));
                    preview.Render(true);
                    Texture2D image = preview.EndStaticPreview();
                    File.WriteAllBytes(Path.Combine(directory, "Unity-Surface-" + region + ".png"), image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
                finally { preview.Cleanup(); }
            }
        }

        public static string Apply()
        {
            Texture arctic = AssetDatabase.LoadAssetAtPath<Texture>(Art + "Textures/T_Env_Ground_Arctic_BaseColor.png");
            Texture seabed = AssetDatabase.LoadAssetAtPath<Texture>(Art + "Textures/T_Env_Seabed_BaseColor.png");
            if (arctic == null || seabed == null) throw new System.InvalidOperationException("Import the final textures first.");
            SetMaterial(Art + "M_GroundArctic.mat", arctic, new Vector2(6, 4));
            Material ice = SetMaterial(Art + "Materials/M_Env_Ice_Arctic.mat", arctic, new Vector2(5.2f, 4));
            Material sea = SetMaterial(Art + "Materials/M_Env_Seabed_A.mat", seabed, new Vector2(20, 4));
            var updated = new List<string>();
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project/Prefabs" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                bool isIce = path.Contains("/TerrainTile_Arctic_");
                bool isSea = path.Contains("/TerrainTile_Sea_");
                if (!isIce && !isSea) continue;
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    SurfaceTextureTiling tiling = root.GetComponent<SurfaceTextureTiling>();
                    if (tiling == null) tiling = root.AddComponent<SurfaceTextureTiling>();
                    tiling.Configure(isIce ? ice : sea, 10);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    updated.Add(path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            return "Connected 3 materials and " + updated.Count + " terrain prefabs (10 m repeats).";
        }

        private static Material SetMaterial(string path, Texture texture, Vector2 scale)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            foreach (string property in new[] { "_BaseMap", "_MainTex" })
            {
                material.SetTexture(property, texture);
                material.SetTextureScale(property, scale);
                material.SetTextureOffset(property, Vector2.zero);
            }
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
