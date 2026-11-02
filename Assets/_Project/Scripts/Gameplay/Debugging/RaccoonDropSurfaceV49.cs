using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    // Prepared in the editor so released props do not require CPU-readable GPU meshes.
    [DisallowMultipleComponent]
    public sealed class RaccoonDropSurfaceV49 : MonoBehaviour
    {
        [SerializeField] private Mesh source;
        [SerializeField] private Vector3[] points;
        public bool Matches(Mesh mesh)=>source==mesh&&points!=null&&points.Length>0;
        public void SetSurface(Mesh mesh,Vector3[] surface){source=mesh;points=surface;}

        public static Vector3[] ReadSurface(MeshFilter filter)
        {
            var mesh=filter.sharedMesh;
            if(mesh==null)return System.Array.Empty<Vector3>();
            var cached=filter.GetComponent<RaccoonDropSurfaceV49>();
            if(cached!=null&&cached.Matches(mesh))return cached.points;
            if(mesh.isReadable)
            {
                var vertices=mesh.vertices;var used=new HashSet<int>(mesh.triangles);var surface=new List<Vector3>(used.Count);
                foreach(int i in used)surface.Add(vertices[i]);
                if(surface.Count>0)return surface.ToArray();
            }
            // Unknown unreadable props still settle safely; never request their buffers.
            var bounds=mesh.bounds;var corners=new Vector3[8];
            for(int i=0;i<8;i++)corners[i]=bounds.center+Vector3.Scale(bounds.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            return corners;
        }
    }
}
