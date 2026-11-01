using System;
using System.IO;
using System.Linq;
using Game.Gameplay.Player;
using Game.Gameplay.Inventory;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonWristSetup
    {
        public const string Output="output/raccoon-support-v16";
        private const string Art=RaccoonEquipmentSetup.Art;
        [Serializable] private class Influence { public string name;public float weight; }
        [Serializable] private class Vertex { public float[] source,position,normal;public Influence[] influences; }
        [Serializable] private class Patch { public Vertex[] vertices;public Vector3 pivot;public Quaternion handDelta; }
        private static Vector3 V(float[] p)=>new Vector3(p[0],p[1],p[2]);
        private static Vector3 ToUnity(Vector3 p)=>new Vector3(-p.x,p.z+.9504841566085815f,-p.y);
        public static void Build()
        {
            Directory.CreateDirectory(Output);
            string prefabPath=Art+"/CH_Raccoon_Player.prefab";
            if(!File.Exists(Output+"/CH_Raccoon.before.prefab"))File.Copy(prefabPath,Output+"/CH_Raccoon.before.prefab");
            string profilePath=Art+"/RaccoonEquipmentProfile.asset";
            if(!File.Exists(Output+"/profile.before.asset"))File.Copy(profilePath,Output+"/profile.before.asset");
            var patch=JsonUtility.FromJson<Patch>(File.ReadAllText(Output+"/wrist_patch.json"));
            var root=PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();
                // Always derive from the approved v13 geometry, so rebuilding is idempotent.
                var mesh=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/MESH_Raccoon_Tail_v13.asset"));mesh.name="Raccoon_SupportWrist_v16";
                var bones=skin.bones.Where(b=>!b.name.StartsWith("LeftWristBlend")).ToList();var bind=mesh.bindposes.ToList();
                for(int i=1;i<=3;i++)
                {
                    string name="LeftWristBlend"+i;var bone=root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
                    if(bone==null){bone=new GameObject(name).transform;bone.SetParent(root.transform,false);}
                    bone.localPosition=patch.pivot;bone.localRotation=Quaternion.identity;bone.localScale=Vector3.one;
                    bones.Add(bone);bind.Add(bone.worldToLocalMatrix*skin.transform.localToWorldMatrix);
                }
                var names=bones.Select((b,i)=>new{b.name,i}).ToDictionary(b=>b.name,b=>b.i);
                var vertices=mesh.vertices;var normals=mesh.normals;var weights=mesh.boneWeights;var tangents=mesh.tangents;int changed=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 world=skin.transform.TransformPoint(vertices[i]);
                    var record=patch.vertices.FirstOrDefault(v=>(ToUnity(V(v.source))-world).sqrMagnitude<1e-8f);
                    if(record==null)continue;
                    vertices[i]=skin.transform.InverseTransformPoint(ToUnity(V(record.position)));
                    var n=V(record.normal);normals[i]=skin.transform.InverseTransformDirection(new Vector3(-n.x,n.z,-n.y)).normalized;
                    Vector3 tangent=Vector3.ProjectOnPlane(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z),normals[i]).normalized;tangents[i]=new Vector4(tangent.x,tangent.y,tangent.z,tangents[i].w);
                    var influences=record.influences.OrderByDescending(v=>v.weight).ToArray();if(influences.Length>4)throw new InvalidOperationException("Wrist influence limit exceeded");
                    var w=new BoneWeight();for(int j=0;j<influences.Length;j++){int bone=names[influences[j].name];float value=influences[j].weight;if(j==0){w.boneIndex0=bone;w.weight0=value;}else if(j==1){w.boneIndex1=bone;w.weight1=value;}else if(j==2){w.boneIndex2=bone;w.weight2=value;}else{w.boneIndex3=bone;w.weight3=value;}}
                    weights[i]=w;changed++;
                }
                if(changed<250)throw new InvalidOperationException("Incomplete wrist mapping: "+changed);
                mesh.vertices=vertices;mesh.normals=normals;mesh.tangents=tangents;mesh.boneWeights=weights;mesh.bindposes=bind.ToArray();mesh.RecalculateBounds();
                string path=Art+"/MESH_Raccoon_SupportWrist_v16.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing==null){AssetDatabase.CreateAsset(mesh,path);existing=mesh;}else{EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);}
                skin.sharedMesh=existing;skin.bones=bones.ToArray();
                if(root.GetComponent<RaccoonWristDeformation>()==null)root.AddComponent<RaccoonWristDeformation>();
                PrefabUtility.SaveAsPrefabAsset(root,prefabPath);Debug.Log("WRIST_PATCH_VERTICES="+changed);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var profile=AssetDatabase.LoadAssetAtPath<RaccoonEquipmentProfile>(profilePath);
            // Save the original quaternion independently of YAML serialization for repeatable rebuilds.
            profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
            string rotationPath=Output+"/original_rotation.json";
            if(!File.Exists(rotationPath))File.WriteAllText(rotationPath,JsonUtility.ToJson(entry.Left.Rotation));
            entry.Left.Rotation=patch.handDelta*JsonUtility.FromJson<Quaternion>(File.ReadAllText(rotationPath));
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            RaccoonLocomotionReview.ReviewV16();
        }
    }
}
