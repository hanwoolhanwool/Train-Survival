using UnityEngine;
namespace Game.Gameplay.Debugging
{
    public static class RaccoonHammerPoseV42
    {
        public const float Scale=110,Duration=1.4f;
        public static readonly Vector3 Grip=new Vector3(-.00085f,0,.00054f);
        public static readonly Vector3 Face=new Vector3(.0014f,-.001195f,.00054f);
        public static readonly Vector3 Contact=new Vector3(.36f,1.28f,.70f);
        public static readonly Vector3 Pole=new Vector3(.28f,-.35f,-.15f);
        static float S(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
        public static float Lift(float t)
        {
            if(t<.08f)return 0;
            if(t<.56f)return S((t-.08f)/.48f);
            if(t<.79f)return 1-S((t-.56f)/.23f);
            if(t<.86f)return .055f*S((t-.79f)/.07f);
            return .055f*(1-S((t-.86f)/.09f));
        }
        public static Vector3 FaceTarget(float t)=>Contact+new Vector3(.06f,.20f,-.40f)*Lift(t);
        public static Vector3 FingerAxis(float t)=>Quaternion.AngleAxis(-45*Lift(t),Vector3.right)*Vector3.forward;
    }
}
