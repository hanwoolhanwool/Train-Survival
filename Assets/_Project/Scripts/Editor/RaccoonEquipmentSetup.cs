using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Game.Gameplay.Player;
using Game.Gameplay.Inventory;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonEquipmentSetup
    {
        public const string Art = "Assets/_Project/Art/Characters/Raccoon";
        public const string Output = "output/raccoon-grip-v5";
        public static void BuildAndIntegrate() { Build(); Integrate(); }
        public static void RefineGrip() { Build(); ExportGripReview(); SaveScene(); ReviewMotion(); }
        [Serializable] private sealed class Exchange { public PoseExchange[] poses; }
        [Serializable] private sealed class BoneExchange { public string name; public Vector3 position; public Quaternion rotation,restRotation; }
        [Serializable] private sealed class MeshExchange { public string name,source; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
        [Serializable] private sealed class PoseExchange { public string name; public BoneExchange[] bones; public MeshExchange[] weapons; public float[] shapes; }

        public static void ExportGripReview()
        {
            var poses=new System.Collections.Generic.List<PoseExchange>();
            foreach(string name in new[]{"Revolver","Shotgun","Revolver_LeftHarpoon","Fist"})
            {
                GameObject character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/CH_Raccoon_Player.prefab"));
                try
                {
                    Animator animator=character.GetComponent<Animator>();animator.runtimeAnimatorController=null;
                    var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();
                    Transform[] bones=skin.bones;Quaternion[] rest=bones.Select(b=>b.rotation).ToArray();
                    var pose=character.GetComponent<RaccoonEquipmentPose>();
                    HotbarItemType item=name=="Shotgun"?HotbarItemType.Shotgun:HotbarItemType.Revolver;
                    bool left=name=="Revolver_LeftHarpoon"||name=="Fist";
                    pose.Apply(item,left,0,10,character.transform);
                    character.GetComponent<RaccoonElbowCorrectives>().Apply();animator.enabled=false;
                    var weapons=new System.Collections.Generic.List<GameObject>();
                    if(name!="Fist")
                    {
                        pose.Profile.TryGetEntry(item,out var entry);
                        weapons.Add(Attach(animator.GetBoneTransform(HumanBodyBones.RightHand),item,entry.Socket));
                        if(left)weapons.Add(Attach(animator.GetBoneTransform(HumanBodyBones.LeftHand),HotbarItemType.Harpoon,pose.Profile.LeftHarpoonSocket));
                    }
                    poses.Add(new PoseExchange { name=name,
                        bones=bones.Select((b,i)=>new BoneExchange {name=b.name,position=b.position,rotation=b.rotation,restRotation=rest[i]}).ToArray(),
                        shapes=Enumerable.Range(0,skin.sharedMesh.blendShapeCount).Select(skin.GetBlendShapeWeight).ToArray(),
                        weapons=weapons.SelectMany(w=>w.GetComponentsInChildren<MeshFilter>()).Select(m=>new MeshExchange {name=m.name,source=m.name.Replace("(Clone)",""),vertices=m.sharedMesh.vertices.Select(m.transform.TransformPoint).ToArray(),uv=m.sharedMesh.uv,triangles=m.sharedMesh.triangles}).ToArray()
                    });
                    Transform hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                    Vector3 aim=hand.position+new Vector3(-.02f,0,.08f);
                    Render(character,name+"_hand_large.png",aim,new Vector3(1,.35f,1),.24f);
                    Render(character,name+"_palm_large.png",aim,new Vector3(-1,.3f,.5f),.24f);
                }
                finally{Object.DestroyImmediate(character);}
            }
            File.WriteAllText(Output+"/blender_grip_exchange.json",JsonUtility.ToJson(new Exchange {poses=poses.ToArray()},false));
        }
        public static void FinalReview()
        {
            var profile=AssetDatabase.LoadAssetAtPath<RaccoonEquipmentProfile>(Art+"/RaccoonEquipmentProfile.asset");
            profile.HeadNeutralPitch=-16f;profile.HandPositionPitchWeight=.35f;profile.MinimumGripForward=.46f;
            EditorUtility.SetDirty(profile);AssetDatabase.SaveAssets();
            Review();SaveScene();ReviewMotion();
        }

        public static void ReviewMotion()
        {
            foreach(HotbarItemType item in new[]{HotbarItemType.Revolver,HotbarItemType.Shotgun,HotbarItemType.Harpoon})
            foreach(float pitch in new[]{-85f,-60f,0f,60f,85f})
            {
                GameObject character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/CH_Raccoon_Player.prefab"));
                try
                {
                    Animator animator=character.GetComponent<Animator>();animator.Rebind();animator.Update(0);
                    animator.SetFloat("Speed",0);animator.SetInteger("HoldPose",item==HotbarItemType.Shotgun?2:1);
                    animator.SetLayerWeight(animator.GetLayerIndex("Hold"),1);animator.Update(.6f);
                    var pose=character.GetComponent<RaccoonEquipmentPose>();
                    animator.GetBoneTransform(HumanBodyBones.Chest).rotation=Quaternion.AngleAxis(pitch*pose.Profile.ChestPitchWeight,Vector3.right)*animator.GetBoneTransform(HumanBodyBones.Chest).rotation;
                    animator.GetBoneTransform(HumanBodyBones.Head).rotation=Quaternion.AngleAxis(pose.Profile.HeadPitchFor(animator)+pitch*pose.Profile.HeadPitchWeight,Vector3.right)*animator.GetBoneTransform(HumanBodyBones.Head).rotation;
                    pose.Apply(item,false,pitch,10,character.transform);
                    pose.Profile.TryGetEntry(item,out var entry);Attach(animator.GetBoneTransform(HumanBodyBones.RightHand),item,entry.Socket);
                    character.GetComponent<RaccoonElbowCorrectives>().Apply();animator.enabled=false;
                    Render(character,"RuntimePose_"+item+"_"+pitch+".png",new Vector3(0,1.02f,.2f),new Vector3(2.5f,.3f,3.5f),1.16f);
                    if(item==HotbarItemType.Shotgun&&pitch==0)Render(character,"RuntimePose_Shotgun_grip.png",new Vector3(.1f,1.08f,.6f),new Vector3(2,.5f,2.5f),.43f);
                }
                finally{Object.DestroyImmediate(character);}
            }
            Debug.Log("RACCOON_MOTION_REVIEW_COMPLETE");
        }

        [MenuItem("Tools/Train Survival/Raccoon/Integrate Player")]
        public static void Integrate()
        {
            const string path="Assets/_Project/Prefabs/Player.prefab";
            GameObject player=PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view=player.GetComponent<PlayerCharacterView>();var so=new SerializedObject(view);
                GameObject model=so.FindProperty("_raccoonModel").objectReferenceValue as GameObject;
                if(model==null)
                {
                    model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/CH_Raccoon_Player.prefab"),player.transform);
                    model.name="Raccoon";
                }
                model.transform.localPosition=Vector3.zero;model.transform.localRotation=Quaternion.identity;model.transform.localScale=Vector3.one;
                Animator animator=model.GetComponent<Animator>();
                so.FindProperty("_useRaccoon").boolValue=true;
                so.FindProperty("_raccoonModel").objectReferenceValue=model;
                so.FindProperty("_raccoonAnimator").objectReferenceValue=animator;
                so.FindProperty("_raccoonRenderer").objectReferenceValue=model.GetComponentInChildren<SkinnedMeshRenderer>();
                foreach(string field in new[]{"_girlModel","_manModel"})
                    if(so.FindProperty(field).objectReferenceValue is GameObject old)old.SetActive(false);
                so.ApplyModifiedPropertiesWithoutUndo();
                var aim=new SerializedObject(player.GetComponent<PlayerAimView>());
                aim.FindProperty("_handedness").objectReferenceValue=AssetDatabase.LoadAssetAtPath<WeaponHandednessSettings>("Assets/_Project/Data/WeaponHandednessSettings.asset");
                aim.ApplyModifiedPropertiesWithoutUndo();
                var poseDriver=model.GetComponent<WeaponHoldPoseDriver>()??model.AddComponent<WeaponHoldPoseDriver>();
                var pd=new SerializedObject(poseDriver);
                pd.FindProperty("_settings").objectReferenceValue=AssetDatabase.LoadAssetAtPath<WeaponHoldSettings>("Assets/_Project/Data/WeaponHoldSettings.asset");
                pd.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(player,path);
            }
            finally{PrefabUtility.UnloadPrefabContents(player);}
            SaveScene();AssetDatabase.SaveAssets();Debug.Log("RACCOON_PLAYER_INTEGRATED");
        }

        private static void SaveScene()
        {
            Scene previous=SceneManager.GetActiveScene();
            bool empty=Application.isBatchMode&&string.IsNullOrEmpty(previous.path)&&!previous.isDirty;
            Scene scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,empty?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                string[] cases={"Revolver","Shotgun","Harpoon","Revolver_LeftHarpoon"};
                for(int i=0;i<cases.Length;i++)
                {
                    var source=AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/QA_Fitted_"+cases[i]+".prefab");
                    var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
                    instance.transform.position=new Vector3((i-1.5f)*2.3f,0,0);
                }
                var camera=new GameObject("Equipment Review Camera").AddComponent<Camera>();camera.tag="MainCamera";
                camera.orthographic=true;camera.orthographicSize=3.1f;camera.transform.position=new Vector3(0,2.4f,8);camera.transform.LookAt(new Vector3(0,1,0));
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.18f,.20f,.22f);
                var light=new GameObject("Key Light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.transform.rotation=Quaternion.Euler(35,-35,0);
                var fill=new GameObject("Fill Light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fill.transform.rotation=Quaternion.Euler(25,145,0);
                GameObject floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Review Ground";floor.transform.localScale=new Vector3(2,1,1.2f);
                floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Art+"/M_ReviewGround.mat");
                EditorSceneManager.SaveScene(scene,Art+"/Raccoon_EquipmentReview.unity");
            }
            finally{if(previous.IsValid())SceneManager.SetActiveScene(previous);if(SceneManager.sceneCount>1)EditorSceneManager.CloseScene(scene,true);}
        }

        [MenuItem("Tools/Train Survival/Raccoon/Build Equipment Fit")]
        public static void Build()
        {
            Directory.CreateDirectory(Output);
            GameObject character = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art + "/CH_Raccoon_Review.prefab"));
            try
            {
                Animator animator = character.GetComponent<Animator>();
                var controller = animator.runtimeAnimatorController;
                animator.runtimeAnimatorController = null;
                string path = Art + "/RaccoonEquipmentProfile.asset";
                var profile = AssetDatabase.LoadAssetAtPath<RaccoonEquipmentProfile>(path);
                if (profile == null) { profile = ScriptableObject.CreateInstance<RaccoonEquipmentProfile>(); AssetDatabase.CreateAsset(profile, path); }
                profile.RightShoulder = animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                profile.LeftShoulder = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
                var right = MakeHand(animator, false, new Vector3(.31f, 1.04f, .56f), new Vector3(.50f,.94f,.29f), false);
                var shotgunRight = MakeHand(animator, false, new Vector3(.19f,1.10f,.43f), new Vector3(.46f,.89f,.27f), true);
                SetTriggerFinger(animator,shotgunRight);
                var shotgunLeft = MakeHand(animator, true, new Vector3(.03f,1.035f,.82f), new Vector3(-.35f,.88f,.40f), true);
                shotgunLeft.Rotation = Quaternion.LookRotation(Vector3.right,Vector3.forward)*shotgunLeft.Rotation;
                var harpoonRight = MakeHand(animator, false, new Vector3(.36f,1.04f,.53f), new Vector3(.51f,.93f,.30f), true);
                profile.LeftHarpoon = MakeHand(animator, true, new Vector3(-.36f,1.04f,.53f), new Vector3(-.51f,.93f,.30f), true);
                profile.Entries = new[] {
                    new RaccoonEquipmentProfile.Entry { Item=HotbarItemType.Revolver, Right=right,
                        Socket=MakeSocket(right,false,new Vector3(0,.065f,.058f),2.1f) },
                    new RaccoonEquipmentProfile.Entry { Item=HotbarItemType.Shotgun, Right=shotgunRight, Left=shotgunLeft, TwoHanded=true,
                        Socket=MakeSocket(shotgunRight,false,new Vector3(0,.205f,.23f),.85f) },
                    new RaccoonEquipmentProfile.Entry { Item=HotbarItemType.Harpoon, Right=harpoonRight,
                        Socket=MakeSocket(harpoonRight,false,new Vector3(0,.14f,.10f),.85f) }
                };
                profile.Entries[1].Socket.Position+=Quaternion.Inverse(shotgunRight.Rotation)*new Vector3(0,-.035f,-.05f);
                profile.LeftHarpoonSocket=MakeSocket(profile.LeftHarpoon,true,new Vector3(0,.14f,.10f),.85f);
                profile.Entries[0].MeshOverride=BuildRevolverGripMesh();
                profile.Entries[1].MeshOverride=BuildShotgunGripMesh();
                EditorUtility.SetDirty(profile);
                var pose=character.GetComponent<RaccoonEquipmentPose>() ?? character.AddComponent<RaccoonEquipmentPose>();pose.Bind(profile);
                var tail=character.GetComponent<RaccoonTailMotion>() ?? character.AddComponent<RaccoonTailMotion>();tail.Bind(animator);
                if(character.GetComponent<RaccoonGrounding>()==null)character.AddComponent<RaccoonGrounding>();
                var narrowTail=AssetDatabase.LoadAssetAtPath<Mesh>(RaccoonTailRootSetup.MeshPath);
                var surroundTail=AssetDatabase.LoadAssetAtPath<Mesh>(RaccoonTailSurroundSetup.MeshPath);
                var roundTail=narrowTail??AssetDatabase.LoadAssetAtPath<Mesh>(RaccoonTailRebuildSetup.MeshPath);
                var correctedSkin=roundTail??AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/MESH_Raccoon_Tail_v9.asset")??AssetDatabase.LoadAssetAtPath<Mesh>(Art+"/MESH_Raccoon_Tail_v8.asset");
                if(correctedSkin!=null)
                {
                    var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();skin.sharedMesh=correctedSkin;
                    if(roundTail!=null)skin.sharedMaterials=skin.sharedMaterials.Take(roundTail.subMeshCount-1).Concat(new[]{AssetDatabase.LoadAssetAtPath<Material>(narrowTail!=null?RaccoonTailRootSetup.MaterialPath:RaccoonTailRebuildSetup.MaterialPath)}).ToArray();
                }
                if(surroundTail!=null)
                {
                    var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();skin.sharedMesh=surroundTail;
                    skin.sharedMaterials=skin.sharedMaterials.Take(surroundTail.subMeshCount-2).Concat(new[]{AssetDatabase.LoadAssetAtPath<Material>(RaccoonTailSurroundSetup.MaterialPath),AssetDatabase.LoadAssetAtPath<Material>(RaccoonTailSurroundSetup.SurroundMaterialPath)}).ToArray();
                }
                var roundedTail=AssetDatabase.LoadAssetAtPath<Mesh>(RaccoonTailRoundSetup.MeshPath);
                if(roundedTail!=null && surroundTail!=null)character.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh=roundedTail;
                animator.runtimeAnimatorController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Art+"/AC_Raccoon.overrideController")??controller;
                PrefabUtility.SaveAsPrefabAsset(character,Art+"/CH_Raccoon_Player.prefab");
            }
            finally {Object.DestroyImmediate(character);}
            AssetDatabase.SaveAssets();
            Review();
        }

        private static Mesh BuildShotgunGripMesh()
        {
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Weapon_Shotgun.fbx"));
            try
            {
                MeshFilter filter=source.GetComponentInChildren<MeshFilter>();Mesh mesh=Object.Instantiate(filter.sharedMesh);
                mesh.name="Raccoon_Shotgun_Grip";Vector3[] vertices=mesh.vertices;
                Vector3[] original=mesh.normals;float[] weights=new float[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 p=filter.transform.TransformPoint(vertices[i]);
                    float weight=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.15f,.23f,p.y)))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.19f,p.z));
                    weights[i]=weight;p.y-=.05f*weight;
                    vertices[i]=filter.transform.InverseTransformPoint(p);
                }
                mesh.vertices=vertices;mesh.RecalculateNormals();Vector3[] normals=mesh.normals;
                for(int i=0;i<normals.Length;i++)normals[i]=Vector3.Slerp(original[i],normals[i],weights[i]).normalized;
                mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();
                string path=Art+"/Raccoon_Shotgun_Grip.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;}
                AssetDatabase.CreateAsset(mesh,path);return mesh;
            }
            finally{Object.DestroyImmediate(source);}
        }

        private static Mesh BuildRevolverGripMesh()
        {
            var source=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Weapon_Revolver.fbx"));
            try
            {
                MeshFilter filter=source.GetComponentInChildren<MeshFilter>();Mesh mesh=Object.Instantiate(filter.sharedMesh);
                mesh.name="Raccoon_Revolver_Grip";Vector3[] vertices=mesh.vertices;
                Vector3[] originalNormals=mesh.normals;float[] weights=new float[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 p=filter.transform.TransformPoint(vertices[i]);
                    float weight=(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.09f,.135f,p.y)))*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.015f,.035f,p.z));
                    weights[i]=weight;
                    p.x*=Mathf.Lerp(1,.5f,weight);
                    p.z=Mathf.Lerp(p.z,.058f+(p.z-.058f)*.6f,weight);
                    vertices[i]=filter.transform.InverseTransformPoint(p);
                }
                mesh.vertices=vertices;mesh.RecalculateNormals();
                Vector3[] normals=mesh.normals;
                for(int i=0;i<normals.Length;i++)normals[i]=Vector3.Slerp(originalNormals[i],normals[i],weights[i]).normalized;
                mesh.normals=normals;mesh.RecalculateTangents();mesh.RecalculateBounds();
                string path=Art+"/Raccoon_Revolver_Grip.asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(existing!=null){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(existing);return existing;}
                AssetDatabase.CreateAsset(mesh,path);return mesh;
            }
            finally{Object.DestroyImmediate(source);}
        }

        private static void SetTriggerFinger(Animator animator,RaccoonEquipmentProfile.HandPose pose)
        {
            Transform hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 forward=(animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position-hand.position).normalized;
            Vector3 up=Vector3.ProjectOnPlane(animator.GetBoneTransform(HumanBodyBones.RightIndexProximal).position-animator.GetBoneTransform(HumanBodyBones.RightLittleProximal).position,forward).normalized;
            Vector3 inward=Vector3.Cross(forward,up);
            Vector3[] directions={forward*.87f+inward*.2f-up*.45f,forward*.45f+inward*.65f-up*.6f,-forward*.2f+inward*.8f-up*.3f};
            Quaternion parent=hand.rotation;
            for(int i=3;i<6;i++)
            {
                Transform bone=animator.GetBoneTransform(RaccoonEquipmentPose.RightFingers[i]);
                Vector3 tangent=i<5?animator.GetBoneTransform(RaccoonEquipmentPose.RightFingers[i+1]).position-bone.position:bone.position-bone.parent.position;
                Quaternion world=Quaternion.FromToRotation(tangent,directions[i-3].normalized)*bone.rotation;
                pose.Fingers[i]=Quaternion.Inverse(parent)*world;parent=world;
            }
        }

        private static RaccoonEquipmentProfile.HandPose MakeHand(Animator animator,bool left,Vector3 position,Vector3 hint,bool closedIndex)
        {
            Transform hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            Transform middle=animator.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
            Transform index=animator.GetBoneTransform(left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal);
            Transform little=animator.GetBoneTransform(left?HumanBodyBones.LeftLittleProximal:HumanBodyBones.RightLittleProximal);
            Vector3 forward=(middle.position-hand.position).normalized;
            Vector3 up=Vector3.ProjectOnPlane(index.position-little.position,forward).normalized;
            Quaternion alignment=Quaternion.Inverse(Quaternion.LookRotation(forward,up));
            var pose=new RaccoonEquipmentProfile.HandPose {Position=position,ElbowHint=hint,Rotation=alignment*hand.rotation};
            Vector3 inward=Vector3.Cross(forward,up)*(left?-1:1);
            HumanBodyBones[] bones=left?RaccoonEquipmentPose.LeftFingers:RaccoonEquipmentPose.RightFingers;
            Quaternion thumbWorld=Quaternion.identity;
            for(int i=0;i<bones.Length;i++)
            {
                Transform bone=animator.GetBoneTransform(bones[i]);
                Vector3 axis=bone.InverseTransformDirection(Vector3.Cross(forward,inward).normalized);
                // Curl around the handle: the distal phalanges must reach its far side.
                float[][] curls={new[]{55f,70f,25f},new[]{65f,70f,30f},new[]{70f,72f,30f},new[]{75f,75f,35f}};
                float angle=i<3?0:curls[(i-3)/3][i%3];
                if(i<3)
                {
                    Transform next=i<2?animator.GetBoneTransform(bones[i+1]):null;
                    Vector3 tangent=next!=null?(next.position-bone.position).normalized:forward;
                    axis=bone.InverseTransformDirection(Vector3.Cross(tangent,(middle.position-bone.position).normalized).normalized);
                    angle=new[]{12f,30f,25f}[i];
                }
                pose.Fingers[i]=bone.localRotation*Quaternion.AngleAxis(angle,axis);
                if(i<3)
                {
                    Transform next=i<2?animator.GetBoneTransform(bones[i+1]):null;
                    Vector3 direction=next!=null?next.position-bone.position:bone.position-bone.parent.position;
                    Vector3[] opposition={inward*.95f+up*.25f,inward*.8f-up*.6f,-forward*.3f-up*.97f};
                    Quaternion world=Quaternion.FromToRotation(direction,opposition[i].normalized)*bone.rotation;
                    Quaternion parent=i==0?bone.parent.rotation:thumbWorld;
                    pose.Fingers[i]=Quaternion.Inverse(parent)*world;
                    thumbWorld=world;
                }
            }
            return pose;
        }

        private static RaccoonEquipmentProfile.SocketPose MakeSocket(RaccoonEquipmentProfile.HandPose hand,bool left,Vector3 naturalGrip,float scale)
        {
            Quaternion natural=Quaternion.Euler(-90,0,0);
            Quaternion facing=Quaternion.Euler(0,180,0);
            Vector3 palm=new Vector3(left?.025f:-.025f,0,.075f);
            return new RaccoonEquipmentProfile.SocketPose {
                Position=Quaternion.Inverse(hand.Rotation)*(palm-facing*(naturalGrip*scale)),
                Rotation=Quaternion.Inverse(hand.Rotation)*facing*natural,
                Scale=Vector3.one*scale
            };
        }

        [MenuItem("Tools/Train Survival/Raccoon/Review Equipment Fit")]
        public static void Review()
        {
            Directory.CreateDirectory(Output);
            foreach(string name in new[]{"Revolver","Shotgun","Harpoon","Revolver_LeftHarpoon"})
            {
                GameObject character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/CH_Raccoon_Player.prefab"));
                character.name="QA_Fitted_"+name;
                try
                {
                    Animator animator=character.GetComponent<Animator>();animator.runtimeAnimatorController=null;
                    var pose=character.GetComponent<RaccoonEquipmentPose>();
                    HotbarItemType item=name=="Shotgun"?HotbarItemType.Shotgun:name=="Harpoon"?HotbarItemType.Harpoon:HotbarItemType.Revolver;
                    bool left=name.Contains("LeftHarpoon");
                    pose.Apply(item,left,0,1,character.transform);
                    pose.Profile.TryGetEntry(item,out var entry);
                    Attach(animator.GetBoneTransform(HumanBodyBones.RightHand),item,entry.Socket);
                    if(left)Attach(animator.GetBoneTransform(HumanBodyBones.LeftHand),HotbarItemType.Harpoon,pose.Profile.LeftHarpoonSocket);
                    character.GetComponent<RaccoonElbowCorrectives>().Apply();
                    animator.enabled=false;pose.enabled=false;character.GetComponent<RaccoonElbowCorrectives>().enabled=false;
                    PrefabUtility.SaveAsPrefabAsset(character,Art+"/"+character.name+".prefab");
                    Render(character,name+"_full.png",new Vector3(0,.99f,.14f),new Vector3(2.4f,.3f,3.5f),1.14f);
                    Render(character,name+"_grip.png",new Vector3(.16f,1.08f,.64f),new Vector3(2,.6f,2.4f),.39f);
                    Render(character,name+"_side.png",new Vector3(.16f,1.08f,.64f),new Vector3(3,.2f,0),.39f);
                }
                finally {Object.DestroyImmediate(character);}
            }
            AssetDatabase.SaveAssets();Debug.Log("RACCOON_FIT_REVIEW_COMPLETE");
        }

        internal static GameObject Attach(Transform hand,HotbarItemType item,RaccoonEquipmentProfile.SocketPose pose)
        {
            string model=item==HotbarItemType.Revolver?"Weapon_Revolver":item==HotbarItemType.Shotgun?"Weapon_Shotgun":"Weapon_Harpoon_Launcher";
            GameObject weapon=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/"+model+".fbx"));
            var profile=AssetDatabase.LoadAssetAtPath<RaccoonEquipmentProfile>(Art+"/RaccoonEquipmentProfile.asset");
            if(profile.TryGetEntry(item,out var fitted)&&fitted.MeshOverride!=null)weapon.GetComponentInChildren<MeshFilter>().sharedMesh=fitted.MeshOverride;
            Transform socket=new GameObject("FittedSocket").transform;socket.SetParent(hand,false);
            Vector3 s=hand.lossyScale;socket.localScale=new Vector3(1/s.x,1/s.y,1/s.z);
            Vector3 scale=weapon.transform.localScale;weapon.transform.SetParent(socket,false);
            weapon.transform.localPosition=pose.Position;weapon.transform.localRotation=pose.Rotation;weapon.transform.localScale=Vector3.Scale(scale,pose.Scale);
            string material=item==HotbarItemType.Harpoon?"M_Weapon_Harpoon":"M_"+model;
            Material mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/"+material+".mat");
            foreach(Renderer r in weapon.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
            return weapon;
        }

        internal static void Render(GameObject character,string filename,Vector3 aim,Vector3 direction,float size,string outputFolder=null,int resolution=900)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                GameObject copy=Object.Instantiate(character);preview.AddSingleGO(copy);
                Transform[] transforms=copy.GetComponentsInChildren<Transform>();
                Quaternion[] rotations=transforms.Select(t=>t.localRotation).ToArray();
                foreach(MonoBehaviour b in copy.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                foreach(Animator a in copy.GetComponentsInChildren<Animator>())a.enabled=false;
                for(int i=0;i<transforms.Length;i++)transforms[i].localRotation=rotations[i];
                if(outputFolder!=null)
                {
                    GameObject floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*.5f;
                    floor.GetComponent<Renderer>().sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Art+"/M_ReviewGround.mat");
                    preview.AddSingleGO(floor);
                }
                preview.camera.orthographic=true;preview.camera.orthographicSize=size;
                preview.camera.transform.SetPositionAndRotation(aim+direction,Quaternion.LookRotation(-direction));
                preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=30;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.18f,.20f,.22f);
                preview.ambientColor=new Color(.5f,.5f,.5f);
                preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(35,210,0);
                preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(25,30,0);
                for(int pass=0;pass<2;pass++)
                {
                    preview.BeginStaticPreview(new Rect(0,0,resolution,resolution));preview.Render(true);Texture2D image=preview.EndStaticPreview();
                    if(pass==1)File.WriteAllBytes((outputFolder??Output)+"/"+filename,image.EncodeToPNG());Object.DestroyImmediate(image);
                }
            }
            finally {preview.Cleanup();}
        }

        [MenuItem("Tools/Train Survival/Raccoon/Inspect Equipment Geometry")]
        public static void Inspect()
        {
            Directory.CreateDirectory(Output);
            var text = new System.Text.StringBuilder();
            GameObject character = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/CH_Raccoon_Review.prefab"));
            try
            {
                Animator animator=character.GetComponent<Animator>();animator.runtimeAnimatorController=null;
                foreach(HumanBodyBones bone in new[]{HumanBodyBones.LeftUpperArm,HumanBodyBones.RightUpperArm,HumanBodyBones.LeftLowerArm,HumanBodyBones.RightLowerArm,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.RightMiddleProximal,HumanBodyBones.RightIndexProximal,HumanBodyBones.RightLittleProximal})
                {
                    Transform t=animator.GetBoneTransform(bone);text.AppendLine(bone+" pos="+t.position.ToString("F5")+" rot="+t.rotation.eulerAngles.ToString("F4")+" localRot="+t.localRotation.eulerAngles.ToString("F4")+" scale="+t.lossyScale.ToString("F4"));
                }
            }
            finally{Object.DestroyImmediate(character);}
            foreach(string name in new[]{"Weapon_Revolver","Weapon_Shotgun","Weapon_Harpoon_Launcher"})
            {
                GameObject item=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/"+name+".fbx"));
                try
                {
                    text.AppendLine(name+" root scale="+item.transform.localScale.ToString("F6")+" rotation="+item.transform.eulerAngles.ToString("F4"));
                    foreach(MeshFilter mf in item.GetComponentsInChildren<MeshFilter>())
                    {
                        Vector3[] p=mf.sharedMesh.vertices.Select(mf.transform.TransformPoint).ToArray();Bounds b=new Bounds(p[0],Vector3.zero);foreach(Vector3 q in p)b.Encapsulate(q);
                        text.AppendLine(mf.name+" bounds="+b.min.ToString("F5")+" to "+b.max.ToString("F5")+" mesh bounds="+mf.sharedMesh.bounds+" matrix="+mf.transform.localToWorldMatrix);
                    }
                }
                finally{Object.DestroyImmediate(item);}
            }
            File.WriteAllText(Output+"/geometry-inspection.txt",text.ToString());Debug.Log(text.ToString());
        }
    }
}
