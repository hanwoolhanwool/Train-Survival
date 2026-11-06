using UnityEngine;
using Game.Gameplay.Player;

namespace Game.Gameplay.Debugging
{
    // A deliberate wind-up, one uninterrupted diagonal cut, then a slower recovery.
    // Targets are in the facing frame, so the same choreography also follows aiming.
    public static class RaccoonMacheteSlashV81
    {
        public const float Duration=1.35f;
        static readonly float[] Times={0,.30f,.45f,.61f,.78f,Duration};
        static readonly Vector3[] Reach={Vector3.zero,new Vector3(.55f,.14f,.07f),new Vector3(.26f,-.02f,.52f),new Vector3(-.18f,-.27f,.44f),new Vector3(-.12f,-.36f,.36f),Vector3.zero};
        static readonly float[] ForearmRoll={0,-15,-30,-10,-5,0};
        static readonly Vector3[] BladeAngles={Vector3.zero,new Vector3(-45,0,-35),new Vector3(75,0,30),new Vector3(105,0,55),new Vector3(90,0,45),Vector3.zero};
        static readonly float[] Turn={0,30,-6,-38,-32,0};
        static readonly float[] Lean={0,-5,4,9,7,0};

        static void Segment(float time,out int i,out float t)
        {
            i=0;time=Mathf.Clamp(time,0,Duration);
            while(i<Times.Length-2&&time>Times[i+1])i++;
            t=Mathf.InverseLerp(Times[i],Times[i+1],time);
        }
        static float Ease(int i,float t)=>i==1?t*t:i==2?1-(1-t)*(1-t):Mathf.SmoothStep(0,1,t);
        static Vector3 HandArc(Vector3 from,Vector3 to,int i,float t)
        {
            if(i!=1&&i!=2)return Vector3.Lerp(from,to,Ease(i,t));
            // A shared tangent at impact keeps the hand moving through the target.
            // The forward waypoint prevents the blade taking a shortcut through the face.
            Vector3 velocity=new Vector3(-3,-1.8f,0)*(Times[i+1]-Times[i]);
            Vector3 m0=i==2?velocity:Vector3.zero,m1=i==1?velocity:Vector3.zero;
            return (2*t*t*t-3*t*t+1)*from+(t*t*t-2*t*t+t)*m0+(-2*t*t*t+3*t*t)*to+(t*t*t-t*t)*m1;
        }
        static float Envelope(int i,float t)=>i==0?t:i==Times.Length-2?1-t:1;

        public static void ApplyBody(Animator animator,Quaternion facing,float time)
        {
            if(time>=Duration)return;
            int i;float t;Segment(time,out i,out t);t=Ease(i,t);
            float turn=Mathf.Lerp(Turn[i],Turn[i+1],t),lean=Mathf.Lerp(Lean[i],Lean[i+1],t);
            var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
            var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest);
            // Distribute the twist through the back; keep the pelvis and planted feet stable.
            spine.rotation=Quaternion.AngleAxis(turn*.4f,facing*Vector3.up)*spine.rotation;
            chest.rotation=Quaternion.AngleAxis(turn*.6f,facing*Vector3.up)*Quaternion.AngleAxis(lean,facing*Vector3.right)*chest.rotation;
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
            Vector3 rest=Quaternion.Inverse(facing)*(hand.position-chest.position);
            Vector3 from=i==0?rest:Reach[i],to=i==Times.Length-2?rest:Reach[i+1];
            Vector3 target=chest.position+facing*HandArc(from,to,i,t);
            var localWrist=hand.localRotation;
            Quaternion gripFrame=Quaternion.Inverse(hand.rotation)*hands.HammerRotation(hand.rotation)*Quaternion.Euler(0,0,-90);
            Quaternion restBlade=Quaternion.Inverse(facing)*hand.rotation*gripFrame;
            RaccoonMacheteArmPoseV83.SolveWeaponArm(animator,facing,Vector3.Lerp(hand.position,target,weight),envelope);
            hand.localRotation=localWrist;
            // Author continuous forearm roll and complete blade frames. An axis-only
            // solve could flip the wrist when that axis passed behind the arm.
            lower.rotation=Quaternion.AngleAxis(Mathf.Lerp(ForearmRoll[i],ForearmRoll[i+1],eased)*weight,(hand.position-lower.position).normalized)*lower.rotation;
            Quaternion bladeRotation=Quaternion.Slerp(i==0?restBlade:Quaternion.Euler(BladeAngles[i]),i==Times.Length-2?restBlade:Quaternion.Euler(BladeAngles[i+1]),eased);
            hand.rotation=facing*bladeRotation*Quaternion.Inverse(gripFrame);
            RaccoonMacheteArmPoseV83.ClearHead(animator,hands,facing);
            RaccoonHammerGripV44.Apply(animator);

            RaccoonMacheteArmPoseV83.FreeArm(animator,facing,envelope,0);
        }
    }
}
