using UnityEngine;
namespace Game.Gameplay.Debugging
{
    // Alternate finger pose fitted around the unmodified source hammer.
    public static class RaccoonHammerGripV44
    {
        public const float PalmClearance=.025f;
        static readonly Quaternion[] Grip={
            // Lower the upper knuckle arc while preserving the distal world pose/contact.
            new Quaternion(-0.4934491f,-0.5793072f,0.6419450f,0.0939035f), // RightThumbProximal
            new Quaternion(0.3735116f,0.0017701f,-0.0022337f,-0.9276212f), // RightThumbIntermediate
            new Quaternion(0.1030215f,0.2314238f,0.1914486f,-0.9482496f), // RightThumbDistal
            new Quaternion(0.00185741414f,-0.8390363f,0.357675642f,-0.4099791f), // RightIndexProximal
            new Quaternion(-0.234815836f,0.115992188f,-0.250349343f,-0.9320583f), // RightIndexIntermediate
            new Quaternion(-0.310819924f,-0.0593165942f,0.0528083481f,-0.947145164f), // RightIndexDistal
            new Quaternion(-0.0673243552f,-0.808608f,0.219922855f,-0.5415299f), // RightMiddleProximal
            new Quaternion(-0.313426882f,0.00494372565f,-0.08648687f,-0.9456528f), // RightMiddleIntermediate
            new Quaternion(-0.352908f,-0.0349704176f,-0.00176554464f,-0.9350026f), // RightMiddleDistal
            new Quaternion(-0.128623337f,-0.7200259f,0.131259724f,-0.6691709f), // RightRingProximal
            new Quaternion(-0.269344836f,-0.09822678f,0.063510634f,-0.955913842f), // RightRingIntermediate
            new Quaternion(-0.314790249f,-0.03376365f,-0.0185044035f,-0.948380053f), // RightRingDistal
            new Quaternion(-0.186437115f,-0.600412667f,0.08738615f,-0.7727286f), // RightLittleProximal
            new Quaternion(-0.179154f,-0.158167526f,0.1369881f,-0.961312234f), // RightLittleIntermediate
            new Quaternion(-0.25099808f,-0.07287783f,0.0123568652f,-0.965161145f), // RightLittleDistal
        };
        public static void Apply(Animator animator)
        {
            for(int i=0;i<Grip.Length;i++)
                animator.GetBoneTransform((HumanBodyBones)((int)HumanBodyBones.RightThumbProximal+i)).localRotation=Grip[i];
        }
    }
}
