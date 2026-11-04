using UnityEngine;
using Game.Gameplay.Player;

namespace Game.Gameplay.Debugging
{
    public static class RaccoonMachetePoseV74
    {
        public const float Duration=1.15f;
        static readonly float[] Times={0,.27f,.50f,.68f,1.15f};
        static readonly float[] Turns={0,-12,15,19,0};
        static readonly Vector2[] HammerArc={Vector2.zero,new Vector2(-25,-24),new Vector2(28,28),new Vector2(35,38),Vector2.zero};
        static readonly Vector3[] Positions={new Vector3(.31f,-.29f,.23f),new Vector3(.43f,-.03f,.19f),new Vector3(-.04f,-.18f,.50f),new Vector3(-.11f,-.30f,.39f),new Vector3(.31f,-.29f,.23f)};
        static readonly Vector3[] Rotations={new Vector3(65,0,-12),new Vector3(-32,0,-28),new Vector3(106,0,30),new Vector3(120,0,40),new Vector3(65,0,-12)};
        static void Segment(float time,out int index,out float weight)
        {
            index=0;time=Mathf.Clamp(time,0,Duration);
            while(index<Times.Length-2&&time>Times[index+1])index++;
            weight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(Times[index],Times[index+1],time));
        }
        public static float TorsoTurn(float time)
        {
            if(time>=Duration)return 0;
            int i;float t;Segment(time,out i,out t);
            return Mathf.Lerp(Turns[i],Turns[i+1],t);
        }
        public static void Apply(Animator animator,Transform root,RaccoonActionHandsV37 hands,Quaternion facing,float time,float weight)
        {
            var upper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            var lower=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest);
            int i;float t;Segment(time>=Duration?0:time,out i,out t);
            Vector3 offset=Vector3.Lerp(Positions[i],Positions[i+1],t);
            Quaternion weaponRotation=facing*Quaternion.Slerp(Quaternion.Euler(Rotations[i]),Quaternion.Euler(Rotations[i+1]),t);
            Quaternion inHand=Quaternion.Inverse(hand.rotation)*hands.HammerRotation(hand.rotation)*Quaternion.Euler(0,0,-90);
            var desiredRotation=weaponRotation*Quaternion.Inverse(inHand);
            var target=chest.position+facing*offset;
            var previous=hand.rotation;
            RaccoonEquipmentPose.SolveArm(upper,lower,hand,Vector3.Lerp(hand.position,target,weight),upper.position+facing*new Vector3(.40f,-.4f,-.18f));
            hand.rotation=Quaternion.Slerp(previous,desiredRotation,weight);
            if(weight>.999f)RaccoonHammerGripV44.Apply(animator);
        }
        public static void Place(Transform prop,Transform hand,RaccoonActionHandsV37 hands)
        {
            prop.rotation=hands.HammerRotation(hand.rotation)*Quaternion.Euler(0,0,-90)*Quaternion.Euler(0,-90,0);
            prop.position=hands.HammerGripCenter(hand,RaccoonHammerGripV44.PalmClearance);
            // The exported mesh is in metres with its origin at the handle centre.
            var parentScale=prop.parent==null?Vector3.one:prop.parent.lossyScale;
            prop.localScale=new Vector3(1/parentScale.x,1/parentScale.y,1/parentScale.z);
        }
        // v75 keeps the accepted hammer elbow, wrist and finger pose. The swing
        // rotates that arm chain as a whole; no independent wrist twist is added.
        public static void ApplyHammerAttack(Animator animator,Quaternion facing,float time,float weight)
        {
            if(time>=Duration)return;
            int i;float t;Segment(time,out i,out t);
            Vector2 angles=Vector2.Lerp(HammerArc[i],HammerArc[i+1],t);
            var q=Quaternion.AngleAxis(angles.x,facing*Vector3.up)*Quaternion.AngleAxis(angles.y,facing*Vector3.right);
            var upper=animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            upper.rotation=Quaternion.Slerp(Quaternion.identity,q,weight)*upper.rotation;
        }
    }
}
