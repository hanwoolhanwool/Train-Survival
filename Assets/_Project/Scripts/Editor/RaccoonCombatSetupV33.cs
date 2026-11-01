using System.IO;
using Game.Gameplay.Combat;
using Game.Gameplay.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonCombatSetupV33
    {
        public const string Folder="Assets/_Project/Art/Characters/Raccoon/CombatCandidate_v33";
        public const string PrefabPath=Folder+"/PC_Raccoon_Combat_v33.prefab";
        public const string ScenePath="Assets/_Project/Scenes/Raccoon_CombatReview_v33.unity";
        [MenuItem("Tools/Train Survival/Raccoon/Build Combat Review v33")]
        public static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonJumpSetupV32.PrefabPath));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);root.name="PC_Raccoon_Combat_v33";
            Object.DestroyImmediate(root.GetComponent<RaccoonJumpMotorV32>());
            var old=root.GetComponentInChildren<RaccoonJumpRigV32>();var model=old.gameObject;Object.DestroyImmediate(old);
            model.AddComponent<RaccoonCombatRigV33>();root.AddComponent<RaccoonCombatMotorV33>();
            var combat=root.AddComponent<RaccoonCombatPoseV33>();combat.Settings=AssetDatabase.LoadAssetAtPath<GunSettings>("Assets/_Project/Data/ShotgunSettings.asset");
            Material Mat(string name,Color color,bool emission=false)
            {
                string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
                m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.25f);
                if(emission){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*4);}
                EditorUtility.SetDirty(m);return m;
            }
            var red=Mat("M_ReviewShell",new Color(.55f,.055f,.025f));var brass=Mat("M_ReviewShellBase",new Color(.65f,.4f,.09f));
            Transform Part(string name,Transform parent,Vector3 scale,Material mat)
            {
                var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;Object.DestroyImmediate(go.GetComponent<Collider>());
                go.transform.SetParent(parent,false);go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;
            }
            var shell=new GameObject("Reload shell visual").transform;shell.SetParent(root.transform,false);
            Part("Shell",shell,new Vector3(.025f,.028f,.025f),red);
            Part("Brass base",shell,new Vector3(.027f,.006f,.027f),brass).localPosition=Vector3.down*.025f;
            combat.Shell=shell;shell.gameObject.SetActive(false);
            var flash=GameObject.CreatePrimitive(PrimitiveType.Sphere);flash.name="Shot flash visual";Object.DestroyImmediate(flash.GetComponent<Collider>());
            flash.transform.SetParent(root.transform,false);flash.transform.localScale=new Vector3(.045f,.045f,.12f);
            flash.GetComponent<Renderer>().sharedMaterial=Mat("M_ReviewFlash",new Color(1,.55f,.06f),true);combat.Flash=flash.transform;flash.SetActive(false);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
            var scene=EditorSceneManager.OpenScene(RaccoonJumpSetupV32.ScenePath,OpenSceneMode.Single);
            var previous=Object.FindFirstObjectByType<RaccoonJumpMotorV32>();Object.DestroyImmediate(previous.gameObject);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));player.transform.position=new Vector3(-10,.03f,0);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();Debug.Log("RACCOON_COMBAT_V33_BUILT");
        }
        [MenuItem("Tools/Train Survival/Raccoon/Open Combat Review v33")]
        public static void Open(){EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;}
    }
}
