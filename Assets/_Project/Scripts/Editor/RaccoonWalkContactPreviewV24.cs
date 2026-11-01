using System;
using System.Linq;
using UnityEngine;
using Game.Gameplay.Player;

namespace Game.Editor
{
    // Isolated flat-ground candidate solver. Never installed on the production prefab.
    internal sealed class RaccoonWalkContactPreviewV24
    {
        private sealed class Foot
        {
            public Transform upper, lower, ankle, toes;
            public int[] samples;
            public Quaternion stopRotation, lastRotation, lastToes, stopToes;
            public Vector3 anchor, lastTarget, stopStart, startTarget; public Quaternion startRotation,startToes;
            public float previousPhase=1, contact;
            public bool acquired;
        }
        private readonly Animator animator;
        private readonly Transform root;
        private readonly Transform[] bones;
        private readonly Matrix4x4[] bind;
        private readonly Vector3[] vertices;
        private readonly BoneWeight[] weights;
        private readonly Foot[] feet=new[]{new Foot(),new Foot()};
        private bool initialized,wasMoving;
        private float stopSeconds,startSeconds,baseDrop;
        public RaccoonWalkContactPreviewV24(Animator animator,Transform root)
        {
            this.animator=animator;this.root=root;
            var skin=animator.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh=skin.sharedMesh;bones=skin.bones;bind=mesh.bindposes;vertices=mesh.vertices;weights=mesh.boneWeights;
            for(int side=0;side<2;side++)
            {
                var f=feet[side];bool left=side==0;string prefix=left?"Left":"Right";
                f.upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                f.lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                f.ankle=animator.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                f.toes=animator.GetBoneTransform(left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
                var candidates=Enumerable.Range(0,vertices.Length).Where(i=>{
                    var w=weights[i];float W(int index,float weight)=>bones[index].name==prefix+"Foot"||bones[index].name==prefix+"Toes"?weight:0;
                    return W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3)>.8f;
                }).ToArray();
                float min=candidates.Min(i=>skin.transform.TransformPoint(vertices[i]).y);
                f.samples=candidates.Where(i=>skin.transform.TransformPoint(vertices[i]).y<min+.025f).ToArray();
            }
        }
        private Vector3 Point(int i)
        {
            var w=weights[i];Vector3 p=vertices[i];
            Vector3 P(int b)=>(bones[b].localToWorldMatrix*bind[b]).MultiplyPoint3x4(p);
            return P(w.boneIndex0)*w.weight0+P(w.boneIndex1)*w.weight1+P(w.boneIndex2)*w.weight2+P(w.boneIndex3)*w.weight3;
        }
        private static float Ease(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        public bool IsFootPlanted(int side)=>feet[side].contact>.99f;
        public float FootTargetError(int side)=>Vector3.Distance(feet[side].ankle.position,feet[side].lastTarget);
        public void Step(float dt,float speed,float phase)
        {
            bool moving=speed>.025f;
            if(!initialized)
            {
                foreach(var f in feet){f.anchor=f.lastTarget=f.stopStart=f.ankle.position;f.stopRotation=f.lastRotation=f.ankle.rotation;f.stopToes=f.lastToes=f.toes.localRotation;}
                initialized=true;
            }
            if(moving&&!wasMoving){startSeconds=0;foreach(var f in feet){f.startTarget=f.lastTarget;f.startRotation=f.lastRotation;f.startToes=f.lastToes;}} if(moving)startSeconds+=dt;
            if(!moving&&wasMoving)
            {
                stopSeconds=0;
                foreach(var f in feet){f.stopStart=f.lastTarget;f.stopRotation=f.lastRotation;f.stopToes=f.lastToes;f.acquired=false;}
            }
            if(!moving)stopSeconds+=dt;
            var targets=new Vector3[2];var rotations=new Quaternion[2];var hints=new Vector3[2];
            for(int side=0;side<2;side++)
            {
                var f=feet[side];Vector3 source=f.ankle.position;Quaternion rotation=f.ankle.rotation;
                hints[side]=f.lower.position+root.forward*.10f;
                float p=Mathf.Repeat(phase*2-(side==0?.80f:.30f),1);
                float weight;
                Vector3 target;
                if(moving)
                {
                    if(p<f.previousPhase-.5f || !f.acquired)
                    {
                        f.anchor=f.samples.Select(Point).Aggregate(Vector3.zero,(a,b)=>a+b)/f.samples.Length;
                        f.acquired=true;
                    }
                    weight=Ease(0,.10f,p)*(1-Ease(.44f,.60f,p));
                    // Preserve the source heel/toe roll; lock the actual sole centroid.
                    rotation=Quaternion.Slerp(f.startRotation,rotation,Ease(0,.30f,startSeconds));f.ankle.rotation=rotation;f.toes.localRotation=Quaternion.Slerp(f.startToes,f.toes.localRotation,Ease(0,.30f,startSeconds)); Vector3 centre=f.samples.Select(Point).Aggregate(Vector3.zero,(a,b)=>a+b)/f.samples.Length;target=source+(f.anchor-centre)*weight;
                    float soleOffset=f.samples.Min(i=>Point(i).y)-f.ankle.position.y;
                    target.y=Mathf.Lerp(source.y,.006f-soleOffset,weight);target=Vector3.Lerp(f.startTarget,target,Ease(0,.30f,startSeconds));
                }
                else
                {
                    weight=1;
                    rotation=f.stopRotation;
                    f.ankle.rotation=rotation;f.toes.localRotation=f.stopToes;
                    float soleOffset=f.samples.Min(i=>Point(i).y)-f.ankle.position.y;
                    target=f.stopStart;target.y=Mathf.Lerp(f.stopStart.y,.006f-soleOffset,Ease(0,.28f,stopSeconds));
                }
                f.contact=weight*(moving?Ease(0,.30f,startSeconds):1);f.previousPhase=p;targets[side]=target;rotations[side]=rotation;
            }
            baseDrop=Mathf.MoveTowards(baseDrop,.06f*Mathf.Clamp01(speed/.6f),dt*.14f);float drop=baseDrop;
            for(int i=0;i<2;i++)
            {
                var f=feet[i];float reach=Vector3.Distance(f.upper.position,f.lower.position)+Vector3.Distance(f.lower.position,f.ankle.position)-.001f;
                var d=f.upper.position-targets[i];float vertical=Mathf.Sqrt(Mathf.Max(.001f,reach*reach-d.x*d.x-d.z*d.z));drop=Mathf.Max(drop,d.y-vertical);
            }
            // A steady 6cm walk fit avoids contact-driven pelvis bobbing; ease it out at rest.
            animator.GetBoneTransform(HumanBodyBones.Hips).position-=Vector3.up*Mathf.Clamp(drop,0,.07f);
            for(int i=0;i<2;i++)
            {
                var f=feet[i];RaccoonEquipmentPose.SolveArm(f.upper,f.lower,f.ankle,targets[i],hints[i]);f.ankle.rotation=rotations[i];
                float clearance=f.samples.Min(v=>Point(v).y);
                if(clearance<.004f){targets[i]+=Vector3.up*(.004f-clearance);RaccoonEquipmentPose.SolveArm(f.upper,f.lower,f.ankle,targets[i],hints[i]);f.ankle.rotation=rotations[i];}
                f.lastTarget=targets[i];f.lastRotation=f.ankle.rotation;f.lastToes=f.toes.localRotation;
            }
            wasMoving=moving;
        }
    }
}



