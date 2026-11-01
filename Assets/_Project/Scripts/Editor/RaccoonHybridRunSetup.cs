using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonHybridRunSetup
    {
        private const string Art=RaccoonEquipmentSetup.Art;
        public const string ClipPath=Art+"/Animations/Raccoon_ShotgunRun_HYBRID_v8.anim";
        [Serializable] private class Influence{public string name;public float weight;}
        [Serializable] private class Vertex{public Vector3 position,normal;public bool hasNormal;public Influence[] influences;}
        [Serializable] private class Weights{public Vertex[] vertices;}

        public static void Build()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            string fbx=Art+"/Animations/Raccoon_Shotgun_Run_Hybrid_v8.fbx";
            var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
            importer.animationType=ModelImporterAnimationType.Human;importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar=AssetDatabase.LoadAllAssetsAtPath(Art+"/CH_Raccoon_TP.fbx").OfType<Avatar>().First();
            importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;importer.optimizeGameObjects=false;importer.SaveAndReimport();
            var definition=importer.defaultClipAnimations[0];definition.name="Raccoon_Shotgun_Run_HYBRID_SOURCE_v8";
            definition.loopTime=true;definition.loopPose=false;definition.lockRootRotation=true;definition.keepOriginalOrientation=true;
            definition.lockRootPositionXZ=true;definition.keepOriginalPositionXZ=true;definition.lockRootHeightY=true;definition.keepOriginalPositionY=true;
            importer.clipAnimations=new[]{definition};importer.SaveAndReimport();
            var imported=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            if(!imported.humanMotion)throw new InvalidOperationException("Hybrid animation must be Humanoid.");
            var clip=Object.Instantiate(imported);clip.name="Raccoon_ShotgunRun_HYBRID_v8";
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if(binding.path.Split('/').Any(p=>p.StartsWith("Tail"))){AnimationUtility.SetEditorCurve(clip,binding,null);continue;}
                if(binding.propertyName=="RootT.x"||binding.propertyName=="RootT.z")
                {
                    var curve=AnimationUtility.GetEditorCurve(clip,binding);float start=curve.Evaluate(0);var keys=curve.keys;
                    for(int i=0;i<keys.Length;i++)keys[i].value-=start;curve.keys=keys;AnimationUtility.SetEditorCurve(clip,binding,curve);
                }
            }
            var asset=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if(asset==null){AssetDatabase.CreateAsset(clip,ClipPath);asset=clip;}
            else{EditorUtility.CopySerialized(clip,asset);Object.DestroyImmediate(clip);EditorUtility.SetDirty(asset);}
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Art+"/AC_Raccoon.overrideController");
            controller["Raccoon_ShotgunRun_AUTHORED_v7"]=asset;EditorUtility.SetDirty(controller);
            AlignRoot(asset);
            FixTailSkin();AssetDatabase.SaveAssets();RaccoonRunSetup.CaptureHybridComparison();
            Debug.Log("RACCOON_HYBRID_V8_COMPLETE");
        }

        private static void AlignRoot(AnimationClip clip)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/CH_Raccoon_Player.prefab"));
            try
            {
                var animator=go.GetComponent<Animator>();animator.Rebind();animator.Update(0);animator.SetFloat("Speed",7);animator.SetLayerWeight(animator.GetLayerIndex("Hold"),0);
                animator.SetFloat("RaccoonShotgunRun",0);animator.Play("Locomotion",0,0);animator.Update(0);Vector3 legacy=animator.GetBoneTransform(HumanBodyBones.Hips).position;
                animator.SetFloat("RaccoonShotgunRun",1);animator.Play("Locomotion",0,0);animator.Update(0);Vector3 current=animator.GetBoneTransform(HumanBodyBones.Hips).position;
                Vector3 correction=(legacy-current)/animator.humanScale;
                foreach(var binding in AnimationUtility.GetCurveBindings(clip).Where(b=>b.propertyName=="RootT.x"||b.propertyName=="RootT.z"))
                {
                    float delta=binding.propertyName=="RootT.x"?correction.x:correction.z;
                    var curve=AnimationUtility.GetEditorCurve(clip,binding);var keys=curve.keys;for(int i=0;i<keys.Length;i++)keys[i].value+=delta;curve.keys=keys;AnimationUtility.SetEditorCurve(clip,binding,curve);
                }
                EditorUtility.SetDirty(clip);Debug.Log("HYBRID_ROOT_ALIGNMENT="+correction);
            }
            finally{Object.DestroyImmediate(go);}
        }

        private static void FixTailSkin()
        {ApplyTailSkin("output/raccoon-run-v8/unity_tail_weights.json",Art+"/MESH_Raccoon_Tail_v8.asset","Raccoon_TailSkin_v8");}

        internal static void ApplyTailSkin(string weightsPath,string meshPath,string meshName)
        {
            var data=JsonUtility.FromJson<Weights>(File.ReadAllText(weightsPath));
            string path=Art+"/CH_Raccoon_Player.prefab";var root=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=Object.Instantiate(skin.sharedMesh);mesh.name=meshName;
                var names=skin.bones.Select((b,i)=>new{b.name,i}).ToDictionary(b=>b.name,b=>b.i);
                var vertices=mesh.vertices;var weights=mesh.boneWeights;var normals=mesh.normals;var tangents=mesh.tangents;int changed=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 world=skin.transform.TransformPoint(vertices[i]);Vector3 source=new Vector3(-world.x,-world.z,world.y-.9504841566085815f);
                    var record=data.vertices.FirstOrDefault(v=>(v.position-source).sqrMagnitude<.00000001f);
                    if(record==null)continue;
                    var influence=record.influences.OrderByDescending(w=>w.weight).ToArray();
                    if(influence.Length>4)throw new InvalidOperationException("Too many tail influences.");
                    var w=new BoneWeight();
                    for(int j=0;j<influence.Length;j++)
                    {
                        int bone=names[influence[j].name];float value=influence[j].weight;
                        if(j==0){w.boneIndex0=bone;w.weight0=value;}else if(j==1){w.boneIndex1=bone;w.weight1=value;}
                        else if(j==2){w.boneIndex2=bone;w.weight2=value;}else{w.boneIndex3=bone;w.weight3=value;}
                    }
                    weights[i]=w;changed++;
                    if(record.hasNormal)
                    {
                        normals[i]=skin.transform.InverseTransformDirection(new Vector3(-record.normal.x,record.normal.z,-record.normal.y)).normalized;
                        if(tangents.Length==vertices.Length)
                        {
                            Vector3 tangent=Vector3.ProjectOnPlane(new Vector3(tangents[i].x,tangents[i].y,tangents[i].z),normals[i]).normalized;
                            tangents[i]=new Vector4(tangent.x,tangent.y,tangent.z,tangents[i].w);
                        }
                    }
                }
                if(changed<100)throw new InvalidOperationException("Tail vertex mapping failed: "+changed);
                mesh.boneWeights=weights;mesh.normals=normals;if(tangents.Length==vertices.Length)mesh.tangents=tangents;
                var asset=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
                if(asset==null){AssetDatabase.CreateAsset(mesh,meshPath);asset=mesh;}else{EditorUtility.CopySerialized(mesh,asset);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(asset);}
                skin.sharedMesh=asset;PrefabUtility.SaveAsPrefabAsset(root,path);Debug.Log("TAIL_WEIGHT_VERTICES="+changed);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
    }
}
