using System.IO;
using System.Linq;
using Game.Gameplay.Debugging;
using Game.Gameplay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonJumpSetupV32
    {
        public const string Folder="Assets/_Project/Art/Characters/Raccoon/JumpCandidate_v32";
        public const string PrefabPath=Folder+"/PC_Raccoon_Jump_v32.prefab";
        public const string ScenePath="Assets/_Project/Scenes/Raccoon_JumpReview_v32.unity";
        private static AnimationClip AuthorPose(string name,float hipDrop,float footLift,float footBack)
        {
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonEquipmentSetup.Art+"/CH_Raccoon_Player.prefab"));
            try
            {
                foreach(var b in model.GetComponents<MonoBehaviour>())b.enabled=false;
                var a=model.GetComponent<Animator>();a.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RaccoonDirectionalSetupV29.Folder+"/AC_Directional_v29.overrideController");a.Rebind();a.Update(0);
                var feet=new[]{a.GetBoneTransform(HumanBodyBones.LeftFoot),a.GetBoneTransform(HumanBodyBones.RightFoot)};
                var toes=new[]{a.GetBoneTransform(HumanBodyBones.LeftToes),a.GetBoneTransform(HumanBodyBones.RightToes)};
                var anchors=feet.Select(f=>f.position).ToArray();var rotations=feet.Select(f=>f.rotation).ToArray();var toeRotations=toes.Select(f=>f.localRotation).ToArray();
                var hips=a.GetBoneTransform(HumanBodyBones.Hips);Vector3 hip=hips.position;
                a.SetFloat("Speed",4.5f);a.SetFloat("MoveZ",1);a.SetLayerWeight(a.GetLayerIndex("Hold"),0);a.Play("Locomotion",0,0);a.Update(0);
                hips.position=hip-Vector3.up*hipDrop;
                for(int side=0;side<2;side++)
                {
                    var upper=a.GetBoneTransform(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                    var lower=a.GetBoneTransform(side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                    // Keep knees forward and feet under the body. The airborne tuck is modest
                    // because the character is carrying a two-handed weapon.
                    Vector3 target=anchors[side]+Vector3.up*footLift+Vector3.back*footBack;
                    RaccoonEquipmentPose.SolveArm(upper,lower,feet[side],target,upper.position+Vector3.forward*.6f);
                    feet[side].rotation=rotations[side];toes[side].localRotation=toeRotations[side];
                }
                using(var handler=new HumanPoseHandler(a.avatar,model.transform))
                {
                    var pose=new HumanPose();handler.GetHumanPose(ref pose);
                    Vector3 centre=model.transform.InverseTransformPoint(hips.position)/a.humanScale;centre.y=0;pose.bodyPosition-=centre;
                    var clip=new AnimationClip{name="Raccoon_Jump_"+name+"_v32",frameRate=60};
                    void Curve(string key,float v)=>AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),key),AnimationCurve.Constant(0,1,v));
                    for(int i=0;i<HumanTrait.MuscleCount;i++)Curve(HumanTrait.MuscleName[i],pose.muscles[i]);
                    Curve("RootT.x",pose.bodyPosition.x);Curve("RootT.y",pose.bodyPosition.y);Curve("RootT.z",pose.bodyPosition.z);
                    Curve("RootQ.x",pose.bodyRotation.x);Curve("RootQ.y",pose.bodyRotation.y);Curve("RootQ.z",pose.bodyRotation.z);Curve("RootQ.w",pose.bodyRotation.w);
                    var s=AnimationUtility.GetAnimationClipSettings(clip);s.loopTime=true;s.loopBlend=false;s.keepOriginalPositionXZ=true;s.keepOriginalPositionY=true;s.keepOriginalOrientation=true;s.loopBlendPositionXZ=true;s.loopBlendPositionY=true;s.loopBlendOrientation=true;AnimationUtility.SetAnimationClipSettings(clip,s);
                    string path=Folder+"/"+clip.name+".anim";var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                    if(saved==null){AssetDatabase.CreateAsset(clip,path);saved=clip;}else{EditorUtility.CopySerialized(clip,saved);Object.DestroyImmediate(clip);EditorUtility.SetDirty(saved);}
                    return saved;
                }
            }
            finally{Object.DestroyImmediate(model);}
        }
        [MenuItem("Tools/Train Survival/Raccoon/Build Jump Review v32")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var clips=new[]{AuthorPose("Takeoff",.10f,0,0),AuthorPose("Rise",.025f,.07f,.035f),AuthorPose("Apex",.025f,.15f,.065f),AuthorPose("Fall",.015f,.025f,-.01f),AuthorPose("Land",.12f,0,0)};
            string cp=Folder+"/AC_Jump_Base_v32.controller";
            if(!File.Exists(cp))AssetDatabase.CopyAsset(RaccoonDirectionalSetupV29.Folder+"/AC_Directional_Base_v29.controller",cp);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(cp);
            if(!controller.parameters.Any(p=>p.name=="JumpPhaseV32"))controller.AddParameter("JumpPhaseV32",AnimatorControllerParameterType.Float);
            var layer=controller.layers.FirstOrDefault(l=>l.name=="Jump V32");
            if(layer==null)
            {
                controller.AddLayer("Jump V32");layer=controller.layers.Last();
                var state=layer.stateMachine.AddState("Collision driven jump");
                var tree=new BlendTree{name="Jump poses V32",blendType=BlendTreeType.Simple1D,blendParameter="JumpPhaseV32",useAutomaticThresholds=false};
                AssetDatabase.AddObjectToAsset(tree,controller);for(int i=0;i<clips.Length;i++)tree.AddChild(clips[i],i);
                state.motion=tree;layer.stateMachine.defaultState=state;
            }
            EditorUtility.SetDirty(controller);
            var overrides=new AnimatorOverrideController(controller);var previous=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(RaccoonDirectionalSetupV29.Folder+"/AC_Directional_v29.overrideController");
            foreach(var clip in controller.animationClips.Distinct()){var replacement=previous[clip.name];if(replacement!=null)overrides[clip.name]=replacement;}
            string op=Folder+"/AC_Jump_v32.overrideController";var saved=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(op);
            if(saved==null){AssetDatabase.CreateAsset(overrides,op);saved=overrides;}else{EditorUtility.CopySerialized(overrides,saved);Object.DestroyImmediate(overrides);EditorUtility.SetDirty(saved);}
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonDirectionalSetupV29.PrefabPath));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name="PC_Raccoon_Jump_v32";
            Object.DestroyImmediate(root.GetComponent<RaccoonDirectionalMotorV29>());var old=root.GetComponentInChildren<RaccoonDirectionalRigV29>();var model=old.gameObject;Object.DestroyImmediate(old);
            model.GetComponent<Animator>().runtimeAnimatorController=saved;model.AddComponent<RaccoonJumpRigV32>();root.AddComponent<RaccoonJumpMotorV32>();
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(RaccoonDirectionalSetupV29.ScenePath,OpenSceneMode.Single);
            var oldPlayer=Object.FindFirstObjectByType<RaccoonDirectionalMotorV29>();Object.DestroyImmediate(oldPlayer.gameObject);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));player.transform.position=new Vector3(-10,.03f,0);
            var mat=AssetDatabase.LoadAssetAtPath<Material>(RaccoonSandboxSetupV26.Folder+"/M_CandidateRamp.mat");
            void Box(string name,Vector3 p,Vector3 size)
            {var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.position=p;box.transform.localScale=size;box.GetComponent<Renderer>().sharedMaterial=mat;}
            Box("Drop ledge 1m",new Vector3(0,.5f,3),new Vector3(4,1,4));
            Box("Low ceiling collision check",new Vector3(10,2.3f,0),new Vector3(4,.2f,5));
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("RACCOON_JUMP_V32_BUILT");
        }
        [MenuItem("Tools/Train Survival/Raccoon/Open Jump Review v32")]
        public static void Open(){EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;}
    }
}
