using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Player;
using Game.Gameplay.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Game.Editor
{
    /// <summary>Reproducible, isolated import and review of the raccoon. Does not change Player.prefab or open scenes.</summary>
    public static class RaccoonCharacterImport
    {
        private const string Folder = "Assets/_Project/Art/Characters/Raccoon";
        private const string Model = Folder + "/CH_Raccoon_TP.fbx";
        private const string Output = "output/raccoon-unity-v2";

        [Serializable] private sealed class ClipResult
        {
            public string path, name;
            public bool human, finite;
            public float duration, maxBoneTravel;
        }
        [Serializable] private sealed class Report
        {
            public string unityVersion, model, prefab;
            public bool avatarValid, avatarHuman;
            public int mappedHumanBones, vertices, triangles, skinBones, blendShapes;
            public Vector3 bounds;
            public string[] missingBones, blendShapeNames;
            public List<ClipResult> clips = new List<ClipResult>();
            public List<string> notes = new List<string>();
        }

        [MenuItem("Tools/Train Survival/Raccoon/Import and Validate")]
        public static void Run()
        {
            Directory.CreateDirectory(Output);
            var report = new Report { unityVersion = Application.unityVersion, model = Model };
            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
                if (importer == null) throw new InvalidOperationException("Raccoon FBX is missing.");
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.importAnimation = false;
                importer.isReadable = true;
                importer.optimizeGameObjects = false;
                importer.importBlendShapes = true;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
                importer.SaveAndReimport();
                GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
                GameObject template = Object.Instantiate(source);
                try
                {
                    var bones = template.GetComponentsInChildren<Transform>(true).GroupBy(t => t.name).ToDictionary(g => g.Key, g => g.First());
                    // Store a real T pose in the avatar description; the source bind mesh stays intact.
                    foreach (string side in new[] { "Left", "Right" })
                    {
                        Transform upper = bones[side + "UpperArm"], lower = bones[side + "LowerArm"], hand = bones[side + "Hand"];
                        float sign = Mathf.Sign(template.transform.InverseTransformPoint(upper.position).x);
                        Vector3 target = template.transform.right * sign;
                        upper.rotation = Quaternion.FromToRotation(lower.position - upper.position, target) * upper.rotation;
                        lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, target) * lower.rotation;
                        Transform middle = bones[side + "MiddleProximal"];
                        hand.rotation = Quaternion.FromToRotation(middle.position - hand.position, target) * hand.rotation;
                    }
                    var human = new List<HumanBone>();
                    foreach (string name in HumanTrait.BoneName)
                    {
                        string boneName = name.Replace(" ", "");
                        if (!bones.ContainsKey(boneName)) continue;
                        human.Add(new HumanBone { humanName = name, boneName = boneName, limit = new HumanLimit { useDefaultValues = true } });
                    }
                    var description = new HumanDescription
                    {
                        human = human.ToArray(),
                        skeleton = template.GetComponentsInChildren<Transform>(true).Select(t => new SkeletonBone
                        {
                            name = t == template.transform ? source.name : t.name,
                            position = t.localPosition, rotation = t.localRotation, scale = t.localScale
                        }).ToArray(),
                        upperArmTwist = .5f, lowerArmTwist = .5f, upperLegTwist = .5f, lowerLegTwist = .5f,
                        armStretch = .05f, legStretch = .05f, feetSpacing = 0f, hasTranslationDoF = false
                    };
                    importer.animationType = ModelImporterAnimationType.Human;
                    importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    importer.humanDescription = description;
                    importer.SaveAndReimport();
                }
                finally { Object.DestroyImmediate(template); }

                source = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
                GameObject character = Object.Instantiate(source);
                character.name = "CH_Raccoon_Review";
                try
                {
                    Animator animator = character.GetComponent<Animator>() ?? character.AddComponent<Animator>();
                    Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<Avatar>().FirstOrDefault();
                    animator.avatar = avatar;
                    report.avatarValid = avatar != null && avatar.isValid;
                    report.avatarHuman = avatar != null && avatar.isHuman;
                    if (!report.avatarValid || !report.avatarHuman) throw new InvalidOperationException("Imported Avatar is not a valid Humanoid.");
                    var missing = new List<string>();
                    foreach (HumanBone human in importer.humanDescription.human)
                    {
                        if (!Enum.TryParse(human.humanName.Replace(" ", ""), out HumanBodyBones id)) continue;
                        if (animator.GetBoneTransform(id) == null) missing.Add(human.humanName);
                        else report.mappedHumanBones++;
                    }
                    report.missingBones = missing.ToArray();
                    if (missing.Count > 0 || report.mappedHumanBones != 52)
                        throw new InvalidOperationException("Incomplete Humanoid mapping: " + string.Join(", ", missing));
                    var skin = character.GetComponentInChildren<SkinnedMeshRenderer>();
                    Mesh mesh = skin.sharedMesh;
                    report.vertices = mesh.vertexCount;
                    report.triangles = mesh.triangles.Length / 3;
                    report.skinBones = skin.bones.Length;
                    report.blendShapes = mesh.blendShapeCount;
                    report.blendShapeNames = Enumerable.Range(0, mesh.blendShapeCount).Select(mesh.GetBlendShapeName).ToArray();
                    report.bounds = skin.bounds.size;
                    skin.updateWhenOffscreen = true;
                    skin.quality = SkinQuality.Bone4;
                    Material[] materials = CreateMaterials();
                    report.notes.Add("FBX material order: " + string.Join(", ", skin.sharedMaterials.Select(m => m != null ? m.name : "null")));
                    skin.sharedMaterials = skin.sharedMaterials.Select(m =>
                    {
                        string name = m != null ? m.name : "";
                        if (name.Contains("HandsTail")) return materials[1];
                        if (name.Contains("CuffFabric")) return materials[2];
                        if (name == "Material_0") return materials[0];
                        throw new InvalidOperationException("Unknown FBX material: " + name);
                    }).ToArray();
                    var corrective = character.AddComponent<RaccoonElbowCorrectives>();
                    corrective.Bind(animator, skin);
                    animator.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Animations/AC_Player.controller");
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    report.prefab = Folder + "/CH_Raccoon_Review.prefab";
                    PrefabUtility.SaveAsPrefabAsset(character, report.prefab);
                    Render(character, "Unity_Raccoon_Rest.png");
                    ValidateClips(character, animator, corrective, report);
                    report.notes.Add("Review prefab only. Player.prefab and existing WeaponHoldSettings are unchanged.");
                    report.notes.Add("Humanoid validation and sampled clips do not certify network gameplay or equipment contact.");
                }
                finally { Object.DestroyImmediate(character); }
                AssetDatabase.SaveAssets();
                File.WriteAllText(Output + "/unity_validation.json", JsonUtility.ToJson(report, true));
                Debug.Log("RACCOON_VALIDATION_SUCCESS " + JsonUtility.ToJson(report));
            }
            catch (Exception exception)
            {
                File.WriteAllText(Output + "/unity_failure.txt", exception.ToString());
                Debug.LogException(exception);
                throw;
            }
        }

        private static Material[] CreateMaterials()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("Project URP Lit shader is missing.");
            string[] files = { "T_Raccoon_Body_BaseColor", "T_Raccoon_HandsTail_BaseColor", "T_Raccoon_Cuff_BaseColor" };
            string[] names = { "M_Raccoon_Body", "M_Raccoon_HandsTail", "M_Raccoon_Cuff" };
            var output = new Material[3];
            for (int i = 0; i < output.Length; i++)
            {
                string path = Folder + "/" + names[i] + ".mat";
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); }
                mat.shader = shader;
                mat.SetTexture("_BaseMap", Texture(files[i], false));
                mat.SetColor("_BaseColor", Color.white);
                mat.SetFloat("_Metallic", 0f);
                mat.SetFloat("_Smoothness", .24f);
                output[i] = mat;
                EditorUtility.SetDirty(mat);
            }
            Texture2D normal = Texture("T_Raccoon_Body_Normal", true);
            output[0].SetTexture("_BumpMap", normal);
            output[0].EnableKeyword("_NORMALMAP");
            // Original packed texture: G = roughness, B = metallic. URP reads metallic R and smoothness A.
            string packedPath = Folder + "/Textures/T_Raccoon_Body_PackedSource.png";
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            source.LoadImage(File.ReadAllBytes(packedPath));
            Color32[] pixels = source.GetPixels32();
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(pixels[i].b, 0, 0, (byte)(255 - pixels[i].g));
            source.SetPixels32(pixels);source.Apply();
            string packed = Folder + "/Textures/T_Raccoon_Body_MetallicSmoothness.png";
            File.WriteAllBytes(packed, source.EncodeToPNG());Object.DestroyImmediate(source);
            AssetDatabase.ImportAsset(packed, ImportAssetOptions.ForceSynchronousImport);
            var ti = (TextureImporter)AssetImporter.GetAtPath(packed);ti.sRGBTexture = false;ti.alphaSource = TextureImporterAlphaSource.FromInput;ti.SaveAndReimport();
            output[0].SetTexture("_MetallicGlossMap", AssetDatabase.LoadAssetAtPath<Texture2D>(packed));
            output[0].SetFloat("_Smoothness", 1f);output[0].EnableKeyword("_METALLICSPECGLOSSMAP");
            return output;
        }

        [Serializable] private sealed class EquipmentCheck
        {
            public string name, status;
            public float rightTargetError, leftTargetError;
            public Vector3 weaponBounds;
        }
        [Serializable] private sealed class EquipmentReport
        {
            public List<EquipmentCheck> checks = new List<EquipmentCheck>();
            public float elbowShapeAtStraight, elbowShapeAt90;
            public string scope = "Static reach and existing socket diagnostic. The left harpoon is a mirrored candidate, not the production left-hand attachment path. Finger contact is not approved.";
        }

        [MenuItem("Tools/Train Survival/Raccoon/Review Existing Equipment Fit")]
        public static void ReviewEquipment()
        {
            Directory.CreateDirectory(Output);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/CH_Raccoon_Review.prefab");
            var settings = AssetDatabase.LoadAssetAtPath<WeaponHoldSettings>("Assets/_Project/Data/WeaponHoldSettings.asset");
            var report = new EquipmentReport();
            GameObject neutral = Object.Instantiate(prefab);
            try
            {
                Animator testAnimator = neutral.GetComponent<Animator>();
                Transform upper = testAnimator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                Transform lower = testAnimator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                Transform hand = testAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                Quaternion saved = lower.rotation;
                Vector3 direction = (lower.position - upper.position).normalized;
                Vector3 axis = Vector3.Cross(direction,Vector3.forward).normalized;
                if(axis.sqrMagnitude < .01f) axis = Vector3.right;
                var corrective = neutral.GetComponent<RaccoonElbowCorrectives>();
                var testSkin = neutral.GetComponentInChildren<SkinnedMeshRenderer>();
                int shape = Enumerable.Range(0,testSkin.sharedMesh.blendShapeCount).First(i => testSkin.sharedMesh.GetBlendShapeName(i).EndsWith("COR_Elbow90.L",StringComparison.Ordinal));
                lower.rotation = Quaternion.FromToRotation(hand.position - lower.position,direction) * lower.rotation;
                corrective.Apply();report.elbowShapeAtStraight = testSkin.GetBlendShapeWeight(shape);
                lower.rotation = Quaternion.FromToRotation(hand.position - lower.position,Quaternion.AngleAxis(90,axis)*direction) * lower.rotation;
                corrective.Apply();report.elbowShapeAt90 = testSkin.GetBlendShapeWeight(shape);
                lower.rotation=saved;corrective.Apply();
                if(report.elbowShapeAtStraight > .01f || report.elbowShapeAt90 < 99.9f) throw new InvalidOperationException($"Elbow corrective runtime check failed: straight={report.elbowShapeAtStraight}, bent={report.elbowShapeAt90}.");
                Render(neutral,"Unity_Raccoon_Rest.png");
                var animationReport = new Report();
                ValidateClips(neutral, neutral.GetComponent<Animator>(), neutral.GetComponent<RaccoonElbowCorrectives>(), animationReport);
                File.WriteAllText(Output + "/animation_sample_validation.json", JsonUtility.ToJson(animationReport,true));
            }
            finally { Object.DestroyImmediate(neutral); }
            foreach (string name in new[] { "Revolver", "Shotgun", "Revolver_LeftHarpoon" })
            {
                GameObject character = Object.Instantiate(prefab);character.name="QA_ExistingFit_"+name;
                try
                {
                    Animator animator=character.GetComponent<Animator>();animator.runtimeAnimatorController=null;
                    HotbarItemType type=name=="Shotgun"?HotbarItemType.Shotgun:HotbarItemType.Revolver;
                    if(!settings.TryGetEntry(type,out WeaponHoldSettings.Entry entry))throw new InvalidOperationException("Missing equipment settings.");
                    var profile=entry.GetProfile(PlayerViewMode.SplitFpTp);Vector3 pivot=settings.GetAimPivotLocalPosition(PlayerViewMode.SplitFpTp);
                    Vector3 right=pivot+profile.RightHandLocalPosition;
                    float rightError=SolveArm(animator,false,right,pivot+profile.RightElbowHintLocalPosition);
                    Transform rightHand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                    Transform rightLower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                    rightHand.rotation=profile.StraightenWrist?WeaponHoldMath.StraightWristRotation(rightHand.position-rightLower.position,profile.RightWristRollDegrees,profile.RightWristBendDegrees,false):profile.RightHandLocalRotation;
                    GameObject weapon=AttachExistingWeapon(rightHand,type==HotbarItemType.Shotgun?"Weapon_Shotgun":"Weapon_Revolver",entry,false);
                    float leftError=0;
                    if(name=="Shotgun")
                    {
                        leftError=SolveArm(animator,true,pivot+profile.LeftHandLocalPosition,pivot+profile.LeftElbowHintLocalPosition);
                        Transform hand=animator.GetBoneTransform(HumanBodyBones.LeftHand),lower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                        hand.rotation=profile.StraightenWrist?WeaponHoldMath.StraightWristRotation(hand.position-lower.position,profile.LeftWristRollDegrees,profile.LeftWristBendDegrees,true):profile.LeftHandLocalRotation;
                    }
                    if(name=="Revolver_LeftHarpoon" && settings.TryGetEntry(HotbarItemType.Harpoon,out WeaponHoldSettings.Entry harpoon))
                    {
                        var hp=harpoon.GetProfile(PlayerViewMode.SplitFpTp);Vector3 p=hp.RightHandLocalPosition;p.x=-p.x;Vector3 hint=hp.RightElbowHintLocalPosition;hint.x=-hint.x;
                        leftError=SolveArm(animator,true,pivot+p,pivot+hint);
                        Transform hand=animator.GetBoneTransform(HumanBodyBones.LeftHand),lower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                        hand.rotation=WeaponHoldMath.StraightWristRotation(hand.position-lower.position,hp.RightWristRollDegrees,hp.RightWristBendDegrees,true);
                        AttachExistingWeapon(hand,"Weapon_Harpoon_Launcher",harpoon,true);
                    }
                    character.GetComponent<RaccoonElbowCorrectives>().Apply();
                    Bounds wb=weapon.GetComponentsInChildren<Renderer>()[0].bounds;
                    foreach(Renderer r in weapon.GetComponentsInChildren<Renderer>())wb.Encapsulate(r.bounds);
                    report.checks.Add(new EquipmentCheck{name=name,status="Existing socket fit diagnostic, fingers not yet fitted to this equipment; not gameplay approved",rightTargetError=rightError,leftTargetError=leftError,weaponBounds=wb.size});
                    animator.enabled=false;character.GetComponent<RaccoonElbowCorrectives>().enabled=false;
                    PrefabUtility.SaveAsPrefabAsset(character,Folder+"/QA_ExistingFit_"+name+".prefab");
                    Render(character,"Unity_ExistingFit_"+name+".png");
                }
                finally { Object.DestroyImmediate(character); }
            }
            File.WriteAllText(Output+"/equipment_fit_diagnostic.json",JsonUtility.ToJson(report,true));
            SaveReviewScene();
            AssetDatabase.SaveAssets();Debug.Log("RACCOON_EQUIPMENT_REVIEW_COMPLETE "+JsonUtility.ToJson(report));
        }

        [MenuItem("Tools/Train Survival/Raccoon/Create Review Scene")]
        public static void SaveReviewScene()
        {
            Scene previous = SceneManager.GetActiveScene();
            bool emptyBatchScene = Application.isBatchMode && string.IsNullOrEmpty(previous.path) && !previous.isDirty;
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,emptyBatchScene?NewSceneMode.Single:NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            try
            {
                string[] names = { "CH_Raccoon_Review", "QA_ExistingFit_Revolver", "QA_ExistingFit_Shotgun", "QA_ExistingFit_Revolver_LeftHarpoon" };
                for(int i=0;i<names.Length;i++)
                {
                    var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/"+names[i]+".prefab");
                    GameObject instance=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
                    instance.transform.position=new Vector3((i-1.5f)*2.5f,0,0);
                }
                var cameraObject=new GameObject("Review Camera");cameraObject.tag="MainCamera";
                var camera=cameraObject.AddComponent<Camera>();camera.orthographic=true;camera.orthographicSize=3.35f;
                cameraObject.transform.position=new Vector3(0,2.4f,8);cameraObject.transform.LookAt(new Vector3(0,.95f,0));
                camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.16f,.18f,.20f);
                var lightObject=new GameObject("Main Light");var light=lightObject.AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.5f;light.shadows=LightShadows.Soft;lightObject.transform.rotation=Quaternion.Euler(45,-35,0);
                var fillObject=new GameObject("Fill Light");var fill=fillObject.AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.6f;fillObject.transform.rotation=Quaternion.Euler(20,145,0);
                GameObject floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Review Ground";floor.transform.localScale=new Vector3(2,1,1.2f);
                string matPath=Folder+"/M_ReviewGround.mat";Material mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.SetColor("_BaseColor",new Color(.19f,.21f,.23f));mat.SetFloat("_Smoothness",.1f);AssetDatabase.CreateAsset(mat,matPath);}
                floor.GetComponent<Renderer>().sharedMaterial=mat;
                EditorSceneManager.SaveScene(scene,Folder+"/Raccoon_RigReview.unity");
            }
            finally
            {
                if(previous.IsValid())SceneManager.SetActiveScene(previous);
                if(SceneManager.sceneCount>1)EditorSceneManager.CloseScene(scene,true);
            }
        }

        private static float SolveArm(Animator animator,bool left,Vector3 target,Vector3 hint)
        {
            Transform upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            Transform lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            Transform hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            Vector3 start=upper.position;float a=Vector3.Distance(start,lower.position),b=Vector3.Distance(lower.position,hand.position);
            Vector3 delta=target-start;float distance=Mathf.Clamp(delta.magnitude,Mathf.Abs(a-b)+.0001f,a+b-.0001f);Vector3 direction=delta.normalized;
            Vector3 bend=Vector3.ProjectOnPlane(hint-start,direction).normalized;
            if(bend.sqrMagnitude<.001f)bend=Vector3.ProjectOnPlane(Vector3.down,direction).normalized;
            float along=(a*a-b*b+distance*distance)/(2*distance);float height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            Vector3 elbow=start+direction*along+bend*height;
            upper.rotation=Quaternion.FromToRotation(lower.position-start,elbow-start)*upper.rotation;
            lower.rotation=Quaternion.FromToRotation(hand.position-lower.position,target-lower.position)*lower.rotation;
            return Vector3.Distance(hand.position,target);
        }

        private static GameObject AttachExistingWeapon(Transform hand,string model,WeaponHoldSettings.Entry entry,bool mirrored)
        {
            var socket=new GameObject("EquipmentFitSocket").transform;socket.SetParent(hand,false);
            Vector3 s=hand.lossyScale;socket.localScale=new Vector3(1/s.x,1/s.y,1/s.z);
            GameObject weapon=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/"+model+".fbx"));
            Vector3 scale=weapon.transform.localScale;weapon.transform.SetParent(socket,false);
            Vector3 p=entry.SocketLocalPosition;if(mirrored)p.x=-p.x;
            weapon.transform.localPosition=p;weapon.transform.localRotation=entry.SocketLocalRotation;weapon.transform.localScale=Vector3.Scale(scale,entry.SocketLocalScale);
            Material mat=AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Materials/M_"+model+".mat");
            if(mat!=null)foreach(Renderer r in weapon.GetComponentsInChildren<Renderer>())r.sharedMaterials=Enumerable.Repeat(mat,r.sharedMaterials.Length).ToArray();
            return weapon;
        }

        private static Texture2D Texture(string name, bool normal)
        {
            string path = Folder + "/Textures/" + name + ".png";
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            if (ti == null) throw new FileNotFoundException(path);
            ti.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            ti.sRGBTexture = !normal;ti.maxTextureSize = 2048;ti.mipmapEnabled = true;ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static void ValidateClips(GameObject character, Animator animator, RaccoonElbowCorrectives corrective, Report report)
        {
            var transforms = character.GetComponentsInChildren<Transform>(true);
            Vector3[] positions = transforms.Select(t => t.localPosition).ToArray();
            Quaternion[] rotations = transforms.Select(t => t.localRotation).ToArray();
            Vector3[] scales = transforms.Select(t => t.localScale).ToArray();
            void Reset()
            {
                for (int i = 0; i < transforms.Length; i++) { transforms[i].localPosition = positions[i]; transforms[i].localRotation = rotations[i]; transforms[i].localScale = scales[i]; }
            }
            var paths = new[] { "Assets/_Project/Art/Models/Character_Girl.fbx", "Assets/_Project/Art/Models/Character_Man.fbx", "Assets/_Project/Art/Animations/Anim_Hold_RifleAim.fbx", "Assets/_Project/Art/Animations/Anim_Hold_Gunplay.fbx" };
            int rendered = 0;
            foreach (string path in paths)
            foreach (AnimationClip clip in AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__") && c.isHumanMotion).Take(3))
            {
                var result = new ClipResult { path = path, name = clip.name, human = clip.isHumanMotion, duration = clip.length, finite = true };
                Reset();Vector3[] before = transforms.Select(t => t.position).ToArray();
                foreach (float fraction in new[] { 0f, .25f, .5f, .75f })
                {
                    clip.SampleAnimation(character, clip.length * fraction);corrective.Apply();
                    for (int i = 0; i < transforms.Length; i++)
                    {
                        Vector3 p = transforms[i].position;
                        result.finite &= !float.IsNaN(p.x) && !float.IsNaN(p.y) && !float.IsNaN(p.z) && !float.IsInfinity(p.sqrMagnitude);
                        result.maxBoneTravel = Mathf.Max(result.maxBoneTravel, Vector3.Distance(before[i], p));
                    }
                }
                if (rendered < 3) { Render(character, "Unity_Raccoon_Clip_" + rendered + ".png");rendered++; }
                report.clips.Add(result);
            }
            Reset();corrective.Apply();
        }

        private static void Render(GameObject character, string file)
        {
            var preview = new PreviewRenderUtility();
            try
            {
                GameObject copy = Object.Instantiate(character);preview.AddSingleGO(copy);
                foreach (MonoBehaviour behaviour in copy.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                Animator animator = copy.GetComponent<Animator>();if (animator != null) animator.enabled = false;
                SkinnedMeshRenderer skin = copy.GetComponentInChildren<SkinnedMeshRenderer>();
                Transform head = copy.GetComponentsInChildren<Transform>(true).First(t => t.name == "Head");
                Transform leftFoot = copy.GetComponentsInChildren<Transform>(true).First(t => t.name == "LeftFoot");
                Transform rightFoot = copy.GetComponentsInChildren<Transform>(true).First(t => t.name == "RightFoot");
                Vector3 feet = (leftFoot.position + rightFoot.position) * .5f;
                preview.camera.orthographic = true;preview.camera.orthographicSize = 1.15f;
                Vector3 aim = (head.position + feet) * .5f + Vector3.up * .13f;
                preview.camera.transform.SetPositionAndRotation(aim + new Vector3(2.3f, .25f, 3.5f), Quaternion.LookRotation(new Vector3(-2.3f, -.25f, -3.5f)));
                preview.camera.nearClipPlane = .01f;preview.camera.farClipPlane = 30;
                preview.camera.clearFlags = CameraClearFlags.SolidColor;preview.camera.backgroundColor = new Color(.18f,.20f,.22f);
                preview.ambientColor = new Color(.5f,.5f,.5f);
                preview.lights[0].intensity = 1.2f;preview.lights[0].transform.rotation = Quaternion.Euler(35,210,0);
                preview.lights[1].intensity = .7f;preview.lights[1].transform.rotation = Quaternion.Euler(25,30,0);
                // Warm the preview pipeline before capture; the first SRP draw after import can use stale bindings.
                for(int pass=0;pass<2;pass++)
                {
                    preview.BeginStaticPreview(new Rect(0,0,900,900));preview.Render(true);Texture2D image=preview.EndStaticPreview();
                    if(pass==1)File.WriteAllBytes(Output + "/" + file,image.EncodeToPNG());
                    Object.DestroyImmediate(image);
                }
            }
            finally { preview.Cleanup(); }
        }
    }
}
