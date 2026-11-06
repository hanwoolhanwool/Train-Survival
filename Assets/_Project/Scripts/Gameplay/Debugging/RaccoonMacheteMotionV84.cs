using UnityEngine;

namespace Game.Gameplay.Debugging
{
    // Joint-space animation: no moving IK poles, reach clamps or independent
    // world-space wrist targets. Both takes retain the normal local grip.
    public static class RaccoonMacheteMotionV84
    {
        public const float Duration=1.5f;
        // v85 choreography: folded preparation, elbow-led cut, then slower recovery.
        static readonly float[] Times={0,.34f,.52f,.76f,.94f,Duration};
        // Shoulder lift/yaw/spread, elbow flex delta, forearm roll, torso yaw/lean.
        static readonly float[,] High={
            {0,0,0,0,0,0,0},
            {55,48,18,38,-50,22,-3},
            {65,0,12,-5,-40,-5,5},
            {15,-55,12,-32,-30,-30,8},
            {8,-45,10,-22,-20,-24,4},
            {0,0,0,0,0,0,0}};
        static readonly float[,] Low={
            {0,0,0,0,0,0,0},
            {15,55,20,32,-50,24,-3},
            {45,0,14,-5,-40,-5,4},
            {30,-60,12,-32,-30,-32,6},
            {20,-45,10,-20,-20,-24,3},
            {0,0,0,0,0,0,0}};

        static float Slope(float[,] poses,int k,int column)
        {
            if(k==0||k==Times.Length-1)return 0;
            float h0=Times[k]-Times[k-1],h1=Times[k+1]-Times[k];
            float d0=(poses[k,column]-poses[k-1,column])/h0,d1=(poses[k+1,column]-poses[k,column])/h1;
            if(d0*d1<=0)return 0;
            float w0=2*h1+h0,w1=h1+2*h0;
            return (w0+w1)/(w0/d0+w1/d1);
        }
        static float Value(bool low,float time,int column)
        {
            var poses=low?Low:High;int k=0;
            time=Mathf.Clamp(time,0,Duration);
            while(k<Times.Length-2&&time>Times[k+1])k++;
            float h=Times[k+1]-Times[k],u=(time-Times[k])/h,u2=u*u,u3=u2*u;
            return (2*u3-3*u2+1)*poses[k,column]+(u3-2*u2+u)*h*Slope(poses,k,column)
                +(-2*u3+3*u2)*poses[k+1,column]+(u3-u2)*h*Slope(poses,k+1,column);
        }
        public static void ApplyBody(Animator animator,Quaternion facing,float time,bool low)
        {
            if(time>=Duration)return;
            float turn=Value(low,time,5),lean=Value(low,time,6);
            var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
            var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest);
            spine.rotation=Quaternion.AngleAxis(turn*.4f,facing*Vector3.up)*spine.rotation;
            chest.rotation=Quaternion.AngleAxis(turn*.6f,facing*Vector3.up)*Quaternion.AngleAxis(lean,facing*Vector3.right)*chest.rotation;
        }
        public static void ApplyArms(Animator animator,Quaternion facing,float time,bool low,float weight)
        {
            if(time>=Duration)return;
            RotateArm(animator,false,facing,Value(low,time,0)*weight,Value(low,time,1)*weight,
                Value(low,time,2)*weight,Value(low,time,3)*weight,Value(low,time,4)*weight);
            // A small, relaxed counter-motion. Fingers and wrist remain the free
            // hand pose evaluated before this layer, including during recovery.
            float activity=Value(low,time,0)/35f;
            RotateArm(animator,true,facing,activity*12,activity*-8,activity*10,activity*-4,0);
            RaccoonHammerGripV44.Apply(animator);
        }
        static void RotateArm(Animator animator,bool left,Quaternion facing,float lift,float yaw,float spread,float flex,float roll)
        {
            var upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            Vector3 hinge=Vector3.Cross(lower.position-upper.position,hand.position-lower.position).normalized;
            Vector3 localHinge=upper.InverseTransformDirection(hinge);
            Quaternion wrist=hand.localRotation;
            upper.rotation=Quaternion.AngleAxis(yaw,facing*Vector3.up)*Quaternion.AngleAxis(-lift,facing*Vector3.right)
                *Quaternion.AngleAxis(left?-spread:spread,facing*Vector3.forward)*upper.rotation;
            lower.localRotation=Quaternion.AngleAxis(flex,localHinge)*lower.localRotation;
            lower.rotation=Quaternion.AngleAxis(roll,(hand.position-lower.position).normalized)*lower.rotation;
            hand.localRotation=wrist;
        }
    }
}
