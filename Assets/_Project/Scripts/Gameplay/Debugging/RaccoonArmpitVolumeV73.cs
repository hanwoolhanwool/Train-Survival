using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    // Local volume correction for the review character. The original asset, UVs,
    // weights, bone poses and existing elbow/wrist shapes are never modified.
    public sealed class RaccoonArmpitVolumeV73 : IDisposable
    {
        readonly Mesh source;
        readonly SkinnedMeshRenderer skin;
        readonly Transform actor;
        readonly Transform[] bones;
        readonly Matrix4x4 restMap, inverseRest, bindInverseRest;
        readonly Matrix4x4[] bind, matrices;
        readonly Quaternion[] real, dual;
        readonly Vector3[] original, vertices, originalNormals, normals;
        readonly Vector4[] originalTangents, tangents;
        readonly BoneWeight[] weights;
        readonly int[] indices;
        readonly float[] masks;
        public Mesh RuntimeMesh { get; private set; }
        public float MaxDisplacement { get; private set; }
        public int CorrectedVertices => indices.Length;

        public RaccoonArmpitVolumeV73(Mesh mesh, SkinnedMeshRenderer renderer, Transform root, Matrix4x4 bindMap)
        {
            source=mesh;skin=renderer;actor=root;restMap=bindMap;inverseRest=bindMap.inverse;
            bindInverseRest=inverseRest;bones=skin.bones;bind=source.bindposes;
            matrices=new Matrix4x4[bones.Length];real=new Quaternion[bones.Length];dual=new Quaternion[bones.Length];
            original=mesh.vertices;vertices=(Vector3[])original.Clone();originalNormals=mesh.normals;normals=(Vector3[])originalNormals.Clone();
            originalTangents=mesh.tangents;tangents=(Vector4[])originalTangents.Clone();weights=mesh.boneWeights;
            // UV seams at identical positions share a mask, including the pinned cuff rim.
            var pinned=new HashSet<Vector3Int>();
            foreach(int i in mesh.GetTriangles(2))pinned.Add(Key(restMap.MultiplyPoint3x4(original[i])));
            var body=new HashSet<int>(mesh.GetTriangles(1));var ids=new List<int>();var ms=new List<float>();
            foreach(int i in body)
            {
                var p=restMap.MultiplyPoint3x4(original[i]);float x=Mathf.Abs(p.x);
                if(pinned.Contains(Key(p))||p.y<.88f||p.y>1.31f||x<.14f||x>.49f)continue;
                float mask=Smooth(.14f,.22f,x)*Smooth(.88f,1.0f,p.y)*(1-Smooth(1.26f,1.31f,p.y));
                if(mask<.001f)continue;ids.Add(i);ms.Add(mask);
            }
            indices=ids.ToArray();masks=ms.ToArray();
            RuntimeMesh=UnityEngine.Object.Instantiate(mesh);RuntimeMesh.name=mesh.name+"_ArmpitVolume_v73";
            RuntimeMesh.hideFlags=HideFlags.DontSave;RuntimeMesh.MarkDynamic();
        }
        static Vector3Int Key(Vector3 p)=>Vector3Int.RoundToInt(p*100000);
        static float Smooth(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        static Quaternion Scale(Quaternion q,float f)=>new Quaternion(q.x*f,q.y*f,q.z*f,q.w*f);
        static Quaternion Add(Quaternion a,Quaternion b)=>new Quaternion(a.x+b.x,a.y+b.y,a.z+b.z,a.w+b.w);
        void Accumulate(int bone,float weight,Quaternion reference,ref Quaternion r,ref Quaternion d,ref Matrix4x4 matrix)
        {
            if(weight<=0)return;
            float signed=Quaternion.Dot(reference,real[bone])<0?-weight:weight;
            r=Add(r,Scale(real[bone],signed));d=Add(d,Scale(dual[bone],signed));
            for(int k=0;k<16;k++)matrix[k]+=matrices[bone][k]*weight;
        }
        public void Apply()
        {
            var toActor=actor.worldToLocalMatrix;
            for(int i=0;i<bones.Length;i++)
            {
                matrices[i]=toActor*bones[i].localToWorldMatrix*bind[i]*bindInverseRest;
                real[i]=matrices[i].rotation;var t=matrices[i].GetColumn(3);
                dual[i]=Scale(new Quaternion(t.x,t.y,t.z,0)*real[i],.5f);
            }
            MaxDisplacement=0;
            for(int k=0;k<indices.Length;k++)
            {
                int i=indices[k];var w=weights[i];var reference=real[w.boneIndex0];
                var r=new Quaternion(0,0,0,0);var d=r;var matrix=new Matrix4x4();
                Accumulate(w.boneIndex0,w.weight0,reference,ref r,ref d,ref matrix);
                Accumulate(w.boneIndex1,w.weight1,reference,ref r,ref d,ref matrix);
                Accumulate(w.boneIndex2,w.weight2,reference,ref r,ref d,ref matrix);
                Accumulate(w.boneIndex3,w.weight3,reference,ref r,ref d,ref matrix);
                vertices[i]=original[i];normals[i]=originalNormals[i];tangents[i]=originalTangents[i];
                float length=Mathf.Sqrt(Quaternion.Dot(r,r));
                if(length<.001f||Mathf.Abs(matrix.determinant)<.001f)continue;
                r=Scale(r,1/length);d=Scale(d,1/length);
                var t=Scale(d*Quaternion.Inverse(r),2);var p=restMap.MultiplyPoint3x4(original[i]);
                var linear=matrix.MultiplyPoint3x4(p);var rigid=r*p+new Vector3(t.x,t.y,t.z);
                // Partial preservation avoids the inflated sleeve produced by full DQ skinning.
                var delta=Vector3.ClampMagnitude((rigid-linear)*.40f,.085f)*masks[k];
                var inverse=matrix.inverse;var correction=inverse.MultiplyVector(delta);
                correction=Vector3.ClampMagnitude(correction,.16f);
                vertices[i]=inverseRest.MultiplyPoint3x4(p+correction);
                MaxDisplacement=Mathf.Max(MaxDisplacement,matrix.MultiplyVector(correction).magnitude);
                float amount=.40f*masks[k];
                var n=restMap.MultiplyVector(originalNormals[i]).normalized;
                var targetNormal=Vector3.Slerp(matrix.MultiplyVector(n).normalized,r*n,amount);
                normals[i]=inverseRest.MultiplyVector(inverse.MultiplyVector(targetNormal)).normalized;
                var tangent=originalTangents[i];var v=restMap.MultiplyVector(new Vector3(tangent.x,tangent.y,tangent.z)).normalized;
                var targetTangent=Vector3.Slerp(matrix.MultiplyVector(v).normalized,r*v,amount);
                v=inverseRest.MultiplyVector(inverse.MultiplyVector(targetTangent)).normalized;
                tangents[i]=new Vector4(v.x,v.y,v.z,tangent.w);
            }
            RuntimeMesh.vertices=vertices;RuntimeMesh.normals=normals;RuntimeMesh.tangents=tangents;
            RuntimeMesh.RecalculateBounds();skin.sharedMesh=RuntimeMesh;
        }
        public void Dispose()
        {
            if(skin!=null&&skin.sharedMesh==RuntimeMesh)skin.sharedMesh=source;
            if(RuntimeMesh!=null)UnityEngine.Object.Destroy(RuntimeMesh);RuntimeMesh=null;
        }
    }
}
