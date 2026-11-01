using Game.Gameplay.Combat;
using Game.Gameplay.Player;
using UnityEngine;

namespace Game.Gameplay.Debugging
{
    // Review-only, evaluated after the established grip solver, before deformation correctives.
    public sealed class RaccoonCombatPoseV33 : MonoBehaviour
    {
        public GunSettings Settings;
        public Transform Shell,Flash;
        public Vector3 ReceiverFromGrip=new Vector3(-.08f,-.13f,.20f);
        private GunMagazine magazine;
        private float shotTime=10,reloadTime;
        private int loadCount;
        private RaccoonReloadPinchV35 pinch;
        public float PinchError {get;private set;}
        public Vector3 ThumbContact=>pinch==null?Vector3.zero:pinch.ThumbTip;
        public Vector3 IndexContact=>pinch==null?Vector3.zero:pinch.IndexTip;
        public int Shots {get;private set;}
        public int Reloads {get;private set;}
        public int Rounds=>magazine==null?0:magazine.RoundsLoaded;
        public bool Reloading=>magazine!=null&&magazine.IsReloading;
        public float ReloadPhase=>Reloading?Mathf.Clamp01(reloadTime/Settings.ReloadDuration):0;
        public float Recoil=>Pulse(shotTime,.045f,.30f);
        public float GripError {get;private set;}
        public float ReachError {get;private set;}
        public Vector3 LeftTarget {get;private set;}
        public string Label=>Reloading?"재장전":shotTime<.3f?"발사 반동":"준비";
        public void ResetState()
        {
            magazine=new GunMagazine(Settings.MagazineCapacity,Settings.FireInterval,Settings.ReloadDuration);
            shotTime=10;reloadTime=0;loadCount=0;Shots=Reloads=0;
            if(Shell!=null)Shell.gameObject.SetActive(false);
            if(Flash!=null)Flash.gameObject.SetActive(false);
        }
        public bool Fire()
        {
            if(magazine==null)ResetState();
            if(!magazine.TryFire())return false;
            shotTime=0;Shots++;return true;
        }
        public bool Reload()
        {
            if(magazine==null)ResetState();
            // Finish the short recoil recovery before the support hand leaves the gun.
            if(shotTime<.30f||!magazine.TryStartReload(Settings.MagazineCapacity))return false;
            loadCount=magazine.PendingLoad;magazine.ConfirmPendingLoad(loadCount);reloadTime=0;Reloads++;return true;
        }
        public void Step(float dt)
        {
            if(magazine==null)ResetState();
            shotTime+=dt;if(Reloading)reloadTime+=dt;magazine.Tick(dt);
        }
        private static float Smooth(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
        private static float Pulse(float t,float peak,float end)=>t<peak?Smooth(t/peak):1-Smooth((t-peak)/(end-peak));
        public void Apply(Animator animator,Transform root)
        {
            if(!Reloading&&Recoil<=0)
            {
                GripError=ReachError=PinchError=0;
                if(Shell!=null)Shell.gameObject.SetActive(false);
                if(Flash!=null)Flash.gameObject.SetActive(false);
                return;
            }
            var right=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Vector3 rp=right.position,lp=left.position;Quaternion rr=right.rotation,lr=left.rotation;
            float t=ReloadPhase;
            float tilt=Reloading?Smooth(t/.16f)*(1-Smooth((t-.82f)/.18f)):0;
            Quaternion delta=root.rotation*Quaternion.Euler(18*tilt-4*Recoil,0,-16*tilt)*Quaternion.Inverse(root.rotation);
            Vector3 offset=root.TransformVector(new Vector3(0,-.035f*tilt,-.025f*Recoil-.02f*tilt));
            Vector3 rightTarget=rp+offset;
            Vector3 support=rightTarget+delta*(lp-rp);
            Quaternion rightRotation=delta*rr,leftRotation=delta*lr;
            Vector3 target=support;float release=0;bool shellVisible=false;
            Vector3 receiver=rightTarget+delta*root.TransformVector(ReceiverFromGrip);
            Vector3 approach=receiver+delta*root.TransformVector(new Vector3(-.025f,-.08f,0));
            Vector3 pouch=root.TransformPoint(new Vector3(-.30f,.88f,.32f));
            if(Reloading)
            {
                release=Smooth(t/.16f)*(1-Smooth((t-.82f)/.18f));
                // Every shell has its own reach / present / insertion sequence. A partial
                // reload uses one sequence rather than inserting an imaginary second shell.
                if(t<.18f)target=Vector3.Lerp(support,pouch,Smooth(t/.18f));
                else if(t<.82f)
                {
                    float cycle=(t-.18f)/.64f*loadCount;
                    int index=Mathf.Min(loadCount-1,Mathf.FloorToInt(cycle));float p=cycle-index;
                    if(p<.42f)target=Vector3.Lerp(index==0?pouch:receiver,pouch,Smooth(p/.42f));
                    else if(p<.76f)target=Vector3.Lerp(pouch,approach,Smooth((p-.42f)/.34f));
                    else target=Vector3.Lerp(approach,receiver,Smooth((p-.76f)/.18f));
                    shellVisible=p>=.42f&&p<.94f;
                }
                else target=Vector3.Lerp(receiver,support,Smooth((t-.82f)/.18f));
            }
            ReachError=Solve(animator,false,rightTarget,rightRotation,root);
            ReachError=Mathf.Max(ReachError,Solve(animator,true,target,leftRotation,root,release));
            if(Reloading)
            {
                // Position-only IK cannot decide wrist orientation. The old fixed world
                // rotation folded the hand backwards when reaching toward the belt.
                // Align its actual palm axis with the solved forearm, retaining the
                // nearest palm roll and the approved orientation when inside the cone.
                var forearm=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var middle=animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                Vector3 palm=(middle.position-left.position).normalized;
                Vector3 arm=(left.position-forearm.position).normalized;
                Vector3 limited=Vector3.RotateTowards(arm,palm,35*Mathf.Deg2Rad,0);
                left.rotation=Quaternion.FromToRotation(palm,limited)*left.rotation;
            }
            LeftTarget=target;
            GripError=Reloading?Vector3.Distance(left.position,target):(left.position-right.position-delta*(lp-rp)).magnitude;
            if(release>0)
            {
                // Relax the carrying curl without changing the approved thumb axes.
                for(int i=3;i<RaccoonEquipmentPose.LeftFingers.Length;i++)
                {
                    var finger=animator.GetBoneTransform(RaccoonEquipmentPose.LeftFingers[i]);
                    if(finger!=null)finger.localRotation*=Quaternion.Euler((i>=6?12:-12)*release,0,0);
                }
            }
            if(Shell!=null)
            {
                Shell.gameObject.SetActive(shellVisible);
            }
            if(Reloading)
            {
                if(pinch==null)pinch=new RaccoonReloadPinchV35(animator);
                PinchError=pinch.Apply(Shell,release);
            }
            if(Flash!=null)
            {
                Flash.gameObject.SetActive(shotTime<.065f);
                Flash.position=right.position+delta*root.TransformVector(new Vector3(-.035f,.035f,.72f));
                Flash.rotation=delta*root.rotation;
            }
            var wrist=animator.GetComponent<RaccoonWristDeformation>();if(wrist!=null)wrist.Apply();
        }
        private static float Solve(Animator a,bool left,Vector3 target,Quaternion rotation,Transform root,float release=0)
        {
            var upper=a.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=a.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            // Preserve the solved elbow plane so zero action weight returns exactly to
            // the established pose instead of snapping to a different pole direction.
            Vector3 hint=lower.position;
            // During loading the elbow belongs beside the ribs, not in front of the
            // loading hand. Blend back to the approved support elbow when regripping.
            if(left&&release>0)hint=Vector3.Lerp(hint,upper.position+root.TransformVector(new Vector3(-.22f,-.35f,-.12f)),release);
            float error=RaccoonEquipmentPose.SolveArm(upper,lower,hand,target,hint);hand.rotation=rotation;return error;
        }
    }
}
