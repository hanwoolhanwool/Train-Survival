using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonTailRoundSetup
    {
        private const string Art=RaccoonEquipmentSetup.Art;
        public const string MeshPath=Art+"/MESH_Raccoon_Tail_v13.asset";
        [Serializable] private class Vertex { public Vector3 position,normal; }
        [Serializable] private class Geometry { public Vertex[] vertices; }

        public static void Build()
        {
            const string output="output/raccoon-tail-v13";
            string prefab=Art+"/CH_Raccoon_Player.prefab";
            if(!File.Exists(output+"/CH_Raccoon_Player.before.prefab"))File.Copy(prefab,output+"/CH_Raccoon_Player.before.prefab");
            var data=JsonUtility.FromJson<Geometry>(File.ReadAllText(output+"/tail_geometry.json"));
            var root=PrefabUtility.LoadPrefabContents(prefab);
            try
            {
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
                var source=AssetDatabase.LoadAssetAtPath<Mesh>(RaccoonTailSurroundSetup.MeshPath);
                var mesh=Object.Instantiate(source);mesh.name="Raccoon_RoundedTail_v13";
                // The dedicated tail submesh uses contiguous per-corner vertices from the authoring export.
                var tailIndices=source.GetTriangles(source.subMeshCount-2).Distinct().OrderBy(i=>i).ToArray();
                int start=tailIndices[0];
                if(tailIndices.Length!=data.vertices.Length || tailIndices.Last()!=start+data.vertices.Length-1)
                    throw new InvalidOperationException("Tail topology differs from the archived v12 mesh.");
                var positions=mesh.vertices;var normals=mesh.normals;var originalTangents=mesh.tangents;float maximumChange=0;
                for(int i=0;i<data.vertices.Length;i++)
                {
                    var v=data.vertices[i];int index=start+i;
                    var p=skin.transform.InverseTransformPoint(new Vector3(-v.position.x,v.position.z+.9504841566085815f,-v.position.y));
                    maximumChange=Mathf.Max(maximumChange,(positions[index]-p).magnitude);positions[index]=p;
                    normals[index]=skin.transform.InverseTransformDirection(new Vector3(-v.normal.x,v.normal.z,-v.normal.y)).normalized;
                }
                mesh.vertices=positions;mesh.normals=normals;mesh.RecalculateTangents();var tangents=mesh.tangents;
                for(int i=0;i<tangents.Length;i++)if(i<start || i>=start+data.vertices.Length)tangents[i]=originalTangents[i];
                mesh.tangents=tangents;mesh.RecalculateBounds();
                var asset=AssetDatabase.LoadAssetAtPath<Mesh>(MeshPath);
                if(asset==null){AssetDatabase.CreateAsset(mesh,MeshPath);asset=mesh;}
                else{EditorUtility.CopySerialized(mesh,asset);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(asset);}
                skin.sharedMesh=asset;PrefabUtility.SaveAsPrefabAsset(root,prefab);
                File.WriteAllText(output+"/unity_build_report.json",JsonUtility.ToJson(new Report{changedTailVertices=data.vertices.Length,maximumLocalPositionChange=maximumChange,blendShapes=asset.blendShapeCount},true));
                Debug.Log("RACCOON_TAIL_V13_ROUNDED");
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            AssetDatabase.SaveAssets();RaccoonRunSetup.CaptureRoundedTailComparison();
        }
        [Serializable] private class Report { public int changedTailVertices,blendShapes;public float maximumLocalPositionChange; }
    }
}
