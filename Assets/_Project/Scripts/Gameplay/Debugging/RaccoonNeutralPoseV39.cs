using UnityEngine;
namespace Game.Gameplay.Debugging
{
    // A relaxed hanging arm is specified by limb direction and actual limb lengths,
    // rather than forcing both wrists to the same arbitrary waist-height point.
    public static class RaccoonNeutralPoseV39
    {
        public static Vector3 HandTarget(Animator a,Transform root,bool left)
        {
            var upper=a.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=a.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            float side=left?-1:1;
            var upperDirection=new Vector3(side*.30f,-.95f,-.10f).normalized;
            var lowerDirection=new Vector3(side*.23f,-.94f,.30f).normalized;
            return root.InverseTransformPoint(upper.position)+upperDirection*Vector3.Distance(upper.position,lower.position)+lowerDirection*Vector3.Distance(lower.position,hand.position);
        }
        public static Vector3 PoleOffset(bool left)=>new Vector3(left?-.05f:.05f,-.35f,-.45f);
        public static float Finger(string name)
        {
            if(name.Contains("Thumb"))return name.Contains("Spread")?-.5f:name.Contains("1 Stretched")?-.65f:.35f;
            if(name.Contains("Spread"))return 0;
            return name.Contains("Little")?.42f:name.Contains("Ring")?.46f:.5f;
        }
    }
}
