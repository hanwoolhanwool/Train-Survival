using System.Collections.Generic;
using Game.Gameplay.Debugging;
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    public static class RaccoonDropSurfaceSetupV49
    {
        public static int Build()
        {
            if(EditorApplication.isPlaying)throw new System.InvalidOperationException("Stop play before preparing drop surfaces.");
            var path=RaccoonActionsSetupV36.PrefabPath;
            var root=PrefabUtility.LoadPrefabContents(path);int count=0;
            try
            {
                var review=root.GetComponent<RaccoonActionReviewV36>();
                var objects=new List<GameObject>(review.Props);objects.Add(review.Weapon);
                var done=new HashSet<MeshFilter>();
                foreach(var obj in objects)if(obj!=null)foreach(var filter in obj.GetComponentsInChildren<MeshFilter>(true))
                {
                    if(filter.sharedMesh==null||!done.Add(filter))continue;
                    // Editor MeshUtility can read imported GPU-only meshes without changing
                    // Read/Write flags or keeping their entire vertex/index buffers at runtime.
                    using(var data=MeshUtility.AcquireReadOnlyMeshData(filter.sharedMesh))
                    using(var vertices=new NativeArray<Vector3>(data[0].vertexCount,Allocator.Temp))
                    {
                        data[0].GetVertices(vertices);var used=new HashSet<int>();
                        for(int sub=0;sub<data[0].subMeshCount;sub++)
                        using(var indices=new NativeArray<int>(data[0].GetSubMesh(sub).indexCount,Allocator.Temp))
                        {data[0].GetIndices(indices,sub,true);foreach(int i in indices)used.Add(i);}
                        var surface=new List<Vector3>(used.Count);foreach(int i in used)surface.Add(vertices[i]);
                        var component=filter.GetComponent<RaccoonDropSurfaceV49>();if(component==null)component=filter.gameObject.AddComponent<RaccoonDropSurfaceV49>();
                        component.SetSurface(filter.sharedMesh,surface.ToArray());count++;
                    }
                }
                PrefabUtility.SaveAsPrefabAsset(root,path);AssetDatabase.SaveAssets();
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            return count;
        }
    }
}
