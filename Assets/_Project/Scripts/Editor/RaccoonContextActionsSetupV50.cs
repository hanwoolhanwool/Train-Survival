using Game.Gameplay.Debugging;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Game.Editor
{
    public static class RaccoonContextActionsSetupV50
    {
        public const string ScenePath="Assets/_Project/Scenes/Raccoon_ContextActions_v50.unity";
        [MenuItem("Tools/Raccoon/Create context actions candidate v50")]
        public static void Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop play mode before authoring the candidate scene.");
            if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new System.InvalidOperationException("Save the current scene before building the candidate.");
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonActionsSetupV36.PrefabPath);
            if(prefab==null)throw new System.InvalidOperationException("Preserved review prefab missing.");
            var scene=EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects,NewSceneMode.Single);
            var actor=(GameObject)PrefabUtility.InstantiatePrefab(prefab);actor.name="Raccoon · context candidate v50";
            actor.transform.position=new Vector3(0,.03f,0);
            foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            foreach(var c in actor.GetComponentsInChildren<Collider>(true))c.enabled=false;
            foreach(var a in actor.GetComponentsInChildren<AudioSource>(true))a.enabled=false;
            actor.GetComponentInChildren<Animator>().enabled=false;
            var review=actor.GetComponent<RaccoonActionReviewV36>();
            review.Water.SetActive(false);review.Ladder.SetActive(false);review.WorkTarget.SetActive(false);
            var director=new GameObject("상태별 동작 검토 v50").AddComponent<RaccoonContextActionsV50>();
            director.SourcePrefab=prefab;director.Display=review;
            var camera=Camera.main;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.15f,.19f);
            camera.transform.SetPositionAndRotation(new Vector3(2.6f,2.0f,3.2f),Quaternion.LookRotation(new Vector3(-2.6f,-.8f,-3.2f)));
            camera.nearClipPlane=.03f;
            var orbit=camera.gameObject.AddComponent<ViewLabOrbitCamera>();orbit.Frame(actor.transform,3.5f,1.05f);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Review floor";floor.transform.position=new Vector3(0,-.08f,0);floor.transform.localScale=new Vector3(20,.15f,20);
            var mat=new Material(review.WorkTarget.GetComponent<Renderer>().sharedMaterial);mat.name="Context review floor v50";mat.color=new Color(.25f,.32f,.36f);
            const string folder="Assets/_Project/Art/Characters/Raccoon/ContextActionsCandidate_v50";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/_Project/Art/Characters/Raccoon","ContextActionsCandidate_v50");
            string materialPath=folder+"/ReviewFloor.mat";
            var existing=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if(existing==null){AssetDatabase.CreateAsset(mat,materialPath);existing=mat;}else Object.DestroyImmediate(mat);
            floor.GetComponent<Renderer>().sharedMaterial=existing;
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.69f,.73f);
            var light=Object.FindFirstObjectByType<Light>();light.intensity=1.4f;light.transform.rotation=Quaternion.Euler(45,-35,0);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
        }
    }
}
