using System.Collections.Generic;
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
    public static class RaccoonDirectionalSetupV29
    {
        public const string Folder="Assets/_Project/Art/Characters/Raccoon/DirectionalCandidate_v29";
        public const string PrefabPath=Folder+"/PC_Raccoon_Directional_v29.prefab";
        public const string ScenePath="Assets/_Project/Scenes/Raccoon_DirectionalReview_v29.unity";
        public const float Duration=7f/3;
        // The new lower-body paths are authored for their own travel directions. The approved
        // calm walk supplies the upper body; no backward playback or whole-body rotation is used.
        private static AnimationClip Author(string label,Vector2 direction,float speed)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonEquipmentSetup.Art+"/CH_Raccoon_Player.prefab"));
            try
            {
                foreach(var b in go.GetComponents<MonoBehaviour>())b.enabled=false;
                var animator=go.GetComponent<Animator>();animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(RaccoonSandboxSetupV26.Folder+"/AC_Candidate_v26.overrideController");
                animator.Rebind();animator.Update(0);
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                var feet=new[]{animator.GetBoneTransform(HumanBodyBones.LeftFoot),animator.GetBoneTransform(HumanBodyBones.RightFoot)};
                var toes=new[]{animator.GetBoneTransform(HumanBodyBones.LeftToes),animator.GetBoneTransform(HumanBodyBones.RightToes)};
                var anchors=feet.Select(f=>f.position).ToArray();var rotations=feet.Select(f=>f.rotation).ToArray();var toeRotations=toes.Select(f=>f.localRotation).ToArray();
                Vector3 hipPosition=hips.position;
                animator.SetFloat("Speed",4.5f);animator.SetFloat("RaccoonShotgunRun",1);animator.SetLayerWeight(animator.GetLayerIndex("Hold"),0);
                var values=new Dictionary<string,List<float>>();
                foreach(var name in HumanTrait.MuscleName)values[name]=new List<float>();
                foreach(var name in new[]{"RootT.x","RootT.y","RootT.z","RootQ.x","RootQ.y","RootQ.z","RootQ.w"})values[name]=new List<float>();
                using(var handler=new HumanPoseHandler(animator.avatar,go.transform))
                {
                    var pose=new HumanPose();Vector3 centre=Vector3.zero;Quaternion previous=Quaternion.identity;
                    for(int frame=0;frame<=140;frame++)
                    {
                        float n=frame/140f;animator.Play("Locomotion",0,frame==140?0:n);animator.Update(0);
                        float phase=Mathf.Repeat((n+RaccoonDirectionalRigV29.WalkCycleOffset)*2,1);
                        hips.position=hipPosition+new Vector3(.009f*Mathf.Sin(phase*Mathf.PI*2),-.055f+.004f*Mathf.Cos(phase*Mathf.PI*4),0);
                        for(int side=0;side<2;side++)
                        {
                            float p=Mathf.Repeat(phase-(side==0?.8f:.3f),1);
                            const float stance=.6f;float stride=speed*(Duration*.5f),along,height=0;
                            if(p<stance)along=stride*(stance*.5f-p);
                            else
                            {
                                float u=(p-stance)/(1-stance),u2=u*u,u3=u2*u;
                                float a=-stride*stance*.5f,b=-a,m=-stride*(1-stance);
                                along=(2*u3-3*u2+1)*a+(u3-2*u2+u)*m+(-2*u3+3*u2)*b+(u3-u2)*m;
                                height=.065f*Mathf.Pow(Mathf.Sin(Mathf.PI*u),2);
                            }
                            Vector3 target=anchors[side]+new Vector3(direction.x*along,height,direction.y*along);
                            var upper=animator.GetBoneTransform(side==0?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                            var lower=animator.GetBoneTransform(side==0?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                            RaccoonEquipmentPose.SolveArm(upper,lower,feet[side],target,upper.position+Vector3.forward*.6f);
                            feet[side].rotation=rotations[side];toes[side].localRotation=toeRotations[side];
                        }
                        handler.GetHumanPose(ref pose);
                        if(frame==0){centre=go.transform.InverseTransformPoint(hips.position)/animator.humanScale;centre.y=0;}
                        pose.bodyPosition-=centre;
                        if(frame>0&&Quaternion.Dot(previous,pose.bodyRotation)<0)pose.bodyRotation=new Quaternion(-pose.bodyRotation.x,-pose.bodyRotation.y,-pose.bodyRotation.z,-pose.bodyRotation.w);
                        previous=pose.bodyRotation;
                        for(int j=0;j<HumanTrait.MuscleCount;j++)values[HumanTrait.MuscleName[j]].Add(pose.muscles[j]);
                        values["RootT.x"].Add(pose.bodyPosition.x);values["RootT.y"].Add(pose.bodyPosition.y);values["RootT.z"].Add(pose.bodyPosition.z);
                        values["RootQ.x"].Add(pose.bodyRotation.x);values["RootQ.y"].Add(pose.bodyRotation.y);values["RootQ.z"].Add(pose.bodyRotation.z);values["RootQ.w"].Add(pose.bodyRotation.w);
                    }
                }
                var clip=new AnimationClip{name="Raccoon_"+label+"_v29",frameRate=60};
                foreach(var pair in values)
                {
                    var v=pair.Value;v[140]=v[0];var keys=new Keyframe[141];
                    for(int i=0;i<=140;i++)
                    {
                        float tangent=(v[(i+1)%140]-v[(i+139)%140])*30;
                        keys[i]=new Keyframe(i/60f,v[i],tangent,tangent);
                    }
                    AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve("",typeof(Animator),pair.Key),new AnimationCurve(keys));
                }
                var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=false;
                settings.keepOriginalPositionXZ=true;settings.keepOriginalPositionY=true;settings.keepOriginalOrientation=true;
                settings.loopBlendPositionXZ=true;settings.loopBlendPositionY=true;settings.loopBlendOrientation=true;
                AnimationUtility.SetAnimationClipSettings(clip,settings);
                string path=Folder+"/"+clip.name+".anim";var saved=AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                if(saved==null){AssetDatabase.CreateAsset(clip,path);saved=clip;}else{EditorUtility.CopySerialized(clip,saved);Object.DestroyImmediate(clip);EditorUtility.SetDirty(saved);}
                return saved;
            }
            finally{Object.DestroyImmediate(go);}
        }
        [MenuItem("Tools/Train Survival/Raccoon/Build Directional Review v29")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var left=Author("StrafeLeft",Vector2.left,.45f);var right=Author("StrafeRight",Vector2.right,.45f);var back=Author("WalkBackward",Vector2.down,.55f);
            string path=Folder+"/AC_Directional_Base_v29.controller";
            if(!File.Exists(path))AssetDatabase.CopyAsset(RaccoonSandboxSetupV26.Folder+"/AC_Candidate_Base_v26.controller",path);
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if(!controller.parameters.Any(p=>p.name=="MoveX")){controller.AddParameter("MoveX",AnimatorControllerParameterType.Float);controller.AddParameter(new AnimatorControllerParameter{name="MoveZ",type=AnimatorControllerParameterType.Float,defaultFloat=1});}
            var tree=AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().First(t=>t.name=="Locomotion");
            var children=tree.children;int index=System.Array.FindIndex(children,c=>Mathf.Approximately(c.threshold,4.5f));
            var directional=AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().FirstOrDefault(t=>t.name=="Directional walk v29");
            if(directional==null)
            {
                directional=new BlendTree{name="Directional walk v29",blendType=BlendTreeType.FreeformCartesian2D,blendParameter="MoveX",blendParameterY="MoveZ"};
                AssetDatabase.AddObjectToAsset(directional,controller);
                directional.children=new[]{new ChildMotion{motion=children[index].motion,position=Vector2.up,timeScale=1,cycleOffset=RaccoonDirectionalRigV29.WalkCycleOffset},new ChildMotion{motion=left,position=Vector2.left,timeScale=1},new ChildMotion{motion=right,position=Vector2.right,timeScale=1},new ChildMotion{motion=back,position=Vector2.down,timeScale=1}};
            }
            children[index].motion=directional;children[index].cycleOffset=0;tree.children=children;EditorUtility.SetDirty(tree);EditorUtility.SetDirty(directional);EditorUtility.SetDirty(controller);
            var overrides=new AnimatorOverrideController(controller);var previous=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(RaccoonSandboxSetupV26.Folder+"/AC_Candidate_v26.overrideController");
            foreach(var clip in controller.animationClips.Distinct()){var replacement=previous[clip.name];if(replacement!=null)overrides[clip.name]=replacement;}
            string op=Folder+"/AC_Directional_v29.overrideController";var existing=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(op);
            if(existing==null){AssetDatabase.CreateAsset(overrides,op);existing=overrides;}else{EditorUtility.CopySerialized(overrides,existing);Object.DestroyImmediate(overrides);EditorUtility.SetDirty(existing);}
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonSandboxSetupV26.PrefabPath));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            root.name="PC_Raccoon_Directional_v29";Object.DestroyImmediate(root.GetComponent<RaccoonSandboxV26>());
            var old=root.GetComponentInChildren<RaccoonCandidateRigV26>();var model=old.gameObject;Object.DestroyImmediate(old);
            model.GetComponent<Animator>().runtimeAnimatorController=existing;model.AddComponent<RaccoonDirectionalRigV29>();root.AddComponent<RaccoonDirectionalMotorV29>();
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            // Reuse the approved inspection course without saving over it.
            var scene=EditorSceneManager.OpenScene(RaccoonSandboxSetupV26.ScenePath,OpenSceneMode.Single);
            var oldPlayer=Object.FindFirstObjectByType<RaccoonSandboxV26>();var spawn=oldPlayer.transform.position;Object.DestroyImmediate(oldPlayer.gameObject);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));player.transform.position=spawn;
            EditorSceneManager.SaveScene(scene,ScenePath);Debug.Log("RACCOON_DIRECTIONAL_V29_BUILT");
        }
        [MenuItem("Tools/Train Survival/Raccoon/Open Directional Review v29")]
        public static void Open(){EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;}
    }
}
