using System;
using System.Linq;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>Explicit evaluation for the isolated review prefab. Production prefabs never reference it.</summary>
    public sealed class RaccoonDirectionalRigV29 : MonoBehaviour
    {
        private Animator animator;
        private RaccoonEquipmentPose equipment;
        private RaccoonTailMotion tail;
        private RaccoonElbowCorrectives elbows;
        private RaccoonDirectionalContactsV30 walkGrounding;
        private Transform root;
        private Transform[] bones;
        private Vector3[] rawPositions;
        private Quaternion[] rawRotations;
        private bool initialized,walking=true;
        private float previousHeading;
        private Vector2 direction=Vector2.up;
        private Quaternion stableHead;
        private bool hasStableHead;
        private float headStabilityWeight;
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
            walkGrounding=new RaccoonDirectionalContactsV30(animator,root);
            direction=Vector2.up;hasStableHead=false;headStabilityWeight=0;tail.ResetMotion();previousHeading=root.eulerAngles.y;initialized=true;walking=true;
        }

        public void Tick(float dt,Vector2 velocity,bool sprint,bool grounded)
        {
            // Unity can restore the flag and object references after a script reload,
            // while the non-serialized contact solver is recreated from scratch.
            if(!initialized||walkGrounding==null)Initialize(root!=null?root:transform.parent!=null?transform.parent:transform);
            float speed=velocity.magnitude;
            if(dt<=0||!float.IsFinite(dt)||!float.IsFinite(speed))return;
            dt=Mathf.Min(dt,.05f);speed=Mathf.Max(0,speed);
            // Interpolate the vector without normalizing each intermediate value: normalizing
            // a left-to-right blend can keep it stuck pointing left, or detour through forward.
            if(speed>.025f) direction=Vector2.MoveTowards(direction,velocity.normalized,dt*6);
            animator.SetFloat("MoveX",direction.x);animator.SetFloat("MoveZ",direction.y);
            float forwardWeight=Mathf.Clamp01(direction.y)*(1-Mathf.Abs(direction.x));
            float nominal=RaccoonDirectionalMotorV29.WalkSpeed(speed>.025f?velocity:direction);
            float scaled=speed/nominal*.8f;
            RunBlend=forwardWeight*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,2.1f,speed));
            float parameter=scaled<=.8f?scaled/.8f*4.5f:4.5f+2.5f*RunBlend;
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
            StabilizeDirectionalHead(head,dt);
            float heading=root.eulerAngles.y;
            tail.Step(speed,Mathf.DeltaAngle(previousHeading,heading)/dt,dt);previousHeading=heading;
            if(grounded)walkGrounding.FitTail(dt);
            elbows.Apply();
        }

        private void StabilizeDirectionalHead(Transform head,float dt)
        {
            // Keep the gaze continuous through short idle blends during a strafe reversal.
            // Root-relative rotation follows intentional body turns immediately. Only the
            // head is adjusted, after arm solving, so grip anchors and the leg cycle stay intact.
            Quaternion target=Quaternion.Inverse(root.rotation)*head.rotation;
            if(!hasStableHead){stableHead=target;hasStableHead=true;}
            bool directional=Mathf.Abs(direction.x)>.05f||direction.y<-.05f;
            headStabilityWeight=Mathf.MoveTowards(headStabilityWeight,directional?1:0,dt/.2f);
            if(headStabilityWeight>0)
            {
                Quaternion eased=Quaternion.Slerp(stableHead,target,1-Mathf.Exp(-dt*12));
                stableHead=Quaternion.RotateTowards(stableHead,eased,100*dt);
                head.rotation=root.rotation*Quaternion.Slerp(target,stableHead,headStabilityWeight);
            }
            else stableHead=target;
        }
    }
}


