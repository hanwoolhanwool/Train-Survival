using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonTailRootSetup
    {
        private const string Art = RaccoonEquipmentSetup.Art;
        public const string MeshPath = Art + "/MESH_Raccoon_Tail_v11.asset";
        public const string MaterialPath = Art + "/M_Raccoon_Tail_v11.mat";
        [Serializable] private class Influence { public string name; public float weight; }
        [Serializable] private class Vertex { public Vector3 position, normal; public Vector2 uv; public Influence[] influences; }
        [Serializable] private class Opening { public Vertex[] vertices; public int[] triangles; }
        [Serializable] private class Geometry { public Vector3[] removePositions; public Vertex[] vertices; public int[] triangles; public Opening opening; }
        private class Shape { public string name; public float weight; public Vector3[] positions, normals, tangents; }

        public static void Build()
        {
            const string output = "output/raccoon-tail-v11";
            string path = Art + "/CH_Raccoon_Player.prefab";
            if (!File.Exists(output + "/CH_Raccoon_Player.before.prefab")) File.Copy(path, output + "/CH_Raccoon_Player.before.prefab");
            var data = JsonUtility.FromJson<Geometry>(File.ReadAllText(output + "/tail_geometry.json"));
            int tailVertexCount = data.vertices.Length;
            data.vertices = data.vertices.Concat(data.opening.vertices).ToArray();
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var skin = root.GetComponentInChildren<SkinnedMeshRenderer>();
                // Always rebuild from the archived pre-rebuild mesh, making this operation repeatable.
                var source = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/MESH_Raccoon_Tail_v9.asset");
                if (source == null) throw new InvalidOperationException("Missing v9 source mesh.");
                int oldCount = source.vertexCount, total = oldCount + data.vertices.Length;
                var positions = new Vector3[total]; var normals = new Vector3[total]; var uv = new Vector2[total];
                var weights = new BoneWeight[total]; var tangents = new Vector4[total];
                Array.Copy(source.vertices, positions, oldCount); Array.Copy(source.normals, normals, oldCount);
                Array.Copy(source.uv, uv, oldCount); Array.Copy(source.boneWeights, weights, oldCount);
                if (source.tangents.Length == oldCount) Array.Copy(source.tangents, tangents, oldCount);
                var removed = new bool[oldCount];
                for (int i = 0; i < oldCount; i++)
                {
                    Vector3 world = skin.transform.TransformPoint(positions[i]);
                    Vector3 native = new Vector3(-world.x, -world.z, world.y - .9504841566085815f);
                    removed[i] = data.removePositions.Any(p => (p - native).sqrMagnitude < 1e-8f);
                }
                var bones = skin.bones.Select((b, i) => new { b.name, i }).ToDictionary(b => b.name, b => b.i);
                for (int i = 0; i < data.vertices.Length; i++)
                {
                    var v = data.vertices[i]; int index = oldCount + i;
                    positions[index] = skin.transform.InverseTransformPoint(new Vector3(-v.position.x, v.position.z + .9504841566085815f, -v.position.y));
                    normals[index] = skin.transform.InverseTransformDirection(new Vector3(-v.normal.x, v.normal.z, -v.normal.y)).normalized;
                    uv[index] = v.uv;
                    if (v.influences.Length > 4 || Mathf.Abs(v.influences.Sum(w => w.weight) - 1) > 1e-5f) throw new InvalidOperationException("Invalid rebuilt tail weights.");
                    var w = new BoneWeight { boneIndex0 = bones[v.influences[0].name], weight0 = v.influences[0].weight };
                    if (v.influences.Length >= 2) { w.boneIndex1 = bones[v.influences[1].name]; w.weight1 = v.influences[1].weight; }
                    if (v.influences.Length >= 3) { w.boneIndex2 = bones[v.influences[2].name]; w.weight2 = v.influences[2].weight; }
                    if (v.influences.Length >= 4) { w.boneIndex3 = bones[v.influences[3].name]; w.weight3 = v.influences[3].weight; }
                    weights[index] = w;
                }
                var shapes = new List<Shape>();
                for (int s = 0; s < source.blendShapeCount; s++)
                    for (int f = 0; f < source.GetBlendShapeFrameCount(s); f++)
                    {
                        var p = new Vector3[oldCount]; var n = new Vector3[oldCount]; var t = new Vector3[oldCount];
                        source.GetBlendShapeFrameVertices(s, f, p, n, t);
                        Array.Resize(ref p, total); Array.Resize(ref n, total); Array.Resize(ref t, total);
                        shapes.Add(new Shape { name = source.GetBlendShapeName(s), weight = source.GetBlendShapeFrameWeight(s, f), positions = p, normals = n, tangents = t });
                    }
                var mesh = new Mesh { name = "Raccoon_RoundTail_v11", indexFormat = source.indexFormat };
                mesh.vertices = positions; mesh.normals = normals; mesh.uv = uv; mesh.boneWeights = weights; mesh.bindposes = source.bindposes;
                mesh.subMeshCount = source.subMeshCount + 1; int removedTriangles = 0;
                for (int s = 0; s < source.subMeshCount; s++)
                {
                    var old = source.GetTriangles(s); var kept = new List<int>();
                    for (int i = 0; i < old.Length; i += 3)
                    {
                        if (removed[old[i]] || removed[old[i + 1]] || removed[old[i + 2]]) { removedTriangles++; continue; }
                        kept.Add(old[i]); kept.Add(old[i + 1]); kept.Add(old[i + 2]);
                    }
                    if (s == 0)
                    {
                        int offset = oldCount + tailVertexCount;
                        for (int i = 0; i < data.opening.triangles.Length; i += 3)
                        { kept.Add(offset + data.opening.triangles[i]); kept.Add(offset + data.opening.triangles[i + 2]); kept.Add(offset + data.opening.triangles[i + 1]); }
                    }
                    mesh.SetTriangles(kept, s);
                }
                if (removedTriangles != 290) throw new InvalidOperationException("Unexpected tail removal count: " + removedTriangles);
                var triangles = new int[data.triangles.Length];
                for (int i = 0; i < triangles.Length; i += 3)
                { triangles[i] = oldCount + data.triangles[i]; triangles[i + 1] = oldCount + data.triangles[i + 2]; triangles[i + 2] = oldCount + data.triangles[i + 1]; }
                mesh.SetTriangles(triangles, source.subMeshCount);
                foreach (var shape in shapes) mesh.AddBlendShapeFrame(shape.name, shape.weight, shape.positions, shape.normals, shape.tangents);
                mesh.RecalculateTangents(); var generatedTangents = mesh.tangents;
                for (int i = oldCount; i < total; i++) tangents[i] = generatedTangents[i];
                mesh.tangents = tangents; mesh.RecalculateBounds();
                var asset = AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if (asset == null) { AssetDatabase.CreateAsset(mesh, MeshPath); asset = mesh; }
                else { EditorUtility.CopySerialized(mesh, asset); Object.DestroyImmediate(mesh); EditorUtility.SetDirty(asset); }
                string texturePath = Art + "/Textures/T_Raccoon_Tail_v11.png";
                File.Copy(output + "/T_Raccoon_Tail_v11.png", texturePath, true);
                AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
                var importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
                importer.sRGBTexture = true; importer.wrapModeU = TextureWrapMode.Repeat; importer.wrapModeV = TextureWrapMode.Clamp; importer.SaveAndReimport();
                var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
                if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, MaterialPath); }
                material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));
                material.SetColor("_BaseColor", Color.white); material.SetFloat("_Smoothness", .15f); material.SetFloat("_Metallic", 0); EditorUtility.SetDirty(material);
                skin.sharedMesh = asset; skin.sharedMaterials = skin.sharedMaterials.Take(source.subMeshCount).Concat(new[] { material }).ToArray();
                PrefabUtility.SaveAsPrefabAsset(root, path);
                File.WriteAllText(output + "/unity_build_report.json", JsonUtility.ToJson(new Report { sourceVertices = oldCount, tailVertices = data.vertices.Length, removedTriangles = removedTriangles, tailTriangles = triangles.Length / 3, blendShapes = asset.blendShapeCount }, true));
                Debug.Log("RACCOON_TAIL_V11_REBUILT");
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets(); RaccoonRunSetup.CaptureNarrowTailComparison();
        }
        [Serializable] private class Report { public int sourceVertices, tailVertices, removedTriangles, tailTriangles, blendShapes; }
    }
}

