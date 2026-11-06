using UnityEngine;
using Game.Gameplay.Player;

namespace Game.Gameplay.Debugging
{
    // Shared clearance and recovery for both review takes. Targets follow the
    // actual turning torso, rather than treating the character as facing forward.
    public static class RaccoonMacheteArmPoseV83
    {
        public static Quaternion BodyFrame(Animator animator,Quaternion facing)
        {
            Vector3 right=animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position-
                animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position;
            Vector3 up=facing*Vector3.up;
            return Quaternion.LookRotation(Vector3.Cross(right,up).normalized,up);
        }

        public static void SolveWeaponArm(Animator animator,Quaternion facing,Vector3 target,float envelope)
        {
            var upper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var lower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest);
            Quaternion frame=BodyFrame(animator,facing);
            Vector3 originalTarget=target,restElbow=lower.position;
            Vector3 local=Quaternion.Inverse(frame)*(target-chest.position);
            // This rig has a long upper arm and a short forearm. Asking the hand
            // to reach past the opposite side forces a straight arm through the
            // ribcage; let the torso and blade carry that part of the sweep.
            local.x=Mathf.Max(local.x,.12f);
            // Shirt, hand thickness and the handle need room in front of the body.
            float side=Mathf.Abs(local.x)/.44f;
            if(side<1)
                local.z=Mathf.Max(local.z,.43f*Mathf.Sqrt(1-side*side));
            Vector3 shoulder=Quaternion.Inverse(frame)*(upper.position-chest.position);
            float reach=(Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position))*.97f;
            // Preserve forward clearance when a low target is beyond reach.
            // Move the target toward the weapon shoulder, never back into the body.
            for(int pass=0;pass<12&&(local-shoulder).magnitude>reach;pass++)
            {
                local.x=Mathf.Lerp(local.x,Mathf.Max(.32f,shoulder.x),.2f);
                local.y=Mathf.Lerp(local.y,-.08f,.2f);
            }
            target=Vector3.Lerp(originalTarget,chest.position+frame*local,envelope);
            // A forward elbow keeps the entire upper/lower arm outside the chest.
            Vector3 pole=Vector3.Lerp(restElbow,upper.position+frame*new Vector3(.40f,-.16f,.85f),envelope);
            RaccoonEquipmentPose.SolveArm(upper,lower,hand,target,pole);
            // Interpolate the elbow hint back to the actual resting elbow. Blending
            // local arm rotations instead would bow the hand path into the chest.
        }

        public static void FreeArm(Animator animator,Quaternion facing,float envelope,float opening)
        {
            var upper=animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            var lower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Quaternion frame=BodyFrame(animator,facing),ur=upper.localRotation,lr=lower.localRotation,hr=hand.localRotation;
            // Keep a comfortable elbow angle and the hand outside the shoulder.
            // Preserve the relaxed fingers supplied by the normal free-hand pose.
            Vector3 offset=Vector3.Lerp(new Vector3(-.16f,-.32f,.23f),new Vector3(-.22f,-.38f,.04f),opening);
            RaccoonEquipmentPose.SolveArm(upper,lower,hand,upper.position+frame*offset,upper.position+frame*new Vector3(-.55f,-.35f,.15f));
            upper.localRotation=Quaternion.Slerp(ur,upper.localRotation,envelope);
            lower.localRotation=Quaternion.Slerp(lr,lower.localRotation,envelope);
            hand.localRotation=hr;
        }

        public static void ClearHead(Animator animator,RaccoonActionHandsV37 hands,Quaternion facing)
        {
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var head=animator.GetBoneTransform(HumanBodyBones.Head);
            Vector3 center=head.position+facing*new Vector3(0,.28f,.08f);
            Quaternion original=hand.rotation;
            if(HeadClearance(hand,hands,facing,center)>=1)return;
            // Tilt outward only as far as needed. Include blade width, not just
            // its tip, and recompute the grip offset for the candidate rotation.
            float low=0,high=70;
            for(int pass=0;pass<12;pass++)
            {
                float angle=(low+high)*.5f;
                hand.rotation=Quaternion.AngleAxis(-angle,facing*Vector3.forward)*original;
                if(HeadClearance(hand,hands,facing,center)<1)low=angle;else high=angle;
            }
            hand.rotation=Quaternion.AngleAxis(-high,facing*Vector3.forward)*original;
        }
        static float HeadClearance(Transform hand,RaccoonActionHandsV37 hands,Quaternion facing,Vector3 center)
        {
            Vector3 grip=hands.HammerGripCenter(hand,RaccoonHammerGripV44.PalmClearance);
            Vector3 axis=hands.HammerRotation(hand.rotation)*Quaternion.Euler(0,0,-90)*Vector3.up;
            Vector3 a=Quaternion.Inverse(facing)*(grip+axis*.10f-center);
            Vector3 b=Quaternion.Inverse(facing)*(grip+axis*.76f-center);
            a=new Vector3(a.x/.50f,a.y/.47f,a.z/.46f);
            b=new Vector3(b.x/.50f,b.y/.47f,b.z/.46f);
            Vector3 d=b-a;
            return (a+d*Mathf.Clamp01(-Vector3.Dot(a,d)/Mathf.Max(.000001f,d.sqrMagnitude))).magnitude;
        }
    }
}
