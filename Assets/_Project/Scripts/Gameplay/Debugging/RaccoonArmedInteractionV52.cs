using System;
using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>Release directly from the current equipment pose, touch, and return to that pose.</summary>
    public sealed class RaccoonArmedInteractionV52
    {
        public const float Duration=1.65f, PickupDuration=2.15f;
        readonly Animator animator;
        readonly bool left;
        readonly Transform root,hand;
        readonly RaccoonActionHandsV37 anatomy;
        readonly Quaternion palmFrame;
        readonly Transform[] fingers;
        readonly Quaternion[] openFingers;
        readonly Quaternion[] graspFingers;
        Quaternion previousRotation;
        float previousTime=-1;
        public Vector3 PalmPosition => anatomy.PalmCenter(hand,left);
        public Quaternion PalmRotation => hand.rotation*palmFrame;
        public float WristAngle { get; private set; }
        public float ContactError { get; private set; }
        public RaccoonArmedInteractionV52(Animator a,Transform movementRoot,bool useLeftHand=true)
        {
            animator=a;root=movementRoot;left=useLeftHand;
            hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            anatomy=new RaccoonActionHandsV37(a);
            var middle=a.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
            palmFrame=Quaternion.LookRotation(hand.InverseTransformDirection(middle.position-hand.position),hand.InverseTransformDirection(anatomy.PalmNormal(hand,left)));
            fingers=Array.ConvertAll(left?RaccoonEquipmentPose.LeftFingers:RaccoonEquipmentPose.RightFingers,b=>a.GetBoneTransform(b));
            openFingers=new Quaternion[fingers.Length];
            graspFingers=new Quaternion[fingers.Length];
            // Derive a relaxed open hand in this avatar's axes, then restore every transform.
            var bones=a.GetComponentsInChildren<Transform>(true);
            var positions=Array.ConvertAll(bones,b=>b.localPosition);
            var rotations=Array.ConvertAll(bones,b=>b.localRotation);
            using(var handler=new HumanPoseHandler(a.avatar,a.transform))
            {
                var pose=new HumanPose();handler.GetHumanPose(ref pose);
                for(int i=0;i<HumanTrait.MuscleCount;i++)
                {
                    string n=HumanTrait.MuscleName[i];
                    if(n.StartsWith(left?"Left":"Right")&&(n.Contains("Thumb")||n.Contains("Index")||n.Contains("Middle")||n.Contains("Ring")||n.Contains("Little")))
                        pose.muscles[i]=n.Contains("Spread")?0:n.Contains("Thumb")?.15f:.75f;
                }
                handler.SetHumanPose(ref pose);
                for(int i=0;i<fingers.Length;i++)openFingers[i]=fingers[i].localRotation;
                for(int i=0;i<HumanTrait.MuscleCount;i++)
                {
                    string n=HumanTrait.MuscleName[i];
                    if(n.StartsWith(left?"Left":"Right")&&(n.Contains("Thumb")||n.Contains("Index")||n.Contains("Middle")||n.Contains("Ring")||n.Contains("Little")))
                        pose.muscles[i]=n.Contains("Spread")?0:n.Contains("Thumb")?-.12f:n.Contains("2 Stretched")?-.35f:-.15f;
                }
                handler.SetHumanPose(ref pose);
                for(int i=0;i<fingers.Length;i++)graspFingers[i]=fingers[i].localRotation;
            }
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];}
        }
        static float S(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
        public static bool HoldingObject(float time)=>time>=.90f&&time<1.48f;
        public void Apply(float time,bool pickup=false)
        {
            Vector3 point=root.TransformPoint(pickup?new Vector3(left?-.23f:.23f,.50f,.40f):new Vector3(left?-.24f:.22f,.92f,.53f));
            ApplyReach(time,pickup,point,root.rotation);
        }
        public void ApplySwimming(float time,bool pickup,Vector3 worldContact,Quaternion contactFrame)
        {
            ApplyReach(time,pickup,worldContact,contactFrame);
        }
        void ApplyReach(float time,bool pickup,Vector3 contactPoint,Quaternion frame)
        {
            ContactError=0;WristAngle=0;
            float duration=pickup?PickupDuration:Duration,arrival=pickup?.60f:.45f,departure=pickup?1.48f:.82f;
            if(time<=0||time>=duration)return;
            float reach=time<arrival?S(time/arrival):time<=departure?1:1-S((time-departure)/(duration-departure));
            var upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            Vector3 basePosition=hand.position;Quaternion baseRotation=hand.rotation;
            float baselineAngle=Vector3.Angle(basePosition-lower.position,baseRotation*palmFrame*Vector3.forward);
            Vector3 originalPoint=contactPoint;
            float lift=pickup?S((time-.90f)/.35f):0;
            if(pickup)contactPoint+=frame*new Vector3(0,.16f,0)*lift;
            else if(time>=arrival&&time<=departure)contactPoint-=frame*Vector3.up*(.008f*Mathf.Sin(Mathf.PI*(time-arrival)/(departure-arrival)));
            // The elbow stays on its own side and below the shoulder. The hand's axis
            // is bounded against the solved forearm instead of forcing a folded wrist.
            Vector3 pole=upper.position+root.TransformVector(new Vector3(left?-.38f:.38f,-.32f,-.12f));
            Quaternion desired=frame*Quaternion.LookRotation(pickup?new Vector3(0,-.18f,1):Vector3.forward,Vector3.down)*Quaternion.Inverse(palmFrame);
            Quaternion rotation=Quaternion.Slerp(baseRotation,desired,reach);
            float limit=Mathf.Lerp(Mathf.Max(65,baselineAngle),65,reach);
            Vector3 surface=root.TransformPoint(new Vector3(left?-.24f:.22f,.92f,.53f));
            Vector3 clearance=frame*new Vector3(left?-.055f:.055f,.07f,-.015f)*Mathf.Sin(Mathf.PI*reach);
            for(int iteration=0;iteration<6;iteration++)
            {
                Vector3 contactWrist=anatomy.SurfaceWrist(animator,root,left,rotation)+contactPoint-surface;
                Vector3 target=Vector3.Lerp(basePosition,contactWrist,reach)+clearance;
                RaccoonEquipmentPose.SolveArm(upper,lower,hand,target,pole);
                Vector3 forearm=(hand.position-lower.position).normalized;
                Vector3 axis=rotation*palmFrame*Vector3.forward;
                Vector3 bounded=Vector3.RotateTowards(forearm,axis,limit*Mathf.Deg2Rad,0).normalized;
                Vector3 normal=Vector3.ProjectOnPlane(rotation*palmFrame*Vector3.up,bounded).normalized;
                if(normal.sqrMagnitude<.001f)normal=Vector3.ProjectOnPlane(frame*Vector3.down,bounded).normalized;
                rotation=Quaternion.LookRotation(bounded,normal)*Quaternion.Inverse(palmFrame);
            }
            // The continuing swim stroke can cross a wrist-frame singularity on recovery.
            // Bound its angular speed through recovery. The final destination keeps
            // tracking the live baseline, rather than returning to a frozen start pose.
            float delta=time-previousTime;
            if(previousTime>=0&&delta>0&&delta<.10f)
                rotation=Quaternion.RotateTowards(previousRotation,rotation,720*delta);
            previousRotation=rotation;previousTime=time;
            Vector3 finalWrist=anatomy.SurfaceWrist(animator,root,left,rotation)+contactPoint-surface;
            RaccoonEquipmentPose.SolveArm(upper,lower,hand,Vector3.Lerp(basePosition,finalWrist,reach)+clearance,pole);
            hand.rotation=rotation;
            WristAngle=Vector3.Angle(hand.position-lower.position,hand.rotation*palmFrame*Vector3.forward);
            float closing=pickup?S((time-.65f)/.25f):0;
            float opening=S(reach/.8f);
            for(int i=0;i<fingers.Length;i++)
            {
                Quaternion working=Quaternion.Slerp(openFingers[i],graspFingers[i],closing);
                fingers[i].localRotation=Quaternion.Slerp(fingers[i].localRotation,working,opening);
            }
            if(time>=arrival&&time<=(pickup?.90f:departure))ContactError=Vector3.Distance(PalmPosition,originalPoint);
        }
    }
}
