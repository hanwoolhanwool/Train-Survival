using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonSupportArmSurfaceSetup
    {
        private const string Art=RaccoonEquipmentSetup.Art;
        [Serializable] private class Influence {public string name;public float weight;}
        [Serializable] private class Vertex {public float[] source,position,normal;public Influence[] influences;}
        [Serializable] private class Patch {public Vertex[] vertices;}
        private static Vector3 V(float[] p)=>new Vector3(p[0],p[1],p[2]);
        private static Vector3 Convert(Vector3 p)=>new Vector3(-p.x,p.z+.9504841566085815f,-p.y);
        public static void Build()
        {
            const string output="output/raccoon-support-v18";
            string prefabPath=Art+"/CH_Raccoon_Player.prefab";
            Directory.CreateDirectory(output);
            if(!File.Exists(output+"/CH_Raccoon.before.prefab"))File.Copy(prefabPath,output+"/CH_Raccoon.before.prefab");
            var patch=JsonUtility.FromJson<Patch>(File.ReadAllText(output+"/arm_patch.json"));
            var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
                var mesh=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/MESH_Raccoon_SupportWrist_v16.asset"));
                mesh.name="Raccoon_SupportArm_v18";
                var names=skin.bones.Select((b,i)=>new{b.name,i}).ToDictionary(x=>x.name,x=>x.i);
                var vertices=mesh.vertices;var normals=mesh.normals;var weights=mesh.boneWeights;var tangents=mesh.tangents;
                var changed=new bool[vertices.Length];int count=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 world=skin.transform.TransformPoint(vertices[i]);
                    var record=patch.vertices.FirstOrDefault(v=>(Convert(V(v.source))-world).sqrMagnitude<1e-8f);
                    if(record==null)continue;
                    vertices[i]=skin.transform.InverseTransformPoint(Convert(V(record.position)));
                    Vector3 n=V(record.normal);normals[i]=skin.transform.InverseTransformDirection(new Vector3(-n.x,n.z,-n.y)).normalized;
                    var t=Vector3.ProjectOnPlane(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z),normals[i]).normalized;
                    tangents[i]=new Vector4(t.x,t.y,t.z,tangents[i].w);
                    var influence=record.influences.OrderByDescending(x=>x.weight).ToArray();
                    if(influence.Length>4)throw new InvalidOperationException("Too many arm influences");
                    var w=new BoneWeight();
                    for(int j=0;j<influence.Length;j++)
                    {
                        int bone=names[influence[j].name];float value=influence[j].weight;
                        if(j==0){w.boneIndex0=bone;w.weight0=value;}else if(j==1){w.boneIndex1=bone;w.weight1=value;}
                        else if(j==2){w.boneIndex2=bone;w.weight2=value;}else{w.boneIndex3=bone;w.weight3=value;}
                    }
                    weights[i]=w;changed[i]=true;count++;
                }
                if(count<600)throw new InvalidOperationException("Incomplete arm patch: "+count);
                var shapes=Enumerable.Range(0,mesh.blendShapeCount).Select(i=>
                {
                    var p=new Vector3[vertices.Length];var n=new Vector3[vertices.Length];var t=new Vector3[vertices.Length];
                    mesh.GetBlendShapeFrameVertices(i,0,p,n,t);string name=mesh.GetBlendShapeName(i);
                    if(name.EndsWith("COR_Elbow90.L",StringComparison.Ordinal))
                        for(int j=0;j<changed.Length;j++)if(changed[j]){p[j]=Vector3.zero;n[j]=Vector3.zero;t[j]=Vector3.zero;}
                    return new{name,p,n,t,weight=mesh.GetBlendShapeFrameWeight(i,0)};
                }).ToArray();
                mesh.ClearBlendShapes();foreach(var s in shapes)mesh.AddBlendShapeFrame(s.name,s.weight,s.p,s.n,s.t);
                mesh.vertices=vertices;mesh.normals=normals;mesh.tangents=tangents;mesh.boneWeights=weights;mesh.RecalculateBounds();
                string path=Art+"/MESH_Raccoon_SupportArm_v18.asset";var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(asset==null){AssetDatabase.CreateAsset(mesh,path);asset=mesh;}else{EditorUtility.CopySerialized(mesh,asset);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(asset);}
                skin.sharedMesh=asset;
                string texturePath=Art+"/Textures/T_Raccoon_HandsTail_BaseColor_v18.png";
                AssetDatabase.ImportAsset(texturePath);
                var materials=skin.sharedMaterials;var old=materials.First(m=>m.HasProperty("_BaseMap")&&m.GetTexture("_BaseMap")!=null&&m.GetTexture("_BaseMap").name.Contains("HandsTail"));
                string materialPath=Art+"/M_Raccoon_HandsTail_v18.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(material==null){material=new Material(old);AssetDatabase.CreateAsset(material,materialPath);}
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath));EditorUtility.SetDirty(material);
                for(int i=0;i<materials.Length;i++)if(materials[i]==old)materials[i]=material;
                skin.sharedMaterials=materials;PrefabUtility.SaveAsPrefabAsset(root,prefabPath);AssetDatabase.SaveAssets();
                Debug.Log("SUPPORT_ARM_PATCH_VERTICES="+count);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            RaccoonLocomotionReview.CaptureV18();
        }
    }
}
