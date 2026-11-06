using UnityEngine;
using Game.Gameplay.Player;

namespace Game.Gameplay.Debugging
{
    // Low draw -> fast horizontal sweep -> open follow-through -> relaxed recovery.
    // Separate choreography from v81; both takes share the v83 clearance/recovery fix.
    public static class RaccoonMacheteSlashV82
    {
        public const float Duration=1.26f;
        static readonly float[] Times={0,.30f,.44f,.58f,.74f,Duration};
        static readonly Vector3[] Reach={Vector3.zero,new Vector3(.60f,-.25f,-.05f),new Vector3(.20f,-.08f,.58f),new Vector3(-.38f,-.16f,.28f),new Vector3(-.40f,-.24f,.16f),Vector3.zero};
        static readonly Vector3[] BladeAngles={Vector3.zero,new Vector3(0,0,-75),new Vector3(90,0,0),new Vector3(90,0,65),new Vector3(100,0,65),Vector3.zero};
        static readonly Vector3[] Shift={Vector3.zero,new Vector3(.035f,-.045f,-.025f),new Vector3(-.01f,-.07f,.07f),new Vector3(-.04f,-.06f,.075f),new Vector3(-.045f,-.035f,.04f),Vector3.zero};
        static readonly float[] Turn={0,38,-12,-54,-48,0};
        static readonly float[] Lean={0,-3,12,10,5,0};
        static readonly float[] ForearmRoll={0,-65,-45,-25,-15,0};

        static void Segment(float time,out int i,out float t)
        {
            i=0;time=Mathf.Clamp(time,0,Duration);
            while(i<Times.Length-2&&time>Times[i+1])i++;
            t=Mathf.InverseLerp(Times[i],Times[i+1],time);
        }
        static float Ease(int i,float t)=>i==1?t*t:i==2?1-(1-t)*(1-t):Mathf.SmoothStep(0,1,t);
        static float Envelope(int i,float t)=>i==0?t:i==Times.Length-2?1-t:1;
        static Vector3 Arc(Vector3 from,Vector3 to,int i,float t)
        {
            if(i!=1&&i!=2)return Vector3.Lerp(from,to,Ease(i,t));
            // The impact waypoint is outside the torso, with a continuous lateral velocity.
            Vector3 v=new Vector3(-4.5f,-.3f,0)*(Times[i+1]-Times[i]);
            return (2*t*t*t-3*t*t+1)*from+(-2*t*t*t+3*t*t)*to+
                (i==1?t*t*t-t*t:t*t*t-2*t*t+t)*v;
        }
        public static void ApplyBody(Animator animator,Quaternion facing,float time,bool grounded)
        {
            if(time>=Duration)return;
            int i;float t;Segment(time,out i,out t);t=Ease(i,t);
            float turn=Mathf.Lerp(Turn[i],Turn[i+1],t),lean=Mathf.Lerp(Lean[i],Lean[i+1],t);
            var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
            var left=animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var right=animator.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 lp=left.position,rp=right.position;
            Quaternion lr=left.rotation,rr=right.rotation;
            if(grounded)
            {
                hips.position+=facing*Vector3.Lerp(Shift[i],Shift[i+1],t);
                hips.rotation=Quaternion.AngleAxis(turn*.25f,facing*Vector3.up)*hips.rotation;
                PlantLeg(animator,true,lp,lr,facing);
                PlantLeg(animator,false,rp,rr,facing);
            }
            var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
            var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest);
            spine.rotation=Quaternion.AngleAxis(turn*(grounded?.35f:.5f),facing*Vector3.up)*spine.rotation;
            chest.rotation=Quaternion.AngleAxis(turn*(grounded?.40f:.5f),facing*Vector3.up)*Quaternion.AngleAxis(lean,facing*Vector3.right)*chest.rotation;
        }
        static void PlantLeg(Animator animator,bool left,Vector3 target,Quaternion rotation,Quaternion facing)
        {
            var upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
            var lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
            var foot=animator.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
            RaccoonEquipmentPose.SolveArm(upper,lower,foot,target,upper.position+facing*new Vector3(left?-.12f:.12f,-.25f,.6f));
            foot.rotation=rotation;
        }
        public static void ApplyArms(Animator animator,RaccoonActionHandsV37 hands,Quaternion facing,float time,float weight)
        {
            if(time>=Duration)return;
            int i;float t;Segment(time,out i,out t);
            float eased=Ease(i,t),envelope=Envelope(i,eased)*weight;
            var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest);
            var upper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var lower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var localWrist=hand.localRotation;
            Quaternion gripFrame=Quaternion.Inverse(hand.rotation)*hands.HammerRotation(hand.rotation)*Quaternion.Euler(0,0,-90);
            Quaternion restBlade=Quaternion.Inverse(facing)*hand.rotation*gripFrame;
            Vector3 rest=Quaternion.Inverse(facing)*(hand.position-chest.position);
            Vector3 from=i==0?rest:Reach[i],to=i==Times.Length-2?rest:Reach[i+1];
            RaccoonMacheteArmPoseV83.SolveWeaponArm(animator,facing,Vector3.Lerp(hand.position,chest.position+facing*Arc(from,to,i,t),weight),envelope);
            hand.localRotation=localWrist;
            // Authored forearm roll avoids the shortest-axis solution changing
            // branches as the horizontal blade passes the elbow's direction.
            lower.rotation=Quaternion.AngleAxis(Mathf.Lerp(ForearmRoll[i],ForearmRoll[i+1],eased)*weight,(hand.position-lower.position).normalized)*lower.rotation;
            Quaternion bladeRotation=Quaternion.Slerp(i==0?restBlade:Quaternion.Euler(BladeAngles[i]),i==Times.Length-2?restBlade:Quaternion.Euler(BladeAngles[i+1]),eased);
            hand.rotation=facing*bladeRotation*Quaternion.Inverse(gripFrame);
            RaccoonMacheteArmPoseV83.ClearHead(animator,hands,facing);
            RaccoonHammerGripV44.Apply(animator);

            // The free arm opens behind the cutting arm to balance the weight transfer.
            float opening=Mathf.SmoothStep(0,1,Mathf.InverseLerp(Times[1],Times[3],time));
            RaccoonMacheteArmPoseV83.FreeArm(animator,facing,envelope,opening);
        }
    }
}
