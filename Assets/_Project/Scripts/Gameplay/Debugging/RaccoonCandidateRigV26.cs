using System;
using System.Linq;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>Explicit evaluation for the isolated review prefab. Production prefabs never reference it.</summary>
    public sealed class RaccoonCandidateRigV26 : MonoBehaviour
    {
        private Animator animator;
        private RaccoonEquipmentPose equipment;
        private RaccoonTailMotion tail;
        private RaccoonElbowCorrectives elbows;
        private RaccoonCandidateContactsV26 walkGrounding;
        private Transform root;
        private Transform[] bones;
        private Vector3[] rawPositions;
        private Quaternion[] rawRotations;
        private bool initialized,walking=true;
        private float previousHeading;
        public float RunBlend {get;private set;}
        public Animator Animator=>animator;
        // Matches the candidate BlendTree's walk child offset; source walk clip is unchanged.
        public const float WalkCycleOffset=28f/120;

        public void Initialize(Transform movementRoot)
        {
            root=movementRoot;animator=GetComponent<Animator>();
            equipment=GetComponent<RaccoonEquipmentPose>();tail=GetComponent<RaccoonTailMotion>();
            elbows=GetComponent<RaccoonElbowCorrectives>();
            // One coordinator owns ordering, so the old components cannot solve the same pose twice.
            foreach(var behaviour in GetComponents<MonoBehaviour>())if(behaviour!=this)behaviour.enabled=false;
            animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();animator.Update(0);animator.speed=0;
            animator.SetInteger("HoldPose",2);animator.SetFloat("RaccoonShotgunRun",1);
            bones=GetComponentInChildren<SkinnedMeshRenderer>().bones;
            rawPositions=new Vector3[bones.Length];
            rawRotations=new Quaternion[bones.Length];
            walkGrounding=new RaccoonCandidateContactsV26(animator,root);
            tail.ResetMotion();previousHeading=root.eulerAngles.y;initialized=true;walking=true;
        }

        public void Tick(float dt,float speed,bool grounded)
        {
            // Unity can restore the flag and object references after a script reload,
            // while the non-serialized contact solver is recreated from scratch.
            if(!initialized||walkGrounding==null)Initialize(root!=null?root:transform.parent!=null?transform.parent:transform);
            if(dt<=0||!float.IsFinite(dt)||!float.IsFinite(speed))return;
            dt=Mathf.Min(dt,.05f);speed=Mathf.Max(0,speed);
            RunBlend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,2.1f,speed));
            float parameter=speed<=.8f?speed/.8f*4.5f:4.5f+2.5f*RunBlend;
            animator.SetFloat("Speed",parameter);
            animator.SetLayerWeight(animator.GetLayerIndex("Hold"),1-RunBlend);
            animator.speed=1;animator.Update(dt);animator.speed=0;
            for(int i=0;i<bones.Length;i++){rawPositions[i]=bones[i].localPosition;rawRotations[i]=bones[i].localRotation;}
            // The approved hybrid clip owns the running stride and pelvis.
            // Fade out only the walk correction; never replace the run with v21 foot paths.
            bool useWalk=grounded&&RunBlend<.999f;
            if(useWalk)
            {
                if(!walking)walkGrounding.ResetContacts(true);
                walkGrounding.Step(dt,speed,Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime+WalkCycleOffset,1));
            }
            walking=useWalk;
            float weight=useWalk?1-RunBlend:0;
            for(int i=0;i<bones.Length;i++)
            {
                bones[i].localPosition=Vector3.Lerp(rawPositions[i],bones[i].localPosition,weight);
                bones[i].localRotation=Quaternion.Slerp(rawRotations[i],bones[i].localRotation,weight);
            }
            if(grounded)walkGrounding.ClearSoles();
            var head=animator.GetBoneTransform(HumanBodyBones.Head);
            head.rotation=Quaternion.AngleAxis(equipment.Profile.HeadPitchFor(animator),root.right)*head.rotation;
            equipment.Apply(HotbarItemType.Shotgun,false,0,dt,root);
            float heading=root.eulerAngles.y;
            tail.Step(speed,Mathf.DeltaAngle(previousHeading,heading)/dt,dt);previousHeading=heading;
            if(grounded)walkGrounding.FitTail(dt);
            elbows.Apply();
        }
    }
}
