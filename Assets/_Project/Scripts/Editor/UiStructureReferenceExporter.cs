using System;
using System.Collections.Generic;
using System.IO;
using Game.Gameplay.Train;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>Exports the actual placeable prefab geometry/materials for icon art reference.</summary>
    public static class UiStructureReferenceExporter
    {
        [Serializable] private sealed class Reference
        {
            public string key, label, prefab, image;
            public Vector3 bounds;
        }
        [Serializable] private sealed class Manifest { public List<Reference> references = new List<Reference>(); }

        public static void Export()
        {
            string directory = Path.GetFullPath("docs/design/references/world-structures");
            Directory.CreateDirectory(directory);
            var catalog = AssetDatabase.LoadAssetAtPath<StructureCatalog>("Assets/_Project/Data/StructureCatalog.asset");
            var manifest = new Manifest();
            foreach (StructureKind kind in Enum.GetValues(typeof(StructureKind)))
            {
                if (!catalog.IsPlaceable(kind)) continue;
                GameObject prefab = catalog.GetViewPrefab(kind);
                if (prefab == null) throw new InvalidOperationException("Missing prefab: " + kind);
                var preview = new PreviewRenderUtility();
                Material clothPreview = null;
                try
                {
                    var instance = UnityEngine.Object.Instantiate(prefab);
                    // Static preview does not draw the custom URP cloth pass. Use its saved
                    // base colour on this preview-only material; never alter the source prefab.
                    if (kind == StructureKind.Tent)
                    {
                        instance.GetComponent<TentView>().ApplyFootprint(4, 4, 1f);
                        Renderer canopy = instance.transform.Find("Canopy").GetComponent<Renderer>();
                        clothPreview = new Material(instance.transform.Find("Post0").GetComponent<Renderer>().sharedMaterial);
                        clothPreview.SetColor("_BaseColor", canopy.sharedMaterial.GetColor("_BaseColor"));
                        clothPreview.SetColor("_Color", canopy.sharedMaterial.GetColor("_BaseColor"));
                        clothPreview.SetFloat("_Smoothness", 0);
                        clothPreview.SetInt("_Cull", 0);
                        canopy.sharedMaterial = clothPreview;
                    }
                    foreach (MonoBehaviour behaviour in instance.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                    preview.AddSingleGO(instance);
                    Bounds bounds = default;
                    bool found = false;
                    foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
                    {
                        if (!found) { bounds = renderer.bounds; found = true; }
                        else bounds.Encapsulate(renderer.bounds);
                    }
                    if (!found) throw new InvalidOperationException("No renderer: " + kind);
                    Camera camera = preview.camera;
                    camera.orthographic = true;
                    camera.orthographicSize = Mathf.Max(0.1f, bounds.extents.magnitude * 0.88f);
                    camera.nearClipPlane = 0.01f;
                    camera.farClipPlane = 1000;
                    camera.transform.rotation = Quaternion.Euler(25, 150, 0);
                    camera.transform.position = bounds.center - camera.transform.forward * (bounds.size.magnitude + 10);
                    camera.clearFlags = CameraClearFlags.SolidColor;
                    camera.backgroundColor = new Color(0.17f, 0.19f, 0.21f, 1);
                    preview.lights[0].intensity = 1.3f;
                    preview.lights[0].transform.rotation = Quaternion.Euler(45, 35, 0);
                    preview.lights[1].intensity = 0.7f;
                    preview.lights[1].transform.rotation = Quaternion.Euler(340, 210, 0);
                    preview.ambientColor = new Color(0.5f, 0.5f, 0.5f);
                    preview.BeginStaticPreview(new Rect(0, 0, 640, 640));
                    preview.Render(true);
                    Texture2D image = preview.EndStaticPreview();
                    string path = Path.Combine(directory, "Ref_" + kind + ".png");
                    File.WriteAllBytes(path, image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                    manifest.references.Add(new Reference { key = kind.ToString(), label = catalog.GetDisplayName(kind),
                        prefab = AssetDatabase.GetAssetPath(prefab), image = path, bounds = bounds.size });
                    Debug.Log("Reference exported: " + kind + " " + bounds.size);
                }
                finally
                {
                    preview.Cleanup();
                    if (clothPreview != null) UnityEngine.Object.DestroyImmediate(clothPreview);
                }
            }
            File.WriteAllText(Path.Combine(directory, "references.json"), JsonUtility.ToJson(manifest, true));
            Debug.Log("Structure reference export complete: " + manifest.references.Count);
        }
    }
}
