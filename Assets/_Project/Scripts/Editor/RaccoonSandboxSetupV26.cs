using System.IO;
using System.Linq;
using Game.Gameplay.Debugging;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonSandboxSetupV26
    {
        public const string Folder=RaccoonEquipmentSetup.Art+"/PlayableCandidate_v26";
        public const string PrefabPath=Folder+"/PC_Raccoon_Candidate_v26.prefab";
        public const string ScenePath="Assets/_Project/Scenes/Raccoon_PlayableCandidate_v26.unity";
        public static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            string basePath=Folder+"/AC_Candidate_Base_v26.controller";
            if(!File.Exists(basePath))AssetDatabase.CopyAsset(RaccoonEquipmentSetup.Art+"/AC_Raccoon_Base_v7.controller",basePath);
            var baseController=AssetDatabase.LoadAssetAtPath<AnimatorController>(basePath);
            var tree=AssetDatabase.LoadAllAssetsAtPath(basePath).OfType<BlendTree>().First(t=>t.name=="Locomotion");
            var children=tree.children;for(int i=0;i<children.Length;i++)if(Mathf.Approximately(children[i].threshold,4.5f))children[i].timeScale=1;
            tree.children=children;EditorUtility.SetDirty(tree);
            RestoreApprovedRun();
            string overridePath=Folder+"/AC_Candidate_v26.overrideController";
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
            if(controller==null){controller=new AnimatorOverrideController(baseController);AssetDatabase.CreateAsset(controller,overridePath);}
            var production=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(RaccoonEquipmentSetup.Art+"/AC_Raccoon.overrideController");
            foreach(var source in baseController.animationClips.Distinct())
                controller[source.name]=source.name.EndsWith(":walk")
                    ?AssetDatabase.LoadAssetAtPath<AnimationClip>(RaccoonEquipmentSetup.Art+"/WalkCandidates_v23/Raccoon_Walk_armed_calm.anim")
                    :production[source.name]??source;
            EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
            RaccoonContinuityV27.Build();
            var root=new GameObject("PC_Raccoon_Candidate_v26");root.layer=2;
            var cc=root.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.23f;cc.skinWidth=.025f;cc.stepOffset=.22f;cc.slopeLimit=45;
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonEquipmentSetup.Art+"/CH_Raccoon_Player.prefab"),root.transform);
            var animator=model.GetComponent<Animator>();animator.runtimeAnimatorController=controller;
            foreach(var behaviour in model.GetComponents<MonoBehaviour>())behaviour.enabled=false;
            model.AddComponent<RaccoonCandidateRigV26>();root.AddComponent<RaccoonSandboxV26>();
            var pose=model.GetComponent<RaccoonEquipmentPose>();pose.Profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
            RaccoonEquipmentSetup.Attach(animator.GetBoneTransform(HumanBodyBones.RightHand),HotbarItemType.Shotgun,entry.Socket);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);

            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Material Material(string name,Color color)
            {
                string path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
                mat.SetColor("_BaseColor",color);mat.SetFloat("_Smoothness",.12f);EditorUtility.SetDirty(mat);return mat;
            }
            var groundMat=Material("M_CandidateGround",new Color(.23f,.29f,.33f));
            var rampMat=Material("M_CandidateRamp",new Color(.23f,.46f,.49f));
            var crossMat=Material("M_CandidateCrossSlope",new Color(.53f,.40f,.22f));
            void Box(string name,Vector3 p,Vector3 scale,Quaternion rotation,Material mat)
            {
                var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetPositionAndRotation(p,rotation);box.transform.localScale=scale;box.GetComponent<Renderer>().sharedMaterial=mat;
            }
            Box("Flat review area",new Vector3(0,-.5f,35),new Vector3(80,1,120),Quaternion.identity,groundMat);
            float rise=Mathf.Tan(12*Mathf.Deg2Rad)*4;
            foreach(bool up in new[]{true,false})
            {
                var rotation=Quaternion.Euler(up?-12:12,0,0);
                Box(up?"Uphill 12 degrees":"Downhill 12 degrees",new Vector3(0,rise*.5f,up?16:21.5f)-rotation*Vector3.up*.15f,new Vector3(6,.3f,4/Mathf.Cos(12*Mathf.Deg2Rad)),rotation,rampMat);
            }
            Box("Upper landing",new Vector3(0,rise*.5f,18.75f),new Vector3(6,rise,1.5f),Quaternion.identity,rampMat);
            var tilt=Quaternion.Euler(0,0,10);
            Box("Cross slope 10 degrees",new Vector3(12,1,26)-tilt*Vector3.up*.15f,new Vector3(6,.3f,30),tilt,crossMat);
            // Lane markers make travel and foot sliding visible without changing surface collision.
            for(int z=-10;z<90;z+=2)Box("Distance marker",new Vector3(-10,.001f,z),new Vector3(5,.002f,.025f),Quaternion.identity,rampMat);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));player.transform.position=new Vector3(-10,.1f,0);
            var camera=new GameObject("Candidate Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.16f,.2f);camera.nearClipPlane=.03f;
            camera.transform.SetPositionAndRotation(new Vector3(-6,2.8f,4),Quaternion.LookRotation(new Vector3(-4,-1.8f,-4)));
            camera.gameObject.AddComponent<AudioListener>();var orbit=camera.gameObject.AddComponent<ViewLabOrbitCamera>();var so=new SerializedObject(orbit);so.FindProperty("_startPitch").floatValue=20;so.ApplyModifiedPropertiesWithoutUndo();
            var light=new GameObject("Review Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(45,-30,0);light.shadows=LightShadows.Soft;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.72f);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            Debug.Log("RACCOON_SANDBOX_V26_BUILT "+ScenePath);
        }
        [MenuItem("Tools/Train Survival/Raccoon/Open Playable Candidate v26")]
        public static void Open()
        {
            EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            EditorApplication.ExecuteMenuItem("Window/General/Game");
            EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;
        }

        public static void RestoreApprovedRun()
        {
            string path=Folder+"/AC_Candidate_Base_v26.controller";
            var tree=AssetDatabase.LoadAllAssetsAtPath(path).OfType<BlendTree>().First(t=>t.name=="Locomotion");
            var children=tree.children;
            for(int i=0;i<children.Length;i++)
            {
                if(!Mathf.Approximately(children[i].threshold,7))continue;
                children[i].timeScale=1;
                if(children[i].motion is BlendTree run)
                {
                    var variants=run.children;
                    for(int j=0;j<variants.Length;j++)variants[j].timeScale=1;
                    run.children=variants;EditorUtility.SetDirty(run);
                }
            }
            tree.children=children;EditorUtility.SetDirty(tree);AssetDatabase.SaveAssets();
        }
    }
}
