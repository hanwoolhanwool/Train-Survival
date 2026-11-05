using System;
using System.Linq;
using UnityEngine;
using Game.Gameplay.Player;

namespace Game.Gameplay.Debugging
{
    // Surface-aware candidate solver, used only by the separate playable review character.
    public sealed class RaccoonDirectionalContactsV30
    {
        private sealed class Foot
        {
            public Transform upper, lower, ankle, toes;
            public int[] samples;
            public Quaternion stopRotation, lastRotation, lastToes, stopToes;
            public Vector3 anchor, lastTarget, stopStart, startTarget; public Quaternion startRotation,startToes;
            public float previousPhase=1, contact;
            public bool acquired, waitingForPlant; public Quaternion contactHeading; public Vector3 surfaceNormal=Vector3.up;
            public bool observed, stopPlanted;
            public Vector3 observedPosition, stopAnchor;
            public Quaternion observedRotation, observedToes, plantedRotation, plantedToes;
            public float observedClearance;
            public Vector3 idlePosition, settleFrom, settleGoal;
            public Quaternion idleRotation, idleToes, settleRotation, settleToes, goalRotation;
        }
        private readonly Animator animator;
        private readonly Transform root;
        private readonly Transform[] bones;
        private readonly Matrix4x4[] bind; private readonly Matrix4x4[] skinMatrices;
        private readonly Vector3[] vertices;
        private readonly BoneWeight[] weights;
        private readonly Foot[] feet=new[]{new Foot(),new Foot()};
        private bool initialized,wasMoving,stopping;
        private bool flatStop,settleReady;
        private float settleTime;
        private int settleFirst;
        private const float SettleStepSeconds=.28f;
        private float stopSeconds,startSeconds,baseDrop,tailLift; private readonly Transform tailRoot; private readonly int[] tailSamples;
        public RaccoonDirectionalContactsV30(Animator animator,Transform root)
        {
            this.animator=animator;this.root=root;
            var skin=animator.GetComponentInChildren<SkinnedMeshRenderer>();
            // Split UV/normal vertices can have identical positions and skin weights.
            // A minimum-clearance query needs each exact trajectory only once. This is
            // lossless: the rendered mesh and the set of tested world positions are unchanged.
            var mesh=skin.sharedMesh;bones=skin.bones;bind=mesh.bindposes;skinMatrices=new Matrix4x4[bones.Length];vertices=mesh.vertices;weights=mesh.boneWeights;tailRoot=bones.First(b=>b.name=="Tail01");tailSamples=mesh.GetTriangles(mesh.subMeshCount-2).Distinct().GroupBy(i=>new {position=vertices[i],weight=weights[i]}).Select(g=>g.First()).ToArray();
            for(int side=0;side<2;side++)
            {
                var f=feet[side];bool left=side==0;string prefix=left?"Left":"Right";
                f.upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                f.lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                f.ankle=animator.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                f.toes=animator.GetBoneTransform(left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
                // The rig constructs this solver from its sampled idle pose.
                f.idlePosition=root.InverseTransformPoint(f.ankle.position);
                f.idleRotation=Quaternion.Inverse(root.rotation)*f.ankle.rotation;f.idleToes=f.toes.localRotation;
                var candidates=Enumerable.Range(0,vertices.Length).Where(i=>{
                    var w=weights[i];float W(int index,float weight)=>bones[index].name==prefix+"Foot"||bones[index].name==prefix+"Toes"?weight:0;
                    return W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3)>.8f;
                }).ToArray();
                float min=candidates.Min(i=>skin.transform.TransformPoint(vertices[i]).y);
                f.samples=candidates.Where(i=>skin.transform.TransformPoint(vertices[i]).y<min+.025f).ToArray();
            }
        }
        public void ResetContacts(bool resumeMoving=false)
        {
            initialized=false;stopping=flatStop=settleReady=false;wasMoving=resumeMoving;startSeconds=resumeMoving?.3f:0;stopSeconds=baseDrop=settleTime=0;
            foreach(var f in feet){f.acquired=false;f.observed=f.stopPlanted=false;f.waitingForPlant=resumeMoving;f.previousPhase=resumeMoving?float.NaN:1;f.contact=0;}
        }
        // Refresh once after each skeletal edit, not four native matrix reads and
        // matrix products for every vertex on every tail/sole clearance probe.
        private void RefreshMatrices()
        {
            for(int i=0;i<bones.Length;i++)skinMatrices[i]=bones[i].localToWorldMatrix*bind[i];
        }
        private Vector3 Point(int i)
        {
            var w=weights[i];Vector3 p=vertices[i];
            Vector3 P(int b)=>skinMatrices[b].MultiplyPoint3x4(p);
            return P(w.boneIndex0)*w.weight0+P(w.boneIndex1)*w.weight1+P(w.boneIndex2)*w.weight2+P(w.boneIndex3)*w.weight3;
        }
                private bool Surface(Vector3 point,out RaycastHit hit)
            =>Physics.Raycast(point+Vector3.up*1.5f,Vector3.down,out hit,3.5f,~(1<<2),QueryTriggerInteraction.Ignore);
        private float Clearance(Vector3 point)=>Surface(point,out var hit)?point.y-hit.point.y:999;
        private float RequiredLift(Foot f,Vector3 horizontalDelta)
            =>f.samples.Max(i=>.006f-Clearance(Point(i)+horizontalDelta));
        private void PlantStoppedFoot(Foot f)
        {
            if(f.stopPlanted||!f.observed||f.observedClearance>.008f)return;
            f.stopAnchor=f.observedPosition;f.plantedRotation=f.observedRotation;
            f.plantedToes=f.observedToes;f.stopPlanted=true;
        }
        private bool IsFlatStopSurface()
        {
            if(!Surface(root.position,out var floor)||Vector3.Angle(floor.normal,Vector3.up)>5)return false;
            foreach(var f in feet)
                if(!Surface(f.observed?f.observedPosition:f.ankle.position,out var hit)||
                    Vector3.Angle(hit.normal,Vector3.up)>5||Mathf.Abs(hit.point.y-floor.point.y)>.025f)return false;
            return true;
        }
        private void BeginFlatSettle()
        {
            // Only plan after deceleration: the idle targets then belong to the
            // final body position, rather than a transient contact along the stride.
            foreach(var f in feet)
            {
                f.settleFrom=f.observed?f.observedPosition:f.lastTarget;
                f.settleRotation=f.observed?f.observedRotation:f.lastRotation;
                f.settleToes=f.observed?f.observedToes:f.lastToes;
                f.settleGoal=root.TransformPoint(f.idlePosition);f.goalRotation=root.rotation*f.idleRotation;
                var rotation=f.ankle.rotation;var toes=f.toes.localRotation;
                f.ankle.rotation=f.goalRotation;f.toes.localRotation=f.idleToes;RefreshMatrices();
                Vector3 shift=f.settleGoal-f.ankle.position;shift.y=0;
                f.settleGoal.y=f.ankle.position.y+RequiredLift(f,shift);
                f.ankle.rotation=rotation;f.toes.localRotation=toes;
            }
            // Finish the arriving foot first; the other foot remains a stable
            // support until it is its turn to return to the idle stance.
            settleFirst=feet[0].observedClearance>=feet[1].observedClearance?0:1;
            settleTime=0;settleReady=true;RefreshMatrices();
        }
        public void ClearSoles()
        {
            RefreshMatrices();
            // Blending two individually grounded poses can rotate a sole through the floor.
            // Recheck the final geometry after the walk/run blend, before solving the arms.
            foreach(var foot in feet)
            {
                var rotation=foot.ankle.rotation;
                for(int pass=0;pass<3;pass++)
                {
                    float clearance=foot.samples.Min(i=>Clearance(Point(i)));
                    if(clearance>=.004f)break;
                    var target=foot.ankle.position+Vector3.up*(.004f-clearance);
                    RaccoonEquipmentPose.SolveArm(foot.upper,foot.lower,foot.ankle,target,foot.lower.position+root.forward*.1f);
                    foot.ankle.rotation=rotation;
                    RefreshMatrices();
                }
                // Observe the rendered pose after the walk/run blend and sole
                // clearance pass. Targets from before IK are not the actual contact.
                foot.observedPosition=foot.ankle.position;foot.observedRotation=foot.ankle.rotation;
                foot.observedToes=foot.toes.localRotation;
                foot.observedClearance=foot.samples.Min(i=>Clearance(Point(i)));foot.observed=true;
                if(stopping&&!flatStop)PlantStoppedFoot(foot);
            }
        }
        public void FitTail(float dt)
        {
            var original=tailRoot.rotation;
            float ClearanceAt(float angle)
            {
                tailRoot.rotation=Quaternion.AngleAxis(angle,root.right)*original;RefreshMatrices();
                return tailSamples.Min(i=>Clearance(Point(i)));
            }
            float required=0;
            if(ClearanceAt(0)<.025f)
            {
                float low=0,high=24;
                for(int i=0;i<9;i++){float mid=(low+high)*.5f;if(ClearanceAt(mid)<.025f)low=mid;else high=mid;}
                required=high;
            }
            tailLift=Mathf.Max(required,Mathf.Lerp(tailLift,required,1-Mathf.Exp(-dt*5)));
            tailRoot.rotation=Quaternion.AngleAxis(tailLift,root.right)*original;
        }
        private static float Ease(float a,float b,float x)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,x));
        public bool IsFootPlanted(int side)=>feet[side].contact>.99f;
        public float FootTargetError(int side)=>Vector3.Distance(feet[side].ankle.position,feet[side].lastTarget);
        public void Step(float dt,float speed,float phase,bool allowNewPlant=true,bool stopRequested=false)
        {
            RefreshMatrices();bool moving=speed>.025f;
            if(!initialized)
            {
                foreach(var f in feet){f.anchor=f.lastTarget=f.stopStart=f.startTarget=f.ankle.position;f.stopRotation=f.lastRotation=f.startRotation=f.ankle.rotation;f.stopToes=f.lastToes=f.startToes=f.toes.localRotation;}
                initialized=true;
            }
            if(stopRequested&&!stopping)
            {
                flatStop=IsFlatStopSurface();settleReady=false;
                foreach(var f in feet)PlantStoppedFoot(f);
            }
            else if(!stopRequested&&stopping)
            {
                // A new movement request releases both independent stop contacts.
                // Reuse the normal start blend from their last solved poses.
                wasMoving=false;
                flatStop=settleReady=false;
                foreach(var f in feet){f.stopPlanted=false;f.acquired=false;}
            }
            stopping=stopRequested;
            if(moving&&!wasMoving){startSeconds=0;foreach(var f in feet){f.startTarget=f.lastTarget;f.startRotation=f.lastRotation;f.startToes=f.lastToes;}} if(moving)startSeconds+=dt;
            if(!moving&&wasMoving)
            {
                stopSeconds=0;
                foreach(var f in feet){f.stopStart=f.lastTarget;f.stopRotation=f.lastRotation;f.stopToes=f.lastToes;f.acquired=false;}
            }
            if(!moving)stopSeconds+=dt;
            if(stopping&&flatStop&&!moving&&!settleReady)BeginFlatSettle();
            if(stopping&&settleReady)settleTime+=dt;
            var targets=new Vector3[2];var rotations=new Quaternion[2];var hints=new Vector3[2];
            for(int side=0;side<2;side++)
            {
                var f=feet[side];Vector3 source=f.ankle.position;Quaternion rotation=f.ankle.rotation;
                hints[side]=f.lower.position+root.forward*.10f;
                float p=Mathf.Repeat(phase*2-(side==0?.80f:.30f),1);
                float weight;
                Vector3 target;
                if(stopping&&flatStop&&settleReady)
                {
                    float progress=Ease(0,SettleStepSeconds,settleTime-(side==settleFirst?0:SettleStepSeconds));
                    Vector3 travel=f.settleGoal-f.settleFrom;travel.y=0;
                    float lift=.035f*Mathf.Clamp01(travel.magnitude/.06f)*4*progress*(1-progress);
                    target=Vector3.Lerp(f.settleFrom,f.settleGoal,progress)+Vector3.up*lift;
                    rotation=Quaternion.Slerp(f.settleRotation,f.goalRotation,progress);
                    f.ankle.rotation=rotation;f.toes.localRotation=Quaternion.Slerp(f.settleToes,f.idleToes,progress);RefreshMatrices();
                    weight=progress>=1||progress<=0&&f.stopPlanted?1:0;
                }
                else if(stopping&&f.stopPlanted)
                {
                    weight=1;target=f.stopAnchor;rotation=f.plantedRotation;
                    f.ankle.rotation=rotation;f.toes.localRotation=f.plantedToes;RefreshMatrices();
                }
                else if(moving)
                {
                    bool touchdown=p<f.previousPhase-.5f;
                    // After a run/airborne pose, an arbitrary mid-stance sample is
                    // not a planted foot. Wait for the next walk touchdown so an
                    // old running sole cannot pull the recovering foot backwards.
                    if(f.waitingForPlant&&allowNewPlant&&touchdown)f.waitingForPlant=false;
                    if(!f.waitingForPlant&&(touchdown || !f.acquired))
                    {
                        f.anchor=f.samples.Select(Point).Aggregate(Vector3.zero,(a,b)=>a+b)/f.samples.Length;
                        f.contactHeading=root.rotation;f.acquired=true;
                    }
                    weight=f.waitingForPlant?0:Ease(0,.10f,p)*(1-Ease(.44f,.60f,p)); weight*=1-Ease(35,55,Quaternion.Angle(f.contactHeading,root.rotation));
                    // Preserve the source heel/toe roll; lock the actual sole centroid.
                                        var locked=f.contactHeading*Quaternion.Inverse(root.rotation)*rotation;
                    rotation=Quaternion.Slerp(rotation,locked,weight);
                    Vector3 probe=Vector3.Lerp(source,f.anchor,weight);
                    Vector3 normal=Surface(probe,out var groundHit)?groundHit.normal:Vector3.up;
                    f.surfaceNormal=Vector3.Slerp(f.surfaceNormal,normal,1-Mathf.Exp(-dt*12));
                    rotation=Quaternion.FromToRotation(Vector3.up,f.surfaceNormal)*rotation;
                    rotation=Quaternion.Slerp(f.startRotation,rotation,Ease(0,.30f,startSeconds));f.ankle.rotation=rotation;f.toes.localRotation=Quaternion.Slerp(f.startToes,f.toes.localRotation,Ease(0,.30f,startSeconds)); RefreshMatrices();Vector3 centre=f.samples.Select(Point).Aggregate(Vector3.zero,(a,b)=>a+b)/f.samples.Length;target=source+(f.anchor-centre)*weight;
                    float lift=RequiredLift(f,new Vector3(target.x-source.x,0,target.z-source.z)); target.y=source.y+lift*weight;target=Vector3.Lerp(f.startTarget,target,Ease(0,.30f,startSeconds));
                }
                else
                {
                    f.waitingForPlant=false;
                    weight=1;
                    rotation=f.stopRotation;
                    f.ankle.rotation=rotation;f.toes.localRotation=f.stopToes;RefreshMatrices();
                    float soleOffset=f.samples.Min(i=>Point(i).y)-f.ankle.position.y;
                    target=f.stopStart;Vector3 shift=new Vector3(target.x-source.x,0,target.z-source.z);target.y=Mathf.Lerp(f.stopStart.y,source.y+RequiredLift(f,shift),Ease(0,.28f,stopSeconds));
                }
                f.contact=weight*(moving?Ease(0,.30f,startSeconds):1);f.previousPhase=p;targets[side]=target;rotations[side]=rotation;
            }
            float slope=Surface(root.position,out var floorHit)?Vector3.Angle(Vector3.up,floorHit.normal):0; baseDrop=Mathf.MoveTowards(baseDrop,(.06f+.02f*Mathf.Clamp01(slope/15))*Mathf.Clamp01(speed/.6f),dt*.14f);float drop=baseDrop;
            for(int i=0;i<2;i++)
            {
                var f=feet[i];float reach=Vector3.Distance(f.upper.position,f.lower.position)+Vector3.Distance(f.lower.position,f.ankle.position)-.001f;
                var d=f.upper.position-targets[i];float vertical=Mathf.Sqrt(Mathf.Max(.001f,reach*reach-d.x*d.x-d.z*d.z));drop=Mathf.Max(drop,d.y-vertical);
            }
            // A steady 6cm walk fit avoids contact-driven pelvis bobbing; ease it out at rest.
            animator.GetBoneTransform(HumanBodyBones.Hips).position-=Vector3.up*Mathf.Clamp(drop,0,.12f);
            for(int i=0;i<2;i++)
            {
                var f=feet[i];RaccoonEquipmentPose.SolveArm(f.upper,f.lower,f.ankle,targets[i],hints[i]);f.ankle.rotation=rotations[i];RefreshMatrices();
                float clearance=f.samples.Min(v=>Clearance(Point(v)));
                for(int pass=0;pass<3 && clearance<.004f;pass++){targets[i]+=Vector3.up*(.004f-clearance);RaccoonEquipmentPose.SolveArm(f.upper,f.lower,f.ankle,targets[i],hints[i]);f.ankle.rotation=rotations[i];RefreshMatrices();clearance=f.samples.Min(v=>Clearance(Point(v)));}
                f.lastTarget=targets[i];f.lastRotation=f.ankle.rotation;f.lastToes=f.toes.localRotation;
            }
            wasMoving=moving;
        }
    }
}








