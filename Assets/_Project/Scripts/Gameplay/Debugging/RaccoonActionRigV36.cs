using System;
using System.Linq;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    /// <summary>Explicit evaluation for the isolated review prefab. Production prefabs never reference it.</summary>
    public sealed class RaccoonActionRigV36 : MonoBehaviour
    {
        private RaccoonWeaponPoseV36 combat;
        private RaccoonActionReviewV36 actions;
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
        private bool directWalk;
        private readonly float[] walkWeights={1,0,0,0};
        private readonly float[] walkWeightVelocities=new float[4];
        private static readonly string[] walkParameters={"ReviewWalkForward","ReviewWalkLeft","ReviewWalkRight","ReviewWalkBack"};
        private float jumpBlend;
        private Quaternion stableHead;
        private bool hasStableHead;
        private bool jumpHeadActive;
        private float headStabilityWeight;
        public float RunBlend {get;private set;}
        public Animator Animator=>animator;
        // The v76 composer supplies equipment separately. Keep its movement body
        // free of the shotgun Hold layer, torso yaw, hand IK and context idle.
        public bool LocomotionBodyOnly {get;set;}
        public Vector2 LocomotionInput {get;set;}
        public float LocomotionWeight {get;private set;}
        // Matches the candidate BlendTree's walk child offset; source walk clip is unchanged.
        public const float WalkCycleOffset=28f/120;

        public void Initialize(Transform movementRoot)
        {
            root=movementRoot;combat=root.GetComponent<RaccoonWeaponPoseV36>();actions=root.GetComponent<RaccoonActionReviewV36>();animator=GetComponent<Animator>();
            equipment=GetComponent<RaccoonEquipmentPose>();tail=GetComponent<RaccoonTailMotion>();
            elbows=GetComponent<RaccoonElbowCorrectives>();
            // One coordinator owns ordering, so the old components cannot solve the same pose twice.
            foreach(var behaviour in GetComponents<MonoBehaviour>())if(behaviour!=this)behaviour.enabled=false;
            animator.enabled=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            animator.Rebind();animator.Update(0);animator.speed=0;
            animator.SetInteger("HoldPose",2);animator.SetFloat("RaccoonShotgunRun",1);
            animator.SetLayerWeight(animator.GetLayerIndex("Hold"),1);animator.Update(.5f);
            equipment.Apply(HotbarItemType.Shotgun,false,0,.5f,root);
            if(actions!=null)actions.CacheContacts(animator);
            bones=GetComponentInChildren<SkinnedMeshRenderer>().bones;
            rawPositions=new Vector3[bones.Length];
            rawRotations=new Quaternion[bones.Length];
            walkGrounding=new RaccoonDirectionalContactsV30(animator,root);
            directWalk=animator.parameters.Any(p=>p.name==walkParameters[0]);
            for(int i=0;i<4;i++){walkWeights[i]=i==0?1:0;walkWeightVelocities[i]=0;}
            direction=Vector2.up;LocomotionWeight=0;hasStableHead=false;jumpHeadActive=false;headStabilityWeight=0;tail.ResetMotion();previousHeading=root.eulerAngles.y;initialized=true;walking=true;
        }

        public void Tick(float dt,Vector2 velocity,bool sprint,bool grounded,float airborneWeight=0,float airbornePose=0)
        {
            // Unity can restore the flag and object references after a script reload,
            // while the non-serialized contact solver is recreated from scratch.
            if(!initialized||walkGrounding==null)Initialize(root!=null?root:transform.parent!=null?transform.parent:transform);
            if(actions!=null&&actions.SoftDeath)
            {
                actions.ApplySoftDeath();
                var deathWrist=GetComponent<RaccoonWristDeformation>();if(deathWrist!=null)deathWrist.Apply();
                elbows.Apply();return;
            }
            float speed=velocity.magnitude;
            if(dt<=0||!float.IsFinite(dt)||!float.IsFinite(speed))return;
            dt=Mathf.Min(dt,.05f);speed=Mathf.Max(0,speed);
            // Interpolate the vector without normalizing each intermediate value: normalizing
            // a left-to-right blend can keep it stuck pointing left, or detour through forward.
            // Reversing physical velocity crosses zero; that is not an idle request.
            // Keep the gait active and blend direction directly from review input.
            if(LocomotionBodyOnly)
            {
                bool requested=LocomotionInput.sqrMagnitude>.001f;
                LocomotionWeight=Mathf.MoveTowards(LocomotionWeight,requested?1:0,dt/(requested?.20f:.24f));
                if(requested)direction=Vector2.MoveTowards(direction,LocomotionInput.normalized,dt*6);
                if(directWalk)
                {
                    // Direct weights avoid the 2D tree's centre, which introduces
                    // forward/back clips during a pure left/right reversal.
                    float total=Mathf.Abs(LocomotionInput.x)+Mathf.Abs(LocomotionInput.y);
                    for(int i=0;i<4;i++)
                    {
                        float value=i==0?LocomotionInput.y:i==1?-LocomotionInput.x:i==2?LocomotionInput.x:-LocomotionInput.y;
                        if(requested)walkWeights[i]=Mathf.SmoothDamp(walkWeights[i],Mathf.Max(0,value)/total,ref walkWeightVelocities[i],.16f,Mathf.Infinity,dt);
                        animator.SetFloat(walkParameters[i],walkWeights[i]);
                    }
                }
            }
            else if(speed>.025f) direction=Vector2.MoveTowards(direction,velocity.normalized,dt*6);
            animator.SetFloat("MoveX",direction.x);animator.SetFloat("MoveZ",direction.y);
            float forwardWeight=Mathf.Clamp01(direction.y)*(1-Mathf.Abs(direction.x));
            float nominal=RaccoonDirectionalMotorV29.WalkSpeed(speed>.025f?velocity:direction);
            float scaled=speed/nominal*.8f;
            RunBlend=forwardWeight*Mathf.SmoothStep(0,1,Mathf.InverseLerp(.9f,2.1f,speed));
            float parameter=scaled<=.8f?scaled/.8f*4.5f:4.5f+2.5f*RunBlend;
            if(LocomotionBodyOnly)parameter=4.5f*LocomotionWeight+2.5f*RunBlend;
            animator.SetFloat("Speed",parameter);
            animator.SetLayerWeight(animator.GetLayerIndex("Hold"),1-RunBlend);
            jumpBlend=Mathf.Clamp01(airborneWeight);
            animator.SetFloat("JumpPhaseV32",airbornePose);
            animator.SetLayerWeight(animator.GetLayerIndex("Jump V32"),jumpBlend);
            if(actions!=null)actions.PrepareAnimator(animator);
            if(LocomotionBodyOnly)
            {
                foreach(string layer in new[]{"Hold","Review Context V38","Actions Upper V36","Actions Full V36"})
                {int index=animator.GetLayerIndex(layer);if(index>=0)animator.SetLayerWeight(index,0);}
            }
            animator.speed=1;animator.Update(dt);animator.speed=0;
            for(int i=0;i<bones.Length;i++){rawPositions[i]=bones[i].localPosition;rawRotations[i]=bones[i].localRotation;}
            // The approved hybrid clip owns the running stride and pelvis.
            // Fade out only the walk correction; never replace the run with v21 foot paths.
            bool fullAction=!LocomotionBodyOnly&&actions!=null&&actions.HasFullBodyPose;
            bool useWalk=grounded&&RunBlend<.999f&&jumpBlend<.001f&&!fullAction;
            if(useWalk)
            {
                if(!walking)walkGrounding.ResetContacts(true);
                float contactSpeed=LocomotionBodyOnly?Mathf.Max(speed,nominal*LocomotionWeight):speed;
                walkGrounding.Step(dt,contactSpeed,Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime+WalkCycleOffset,1),RunBlend<.05f,
                    LocomotionBodyOnly&&LocomotionInput.sqrMagnitude<=.001f);
            }
            walking=useWalk;
            float weight=useWalk?1-RunBlend:0;
            for(int i=0;i<bones.Length;i++)
            {
                bones[i].localPosition=Vector3.Lerp(rawPositions[i],bones[i].localPosition,weight);
                bones[i].localRotation=Quaternion.Slerp(rawRotations[i],bones[i].localRotation,weight);
            }
            if(grounded&&!fullAction)walkGrounding.ClearSoles();
            var head=animator.GetBoneTransform(HumanBodyBones.Head);
            if(!fullAction)head.rotation=Quaternion.AngleAxis(equipment.Profile.HeadPitchFor(animator),root.right)*head.rotation;
            if(!LocomotionBodyOnly&&(actions==null||(actions.UsesShotgunBase&&(!actions.Active||actions.Entry.UpperBody||actions.ShotgunHit))))
            {
                equipment.Apply(HotbarItemType.Shotgun,false,0,dt,root);
                if(combat!=null)combat.Apply(animator,root);
                StabilizeDirectionalHead(head,dt);
            }
            float heading=root.eulerAngles.y;
            tail.Step(speed,Mathf.DeltaAngle(previousHeading,heading)/dt,dt);previousHeading=heading;
            if(grounded&&!fullAction)walkGrounding.FitTail(dt);
            if(actions!=null&&!LocomotionBodyOnly)actions.ApplyContacts(animator);
            if(actions!=null&&!LocomotionBodyOnly)actions.ApplyShotgunActions(velocity,RunBlend,jumpBlend);
            // Extra wrist joints are not Humanoid bones; update them for death too.
            var wrist=GetComponent<RaccoonWristDeformation>();if(wrist!=null)wrist.Apply();
            elbows.Apply();
        }

        private void StabilizeDirectionalHead(Transform head,float dt)
        {
            // Keep the gaze continuous through short idle blends during a strafe reversal.
            // Root-relative rotation follows intentional body turns immediately. Only the
            // head is adjusted, after arm solving, so grip anchors and the leg cycle stay intact.
            Quaternion target=Quaternion.Inverse(root.rotation)*head.rotation;
            if(!hasStableHead){stableHead=target;hasStableHead=true;}
            // Keep the bound active until the landing has caught up to the ground pose.
            // Fading the bound itself exposes the much faster raw jump transition.
            if(jumpBlend>.001f)jumpHeadActive=true;
            if(jumpHeadActive)
            {
                stableHead=Quaternion.RotateTowards(stableHead,target,100*dt);
                head.rotation=root.rotation*stableHead;
                if(jumpBlend<=.001f&&Quaternion.Angle(stableHead,target)<.1f)jumpHeadActive=false;
                return;
            }
            bool directional=Mathf.Abs(direction.x)>.05f||direction.y<-.05f||jumpBlend>.001f;
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





