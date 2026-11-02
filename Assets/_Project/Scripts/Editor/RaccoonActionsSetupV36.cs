using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.Gameplay.Debugging;
using Game.Gameplay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonActionsSetupV36
    {
        public const string Folder="Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36";
        public const string PrefabPath=Folder+"/PC_Raccoon_Actions_v36.prefab";
        public const string ScenePath="Assets/_Project/Scenes/Raccoon_ActionsReview_v36.unity";
        static RaccoonActionLibraryV36.Entry E(string id,string label,float duration,bool loop=false,bool upper=false,bool gun=false,string prop="")=>new RaccoonActionLibraryV36.Entry{Id=id,Label=label,Duration=duration,Loop=loop,UpperBody=upper,KeepWeapon=gun,Prop=prop,Source="New authored raccoon pose curves, 60 Hz, v36"};
        static RaccoonActionLibraryV36.Entry[] Entries()=>new[]{
            E("HitFront","피격 · 정면",.85f,false,false,true),E("HitBack","피격 · 후면",.85f,false,false,true),E("HitLeft","피격 · 왼쪽",.85f,false,false,true),E("HitRight","피격 · 오른쪽",.85f,false,false,true),
            E("Death","사망 · 기존 모션 기반",5.541667f),E("Respawn","재등장 · 샷건 준비",1.35f,false,false,true),
            E("Equip","샷건 들어 올리기",1.1f,false,false,true),E("Holster","샷건 낮춰 들기",1f,false,false,true),
            E("Interact","상호작용 · 뻗기",1.2f),E("Pickup","줍기 · 무릎 굽힘",1.6f),E("Repair","수리 · 해머 반복",RaccoonHammerPoseV42.Duration,true,false,false,"Hammer"),E("Melee","근접 · 타격",.95f),E("Eat","섭취",1.8f,false,false,false,"Food"),E("Carry","자원 꾸러미 들기",2.4f,true,false,false,"Bundle"),
            E("HarpoonFire","작살 발사",.8f,false,false,false,"Harpoon"),E("HarpoonReel","작살 당기기",1.3f,true,false,false,"Harpoon"),
            E("RevolverFire","리볼버 발사",.65f,false,false,false,"Revolver"),E("RevolverReload","리볼버 장전 · 손동작",2.5f,false,false,false,"Revolver"),
            E("SwimIdle","수영 · 제자리",2.4f,true),E("SwimMove","수영 · 전진",1.8f,true),
            E("LadderHold","사다리 · 대기",2.4f,true),E("LadderUp","사다리 · 오르기",1.6f,true),E("LadderDown","사다리 · 내려가기",1.8f,true),E("Mantle","사다리 끝 · 올라서기",1.2f),
            E("Grabbed","붙잡힘 · 저항",1.6f,true),E("Carried","들려가기",2.2f,true),
            E("FishingCast","낚시 · 던지기 (검토용 낚싯대)",1.4f,false,false,false,"Rod"),E("FishingWait","낚시 · 대기",2.8f,true,false,false,"Rod"),E("FishingReel","낚시 · 감기",1.4f,true,false,false,"Rod"),
            E("RifleFire","소총 발사 · 임시 장총으로 검토",1.5f,false,false,true),E("RifleReload","소총 장전 · 전용 소품 대기",3.2f,false,false,true),E("Neutral","맨손 기본 자세 · 수정 기준",2.4f,true),
            E("SwimLeft","수영 · 왼쪽",2.1f,true),E("SwimRight","수영 · 오른쪽",2.1f,true),E("SwimBack","수영 · 뒤로",2.2f,true),E("ShotgunDeath","사망 · 샷건 놓기",2.1f,false,false,true)
        };
        static T Save<T>(T value,string path)where T:Object
        {var old=AssetDatabase.LoadAssetAtPath<T>(path);if(old==null){AssetDatabase.CreateAsset(value,path);return value;}EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);EditorUtility.SetDirty(old);return old;}
        static float S(float x)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(x));
        static float Pulse(float t,float peak=.3f)=>t<peak?S(t/peak):1-S((t-peak)/(1-peak));
        static Keyframe[] Simplify(List<Keyframe> source)
        {
            var keep=new SortedSet<int>{0,source.Count-1};
            void Reduce(int first,int last)
            {
                float error=.0001f;int chosen=-1;
                for(int i=first+1;i<last;i++){float value=Mathf.Lerp(source[first].value,source[last].value,Mathf.InverseLerp(source[first].time,source[last].time,source[i].time));float difference=Mathf.Abs(value-source[i].value);if(difference>error){error=difference;chosen=i;}}
                if(chosen<0)return;keep.Add(chosen);Reduce(first,chosen);Reduce(chosen,last);
            }
            Reduce(0,source.Count-1);return keep.Select(i=>source[i]).ToArray();
        }
        static void Turn(Animator a,HumanBodyBones bone,Vector3 euler){var b=a.GetBoneTransform(bone);if(b!=null)b.rotation=Quaternion.Euler(euler)*b.rotation;}
        static void Limb(Animator a,bool left,bool leg,Vector3 target,Quaternion rotation)
        {
            var upper=a.GetBoneTransform(leg?(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg):(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm));
            var lower=a.GetBoneTransform(leg?(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg):(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm));
            var end=a.GetBoneTransform(leg?(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot):(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand));
            Vector3 pole=upper.position+(leg?new Vector3(0,0,.7f):new Vector3(left?-.35f:.35f,-.35f,-.18f));
            RaccoonEquipmentPose.SolveArm(upper,lower,end,target,pole);end.rotation=rotation;
            if(!leg)
            {
                var mid=a.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
                Vector3 palm=(mid.position-end.position).normalized,arm=(end.position-lower.position).normalized;
                Vector3 limited=Vector3.RotateTowards(arm,palm,32*Mathf.Deg2Rad,0);end.rotation=Quaternion.FromToRotation(palm,limited)*end.rotation;
            }
        }
        static AnimationClip Author(RaccoonActionLibraryV36.Entry entry)
        {
            if(entry.Id=="Death"||entry.Id=="ShotgunDeath")
            {
                entry.Source="Retargeted existing Raccoon_defeat_03; original preserved";
                var original=AssetDatabase.LoadAssetAtPath<AnimationClip>(RaccoonEquipmentSetup.Art+"/Raccoon_defeat_03.anim");
                var copy=Object.Instantiate(original);copy.name=entry.Id=="Death"?"Raccoon_Death_v36":"Raccoon_ShotgunDeath_v41";
                if(entry.Id=="ShotgunDeath")
                {
                    float scale=2.1f/copy.length;
                    foreach(var binding in AnimationUtility.GetCurveBindings(copy))
                    {var curve=AnimationUtility.GetEditorCurve(copy,binding);var changed=curve.keys;for(int i=0;i<changed.Length;i++){changed[i].time*=scale;changed[i].inTangent/=scale;changed[i].outTangent/=scale;}curve.keys=changed;AnimationUtility.SetEditorCurve(copy,binding,curve);}
                    var shortened=AnimationUtility.GetAnimationClipSettings(copy);shortened.stopTime=2.1f;AnimationUtility.SetAnimationClipSettings(copy,shortened);
                    entry.Source="Existing defeat body motion retimed to 2.1 s with a separate shotgun release; v41";
                }
                entry.Duration=copy.length;
                var settings=AnimationUtility.GetAnimationClipSettings(copy);settings.loopTime=false;AnimationUtility.SetAnimationClipSettings(copy,settings);
                return Save(copy,Folder+"/"+copy.name+".anim");
            }
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonCombatSetupV33.PrefabPath));model.transform.position=Vector3.zero;
            try
            {
                foreach(var b in model.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                var rig=model.GetComponentInChildren<RaccoonCombatRigV33>();rig.Initialize(model.transform);
                for(int i=0;i<30;i++)rig.Tick(1f/60,Vector2.zero,false,true);
                var a=rig.Animator;a.enabled=false;
                if(!entry.KeepWeapon&&!entry.UpperBody)
                {
                    using(var neutralHandler=new HumanPoseHandler(a.avatar,a.transform))
                    {
                        var neutral=new HumanPose();neutralHandler.GetHumanPose(ref neutral);
                        for(int i=0;i<HumanTrait.MuscleCount;i++){string n=HumanTrait.MuscleName[i];if(!n.Contains("Leg")&&!n.Contains("Foot")&&!n.Contains("Toes"))neutral.muscles[i]=0;}
                        neutral.bodyRotation=Quaternion.identity;neutralHandler.SetHumanPose(ref neutral);
                    }
                    var head=a.GetBoneTransform(HumanBodyBones.Head);var hr=head.rotation;
                    Vector3 across=a.GetBoneTransform(HumanBodyBones.RightUpperArm).position-a.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                    // Clavicles and arm lengths are not perfectly mirrored in this rig.
                    // Levelling the upper-arm anchors by tilting the entire spine creates
                    // a sideways lean in a relaxed idle. Keep its torso centred instead.
                    if(entry.Id!="Neutral"&&!entry.Id.StartsWith("Swim")&&entry.Prop!="Hammer"&&!CentredProp(entry))
                    {var spine=a.GetBoneTransform(HumanBodyBones.Spine);spine.rotation=Quaternion.FromToRotation(across,Vector3.right)*spine.rotation;head.rotation=hr;}
                }
                if(entry.Prop=="Revolver"||entry.Prop=="Harpoon")
                {
                    var p=a.GetComponent<RaccoonEquipmentPose>();var item=entry.Prop=="Revolver"?Game.Gameplay.Inventory.HotbarItemType.Revolver:Game.Gameplay.Inventory.HotbarItemType.Harpoon;
                    p.Apply(item,false,0,1,model.transform);
                }
                var bones=model.GetComponentsInChildren<Transform>();var bp=bones.Select(x=>x.localPosition).ToArray();var br=bones.Select(x=>x.localRotation).ToArray();
                var hips=a.GetBoneTransform(HumanBodyBones.Hips);var hip=hips.position;
                var lh=a.GetBoneTransform(HumanBodyBones.LeftHand);var rh=a.GetBoneTransform(HumanBodyBones.RightHand);
                var hands=new[]{lh.position,rh.position};var handR=new[]{lh.rotation,rh.rotation};
                var anatomicalHands=new RaccoonActionHandsV37(a);
                var feet=new[]{a.GetBoneTransform(HumanBodyBones.LeftFoot),a.GetBoneTransform(HumanBodyBones.RightFoot)};var fp=feet.Select(x=>x.position).ToArray();var fr=feet.Select(x=>x.rotation).ToArray();
                var clip=new AnimationClip{name="Raccoon_"+entry.Id+(CentredProp(entry)?"_v47":entry.Prop=="Hammer"?"_v42":entry.Id.StartsWith("Swim")?"_v40":entry.KeepWeapon&&RaccoonShotgunActionsV41.IsAction(entry.Id)?"_v41":"_v36"),frameRate=60};
                var keys=new List<Keyframe>[HumanTrait.MuscleCount+7];for(int k=0;k<keys.Length;k++)keys[k]=new List<Keyframe>();
                int count=Mathf.RoundToInt(entry.Duration*60);
                using(var handler=new HumanPoseHandler(a.avatar,a.transform))
                for(int frame=0;frame<=count;frame++)
                {
                    for(int b=0;b<bones.Length;b++){bones[b].localPosition=bp[b];bones[b].localRotation=br[b];}
                    float t=(float)frame/count;
                    var goals=RaccoonActionTargetsV36.Evaluate(entry,t,hands,fp);
                    if(entry.Id=="Neutral")
                    {goals.Left=RaccoonNeutralPoseV39.HandTarget(a,model.transform,true);goals.Right=RaccoonNeutralPoseV39.HandTarget(a,model.transform,false);}
                    if(entry.Prop=="Hammer")goals.Left=RaccoonNeutralPoseV39.HandTarget(a,model.transform,true);
                    if(entry.Id=="Repair")goals.Right=anatomicalHands.HammerWrist(a,model.transform,anatomicalHands.Rotation(a,model.transform,entry,t,false),t);
                    Vector3 left=goals.Left,right=goals.Right,lf=goals.LeftFoot,rf=goals.RightFoot,torso=goals.Torso;float drop=goals.Drop;
                    bool solveHands=!entry.UpperBody;
                    hips.position=hip-Vector3.up*drop;
                    Turn(a,HumanBodyBones.Spine,torso*.55f);Turn(a,HumanBodyBones.Chest,torso*.45f);
                    if(entry.Id.StartsWith("Swim"))Turn(a,HumanBodyBones.Head,new Vector3(-torso.x*.65f,-torso.y*.5f,-torso.z*.5f));
                    if(entry.Id=="Interact")Turn(a,HumanBodyBones.Head,Vector3.right*(10*Pulse(t)));
                    if(entry.Id=="Repair")Turn(a,HumanBodyBones.Head,Vector3.right*(10-4*RaccoonActionHandsV37.RepairLift(t)));
                    if(!entry.UpperBody){Limb(a,true,true,lf,fr[0]);Limb(a,false,true,rf,fr[1]);}
                    if(entry.Id.StartsWith("Swim"))for(int side=0;side<2;side++)feet[side].rotation=RaccoonSwimPoseV40.FootOffset(entry.Id,t,side==0)*fr[side];
                    if(solveHands){Limb(a,true,false,left,handR[0]);Limb(a,false,false,right,handR[1]);}
                    if(!entry.KeepWeapon&&!entry.UpperBody)
                    for(int side=0;side<2;side++)
                    {
                        bool isLeft=side==0;var hand=side==0?lh:rh;
                        var upper=a.GetBoneTransform(isLeft?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);var lower=a.GetBoneTransform(isLeft?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                        Vector3 pole=upper.position+new Vector3(isLeft?-.35f:.35f,-.35f,-.18f);
                        if(entry.Id=="Neutral")pole=upper.position+RaccoonNeutralPoseV39.PoleOffset(isLeft);
                        if(entry.Id.StartsWith("Swim"))pole=upper.position+RaccoonSwimPoseV40.ElbowPole(isLeft);
                        if(entry.Prop=="Hammer")pole=upper.position+(isLeft?RaccoonNeutralPoseV39.PoleOffset(true):RaccoonHammerPoseV42.Pole);
                        Vector3 target=isLeft?left:right;RaccoonEquipmentPose.SolveArm(upper,lower,hand,target,pole);
                        var rotation=anatomicalHands.Rotation(a,model.transform,entry,t,isLeft);float contact=anatomicalHands.ContactWeight(entry,t,isLeft);
                        if(contact>0)for(int solve=0;solve<3;solve++){RaccoonEquipmentPose.SolveArm(upper,lower,hand,Vector3.Lerp(target,anatomicalHands.SurfaceWrist(a,model.transform,isLeft,rotation),contact),pole);rotation=anatomicalHands.Rotation(a,model.transform,entry,t,isLeft);}
                        hand.rotation=rotation;
                    }
                    var pose=new HumanPose();handler.GetHumanPose(ref pose);
                    if(!entry.KeepWeapon&&!entry.UpperBody)
                    for(int m=55;m<HumanTrait.MuscleCount;m++)
                    {
                        string name=HumanTrait.MuscleName[m];
                        if(m>=75&&(entry.Prop=="Revolver"||entry.Prop=="Harpoon"))continue;
                        pose.muscles[m]=RaccoonActionHandsV37.Finger(entry.Id,entry.Prop,t,m,name);
                    }
                    Vector3 centre=a.transform.InverseTransformPoint(hips.position)/a.humanScale;centre.y=0;pose.bodyPosition-=centre;
                    float time=t*entry.Duration;
                    for(int m=0;m<HumanTrait.MuscleCount;m++)keys[m].Add(new Keyframe(time,pose.muscles[m]));
                    float[] root={pose.bodyPosition.x,pose.bodyPosition.y,pose.bodyPosition.z,pose.bodyRotation.x,pose.bodyRotation.y,pose.bodyRotation.z,pose.bodyRotation.w};
                    for(int m=0;m<7;m++)keys[HumanTrait.MuscleCount+m].Add(new Keyframe(time,root[m]));
                }
                string[] rootNames={"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"};
                for(int k=0;k<keys.Length;k++)
                {
                    var curve=new AnimationCurve(Simplify(keys[k]));
                    for(int n=0;n<curve.length;n++){AnimationUtility.SetKeyLeftTangentMode(curve,n,AnimationUtility.TangentMode.ClampedAuto);AnimationUtility.SetKeyRightTangentMode(curve,n,AnimationUtility.TangentMode.ClampedAuto);}
                    if(entry.Loop)
                    {var first=curve[0];var last=curve[curve.length-1];last.value=first.value;float slope=(keys[k][1].value-keys[k][keys[k].Count-2].value)/(2*entry.Duration/count);first.inTangent=first.outTangent=last.inTangent=last.outTangent=slope;curve.MoveKey(0,first);curve.MoveKey(curve.length-1,last);}
                    string property=k<HumanTrait.MuscleCount?HumanTrait.MuscleName[k]:rootNames[k-HumanTrait.MuscleCount];
                    if(k>=55&&k<HumanTrait.MuscleCount){int first=property.IndexOf(' '),second=property.IndexOf(' ',first+1);property=property.Substring(0,first)+"Hand."+property.Substring(first+1,second-first-1)+"."+property.Substring(second+1);}
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),property),curve);
                }
                clip.EnsureQuaternionContinuity();var s=AnimationUtility.GetAnimationClipSettings(clip);s.loopTime=entry.Loop;s.loopBlend=false;s.keepOriginalPositionXZ=true;s.keepOriginalPositionY=true;s.keepOriginalOrientation=true;AnimationUtility.SetAnimationClipSettings(clip,s);
                return Save(clip,Folder+"/"+clip.name+".anim");
            }
            finally{Object.DestroyImmediate(model);}
        }
        [MenuItem("Tools/Train Survival/Raccoon/Build Actions Review v36")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play before building.");
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var library=ScriptableObject.CreateInstance<RaccoonActionLibraryV36>();library.Entries=Entries();
            foreach(var e in library.Entries)e.Clip=Author(e);
            library.ContextPoses=new[]{library.Entries.First(e=>e.Id=="Neutral"),
                E("HammerIdle","해머 기본 자세",1,true,false,false,"Hammer"),
                E("RevolverIdle","리볼버 기본 자세",1,true,false,false,"Revolver"),
                E("HarpoonIdle","작살총 기본 자세",1,true,false,false,"Harpoon"),
                E("FoodIdle","음식 기본 자세",1,true,false,false,"Food"),
                library.Entries.First(e=>e.Id=="Carry"),library.Entries.First(e=>e.Id=="FishingWait")};
            foreach(var e in library.ContextPoses)if(e.Clip==null)e.Clip=Author(e);
            library=Save(library,Folder+"/Raccoon_ActionLibrary_v36.asset");
            string cp=Folder+"/AC_Actions_Base_v36.controller";
            if(!File.Exists(cp))AssetDatabase.CopyAsset(RaccoonJumpSetupV32.Folder+"/AC_Jump_Base_v32.controller",cp);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(cp);
            for(int i=controller.layers.Length-1;i>=0;i--)if(controller.layers[i].name.StartsWith("Actions ")||controller.layers[i].name=="Review Context V38")controller.RemoveLayer(i);
            // A persistent full-body baseline prevents the shotgun locomotion layer from
            // leaking through when an unarmed/tool action fades out.
            controller.AddLayer("Review Context V38");
            var contextLayers=controller.layers;var contextLayer=contextLayers.Last();contextLayer.defaultWeight=1;controller.layers=contextLayers;
            foreach(var e in library.ContextPoses){var state=contextLayer.stateMachine.AddState(e.Id);state.motion=e.Clip;state.speed=0;state.writeDefaultValues=false;}
            var mask=new AvatarMask{name="Upper body actions V36"};for(int i=0;i<(int)AvatarMaskBodyPart.LastBodyPart;i++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i,i!=(int)AvatarMaskBodyPart.Root&&i!=(int)AvatarMaskBodyPart.LeftLeg&&i!=(int)AvatarMaskBodyPart.RightLeg&&i!=(int)AvatarMaskBodyPart.LeftFootIK&&i!=(int)AvatarMaskBodyPart.RightFootIK);
            mask=Save(mask,Folder+"/AM_Upper_v36.mask");
            foreach(bool upper in new[]{true,false})
            {
                controller.AddLayer(upper?"Actions Upper V36":"Actions Full V36");var layers=controller.layers;var l=layers.Last();l.defaultWeight=0;l.avatarMask=upper?mask:null;controller.layers=layers;
                foreach(var e in library.Entries.Where(x=>x.UpperBody==upper)){var state=l.stateMachine.AddState(e.Id);state.motion=e.Clip;state.speed=0;state.writeDefaultValues=false;}
            }
            EditorUtility.SetDirty(controller);
            var overrides=new AnimatorOverrideController(controller);var previous=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(RaccoonJumpSetupV32.Folder+"/AC_Jump_v32.overrideController");
            foreach(var clip in controller.animationClips.Distinct()){var replacement=previous[clip.name];if(replacement!=null)overrides[clip.name]=replacement;}
            var saved=Save(overrides,Folder+"/AC_Actions_v36.overrideController");
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonCombatSetupV33.PrefabPath));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name="PC_Raccoon_Actions_v36";
            var oldPose=root.GetComponent<RaccoonCombatPoseV33>();var combat=root.AddComponent<RaccoonWeaponPoseV36>();combat.Settings=oldPose.Settings;combat.Shell=oldPose.Shell;combat.Flash=oldPose.Flash;combat.ReceiverFromGrip=oldPose.ReceiverFromGrip;Object.DestroyImmediate(oldPose);
            Object.DestroyImmediate(root.GetComponent<RaccoonCombatMotorV33>());var oldRig=root.GetComponentInChildren<RaccoonCombatRigV33>();var model=oldRig.gameObject;Object.DestroyImmediate(oldRig);
            model.GetComponent<Animator>().runtimeAnimatorController=saved;model.AddComponent<RaccoonActionRigV36>();root.AddComponent<RaccoonActionMotorV36>();
            var actions=root.AddComponent<RaccoonActionReviewV36>();actions.Library=library;actions.Weapon=root.GetComponentsInChildren<MeshFilter>(true).First(x=>x.sharedMesh!=null&&x.sharedMesh.name=="Raccoon_Shotgun_Grip").gameObject;
            RaccoonWeaponSetupV36.Build(root,combat,actions.Weapon.transform);
            BuildProps(root,actions);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(RaccoonCombatSetupV33.ScenePath,OpenSceneMode.Single);Object.DestroyImmediate(Object.FindFirstObjectByType<RaccoonCombatMotorV33>().gameObject);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));player.transform.position=new Vector3(-10,.03f,0);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("output/raccoon-actions-v36");File.WriteAllText("output/raccoon-actions-v36/actions.json",JsonUtility.ToJson(library,true));Debug.Log("RACCOON_ACTIONS_V36_BUILT "+library.Entries.Length);
        }
        public static void RebuildNeutralV39()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play before rebuilding neutral.");
            var library=AssetDatabase.LoadAssetAtPath<RaccoonActionLibraryV36>(Folder+"/Raccoon_ActionLibrary_v36.asset");
            var entry=library.Entries.First(e=>e.Id=="Neutral");entry.Clip=Author(entry);
            entry.Source="Relaxed unarmed pose v39: centred torso, anatomy-relative arm targets and relaxed digits";
            var baseline=library.ContextPoses.First(e=>e.Id=="Neutral");baseline.Clip=entry.Clip;baseline.Source=entry.Source;
            EditorUtility.SetDirty(library);AssetDatabase.SaveAssets();
        }
        public static void RebuildSwimV40()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play before rebuilding swimming.");
            var library=AssetDatabase.LoadAssetAtPath<RaccoonActionLibraryV36>(Folder+"/Raccoon_ActionLibrary_v36.asset");
            var entries=library.Entries.ToList();
            foreach(var candidate in Entries().Where(e=>e.Id.StartsWith("Swim")))
            {
                var entry=entries.FirstOrDefault(e=>e.Id==candidate.Id);
                if(entry==null){entry=candidate;entries.Add(entry);}
                entry.Duration=entry.Id=="SwimMove"?2f:candidate.Duration;
                entry.Clip=Author(entry);entry.Source="New directional unarmed swimming v40; no production swimming dash exists";
            }
            library.Entries=entries.ToArray();EditorUtility.SetDirty(library);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/AC_Actions_Base_v36.controller");
            var layer=controller.layers.First(l=>l.name=="Actions Full V36");
            foreach(var entry in entries.Where(e=>e.Id.StartsWith("Swim")))
            {
                var state=layer.stateMachine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==entry.Id)??layer.stateMachine.AddState(entry.Id);
                state.motion=entry.Clip;state.speed=0;state.writeDefaultValues=false;
            }
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        }
        public static void RebuildShotgunV41()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play before rebuilding shotgun actions.");
            var library=AssetDatabase.LoadAssetAtPath<RaccoonActionLibraryV36>(Folder+"/Raccoon_ActionLibrary_v36.asset");var entries=library.Entries.ToList();
            foreach(var candidate in Entries().Where(e=>e.KeepWeapon&&RaccoonShotgunActionsV41.IsAction(e.Id)))
            {
                var entry=entries.FirstOrDefault(e=>e.Id==candidate.Id);
                if(entry==null){entry=candidate;entries.Add(entry);}
                entry.Label=candidate.Label;entry.Duration=candidate.Duration;entry.UpperBody=false;entry.KeepWeapon=true;
                entry.Clip=Author(entry);if(entry.Id!="ShotgunDeath")entry.Source="Shotgun action v41: full-body weight response and rigid two-hand grip";
            }
            library.Entries=entries.ToArray();EditorUtility.SetDirty(library);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/AC_Actions_Base_v36.controller");
            foreach(var layer in controller.layers.Where(l=>l.name.StartsWith("Actions ")))
            foreach(var state in layer.stateMachine.states.ToArray())
                if(RaccoonShotgunActionsV41.IsAction(state.state.name))layer.stateMachine.RemoveState(state.state);
            var full=controller.layers.First(l=>l.name=="Actions Full V36");
            foreach(var entry in entries.Where(e=>e.KeepWeapon&&RaccoonShotgunActionsV41.IsAction(e.Id)))
            {var state=full.stateMachine.AddState(entry.Id);state.motion=entry.Clip;state.speed=0;state.writeDefaultValues=false;}
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        }
        public static void RebuildHammerV42()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play before rebuilding hammer.");
            var library=AssetDatabase.LoadAssetAtPath<RaccoonActionLibraryV36>(Folder+"/Raccoon_ActionLibrary_v36.asset");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/AC_Actions_Base_v36.controller");
            foreach(var entry in library.Entries.Concat(library.ContextPoses).Where(e=>e.Prop=="Hammer"))
            {
                if(entry.Id=="Repair")entry.Duration=RaccoonHammerPoseV42.Duration;
                entry.Clip=Author(entry);entry.Source="Hammer v42: anatomical grip, stable elbow and short face-driven repair stroke";
                foreach(var layer in controller.layers)
                foreach(var state in layer.stateMachine.states.Where(s=>s.state.name==entry.Id))state.state.motion=entry.Clip;
            }
            EditorUtility.SetDirty(library);EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        }
        static bool CentredProp(RaccoonActionLibraryV36.Entry entry)=>entry.Prop=="Food"||entry.Prop=="Bundle"||entry.Prop=="Rod";
        public static void RebuildPropPostureV47()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop play before rebuilding prop posture.");
            var library=AssetDatabase.LoadAssetAtPath<RaccoonActionLibraryV36>(Folder+"/Raccoon_ActionLibrary_v36.asset");
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/AC_Actions_Base_v36.controller");
            var entries=library.Entries.Concat(library.ContextPoses).Where(CentredProp).ToArray();
            foreach(var group in entries.GroupBy(e=>e.Id))
            {
                var clip=Author(group.First());
                foreach(var entry in group)
                {
                    entry.Clip=clip;
                    entry.Source="Prop posture v47: centred torso without clavicle levelling; existing task timing and hand targets retained";
                }
                foreach(var layer in controller.layers)
                foreach(var state in layer.stateMachine.states.Where(s=>s.state.name==group.Key))
                {state.state.motion=clip;EditorUtility.SetDirty(state.state);}
            }
            EditorUtility.SetDirty(library);EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
        }
        static void BuildProps(GameObject root,RaccoonActionReviewV36 actions)
        {
            var a=root.GetComponentInChildren<Animator>();var right=a.GetBoneTransform(HumanBodyBones.RightHand);var left=a.GetBoneTransform(HumanBodyBones.LeftHand);
            var props=new List<GameObject>();
            void Prop(string name,string search,Transform parent,float size)
            {
                var path=AssetDatabase.FindAssets(search+" t:Model").Select(AssetDatabase.GUIDToAssetPath).FirstOrDefault();if(path==null)return;
                var obj=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));obj.name=name;obj.transform.SetParent(parent,false);
                foreach(var c in obj.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);
                var renders=obj.GetComponentsInChildren<Renderer>();Bounds b=renders[0].bounds;foreach(var r in renders)b.Encapsulate(r.bounds);
                float scale=size/Mathf.Max(b.size.x,Mathf.Max(b.size.y,b.size.z));obj.transform.localScale*=scale;obj.transform.localPosition=Vector3.zero;
                string materialName=name=="Hammer"?"M_Weapon_RepairHammer":name=="Food"?"M_Res_RawFood":"M_Prop_StorageBundle";
                var mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/"+materialName+".mat");foreach(var r in renders)r.sharedMaterial=mat;
                props.Add(obj);obj.SetActive(false);
            }
            Prop("Hammer","Weapon_RepairHammer",right,.42f);Prop("Food","Res_RawFood",right,.10f);Prop("Bundle","Prop_StorageBundle",right,.46f);
            var profile=a.GetComponent<RaccoonEquipmentPose>().Profile;
            foreach(var item in new[]{Game.Gameplay.Inventory.HotbarItemType.Harpoon,Game.Gameplay.Inventory.HotbarItemType.Revolver})
            {profile.TryGetEntry(item,out var e);var fitted=RaccoonEquipmentSetup.Attach(right,item,e.Socket);fitted.name=item.ToString();props.Add(fitted);fitted.SetActive(false);}
            var rod=GameObject.CreatePrimitive(PrimitiveType.Cylinder);rod.name="Rod";Object.DestroyImmediate(rod.GetComponent<Collider>());rod.transform.SetParent(right,false);rod.transform.localScale=new Vector3(.012f,.5f,.012f);rod.transform.localPosition=new Vector3(0,.4f,0);props.Add(rod);rod.SetActive(false);
            actions.Props=props.ToArray();
            var ladder=new GameObject("Ladder review guide");ladder.transform.SetParent(root.transform,false);
            void Bar(Vector3 pos,Vector3 scale){var x=GameObject.CreatePrimitive(PrimitiveType.Cube);Object.DestroyImmediate(x.GetComponent<Collider>());x.transform.SetParent(ladder.transform,false);x.transform.localPosition=pos;x.transform.localScale=scale;}
            Bar(new Vector3(-.36f,1.1f,.5f),new Vector3(.035f,2.5f,.035f));Bar(new Vector3(.36f,1.1f,.5f),new Vector3(.035f,2.5f,.035f));for(int i=0;i<18;i++)Bar(new Vector3(0,.16f+i*.13f,.5f),new Vector3(.72f,.025f,.025f));actions.Ladder=ladder;ladder.SetActive(false);
            var water=GameObject.CreatePrimitive(PrimitiveType.Cube);water.name="Waterline guide";Object.DestroyImmediate(water.GetComponent<Collider>());water.transform.SetParent(root.transform,false);water.transform.localPosition=new Vector3(0,.83f,0);water.transform.localScale=new Vector3(2,.012f,2);actions.Water=water;water.SetActive(false);
            var waterMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));waterMat.SetColor("_BaseColor",new Color(.05f,.38f,.5f,.24f));waterMat.SetFloat("_Surface",1);waterMat.SetFloat("_SrcBlend",5);waterMat.SetFloat("_DstBlend",10);waterMat.SetFloat("_ZWrite",0);waterMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");waterMat.renderQueue=3000;water.GetComponent<Renderer>().sharedMaterial=Save(waterMat,Folder+"/M_ReviewWater_v36.mat");
            var work=GameObject.CreatePrimitive(PrimitiveType.Cube);work.name="Work surface guide";Object.DestroyImmediate(work.GetComponent<Collider>());work.transform.SetParent(root.transform,false);work.transform.localPosition=new Vector3(0,.87f,.72f);work.transform.localScale=new Vector3(1.1f,.1f,.5f);actions.WorkTarget=work;work.SetActive(false);
        }
    }
}


