using UnityEngine;
namespace Game.Gameplay.Debugging
{
    // One mirrored free-hand path shared by idle, locomotion and one-hand equipment.
    public static class RaccoonNeutralPoseV39
    {
        public const float SideClearance=.47f;
        public static Vector3 HandTarget(Animator a,Transform root,bool left,float swing=0,float extraClearance=0,float run=0)
        {
            Vector3 hip=root.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.Hips).position);
            swing=Mathf.Clamp(swing,-.16f,.16f);
            float height=hip.y-.15f;float width=SideClearance+Mathf.Clamp(extraClearance,0,.025f);
            // The source bind rig has unequal clavicle/upper-arm lengths. Do not
            // turn that into unequal idle hands: choose a height both can reach.
            for(int i=0;i<2;i++)
            {
                bool l=i==0;float side=l?-1:1;
                Vector3 upper=root.InverseTransformPoint(a.GetBoneTransform(l?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm).position);
                Vector3 lower=root.InverseTransformPoint(a.GetBoneTransform(l?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm).position);
                Vector3 hand=root.InverseTransformPoint(a.GetBoneTransform(l?HumanBodyBones.LeftHand:HumanBodyBones.RightHand).position);
                float reach=(Vector3.Distance(upper,lower)+Vector3.Distance(lower,hand))*.985f;
                float dx=hip.x+side*width-upper.x,dz=hip.z+.02f+side*swing-upper.z;
                height=Mathf.Max(height,upper.y-Mathf.Sqrt(Mathf.Max(.001f,reach*reach-dx*dx-dz*dz)));
            }
            run=Mathf.Clamp01(run);
            return new Vector3(hip.x+(left?-width:width),height+.10f*run,hip.z+.02f+.07f*run+(left?-swing:swing));
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
