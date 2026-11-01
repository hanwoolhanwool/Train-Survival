using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Game.Gameplay.Player;
using Game.Gameplay.Inventory;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonRunSetup
    {
        public const string Art=RaccoonEquipmentSetup.Art;
        public const string Output="output/raccoon-run-v7";
        public const string ClipPath=Art+"/Animations/Raccoon_ShotgunRun_AUTHORED_v7.anim";
        private const string Fbx=Art+"/Animations/Raccoon_Shotgun_Run_v7.fbx";
        [Serializable] private sealed class Bone { public string name; public Vector3 position; public Quaternion rotation,restRotation; }
        [Serializable] private sealed class Placement { public string name; public Vector3 position,scale; public Quaternion rotation; public bool visible=true; }
        [Serializable] private sealed class Geometry { public string name,source; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
        [Serializable] private sealed class Frame { public int frame; public string label="AuthoredShotgunRun"; public float phase; public Bone[] bones; public Placement[] weapons; public float[] shapes; }
        [Serializable] private sealed class Exchange { public int fps=60; public Geometry[] geometry; public Frame[] frames; }
        [Serializable] private sealed class Sample {public int frame;public float phase,leftSole,rightSole;public Vector3 leftFoot,rightFoot,hips,head;public float gripError;}
        [Serializable] private sealed class Report
        {
            public string provenance="New Blender-authored raccoon run; no existing locomotion sampled. Approved static shotgun grip reused.";
            public float clipLength, minimumSole=999,maximumContactHeight=-999,minimumContactHeight=999,maxGripError,maxHandStep;
            public bool humanoid;public Sample[] samples;
        }

        public static void Build()
        {
            Directory.CreateDirectory(Output);AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var importer=(ModelImporter)AssetImporter.GetAtPath(Fbx);
            importer.animationType=ModelImporterAnimationType.Human;
            importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;
            importer.sourceAvatar=AssetDatabase.LoadAllAssetsAtPath(Art+"/CH_Raccoon_TP.fbx").OfType<Avatar>().First();
            importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;
            importer.optimizeGameObjects=false;importer.SaveAndReimport();
            var definitions=importer.defaultClipAnimations;
            if(definitions.Length==0)throw new InvalidOperationException("No animation exported from Blender.");
            var definition=definitions[0];definition.name="Raccoon_Shotgun_Run_SOURCE_v7";
            definition.loopTime=true;definition.loopPose=false;
            definition.lockRootRotation=true;definition.keepOriginalOrientation=true;
            definition.lockRootPositionXZ=true;definition.keepOriginalPositionXZ=true;
            definition.lockRootHeightY=true;definition.keepOriginalPositionY=true;
            importer.clipAnimations=new[]{definition};importer.SaveAndReimport();
            AnimationClip imported=AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview"));
            if(!imported.humanMotion)throw new InvalidOperationException("New run did not import as Humanoid.");
            var clip=Object.Instantiate(imported);clip.name="Raccoon_ShotgunRun_AUTHORED_v7";
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                // Runtime owns the secondary tail layer. Do not double-apply its authoring preview keys.
                if(binding.path.Split('/').Any(p=>p.StartsWith("Tail")))
                {AnimationUtility.SetEditorCurve(clip,binding,null);continue;}
                if(binding.propertyName=="RootT.x"||binding.propertyName=="RootT.z")
                {
                    var curve=AnimationUtility.GetEditorCurve(clip,binding);float origin=curve.Evaluate(0);
                    var keys=curve.keys;for(int i=0;i<keys.Length;i++)keys[i].value-=origin;curve.keys=keys;
                    AnimationUtility.SetEditorCurve(clip,binding,curve);
                }
            }
            var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if(existing==null)AssetDatabase.CreateAsset(clip,ClipPath);
            else{EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);clip=existing;EditorUtility.SetDirty(clip);}
            InstallController(clip);
            var profile=AssetDatabase.LoadAssetAtPath<RaccoonEquipmentProfile>(Art+"/RaccoonEquipmentProfile.asset");
            profile.HasAuthoredShotgunRun=true;EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            Review();
        }

        private static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
        {
            foreach(var state in machine.states)yield return state.state;
            foreach(var child in machine.stateMachines)foreach(var state in States(child.stateMachine))yield return state;
        }

        private static void InstallController(AnimationClip clip)
        {
            string path=Art+"/AC_Raccoon_Base_v7.controller";
            if(AssetDatabase.LoadAssetAtPath<AnimatorController>(path)==null)
                AssetDatabase.CopyAsset("Assets/_Project/Art/Animations/AC_Player.controller",path);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(!controller.parameters.Any(p=>p.name=="RaccoonShotgunRun"))controller.AddParameter("RaccoonShotgunRun",AnimatorControllerParameterType.Float);
            var locomotion=States(controller.layers[0].stateMachine).First(s=>s.name=="Locomotion");
            var tree=(BlendTree)locomotion.motion;var children=tree.children;
            int index=Array.FindIndex(children,c=>c.threshold>=6.9f);
            if(index<0)throw new InvalidOperationException("Run child missing from locomotion tree.");
            BlendTree style=children[index].motion as BlendTree;
            if(style==null||style.name!="Raccoon_RunStyle")
            {
                Motion previous=children[index].motion;
                style=new BlendTree {name="Raccoon_RunStyle",blendParameter="RaccoonShotgunRun",useAutomaticThresholds=false};
                AssetDatabase.AddObjectToAsset(style,path);style.children=new[]{new ChildMotion {motion=previous,threshold=0,timeScale=1},new ChildMotion {motion=clip,threshold=1,timeScale=1}};
            }
            else{var styles=style.children;styles[1].motion=clip;style.children=styles;}
            children[index].motion=style;tree.children=children;locomotion.iKOnFeet=false;
            EditorUtility.SetDirty(style);EditorUtility.SetDirty(tree);EditorUtility.SetDirty(controller);
            var overrides=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(Art+"/AC_Raccoon.overrideController");
            var saved=new List<KeyValuePair<AnimationClip,AnimationClip>>();overrides.GetOverrides(saved);
            overrides.runtimeAnimatorController=controller;
            var current=new List<KeyValuePair<AnimationClip,AnimationClip>>();overrides.GetOverrides(current);
            for(int i=0;i<current.Count;i++)
            {
                var pair=saved.FirstOrDefault(p=>p.Key==current[i].Key);
                current[i]=new KeyValuePair<AnimationClip,AnimationClip>(current[i].Key,pair.Key!=null?pair.Value:current[i].Key);
            }
            overrides.ApplyOverrides(current);EditorUtility.SetDirty(overrides);
        }

        // Read-only sampling for the Blender comparison; no controller or prefab assets are changed.
        public static void CaptureComparison()
        {CaptureComparisonInto("output/raccoon-run-comparison");}

        public static void CaptureHybridComparison()
        {CaptureComparisonInto("output/raccoon-run-v8/comparison",true);}

        public static void RefineTail()
        {
            RaccoonHybridRunSetup.ApplyTailSkin("output/raccoon-tail-v9/unity_tail_weights.json",Art+"/MESH_Raccoon_Tail_v9.asset","Raccoon_TailSkin_v9");
            AssetDatabase.SaveAssets();CaptureComparisonInto("output/raccoon-tail-v9/comparison",true,true);
        }

        public static void CaptureRoundTailComparison()
        {CaptureComparisonInto("output/raccoon-tail-v10/comparison",true,true);}

        public static void CaptureNarrowTailComparison()
        {CaptureComparisonInto("output/raccoon-tail-v11/comparison",true,true);}

        public static void CaptureTailSurroundComparison()
        {CaptureComparisonInto("output/raccoon-tail-v12/comparison",true,true);}

        public static void CaptureRoundedTailComparison()
        {CaptureComparisonInto("output/raccoon-tail-v13/comparison",true,true);}

        private static void CaptureComparisonInto(string folder,bool render=false,bool authoredOnly=false)
        {
            Directory.CreateDirectory(folder);
            foreach(bool authored in authoredOnly?new[]{true}:new[]{false,true})
            {
                var character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/CH_Raccoon_Player.prefab"));
                try
                {
                    var animator=character.GetComponent<Animator>();var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();
                    var bones=skin.bones;var rest=bones.Select(b=>b.rotation).ToArray();
                    var pose=character.GetComponent<RaccoonEquipmentPose>();var tail=character.GetComponent<RaccoonTailMotion>();tail.ResetMotion();
                    pose.Profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
                    var weapon=RaccoonEquipmentSetup.Attach(animator.GetBoneTransform(HumanBodyBones.RightHand),HotbarItemType.Shotgun,entry.Socket);
                    var filters=weapon.GetComponentsInChildren<MeshFilter>();
                    var geometry=filters.Select(m=>new Geometry{name=m.name,source=m.name.Replace("(Clone)",""),vertices=m.sharedMesh.vertices,uv=m.sharedMesh.uv,triangles=m.sharedMesh.triangles}).ToArray();
                    animator.Rebind();animator.Update(0);animator.SetFloat("Speed",7);animator.SetFloat("RaccoonShotgunRun",authored?1:0);animator.SetInteger("HoldPose",2);
                    animator.SetLayerWeight(animator.GetLayerIndex("Hold"),authored?0:1);
                    var frames=new List<Frame>();
                    for(int i=-300;i<=240;i++)
                    {
                        if(i==0){animator.Play("Locomotion",0,0);animator.Update(0);}else animator.Update(1f/60);
                        var head=animator.GetBoneTransform(HumanBodyBones.Head);
                        head.rotation=Quaternion.AngleAxis(pose.Profile.HeadPitchFor(animator),character.transform.right)*head.rotation;
                        pose.Apply(HotbarItemType.Shotgun,false,0,1f/60,character.transform);tail.Step(7,0,1f/60);
                        character.GetComponent<RaccoonElbowCorrectives>().Apply();
                        if(i<0)continue;
                        frames.Add(new Frame{frame=i+1,label=authored?"New authored v7":"Existing game run",phase=Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1),
                            bones=bones.Select((b,j)=>new Bone{name=b.name,position=b.position,rotation=b.rotation,restRotation=rest[j]}).ToArray(),
                            shapes=Enumerable.Range(0,skin.sharedMesh.blendShapeCount).Select(skin.GetBlendShapeWeight).ToArray(),
                            weapons=filters.Select(m=>new Placement{name=m.name,position=m.transform.position,rotation=m.transform.rotation,scale=m.transform.lossyScale}).ToArray()});
                        if(render&&i<150&&i%3==0)
                        {
                            string images=folder+(authored?"/new-frames":"/existing-frames");Directory.CreateDirectory(images);
                            RaccoonEquipmentSetup.Render(character,(i/3).ToString("D4")+".png",new Vector3(0,.9f,-.08f),new Vector3(4,.2f,0),1.25f,images,600);
                        }
                    }
                    File.WriteAllText(folder+(authored?"/new.json":"/existing.json"),JsonUtility.ToJson(new Exchange{geometry=geometry,frames=frames.ToArray()}));
                    Debug.Log("COMPARISON "+authored+" "+string.Join(",",animator.GetCurrentAnimatorClipInfo(0).Where(c=>c.weight>.001f).Select(c=>c.clip.name+" duration="+c.clip.length+" weight="+c.weight)));
                }
                finally{Object.DestroyImmediate(character);}
            }
        }

        public static void Review()
        {
            Directory.CreateDirectory(Output);foreach(var folder in new[]{"frames","frames-side","frames-back"})Directory.CreateDirectory(Output+"/"+folder);
            var character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/CH_Raccoon_Player.prefab"));
            var baked=new Mesh();
            try
            {
                var animator=character.GetComponent<Animator>();var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();
                Transform[] bones=skin.bones;Quaternion[] rest=bones.Select(b=>b.rotation).ToArray();
                var pose=character.GetComponent<RaccoonEquipmentPose>();var tail=character.GetComponent<RaccoonTailMotion>();tail.ResetMotion();
                pose.Profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
                var weapon=RaccoonEquipmentSetup.Attach(animator.GetBoneTransform(HumanBodyBones.RightHand),HotbarItemType.Shotgun,entry.Socket);
                var filters=weapon.GetComponentsInChildren<MeshFilter>();
                var geometry=filters.Select(m=>new Geometry {name=m.name,source=m.name.Replace("(Clone)",""),vertices=m.sharedMesh.vertices,uv=m.sharedMesh.uv,triangles=m.sharedMesh.triangles}).ToArray();
                var footIndices=new Dictionary<string,int[]>();BoneWeight[] weights=skin.sharedMesh.boneWeights;
                foreach(string side in new[]{"Left","Right"})
                {
                    var ids=new HashSet<int>(bones.Select((b,i)=>new{b.name,i}).Where(b=>b.name==side+"Foot"||b.name==side+"Toes").Select(b=>b.i));
                    footIndices[side]=Enumerable.Range(0,weights.Length).Where(i=>
                        (ids.Contains(weights[i].boneIndex0)?weights[i].weight0:0)+(ids.Contains(weights[i].boneIndex1)?weights[i].weight1:0)+
                        (ids.Contains(weights[i].boneIndex2)?weights[i].weight2:0)+(ids.Contains(weights[i].boneIndex3)?weights[i].weight3:0)>.65f).ToArray();
                }
                animator.Rebind();animator.Update(0);animator.SetFloat("Speed",7);animator.SetFloat("RaccoonShotgunRun",1);animator.SetInteger("HoldPose",2);
                animator.SetLayerWeight(animator.GetLayerIndex("Hold"),0);
                var frames=new List<Frame>();var samples=new List<Sample>();var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                var report=new Report {clipLength=clip.length,humanoid=clip.humanMotion};Vector3 lastHand=Vector3.zero;
                for(int i=-60;i<120;i++)
                {
                    animator.Update(1f/60);pose.Apply(HotbarItemType.Shotgun,false,0,1f/60,character.transform);
                    tail.Step(7,0,1f/60);character.GetComponent<RaccoonElbowCorrectives>().Apply();
                    if(i<0)continue;
                    float phase=Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1);
                    var matrices=bones.Select((b,j)=>b.localToWorldMatrix*skin.sharedMesh.bindposes[j]).ToArray();
                    Vector3[] vertices=skin.sharedMesh.vertices;
                    Func<int,float> height=j=>{var w=weights[j];var v=vertices[j];return
                        (matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+
                         matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3).y;};
                    float left=footIndices["Left"].Min(height),right=footIndices["Right"].Min(height);
                    Vector3 rh=animator.GetBoneTransform(HumanBodyBones.RightHand).position,lh=animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                    float gripError=Vector3.Distance(lh-rh,entry.Left.Position-entry.Right.Position);
                    report.minimumSole=Mathf.Min(report.minimumSole,left,right);report.maxGripError=Mathf.Max(report.maxGripError,gripError);
                    if(phase<.20f){report.maximumContactHeight=Mathf.Max(report.maximumContactHeight,left);report.minimumContactHeight=Mathf.Min(report.minimumContactHeight,left);}
                    if(Mathf.Repeat(phase+.5f,1)<.20f){report.maximumContactHeight=Mathf.Max(report.maximumContactHeight,right);report.minimumContactHeight=Mathf.Min(report.minimumContactHeight,right);}
                    if(i>0)report.maxHandStep=Mathf.Max(report.maxHandStep,Vector3.Distance(lastHand,rh));lastHand=rh;
                    samples.Add(new Sample {frame=i+1,phase=phase,leftSole=left,rightSole=right,leftFoot=animator.GetBoneTransform(HumanBodyBones.LeftFoot).position,rightFoot=animator.GetBoneTransform(HumanBodyBones.RightFoot).position,hips=animator.GetBoneTransform(HumanBodyBones.Hips).position,head=animator.GetBoneTransform(HumanBodyBones.Head).position,gripError=gripError});
                    frames.Add(new Frame {frame=i+1,phase=phase,bones=bones.Select((b,j)=>new Bone {name=b.name,position=b.position,rotation=b.rotation,restRotation=rest[j]}).ToArray(),
                        shapes=Enumerable.Range(0,skin.sharedMesh.blendShapeCount).Select(skin.GetBlendShapeWeight).ToArray(),
                        weapons=filters.Select(m=>new Placement {name=m.name,position=m.transform.position,rotation=m.transform.rotation,scale=m.transform.lossyScale}).ToArray()});
                    if(i%2==0)
                    {
                        string name=(i/2).ToString("D4")+".png";
                        RaccoonEquipmentSetup.Render(character,"frames/"+name,new Vector3(0,.87f,.05f),new Vector3(2.8f,.35f,3.6f),1.02f,Output,600);
                        RaccoonEquipmentSetup.Render(character,"frames-side/"+name,new Vector3(0,.87f,0),new Vector3(4,.1f,0),1.02f,Output,600);
                        RaccoonEquipmentSetup.Render(character,"frames-back/"+name,new Vector3(0,.87f,0),new Vector3(-2.4f,.3f,-3.6f),1.02f,Output,600);
                    }
                }
                report.samples=samples.ToArray();
                File.WriteAllText(Output+"/run_report.json",JsonUtility.ToJson(report,true));
                File.WriteAllText(Output+"/motion_exchange.json",JsonUtility.ToJson(new Exchange {geometry=geometry,frames=frames.ToArray()}));
                Debug.Log("RACCOON_AUTHORED_RUN_REVIEW_COMPLETE contact="+report.minimumContactHeight+".."+report.maximumContactHeight+" grip="+report.maxGripError);
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(character);}
        }
    }
}
