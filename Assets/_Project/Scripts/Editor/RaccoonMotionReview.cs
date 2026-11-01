using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.Gameplay.Player;
using Game.Gameplay.Inventory;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonMotionReview
    {
        public const string Output = "output/raccoon-motion-v6";
        private const string Art = RaccoonEquipmentSetup.Art;
        [Serializable] private sealed class Bone { public string name; public Vector3 position; public Quaternion rotation,restRotation; }
        [Serializable] private sealed class Placement { public string name; public Vector3 position,scale; public Quaternion rotation; public bool visible; }
        [Serializable] private sealed class Geometry { public string name,source; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
        [Serializable] private sealed class Frame { public int frame; public string label; public Bone[] bones; public Placement[] weapons; public float[] shapes; }
        [Serializable] private sealed class Exchange { public int fps=30; public Geometry[] geometry; public Frame[] frames; public string note; }
        [Serializable] private sealed class Report { public int frames; public bool finite=true; public float maxHandStep, minimumTailHeight=999; public string note; }

        public static void InspectClips()
        {
            var controller=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Animations/AC_Player.controller");
            var text=new System.Text.StringBuilder();
            foreach(var clip in controller.animationClips.Distinct())
            {
                text.AppendLine(clip.name+" length="+clip.length+" human="+clip.humanMotion);
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if(!binding.propertyName.Contains("Root")&&!binding.propertyName.Contains("Motion")&&!binding.propertyName.Contains("Body"))continue;
                    var curve=AnimationUtility.GetEditorCurve(clip,binding);
                    text.AppendLine(binding.path+" "+binding.propertyName+" first="+curve.Evaluate(0)+" min="+curve.keys.Min(k=>k.value)+" max="+curve.keys.Max(k=>k.value));
                }
            }
            File.WriteAllText(Output+"/clip-inspection.txt",text.ToString());
        }

        public static void Build()
        {
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Output+"/frames");Directory.CreateDirectory(Output+"/frames-back");
            string path=Art+"/CH_Raccoon_Player.prefab";
            GameObject contents=PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.GetComponent<Animator>().runtimeAnimatorController=BuildMotionController();
                var tail=contents.GetComponent<RaccoonTailMotion>()??contents.AddComponent<RaccoonTailMotion>();
                tail.Bind(contents.GetComponent<Animator>());PrefabUtility.SaveAsPrefabAsset(contents,path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            AssetDatabase.SaveAssets();
            Review();
        }

        private static RuntimeAnimatorController BuildMotionController()
        {
            var original=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Art+"/AC_Raccoon_Base_v7.controller")
                ??AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Animations/AC_Player.controller");
            string path=Art+"/AC_Raccoon.overrideController";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
            if(controller==null){controller=new AnimatorOverrideController(original);AssetDatabase.CreateAsset(controller,path);}
            else controller.runtimeAnimatorController=original;
            var replacements=new List<KeyValuePair<AnimationClip,AnimationClip>>();
            foreach(var source in original.animationClips.Distinct())
            {
                if(source.name=="Raccoon_ShotgunRun_AUTHORED_v7")
                {replacements.Add(new KeyValuePair<AnimationClip,AnimationClip>(source,controller[source.name]??source));continue;}
                string label=source.name.Substring(source.name.LastIndexOf(':')+1);
                if(label=="idle")
                {
                    var authoredIdle=AssetDatabase.LoadAssetAtPath<AnimationClip>(RaccoonIdleSetup.ClipPath);
                    if(authoredIdle!=null){replacements.Add(new KeyValuePair<AnimationClip,AnimationClip>(source,authoredIdle));continue;}
                }
                var clip=Object.Instantiate(source);clip.name="Raccoon_"+label;
                // Imported cycles contain unrelated absolute X/Z offsets (run starts >1m ahead).
                // Remove only that constant offset, preserving the authored cyclic sway and all joint curves.
                foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    if(binding.propertyName!="RootT.x"&&binding.propertyName!="RootT.z")continue;
                    var curve=AnimationUtility.GetEditorCurve(clip,binding);float offset=curve.Evaluate(0);
                    Keyframe[] keys=curve.keys;for(int i=0;i<keys.Length;i++)keys[i].value-=offset;
                    curve.keys=keys;AnimationUtility.SetEditorCurve(clip,binding,curve);
                }
                string clipPath=Art+"/"+clip.name+".anim";var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
                if(existing==null)AssetDatabase.CreateAsset(clip,clipPath);
                else{EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);clip=existing;EditorUtility.SetDirty(clip);}
                replacements.Add(new KeyValuePair<AnimationClip,AnimationClip>(source,clip));
            }
            controller.ApplyOverrides(replacements);EditorUtility.SetDirty(controller);return controller;
        }

        public static void Review()
        {
            Directory.CreateDirectory(Output);Directory.CreateDirectory(Output+"/frames");Directory.CreateDirectory(Output+"/frames-back");
            GameObject character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/CH_Raccoon_Player.prefab"));
            try
            {
                Animator animator=character.GetComponent<Animator>();
                var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();
                Transform[] bones=skin.bones;Quaternion[] rest=bones.Select(b=>b.rotation).ToArray();
                var pose=character.GetComponent<RaccoonEquipmentPose>();
                var tail=character.GetComponent<RaccoonTailMotion>();tail.ResetMotion();
                var weapons=new Dictionary<HotbarItemType,GameObject>();
                foreach(var item in new[]{HotbarItemType.Shotgun,HotbarItemType.Revolver})
                {
                    pose.Profile.TryGetEntry(item,out var entry);
                    weapons[item]=RaccoonEquipmentSetup.Attach(animator.GetBoneTransform(HumanBodyBones.RightHand),item,entry.Socket);
                    weapons[item].SetActive(false);
                }
                var filters=weapons.Values.SelectMany(w=>w.GetComponentsInChildren<MeshFilter>(true)).ToArray();
                var geometry=filters.Select(m=>new Geometry {name=m.name,source=m.name.Replace("(Clone)",""),vertices=m.sharedMesh.vertices,uv=m.sharedMesh.uv,triangles=m.sharedMesh.triangles}).ToArray();
                animator.Rebind();animator.Update(0);animator.SetLayerWeight(animator.GetLayerIndex("Hold"),1);
                var frames=new List<Frame>();var report=new Report {note="Existing gameplay locomotion is used for integration review only; no new locomotion clips authored. Tail motion is raccoon-specific. Tail height is bone height, not a full surface collision test."};
                Vector3 lastHand=Vector3.zero;float lastHeading=0,displaySpeed=0;
                for(int i=0;i<360;i++)
                {
                    float t=i/30f,dt=1f/30f;
                    float speed=t<2?0:t<4?4.5f:t<6?7:t<8?4.5f:0;
                    displaySpeed=PlayerAnimationMath.SmoothTowards(displaySpeed,speed,.15f,dt);
                    float heading=t>=6&&t<8?30*Mathf.Sin((t-6)*Mathf.PI):0;
                    float pitch=t>=8&&t<10?35*Mathf.Sin((t-8)*Mathf.PI):0;
                    HotbarItemType item=t>=10&&t<11?HotbarItemType.Revolver:HotbarItemType.Shotgun;
                    string label=t<2?"Idle":t<4?"Walk":t<6?"Run":t<8?"Turn":t<10?"StopAim":"Switch";
                    character.transform.rotation=Quaternion.Euler(0,heading,0);
                    animator.SetFloat("Speed",displaySpeed);animator.SetInteger("HoldPose",item==HotbarItemType.Shotgun?2:1);
                    if(pose.Profile.HasAuthoredShotgunRun)animator.SetFloat("RaccoonShotgunRun",item==HotbarItemType.Shotgun?1:0);
                    animator.SetLayerWeight(animator.GetLayerIndex("Hold"),1-pose.Profile.AuthoredRunWeight(animator));animator.Update(dt);
                    Transform chest=animator.GetBoneTransform(HumanBodyBones.Chest),head=animator.GetBoneTransform(HumanBodyBones.Head);
                    chest.rotation=Quaternion.AngleAxis(pitch*pose.Profile.ChestPitchWeight,character.transform.right)*chest.rotation;
                    head.rotation=Quaternion.AngleAxis(pose.Profile.HeadPitchFor(animator)+pitch*pose.Profile.HeadPitchWeight,character.transform.right)*head.rotation;
                    pose.Apply(item,false,pitch,dt,character.transform);
                    tail.Step(displaySpeed,Mathf.DeltaAngle(lastHeading,heading)/dt,dt);lastHeading=heading;
                    character.GetComponent<RaccoonElbowCorrectives>().Apply();
                    foreach(var pair in weapons)pair.Value.SetActive(pair.Key==item);
                    Vector3 hand=character.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                    if(i>10)report.maxHandStep=Mathf.Max(report.maxHandStep,Vector3.Distance(lastHand,hand));lastHand=hand;
                    foreach(Transform b in bones)
                    {
                        report.finite&=float.IsFinite(b.position.sqrMagnitude)&&float.IsFinite(b.rotation.x+b.rotation.y+b.rotation.z+b.rotation.w);
                        if(b.name.StartsWith("Tail"))report.minimumTailHeight=Mathf.Min(report.minimumTailHeight,b.position.y);
                    }
                    frames.Add(new Frame {frame=i+1,label=label,
                        bones=bones.Select((b,j)=>new Bone {name=b.name,position=b.position,rotation=b.rotation,restRotation=rest[j]}).ToArray(),
                        shapes=Enumerable.Range(0,skin.sharedMesh.blendShapeCount).Select(skin.GetBlendShapeWeight).ToArray(),
                        weapons=filters.Select(m=>new Placement {name=m.name,position=m.transform.position,rotation=m.transform.rotation,scale=m.transform.lossyScale,visible=m.gameObject.activeInHierarchy}).ToArray()});
                    // 15 fps render; bone review keeps the full 30 fps timeline.
                    if(i%2==0)
                    {
                        RaccoonEquipmentSetup.Render(character,"frames/"+(i/2).ToString("D4")+".png",new Vector3(0,.98f,.12f),new Vector3(2.8f,.35f,3.6f),1.12f,Output,512);
                        RaccoonEquipmentSetup.Render(character,"frames-back/"+(i/2).ToString("D4")+".png",new Vector3(0,.98f,0),new Vector3(-2.2f,.3f,-3.5f),1.12f,Output,512);
                    }
                    if(new[]{45,105,165,225,285,330}.Contains(i))
                        RaccoonEquipmentSetup.Render(character,label+"_back.png",new Vector3(0,.98f,0),new Vector3(-2.2f,.3f,-3.5f),1.12f,Output,800);
                }
                report.frames=frames.Count;
                File.WriteAllText(Output+"/motion_exchange.json",JsonUtility.ToJson(new Exchange {geometry=geometry,frames=frames.ToArray(),note=report.note}));
                File.WriteAllText(Output+"/motion_report.json",JsonUtility.ToJson(report,true));
                if(!report.finite)throw new InvalidOperationException("Non-finite motion pose.");
                Debug.Log("RACCOON_MOTION_REVIEW_COMPLETE "+JsonUtility.ToJson(report));
            }
            finally {Object.DestroyImmediate(character);}
        }
    }
}
