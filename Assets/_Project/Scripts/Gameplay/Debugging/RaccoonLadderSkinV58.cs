using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>Review-only ladder correction. Never edits the imported mesh or its weights.</summary>
    public sealed class RaccoonLadderSkinV58 : IDisposable
    {
        readonly SkinnedMeshRenderer skin;
        readonly Mesh source,working;
        readonly BoneWeight[] original,corrected,buffer;
        readonly int[] affected;
        readonly int[] ids=new int[8];
        readonly float[] values=new float[8];
        float strength;
        public int CorrectedVertices => affected.Length;
        public float Strength => strength;
        public RaccoonLadderSkinV58(Animator animator,Transform root,Mesh baseline)
        {
            skin=animator.GetComponentInChildren<SkinnedMeshRenderer>();source=baseline;
            original=source.boneWeights;corrected=(BoneWeight[])original.Clone();buffer=(BoneWeight[])original.Clone();
            working=UnityEngine.Object.Instantiate(source);working.name=source.name+"_LadderReview_v58";
            var bones=skin.bones;var bind=source.bindposes;
            int hip=Array.IndexOf(bones,animator.GetBoneTransform(HumanBodyBones.Hips));
            int spine=Array.IndexOf(bones,animator.GetBoneTransform(HumanBodyBones.Spine));
            int chest=Array.IndexOf(bones,animator.GetBoneTransform(HumanBodyBones.Chest));
            int upper=Array.IndexOf(bones,animator.GetBoneTransform(HumanBodyBones.UpperChest));
            int neck=Array.IndexOf(bones,animator.GetBoneTransform(HumanBodyBones.Neck));
            int head=Array.IndexOf(bones,animator.GetBoneTransform(HumanBodyBones.Head));
            var map=root.worldToLocalMatrix*skin.transform.localToWorldMatrix;
            float hipY=map.MultiplyPoint3x4(bind[hip].inverse.MultiplyPoint3x4(Vector3.zero)).y;
            float spineY=map.MultiplyPoint3x4(bind[spine].inverse.MultiplyPoint3x4(Vector3.zero)).y;
            float chestY=map.MultiplyPoint3x4(bind[chest].inverse.MultiplyPoint3x4(Vector3.zero)).y;
            float upperY=map.MultiplyPoint3x4(bind[upper].inverse.MultiplyPoint3x4(Vector3.zero)).y;
            float neckY=map.MultiplyPoint3x4(bind[neck].inverse.MultiplyPoint3x4(Vector3.zero)).y;
            float headY=map.MultiplyPoint3x4(bind[head].inverse.MultiplyPoint3x4(Vector3.zero)).y;
            var vertices=source.vertices;var changed=new List<int>();
            var body=new bool[vertices.Length];
            // Original body/face atlas only. Keep fitted hands, cuffs and rebuilt tail intact.
            foreach(int i in source.GetTriangles(1))body[i]=true;
            for(int i=0;i<vertices.Length;i++)
            {
                if(!body[i])continue;
                var p=map.MultiplyPoint3x4(vertices[i]);var w=original[i];
                float waist=S((p.y-(hipY+.01f))/.07f)*(1-S((p.y-(chestY+.02f))/.10f))*(1-S((Mathf.Abs(p.x)-.22f)/.09f));
                if(waist>.001f)
                {
                    int lower=p.y<spineY?hip:p.y<chestY?spine:chest;
                    int higher=p.y<spineY?spine:p.y<chestY?chest:upper;
                    float low=p.y<spineY?hipY:p.y<chestY?spineY:chestY;
                    float high=p.y<spineY?spineY:p.y<chestY?chestY:upperY;
                    float u=Mathf.InverseLerp(low,high,p.y);
                    var torso=new BoneWeight{boneIndex0=lower,weight0=1-u,boneIndex1=higher,weight1=u};
                    corrected[i]=Blend(w,torso,waist);changed.Add(i);continue;
                }
                float headWeight=0;
                int[] wi={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
                float[] ww={w.weight0,w.weight1,w.weight2,w.weight3};
                for(int j=0;j<4;j++)if(wi[j]==head||bones[wi[j]].name.StartsWith("Ear"))headWeight+=ww[j];
                float face=S((p.y-neckY)/(headY-neckY))*S((headWeight-.25f)/.35f);
                if(face<=.001f)continue;
                // Preserve ear articulation; stop the arms/scarf from dragging the face.
                for(int j=0;j<4;j++)if(wi[j]!=head&&wi[j]!=neck&&!bones[wi[j]].name.StartsWith("Ear"))wi[j]=head;
                var faceWeights=new BoneWeight{boneIndex0=wi[0],weight0=ww[0],boneIndex1=wi[1],weight1=ww[1],boneIndex2=wi[2],weight2=ww[2],boneIndex3=wi[3],weight3=ww[3]};
                corrected[i]=Blend(w,faceWeights,face);changed.Add(i);
            }
            affected=changed.ToArray();
        }
        static float S(float x)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(x));
        BoneWeight Blend(BoneWeight a,BoneWeight b,float t)
        {
            int count=0;
            Add(a.boneIndex0,a.weight0*(1-t),ref count);Add(a.boneIndex1,a.weight1*(1-t),ref count);Add(a.boneIndex2,a.weight2*(1-t),ref count);Add(a.boneIndex3,a.weight3*(1-t),ref count);
            Add(b.boneIndex0,b.weight0*t,ref count);Add(b.boneIndex1,b.weight1*t,ref count);Add(b.boneIndex2,b.weight2*t,ref count);Add(b.boneIndex3,b.weight3*t,ref count);
            for(int i=0;i<Mathf.Min(4,count);i++)for(int j=i+1;j<count;j++)if(values[j]>values[i])
            {float v=values[i];values[i]=values[j];values[j]=v;int id=ids[i];ids[i]=ids[j];ids[j]=id;}
            float sum=0;for(int i=0;i<Mathf.Min(4,count);i++)sum+=values[i];
            for(int i=count;i<4;i++){ids[i]=0;values[i]=0;}
            return new BoneWeight{boneIndex0=ids[0],weight0=values[0]/sum,boneIndex1=ids[1],weight1=values[1]/sum,boneIndex2=ids[2],weight2=values[2]/sum,boneIndex3=ids[3],weight3=values[3]/sum};
        }
        void Add(int id,float value,ref int count)
        {
            if(value<=0)return;
            for(int i=0;i<count;i++)if(ids[i]==id){values[i]+=value;return;}
            ids[count]=id;values[count++]=value;
        }
        public void Apply(bool active,float dt,bool originalComparison)
        {
            float next=originalComparison?0:Mathf.MoveTowards(strength,active?1:0,dt*4);
            if(next<=0){strength=0;skin.sharedMesh=source;return;}
            if(!Mathf.Approximately(next,strength))
            {foreach(int i in affected)buffer[i]=Blend(original[i],corrected[i],next);working.boneWeights=buffer;}
            strength=next;skin.sharedMesh=working;
        }
        public void Dispose(){if(skin!=null&&skin.sharedMesh==working)skin.sharedMesh=source;if(working!=null)UnityEngine.Object.Destroy(working);}
    }
}
