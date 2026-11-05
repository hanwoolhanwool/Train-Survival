using UnityEngine;
using Game.Gameplay.Player;

namespace Game.Gameplay.Debugging
{
    // Owns resting hands only. Grips, swimming, climbing and task hands keep their
    // authored contacts. The original mesh, bind rig and animation clips stay intact.
    public sealed class RaccoonFreeHandPose
    {
        readonly Animator animator;
        readonly Transform root;
        readonly RaccoonActionHandsV37 hands;
        readonly Quaternion[][] fingers={new Quaternion[15],new Quaternion[15]};
        static readonly RaccoonActionLibraryV36.Entry Neutral=new RaccoonActionLibraryV36.Entry{Id="Neutral",Loop=true,Prop=""};

        public RaccoonFreeHandPose(Animator animator,Transform root)
        {
            this.animator=animator;this.root=root;hands=new RaccoonActionHandsV37(animator);
            var bones=animator.GetComponentsInChildren<Transform>();
            var positions=new Vector3[bones.Length];var rotations=new Quaternion[bones.Length];
            for(int i=0;i<bones.Length;i++){positions[i]=bones[i].localPosition;rotations[i]=bones[i].localRotation;}
            using(var handler=new HumanPoseHandler(animator.avatar,animator.transform))
            {
                var pose=new HumanPose();handler.GetHumanPose(ref pose);
                for(int i=0;i<HumanTrait.MuscleCount;i++)
                {
                    string name=HumanTrait.MuscleName[i];
                    if(name.Contains("Thumb")||name.Contains("Index")||name.Contains("Middle")||name.Contains("Ring")||name.Contains("Little"))
                        pose.muscles[i]=RaccoonNeutralPoseV39.Finger(name);
                }
                handler.SetHumanPose(ref pose);
                for(int side=0;side<2;side++)for(int i=0;i<15;i++)
                    fingers[side][i]=animator.GetBoneTransform((HumanBodyBones)((int)(side==0?HumanBodyBones.LeftThumbProximal:HumanBodyBones.RightThumbProximal)+i)).localRotation;
            }
            for(int i=0;i<bones.Length;i++){bones[i].localPosition=positions[i];bones[i].localRotation=rotations[i];}
        }

        public void Apply(bool leftFree,bool rightFree,float swing,float extraClearance,float run=0)
        {
            for(int side=0;side<2;side++)
            {
                bool left=side==0;if(left?!leftFree:!rightFree)continue;
                var upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                var lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                var hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                Vector3 target=root.TransformPoint(RaccoonNeutralPoseV39.HandTarget(animator,root,left,swing,extraClearance,run));
                Vector3 pole=upper.position+root.TransformVector(RaccoonNeutralPoseV39.PoleOffset(left));
                RaccoonEquipmentPose.SolveArm(upper,lower,hand,target,pole);
                for(int i=0;i<15;i++)
                    animator.GetBoneTransform((HumanBodyBones)((int)(left?HumanBodyBones.LeftThumbProximal:HumanBodyBones.RightThumbProximal)+i)).localRotation=fingers[side][i];
                hand.rotation=hands.Rotation(animator,root,Neutral,0,left);
            }
        }
    }
}
