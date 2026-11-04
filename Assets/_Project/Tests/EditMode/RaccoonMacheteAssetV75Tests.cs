using NUnit.Framework;
using UnityEngine;
using UnityEditor;

namespace Game.Tests.EditMode
{
    // Asset validation runs in Edit Mode: the preserved v74 mesh intentionally
    // has CPU Read/Write disabled at runtime.
    public sealed class RaccoonMacheteAssetV75Tests
    {
        [Test] public void MacheteV75BladeUVRemovesBlackSeamsWithoutChangingGeometry()
        {
            var old=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Meshes/Mesh_CartoonMachete_Grip_v74.asset");
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Meshes/Mesh_CartoonMachete_UVClean_v75.asset");
            var a=old.vertices;var b=mesh.vertices;var at=old.triangles;var bt=mesh.triangles;
            Assert.That(bt.Length,Is.EqualTo(at.Length));
            for(int i=0;i<at.Length;i++)Assert.That(b[bt[i]],Is.EqualTo(a[at[i]]));
            var texture=new Texture2D(2,2);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes("Assets/_Project/Art/Textures/T_Weapon_CartoonMachete_BaseColor.png"));
                int before=0,after=0;
                foreach(var candidate in new[]{old,mesh})
                {
                    var uv=candidate.uv;var tr=candidate.triangles;var v=candidate.vertices;int black=0;
                    for(int i=0;i<tr.Length;i+=3)
                    {
                        int x=tr[i],y=tr[i+1],z=tr[i+2];if(Mathf.Min(v[x].y,Mathf.Min(v[y].y,v[z].y))<.09f)continue;
                        for(int j=0;j<=32;j++)for(int k=0;k<=32-j;k++)
                        {
                            var p=uv[x]*(j/32f)+uv[y]*(k/32f)+uv[z]*(1-(j+k)/32f);var c=texture.GetPixelBilinear(p.x,p.y);
                            if(Mathf.Max(c.r,Mathf.Max(c.g,c.b))<.12f)black++;
                        }
                    }
                    if(candidate==old)before=black;else after=black;
                }
                Assert.That(before,Is.GreaterThan(100));Assert.That(after,Is.Zero);
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
        }
    }
}
