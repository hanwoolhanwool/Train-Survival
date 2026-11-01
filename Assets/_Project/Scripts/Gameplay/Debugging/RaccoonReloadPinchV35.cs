using System;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    // Shell carrying pose, fitted to the actual distal fingertip vertices of this mesh.
    // Only evaluated by the isolated combat review, never by production characters.
    public sealed class RaccoonReloadPinchV35
    {
        private readonly Transform hand,middle,little;
        private readonly Transform[] thumb,index;
        private readonly Vector3 thumbTip,indexTip;
        public RaccoonReloadPinchV35(Animator a)
        {
            hand=a.GetBoneTransform(HumanBodyBones.LeftHand);
            middle=a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            little=a.GetBoneTransform(HumanBodyBones.LeftLittleProximal);
            thumb=new[]{a.GetBoneTransform(HumanBodyBones.LeftThumbProximal),a.GetBoneTransform(HumanBodyBones.LeftThumbIntermediate),a.GetBoneTransform(HumanBodyBones.LeftThumbDistal)};
            index=new[]{a.GetBoneTransform(HumanBodyBones.LeftIndexProximal),a.GetBoneTransform(HumanBodyBones.LeftIndexIntermediate),a.GetBoneTransform(HumanBodyBones.LeftIndexDistal)};
            var skin=a.GetComponentInChildren<SkinnedMeshRenderer>();
            thumbTip=SurfaceTip(skin,thumb[2]);indexTip=SurfaceTip(skin,index[2]);
        }
        public Vector3 ThumbTip=>thumb[2].TransformPoint(thumbTip);
        public Vector3 IndexTip=>index[2].TransformPoint(indexTip);
        public float Apply(Transform shell,float weight)
        {
            Vector3 palm=(middle.position-hand.position).normalized;
            Vector3 across=(index[0].position-little.position).normalized;
            Vector3 normal=Vector3.Cross(across,palm).normalized;
            Vector3 center=(thumb[0].position+index[0].position)*.5f+normal*.032f+palm*.025f;
            Vector3 side=Vector3.ProjectOnPlane(index[0].position-thumb[0].position,across).normalized;
            // Cylinder diameter is 25 mm. The fingertip surface sits 1.5 mm outside it.
            Vector3 tt=center-side*.014f,it=center+side*.014f;
            Solve(thumb,thumbTip,tt,weight);Solve(index,indexTip,it,weight);
            if(shell!=null){shell.position=center;shell.rotation=Quaternion.FromToRotation(Vector3.up,across);}
            return Mathf.Max(Vector3.Distance(ThumbTip,tt),Vector3.Distance(IndexTip,it));
        }
        private static void Solve(Transform[] bones,Vector3 tip,Vector3 target,float weight)
        {
            Quaternion p=bones[0].localRotation,m=bones[1].localRotation,d=bones[2].localRotation;
            for(int iteration=0;iteration<16;iteration++)
            {
                for(int i=2;i>=0;i--)
                {
                    var bone=bones[i];Quaternion q=Quaternion.FromToRotation(bones[2].TransformPoint(tip)-bone.position,target-bone.position);
                    bone.rotation=Quaternion.RotateTowards(Quaternion.identity,q,25)*bone.rotation;
                    Quaternion initial=i==0?p:i==1?m:d;
                    bone.localRotation=Quaternion.RotateTowards(initial,bone.localRotation,i==0?65:100);
                }
            }
            bones[0].localRotation=Quaternion.Slerp(p,bones[0].localRotation,weight);
            bones[1].localRotation=Quaternion.Slerp(m,bones[1].localRotation,weight);
            bones[2].localRotation=Quaternion.Slerp(d,bones[2].localRotation,weight);
        }
        private static Vector3 SurfaceTip(SkinnedMeshRenderer skin,Transform bone)
        {
            var mesh=skin.sharedMesh;int id=Array.IndexOf(skin.bones,bone);
            if(id<0)return Vector3.up*bone.parent.localPosition.magnitude*.65f;
            var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes[id];
            float max=float.NegativeInfinity;
            float Weight(BoneWeight w)=>(w.boneIndex0==id?w.weight0:0)+(w.boneIndex1==id?w.weight1:0)+(w.boneIndex2==id?w.weight2:0)+(w.boneIndex3==id?w.weight3:0);
            for(int i=0;i<vertices.Length;i++)if(Weight(weights[i])>.9f)max=Mathf.Max(max,bind.MultiplyPoint3x4(vertices[i]).y);
            Vector3 sum=Vector3.zero;int count=0;float margin=.004f/Mathf.Abs(bone.lossyScale.y);
            for(int i=0;i<vertices.Length;i++)
            {
                if(Weight(weights[i])<=.9f)continue;
                Vector3 p=bind.MultiplyPoint3x4(vertices[i]);if(p.y<max-margin)continue;
                sum+=p;count++;
            }
            return count>0?sum/count:Vector3.up*bone.parent.localPosition.magnitude*.65f;
        }
    }
}
