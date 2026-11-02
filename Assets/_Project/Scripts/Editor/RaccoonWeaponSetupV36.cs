using System;
using System.IO;
using System.Collections.Generic;
using Game.Gameplay.Debugging;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Game.Editor
{
    public static class RaccoonWeaponSetupV36
    {
        static string Folder=>RaccoonActionsSetupV36.Folder;
        static Mesh SaveMesh(Mesh mesh,string name)
        {mesh.name=name;string path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old==null){AssetDatabase.CreateAsset(mesh,path);return mesh;}EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);EditorUtility.SetDirty(old);return old;}
        public static void Build(GameObject root,RaccoonWeaponPoseV36 combat,Transform gun)
        {
            var mechanics=root.AddComponent<RaccoonWeaponMechanicsV36>();combat.Mechanics=mechanics;mechanics.Gun=gun;
            var original=gun.GetComponent<MeshFilter>().sharedMesh;var v=original.vertices;var uv=original.uv;var tri=original.triangles;
            var material=gun.GetComponent<Renderer>().sharedMaterial;var texture=material.GetTexture("_BaseMap");
            if(texture==null)texture=material.mainTexture;
            var rt=RenderTexture.GetTemporary(texture.width,texture.height);Graphics.Blit(texture,rt);var active=RenderTexture.active;RenderTexture.active=rt;
            var pixels=new Texture2D(rt.width,rt.height,TextureFormat.RGBA32,false);pixels.ReadPixels(new Rect(0,0,rt.width,rt.height),0,0);pixels.Apply();RenderTexture.active=active;RenderTexture.ReleaseTemporary(rt);
            var body=new List<int>();var pump=new List<int>();
            for(int i=0;i<tri.Length;i+=3)
            {
                var c=(v[tri[i]]+v[tri[i+1]]+v[tri[i+2]])/3;var tc=(uv[tri[i]]+uv[tri[i+1]]+uv[tri[i+2]])/3;Color color=pixels.GetPixelBilinear(tc.x,tc.y);
                bool gold=color.r>color.g*1.18f&&color.g>color.b*1.35f;
                bool slide=c.y>.00205f&&c.y<.00495f&&c.z>.00155f&&c.z<.00255f&&(gold||c.z<.00215f);
                var list=slide?pump:body;list.Add(tri[i]);list.Add(tri[i+1]);list.Add(tri[i+2]);
            }
            Object.DestroyImmediate(pixels);
            var bodyMesh=Object.Instantiate(original);bodyMesh.triangles=body.ToArray();bodyMesh.RecalculateBounds();gun.GetComponent<MeshFilter>().sharedMesh=SaveMesh(bodyMesh,"Shotgun_Body_v36");
            var pumpMesh=Object.Instantiate(original);pumpMesh.triangles=pump.ToArray();pumpMesh.RecalculateBounds();
            var slideObject=new GameObject("Pump moving fore-end");slideObject.transform.SetParent(gun,false);slideObject.AddComponent<MeshFilter>().sharedMesh=SaveMesh(pumpMesh,"Shotgun_Pump_v36");slideObject.AddComponent<MeshRenderer>().sharedMaterial=material;mechanics.Pump=slideObject.transform;
            BuildFeedPort(gun);
            var casing=Object.Instantiate(combat.Shell.gameObject,root.transform);casing.name="Ejected casing";casing.SetActive(false);mechanics.Casing=casing.transform;
            // A star-shaped crossed flash has a readable silhouette from oblique views.
            var flashMesh=new Mesh();var verts=new List<Vector3>();var faces=new List<int>();
            for(int plane=0;plane<3;plane++)
            {
                var rotation=Quaternion.Euler(0,0,plane*60);
                Vector3[] points={new Vector3(-.5f,0,0),new Vector3(-.18f,0,.35f),new Vector3(0,0,1),new Vector3(.18f,0,.35f),new Vector3(.5f,0,0),new Vector3(0,0,.12f)};
                int start=verts.Count;foreach(var p in points)verts.Add(rotation*p);
                for(int i=0;i<5;i++){faces.Add(start+5);faces.Add(start+i);faces.Add(start+(i+1)%5);faces.Add(start+5);faces.Add(start+(i+1)%5);faces.Add(start+i);}
            }
            flashMesh.SetVertices(verts);flashMesh.SetTriangles(faces,0);var flashUV=new Vector2[verts.Count];var planeUV=new[]{new Vector2(0,0),new Vector2(.32f,.35f),new Vector2(.5f,1),new Vector2(.68f,.35f),new Vector2(1,0),new Vector2(.5f,.12f)};for(int i=0;i<flashUV.Length;i++)flashUV[i]=planeUV[i%6];flashMesh.uv=flashUV;flashMesh.RecalculateNormals();flashMesh.RecalculateBounds();combat.Flash.GetComponent<MeshFilter>().sharedMesh=SaveMesh(flashMesh,"Shotgun_MuzzleFlash_v36");combat.Flash.localScale=new Vector3(.12f,.12f,.19f);
            string flashPath=Folder+"/M_MuzzleFlash_v36.mat";var flashMat=AssetDatabase.LoadAssetAtPath<Material>(flashPath);if(flashMat==null){flashMat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(flashMat,flashPath);}flashMat.SetColor("_BaseColor",new Color(4,1.35f,.08f));EditorUtility.SetDirty(flashMat);combat.Flash.GetComponent<Renderer>().sharedMaterial=flashMat;
            var flashTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/T_MuzzleFlash_v36.png");if(flashTexture!=null){flashMat.SetTexture("_BaseMap",flashTexture);flashMat.SetColor("_BaseColor",Color.white);flashMat.SetFloat("_Surface",1);flashMat.SetFloat("_SrcBlend",5);flashMat.SetFloat("_DstBlend",1);flashMat.SetFloat("_ZWrite",0);flashMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");flashMat.renderQueue=3000;}
            mechanics.Source=root.AddComponent<AudioSource>();mechanics.Source.playOnAwake=false;mechanics.Source.spatialBlend=0;mechanics.Source.volume=.55f;
            mechanics.FireSound=Sound("Shot",.46f,0);mechanics.OpenSound=Sound("PumpOpen",.16f,1);mechanics.CloseSound=Sound("PumpClose",.12f,2);mechanics.InsertSound=Sound("ShellInsert",.10f,3);mechanics.ImpactSound=Sound("CasingImpact",.13f,4);
            Directory.CreateDirectory("output/raccoon-actions-v36");File.WriteAllText("output/raccoon-actions-v36/weapon-build.json","{\"originalTriangles\":"+tri.Length/3+",\"bodyTriangles\":"+body.Count/3+",\"pumpTriangles\":"+pump.Count/3+",\"audio\":\"original deterministic synthesis; no third-party recordings\"}");
        }
        static void BuildFeedPort(Transform gun)
        {
            // Open, hollow rear-facing feed throat beneath the receiver; shell travels
            // along the existing magazine tube axis, so it is occluded by real geometry.
            var vertices=new List<Vector3>();var triangles=new List<int>();const int n=24;
            for(int ring=0;ring<4;ring++)for(int i=0;i<n;i++)
            {float angle=2*Mathf.PI*i/n,radius=ring<2?.000205f:.000166f;float y=ring%2==0?.00105f:.00215f;vertices.Add(new Vector3(Mathf.Cos(angle)*radius,y,.00160f+Mathf.Sin(angle)*radius));}
            void Quad(int a,int b,int c,int d){triangles.AddRange(new[]{a,b,c,a,c,d,c,b,a,d,c,a});}
            for(int i=0;i<n;i++){int next=(i+1)%n;Quad(i,next,n+next,n+i);Quad(2*n+i,3*n+i,3*n+next,2*n+next);Quad(i,2*n+i,2*n+next,next);}
            var mesh=new Mesh();mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);var normals=new Vector3[vertices.Count];for(int i=0;i<normals.Length;i++){float angle=2*Mathf.PI*(i%n)/n;normals[i]=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*(i<2*n?1:-1);}mesh.normals=normals;mesh.RecalculateBounds();
            var port=new GameObject("Open magazine feed throat");port.transform.SetParent(gun,false);port.AddComponent<MeshFilter>().sharedMesh=SaveMesh(mesh,"Shotgun_FeedThroat_v36");
            string path=Folder+"/M_FeedThroat_v36.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat==null){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}mat.SetColor("_BaseColor",new Color(.18f,.24f,.08f));mat.SetFloat("_Smoothness",.25f);EditorUtility.SetDirty(mat);port.AddComponent<MeshRenderer>().sharedMaterial=mat;
            var bridge=GameObject.CreatePrimitive(PrimitiveType.Cube);bridge.name="Feed throat receiver mount";Object.DestroyImmediate(bridge.GetComponent<Collider>());bridge.transform.SetParent(gun,false);bridge.transform.localPosition=new Vector3(0,.00168f,.00187f);bridge.transform.localScale=new Vector3(.00043f,.00094f,.00020f);bridge.GetComponent<Renderer>().sharedMaterial=mat;
        }
        static AudioClip Sound(string name,float duration,int kind)
        {
            const int rate=44100;int count=Mathf.CeilToInt(rate*duration);var samples=new float[count];var random=new System.Random(360+kind);float filter=0,peak=0;
            for(int i=0;i<count;i++)
            {
                float t=(float)i/rate,noise=(float)(random.NextDouble()*2-1);filter=Mathf.Lerp(filter,noise,kind==0?.12f:.48f);
                float env=Mathf.Exp(-t/(kind==0?.075f:.023f));float value;
                if(kind==0)value=(filter*.9f+Mathf.Sin(2*Mathf.PI*(64*t+26*t*t))*.5f)*env+noise*.16f*Mathf.Exp(-t/.007f);
                else value=(noise*.35f+filter*.25f+Mathf.Sin(2*Mathf.PI*(kind==4?2700:kind==3?1300:720)*t)*.2f)*env;
                value*=Mathf.Min(1,t/.0008f)*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(duration-.015f,duration,t)));samples[i]=value;peak=Mathf.Max(peak,Mathf.Abs(value));
            }
            string path=Folder+"/SFX_"+name+"_v36.wav";
            using(var stream=new BinaryWriter(File.Create(path)))
            {
                stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));stream.Write(36+count*2);stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));stream.Write(16);stream.Write((short)1);stream.Write((short)1);stream.Write(rate);stream.Write(rate*2);stream.Write((short)2);stream.Write((short)16);stream.Write(System.Text.Encoding.ASCII.GetBytes("data"));stream.Write(count*2);
                foreach(float sample in samples)stream.Write((short)(sample/Mathf.Max(.001f,peak)*.8f*short.MaxValue));
            }
            AssetDatabase.ImportAsset(path);var importer=(AudioImporter)AssetImporter.GetAtPath(path);var settings=importer.defaultSampleSettings;settings.compressionFormat=AudioCompressionFormat.PCM;settings.loadType=AudioClipLoadType.DecompressOnLoad;importer.defaultSampleSettings=settings;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
    }
}
