using System.IO;
using Game.Gameplay.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonTrainSetupV28
    {
        public const string Folder="Assets/_Project/Art/Characters/Raccoon/TrainCandidate_v28";
        public const string PrefabPath=Folder+"/PC_Raccoon_Train_v28.prefab";
        public const string ScenePath="Assets/_Project/Scenes/Raccoon_TrainReview_v28.unity";
        public static void Build()
        {
            Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
            var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonSandboxSetupV26.PrefabPath));
            PrefabUtility.UnpackPrefabInstance(root,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
            root.name="PC_Raccoon_Train_v28";Object.DestroyImmediate(root.GetComponent<RaccoonSandboxV26>());
            var old=root.GetComponentInChildren<RaccoonCandidateRigV26>();var model=old.gameObject;Object.DestroyImmediate(old);
            model.AddComponent<RaccoonTrainRigV28>();root.AddComponent<RaccoonTrainMotorV28>();
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);Object.DestroyImmediate(root);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            Material Mat(string name,Color color)
            {
                string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                if(m==null){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
                m.SetColor("_BaseColor",color);EditorUtility.SetDirty(m);return m;
            }
            var floor=Mat("M_Deck",new Color(.24f,.31f,.33f));var metal=Mat("M_Rail",new Color(.08f,.13f,.16f));
            var marker=Mat("M_Safety",new Color(.8f,.52f,.16f));var ground=Mat("M_Ground",new Color(.15f,.23f,.19f));
            GameObject Box(string name,Transform parent,Vector3 p,Vector3 scale,Material material,bool collision=true)
            {
                var box=GameObject.CreatePrimitive(PrimitiveType.Cube);box.name=name;box.transform.SetParent(parent,false);box.transform.localPosition=p;box.transform.localScale=scale;box.GetComponent<Renderer>().sharedMaterial=material;
                if(!collision)box.GetComponent<Collider>().enabled=false;return box;
            }
            var deck=new GameObject("Review carriage · isolated platform").AddComponent<RaccoonMovingDeckV28>();deck.Origin=Vector3.up*1.4f;deck.Sample(0,false);
            Box("Deck surface",deck.transform,new Vector3(0,-.2f,0),new Vector3(6,.4f,32),floor);
            Box("Carriage body",deck.transform,new Vector3(0,-.7f,0),new Vector3(5.3f,.7f,31),metal);
            for(int side=-1;side<=1;side+=2)
            {
                Box("Deck edge",deck.transform,new Vector3(side*2.9f,.15f,0),new Vector3(.18f,.3f,32),marker);
                for(int z=-14;z<=14;z+=4)Box("Rail post",deck.transform,new Vector3(side*2.9f,.55f,z),new Vector3(.1f,1,.1f),metal);
                Box("Hand rail",deck.transform,new Vector3(side*2.9f,1.05f,0),new Vector3(.1f,.1f,32),metal);
                Box("End stop",deck.transform,new Vector3(0,.15f,side*15.9f),new Vector3(6,.3f,.2f),marker);
            }
            for(int z=-14;z<=14;z+=2)Box("Contact reference stripe",deck.transform,new Vector3(0,.001f,z),new Vector3(5.5f,.002f,.025f),marker,false);
            var scenery=new GameObject("Scrolling scenery reference").transform;
            Box("Surrounding ground",null,new Vector3(0,-.5f,40),new Vector3(150,1,500),ground);
            for(int z=-160;z<240;z+=8)
            {
                Box("Track marker",scenery,new Vector3(0,.02f,z),new Vector3(8,.04f,.25f),metal,false);
                Box("Scenery post",scenery,new Vector3(9,1.5f,z),new Vector3(.2f,3,.2f),marker,false);
            }
            var player=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));player.transform.position=deck.transform.TransformPoint(new Vector3(0,.03f,-9));
            var review=new GameObject("Train review controls").AddComponent<RaccoonTrainReviewV28>();review.Deck=deck;review.Player=player.GetComponent<RaccoonTrainMotorV28>();review.Scenery=scenery;
            var camera=new GameObject("Review Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.nearClipPlane=.03f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.17f,.22f);camera.gameObject.AddComponent<AudioListener>();
            var orbit=camera.gameObject.AddComponent<ViewLabOrbitCamera>();var settings=new SerializedObject(orbit);settings.FindProperty("_startPitch").floatValue=18;settings.ApplyModifiedPropertiesWithoutUndo();
            var light=new GameObject("Review Sun").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.4f;light.shadows=LightShadows.Soft;light.transform.rotation=Quaternion.Euler(45,-30,0);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.68f,.72f);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        }
        [MenuItem("Tools/Train Survival/Raccoon/Open Train Review v28")]
        public static void Open(){EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);EditorApplication.ExecuteMenuItem("Window/General/Game");EditorApplication.delayCall+=()=>EditorApplication.isPlaying=true;}
    }
}
