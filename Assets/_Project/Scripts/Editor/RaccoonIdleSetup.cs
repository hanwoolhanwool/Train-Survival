using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Player;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonIdleSetup
    {
        public const string ClipPath=RaccoonEquipmentSetup.Art+"/Animations/Raccoon_Idle_AUTHORED_v15.anim";
        public static void Build()
        {
            const string art=RaccoonEquipmentSetup.Art;
            Directory.CreateDirectory("output/raccoon-locomotion-v15");
            string controllerPath=art+"/AC_Raccoon.overrideController";
            if(!File.Exists("output/raccoon-locomotion-v15/AC_Raccoon.before.overrideController"))
                File.Copy(controllerPath,"output/raccoon-locomotion-v15/AC_Raccoon.before.overrideController");
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(art+"/CH_Raccoon_Player.prefab"));
            try
            {
                // Author directly from the avatar's neutral pose. No old idle or other animation is sampled.
                foreach(var behaviour in go.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                var animator=go.GetComponent<Animator>();animator.enabled=false;
                using(var handler=new HumanPoseHandler(animator.avatar,go.transform))
                {
                    var pose=new HumanPose();handler.GetHumanPose(ref pose);
                    var feet=new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Select(animator.GetBoneTransform).ToArray();
                    var anchors=feet.Select(f=>f.position).ToArray();var rotations=feet.Select(f=>f.rotation).ToArray();
                    var neutral=new HumanPose{bodyPosition=pose.bodyPosition,bodyRotation=Quaternion.identity,muscles=new float[HumanTrait.MuscleCount]};
                    for(int i=0;i<HumanTrait.MuscleCount;i++)
                    {
                        string name=HumanTrait.MuscleName[i];
                        if(name.EndsWith("Arm Down-Up"))neutral.muscles[i]=-.7f;
                        if(name.EndsWith("Forearm Stretch"))neutral.muscles[i]=.1f;
                    }
                    var curves=new Dictionary<string,AnimationCurve>();
                    foreach(string name in HumanTrait.MuscleName)curves[name]=new AnimationCurve();
                    foreach(string name in new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"})curves[name]=new AnimationCurve();
                    Vector3 centreOffset=Vector3.zero;
                    for(int frame=0;frame<=240;frame++)
                    {
                        float time=frame/60f;
                        handler.SetHumanPose(ref neutral);
                        for(int side=0;side<2;side++)
                        {
                            var upper=animator.GetBoneTransform(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                            var lower=animator.GetBoneTransform(side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                            RaccoonEquipmentPose.SolveArm(upper,lower,feet[side],anchors[side],lower.position+go.transform.forward*.15f);
                            feet[side].rotation=rotations[side];
                        }
                        float breath=Mathf.Sin(time*Mathf.PI*.5f);
                        var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
                        var chest=animator.GetBoneTransform(HumanBodyBones.Chest);
                        spine.rotation=Quaternion.AngleAxis(.35f*breath,go.transform.right)*spine.rotation;
                        if(chest!=null)chest.rotation=Quaternion.AngleAxis(.25f*breath,go.transform.right)*chest.rotation;
                        handler.GetHumanPose(ref pose);
                        if(frame==0)
                        {
                            centreOffset=go.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position)/animator.humanScale;
                            centreOffset.y=0;
                        }
                        pose.bodyPosition-=centreOffset;
                        for(int i=0;i<HumanTrait.MuscleCount;i++)curves[HumanTrait.MuscleName[i]].AddKey(time,pose.muscles[i]);
                        curves["RootT.x"].AddKey(time,pose.bodyPosition.x);curves["RootT.y"].AddKey(time,pose.bodyPosition.y);curves["RootT.z"].AddKey(time,pose.bodyPosition.z);
                        curves["RootQ.x"].AddKey(time,pose.bodyRotation.x);curves["RootQ.y"].AddKey(time,pose.bodyRotation.y);curves["RootQ.z"].AddKey(time,pose.bodyRotation.z);curves["RootQ.w"].AddKey(time,pose.bodyRotation.w);
                    }
                    var clip=new AnimationClip{name="Raccoon_Idle_AUTHORED_v15",frameRate=60};
                    foreach(var pair in curves)AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),pair.Key),pair.Value);
                    var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=false;settings.keepOriginalPositionXZ=true;settings.keepOriginalPositionY=true;settings.keepOriginalOrientation=true;settings.loopBlendPositionXZ=true;settings.loopBlendPositionY=true;settings.loopBlendOrientation=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
                    var existing=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
                    if(existing==null){AssetDatabase.CreateAsset(clip,ClipPath);existing=clip;}
                    else{EditorUtility.CopySerialized(clip,existing);Object.DestroyImmediate(clip);EditorUtility.SetDirty(existing);}
                    var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(controllerPath);
                    var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(overrides);
                    int index=overrides.FindIndex(p=>p.Value!=null&&(p.Value.name=="Raccoon_idle"||p.Value.name=="Raccoon_Idle_AUTHORED_v15"));
                    if(index<0)throw new InvalidOperationException("Idle override not found");
                    overrides[index]=new KeyValuePair<AnimationClip,AnimationClip>(overrides[index].Key,existing);controller.ApplyOverrides(overrides);EditorUtility.SetDirty(controller);
                    Debug.Log("NEW_IDLE_AUTHORED neutral avatar, stationary ankle anchors, four-second upper-body breath. Humanoid="+existing.humanMotion);
                }
            }
            finally{Object.DestroyImmediate(go);}
            AssetDatabase.SaveAssets();
            RaccoonLocomotionReview.ReviewV15();
        }
    }
}
