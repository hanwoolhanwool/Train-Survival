using UnityEngine;
using Game.Gameplay.Player;
namespace Game.Gameplay.Debugging
{
    // Review-only shotgun actions. The firing and loading solver remains unchanged.
    public sealed class RaccoonShotgunActionsV41
    {
        readonly Animator a;readonly Transform root;readonly RaccoonActionReviewV36 review;
        readonly Vector3[] grip=new Vector3[2],feet=new Vector3[2],poles=new Vector3[2];
        readonly Quaternion[] rotation=new Quaternion[2],footRotation=new Quaternion[2];
        readonly Quaternion[][] fingers=new Quaternion[2][];
        GameObject dropped;Vector3 releasePosition;Quaternion releaseRotation;bool released;
        public float GripError{get;private set;}
        public bool DroppedVisible=>dropped!=null&&dropped.activeSelf;
        public RaccoonShotgunActionsV41(Animator animator,Transform movementRoot,RaccoonActionReviewV36 owner)
        {
            a=animator;root=movementRoot;review=owner;
            for(int side=0;side<2;side++)
            {
                bool left=side==0;var hand=Bone(left,2);var upper=Bone(left,0);var lower=Bone(left,1);
                grip[side]=root.InverseTransformPoint(hand.position);rotation[side]=Quaternion.Inverse(root.rotation)*hand.rotation;
                poles[side]=root.InverseTransformVector(lower.position-upper.position);
                var foot=a.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                feet[side]=foot.position;footRotation[side]=Quaternion.Inverse(root.rotation)*foot.rotation;
                var ids=left?RaccoonEquipmentPose.LeftFingers:RaccoonEquipmentPose.RightFingers;fingers[side]=new Quaternion[ids.Length];
                for(int i=0;i<ids.Length;i++)fingers[side][i]=a.GetBoneTransform(ids[i]).localRotation;
            }
        }
        Transform Bone(bool left,int joint)=>a.GetBoneTransform(joint==0?(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm):joint==1?(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm):(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand));
        public static bool IsAction(string id)=>id.StartsWith("Hit")||id=="Equip"||id=="Holster"||id=="Respawn"||id=="ShotgunDeath";
        static float S(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
        public static float Impact(float t)
        {
            if(t<.15f)return S(t/.15f);
            if(t<.45f)return Mathf.Lerp(1,-.12f,S((t-.15f)/.30f));
            return -.12f*(1-S((t-.45f)/.55f));
        }
        public static Vector3 Body(string id,float phase)
        {
            float p=Impact(phase);
            if(id=="HitFront")return new Vector3(-9,1,0)*p;
            if(id=="HitBack")return new Vector3(10,-1,0)*p;
            if(id=="HitLeft")return new Vector3(2,-5,-8)*p;
            if(id=="HitRight")return new Vector3(2,5,8)*p;
            if(id=="Respawn")return new Vector3(5,0,0)*(1-S(phase));
            return Vector3.zero;
        }
        public static float Drop(string id,float phase)=>id.StartsWith("Hit")?.022f*Mathf.Max(0,Impact(phase)):id=="Respawn"?.055f*(1-S(phase)):0;
        public static Quaternion LowRotation(float amount)=>Quaternion.Euler(27*amount,-5*amount,-4*amount);
        public static Vector3 LowOffset(float amount)=>new Vector3(.02f,-.115f,.025f)*amount;
        public static RaccoonActionTargetsV36.Goals Goals(string id,float phase,Vector3[] hands,Vector3[] foot)
        {
            float low=id=="Holster"?S(phase):id=="Equip"||id=="Respawn"?1-S(phase):0;
            var body=Body(id,phase);var delta=Quaternion.Euler(body*.7f)*LowRotation(low);
            Vector3 offset=LowOffset(low)+new Vector3(body.z*.0015f,-Drop(id,phase),body.x*.002f);
            Vector3 right=hands[1]+offset;
            return new RaccoonActionTargetsV36.Goals{Left=right+delta*(hands[0]-hands[1]),Right=right,LeftFoot=foot[0],RightFoot=foot[1],Torso=body,Drop=Drop(id,phase)};
        }
        public void Reset()
        {released=false;GripError=0;if(dropped!=null)dropped.SetActive(false);}
        public void PlantCurrentFeet()
        {
            for(int side=0;side<2;side++)
            {var foot=a.GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);feet[side]=foot.position;footRotation[side]=Quaternion.Inverse(root.rotation)*foot.rotation;}
        }
        public void Dispose(){if(dropped!=null)Object.Destroy(dropped);}
        public void Apply(Vector2 velocity,float runBlend,float jumpBlend)
        {
            if(review.Context!=RaccoonActionReviewV36.ReviewContext.Shotgun){Reset();return;}
            bool hit=review.ShotgunHit;
            bool action=review.Active&&IsAction(review.Entry.Id)&&!hit;
            string id=action?review.Entry.Id:"";float phase=action?Mathf.Clamp01(review.Elapsed/review.Entry.Duration):0;
            if(id=="ShotgunDeath")
            {
                ApplyDeath();return;
            }
            var combat=root.GetComponent<RaccoonWeaponPoseV36>();
            // No modification of either accepted firing/loading sequence, including its recovery.
            if(combat.Reloading||combat.ShotTime<.8f)return;
            if(review.Active&&!action&&!hit)return;
            float low=review.ShotgunLowered?1:0;
            if(id=="Holster")low=S(phase);
            if(id=="Equip"||id=="Respawn")low=1-S(phase);
            Vector3 body=action?Body(id,phase):Vector3.zero;
            if(review.Returning&&!hit){low*=review.Weight;body*=review.Weight;}
            float movement=action?0:Mathf.Clamp01(velocity.magnitude/1.2f);
            float carry=low*27+(1-low)*(7*movement+7*runBlend+5*jumpBlend);
            if(!action&&carry<.0001f){ApplyHit();return;}
            Quaternion delta=Quaternion.Euler(body*.7f)*Quaternion.Euler(carry,-5*low,-4*low);
            Vector3 offset=LowOffset(low)+new Vector3(body.z*.0015f,-(action?Drop(id,phase):0)-.012f*movement,body.x*.002f);
            var currentRight=Bone(false,2);var currentLeft=Bone(true,2);
            Vector3 right=action?root.TransformPoint(grip[1]+offset):currentRight.position+root.TransformVector(offset);
            Vector3 separation=action?root.TransformVector(grip[0]-grip[1]):currentLeft.position-currentRight.position;
            Quaternion worldDelta=root.rotation*delta*Quaternion.Inverse(root.rotation);
            Vector3 left=right+worldDelta*separation;
            Quaternion rr=worldDelta*(action?root.rotation*rotation[1]:currentRight.rotation),lr=worldDelta*(action?root.rotation*rotation[0]:currentLeft.rotation);
            SolvePair(ref left,ref right,lr,rr);
            if(action)
            {
                for(int side=0;side<2;side++)
                {
                    bool isLeft=side==0;var upper=a.GetBoneTransform(isLeft?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);var lower=a.GetBoneTransform(isLeft?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);var foot=a.GetBoneTransform(isLeft?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                    RaccoonEquipmentPose.SolveArm(upper,lower,foot,feet[side],upper.position+root.forward*.7f);foot.rotation=root.rotation*footRotation[side];
                }
            }
            ApplyHit();
        }
        void ApplyHit()
        {
            if(!review.ShotgunHit)return;
            float phase=Mathf.Clamp01(review.Elapsed/review.Entry.Duration);
            float fade=review.HitReturnBlend;
            Vector3 body=Body(review.Entry.Id,phase)*fade;
            float drop=Drop(review.Entry.Id,phase)*fade;
            // Keep the same ground, head and equipment solvers alive on both sides of
            // the transition. Rotate the common torso ancestors so both hands and the
            // gun move as one rigid grip, with a zero offset/velocity at the endpoint.
            var lf=a.GetBoneTransform(HumanBodyBones.LeftFoot);var rf=a.GetBoneTransform(HumanBodyBones.RightFoot);
            Vector3 lp=lf.position,rp=rf.position;Quaternion lq=lf.rotation,rq=rf.rotation;
            a.GetBoneTransform(HumanBodyBones.Hips).position-=root.up*drop;
            var spine=a.GetBoneTransform(HumanBodyBones.Spine);var chest=a.GetBoneTransform(HumanBodyBones.Chest);
            spine.rotation=root.rotation*Quaternion.Euler(body*.55f)*Quaternion.Inverse(root.rotation)*spine.rotation;
            chest.rotation=root.rotation*Quaternion.Euler(body*.45f)*Quaternion.Inverse(root.rotation)*chest.rotation;
            if(drop>0)
            {
                RaccoonEquipmentPose.SolveArm(a.GetBoneTransform(HumanBodyBones.LeftUpperLeg),a.GetBoneTransform(HumanBodyBones.LeftLowerLeg),lf,lp,a.GetBoneTransform(HumanBodyBones.LeftUpperLeg).position+root.forward*.7f);
                RaccoonEquipmentPose.SolveArm(a.GetBoneTransform(HumanBodyBones.RightUpperLeg),a.GetBoneTransform(HumanBodyBones.RightLowerLeg),rf,rp,a.GetBoneTransform(HumanBodyBones.RightUpperLeg).position+root.forward*.7f);
                lf.rotation=lq;rf.rotation=rq;
            }
        }
        void SolvePair(ref Vector3 left,ref Vector3 right,Quaternion lr,Quaternion rr)
        {
            for(int i=0;i<6;i++)for(int side=0;side<2;side++)
            {
                bool isLeft=side==0;var upper=Bone(isLeft,0);var lower=Bone(isLeft,1);var hand=Bone(isLeft,2);
                float length=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position)-.004f;
                Vector3 d=(isLeft?left:right)-upper.position;
                if(d.magnitude>length){Vector3 shift=d.normalized*(length-d.magnitude);left+=shift;right+=shift;}
            }
            Vector3 separation=left-right;
            for(int side=0;side<2;side++)
            {
                bool isLeft=side==0;var upper=Bone(isLeft,0);var lower=Bone(isLeft,1);var hand=Bone(isLeft,2);
                RaccoonEquipmentPose.SolveArm(upper,lower,hand,isLeft?left:right,upper.position+root.TransformVector(poles[side]));hand.rotation=isLeft?lr:rr;
                var ids=isLeft?RaccoonEquipmentPose.LeftFingers:RaccoonEquipmentPose.RightFingers;
                for(int i=0;i<ids.Length;i++)a.GetBoneTransform(ids[i]).localRotation=fingers[side][i];
            }
            GripError=Vector3.Distance(Bone(true,2).position-Bone(false,2).position,separation);
        }
        void ApplyDeath()
        {
            float time=review.Elapsed;
            if(time<.20f)
            {
                var right=root.TransformPoint(grip[1]+new Vector3(0,-.05f*time/.2f,-.02f));var left=right+root.TransformVector(grip[0]-grip[1]);
                SolvePair(ref left,ref right,root.rotation*rotation[0],root.rotation*rotation[1]);
                releasePosition=review.Weapon.transform.position;releaseRotation=review.Weapon.transform.rotation;return;
            }
            if(!released)
            {
                if(dropped==null)
                {dropped=Object.Instantiate(review.Weapon,root);dropped.name="Shotgun released review";var scale=review.Weapon.transform.lossyScale;dropped.transform.localScale=new Vector3(scale.x/root.lossyScale.x,scale.y/root.lossyScale.y,scale.z/root.lossyScale.z);}
                dropped.SetActive(true);released=true;
            }
            float fall=Mathf.Clamp(time-.20f,0,.58f),t=fall/.58f;
            dropped.transform.position=releasePosition+root.TransformVector(new Vector3(-1.7f,.12f,.12f))*fall-Vector3.up*(4.9f*fall*fall);
            dropped.transform.rotation=Quaternion.Slerp(releaseRotation,root.rotation*Quaternion.Euler(0,0,90)*Quaternion.Inverse(root.rotation)*releaseRotation,S(t));
            // Settle the actual rendered weapon above the floor, including the pump mesh.
            var renders=dropped.GetComponentsInChildren<Renderer>();float bottom=float.PositiveInfinity;
            foreach(var r in renders)bottom=Mathf.Min(bottom,r.bounds.min.y);
            float floor=root.position.y+.015f;if(bottom<floor)dropped.transform.position+=Vector3.up*(floor-bottom);
        }
    }
}
