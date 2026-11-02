using UnityEngine;
namespace Game.Gameplay.Debugging
{
    // Calibrated local finger rotations for this raccoon's fitted hammer handle.
    // Keep this exact grip through idle, repair, and its entry/return blends.
    public static class RaccoonHammerGripV43
    {
        static readonly Quaternion[] Grip={
            new Quaternion(-0.3977472f,-0.7340621f,0.5421762f,0.09484243f), // RightThumbProximal
            new Quaternion(0.20725067f,0.115522593f,0.2183366f,-0.9465891f), // RightThumbIntermediate
            new Quaternion(0.02205849f,0.192684889f,0.235685825f,-0.9522805f), // RightThumbDistal
            new Quaternion(-0.113140456f,-0.759962559f,0.482408971f,-0.420639724f), // RightIndexProximal
            new Quaternion(-0.564919055f,-0.0554374978f,0.0242628045f,-0.822924435f), // RightIndexIntermediate
            new Quaternion(-0.498488128f,-0.03402725f,0.0170850456f,-0.866059959f), // RightIndexDistal
            new Quaternion(-0.207967311f,-0.7243053f,0.418703735f,-0.506772757f), // RightMiddleProximal
            new Quaternion(-0.555958f,-0.06044749f,0.0420264751f,-0.8279437f), // RightMiddleIntermediate
            new Quaternion(-0.489383966f,-0.0513513535f,0.03829989f,-0.869712353f), // RightMiddleDistal
            new Quaternion(-0.2946856f,-0.6504618f,0.374733835f,-0.591298938f), // RightRingProximal
            new Quaternion(-0.557124436f,-0.06577177f,0.063792f,-0.825358748f), // RightRingIntermediate
            new Quaternion(-0.487842232f,-0.0682769f,0.064185366f,-0.8678874f), // RightRingDistal
            new Quaternion(-0.427410662f,-0.5379075f,0.303171158f,-0.6603507f), // RightLittleProximal
            new Quaternion(-0.5635464f,-0.07232654f,0.09761451f,-0.817101955f), // RightLittleIntermediate
            new Quaternion(-0.486565471f,-0.08756944f,0.104921639f,-0.8628889f), // RightLittleDistal
        };
        public static void Apply(Animator animator)
        {
            for(int i=0;i<Grip.Length;i++)
                animator.GetBoneTransform((HumanBodyBones)((int)HumanBodyBones.RightThumbProximal+i)).localRotation=Grip[i];
        }
    }
}
