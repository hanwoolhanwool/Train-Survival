using UnityEngine;
namespace Game.Gameplay.Debugging
{
    // Raccoon anatomical frames, derived from knuckle positions, independent of gun sockets.
    public sealed class RaccoonActionHandsV37
    {
        readonly Quaternion[] localFrames=new Quaternion[2];
        readonly Vector3[] localPalm=new Vector3[2];
        public RaccoonActionHandsV37(Animator a)
        {
            for(int s=0;s<2;s++)
            {
                bool left=s==0;var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                var mid=a.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
                var index=a.GetBoneTransform(left?HumanBodyBones.LeftIndexProximal:HumanBodyBones.RightIndexProximal);
                var little=a.GetBoneTransform(left?HumanBodyBones.LeftLittleProximal:HumanBodyBones.RightLittleProximal);
                Vector3 axis=(mid.position-hand.position).normalized;
                // Mirrored digit order: validated against the direction the middle finger curls.
                Vector3 palm=Vector3.Cross(index.position-little.position,axis).normalized*(left?1:-1);
                localFrames[s]=Quaternion.LookRotation(hand.InverseTransformDirection(axis),hand.InverseTransformDirection(palm));
                localPalm[s]=hand.InverseTransformPoint((index.position+little.position)*.5f+palm*.022f);
            }
        }
        static float S(float t)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(t));
        static float Envelope(float t)=>t<.3f?S(t/.3f):1-S((t-.3f)/.7f);
        public static float RepairLift(float t)=>RaccoonHammerPoseV42.Lift(t);
        public Vector3 PalmNormal(Transform hand,bool left)=>hand.rotation*(localFrames[left?0:1]*Vector3.up);
        public Vector3 PalmCenter(Transform hand,bool left)=>hand.TransformPoint(localPalm[left?0:1]);
        public Vector3 HammerGripCenter(Transform hand,float clearance=0)=>PalmCenter(hand,false)+(hand.rotation*localFrames[1]*Vector3.forward)*.006f+PalmNormal(hand,false)*(.008f+clearance);
        public Quaternion HammerRotation(Quaternion handRotation)
        {
            var frame=handRotation*localFrames[1];Vector3 normal=frame*Vector3.up,axis=frame*Vector3.forward;
            Vector3 shaft=Vector3.Cross(normal,axis).normalized;
            return Quaternion.LookRotation(normal,Vector3.Cross(normal,shaft));
        }
        public static float HammerContactWeight(float phase)=>1-S(RepairLift(phase)/.25f);
        public Vector3 HammerWrist(Animator a,Transform root,Quaternion rotation,float phase=0,float clearance=0)
        {
            var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 grip=rotation*Vector3.Scale(localPalm[1],hand.lossyScale)+(rotation*localFrames[1]*Vector3.forward)*.006f+(rotation*localFrames[1]*Vector3.up)*(.008f+clearance);
            Vector3 face=HammerRotation(rotation)*(RaccoonHammerPoseV42.Face-RaccoonHammerPoseV42.Grip)*RaccoonHammerPoseV42.Scale;
            return root.TransformPoint(RaccoonHammerPoseV42.FaceTarget(phase))-grip-face;
        }
        public Quaternion Rotation(Animator a,Transform root,RaccoonActionLibraryV36.Entry entry,float phase,bool left)
        {
            int s=left?0:1;var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var lower=a.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            Vector3 arm=root.InverseTransformDirection(hand.position-lower.position).normalized;
            Vector3 axis=arm,normal=left?Vector3.right:Vector3.left;
            float e=entry.Loop?1:Envelope(phase);string id=entry.Id;
            if(id=="Interact"&&!left){axis=Vector3.Slerp(arm,Vector3.forward,e);normal=Vector3.Slerp(normal,Vector3.down,e);}
            if(id=="Repair")
            {
                if(!left)return root.rotation*Quaternion.LookRotation(RaccoonHammerPoseV42.FingerAxis(phase),Vector3.left)*Quaternion.Inverse(localFrames[1]);
            }
            if(id=="Pickup"&&!left){axis=Vector3.Slerp(arm,new Vector3(0,-.4f,1).normalized,e);normal=Vector3.Slerp(normal,Vector3.down,e);}
            if(id=="Eat"&&!left){axis=Vector3.Slerp(arm,Vector3.up,e);normal=Vector3.Slerp(normal,Vector3.back,e);}
            if(id=="Carry"){axis=Vector3.forward;normal=left?Vector3.right:Vector3.left;}
            if(id.StartsWith("Swim")){axis=Vector3.Slerp(arm,new Vector3(0,-.35f,1).normalized,.35f);normal=RaccoonSwimPoseV40.PalmNormal(id,phase,left);}
            if(id.StartsWith("Ladder")||id=="Mantle"){axis=Vector3.up;normal=Vector3.forward;}
            if(id=="Grabbed"){axis=Vector3.up;normal=Vector3.forward;}
            if(id.StartsWith("Fishing")){axis=Vector3.forward;normal=left?Vector3.right:Vector3.left;}
            // These two actual weapon grips have dedicated profiles; the free hand does not.
            if(!left&&(entry.Prop=="Revolver"||entry.Prop=="Harpoon"))
            {
                var profile=a.GetComponent<Game.Gameplay.Player.RaccoonEquipmentPose>().Profile;
                profile.TryGetEntry(Game.Gameplay.Inventory.HotbarItemType.Revolver,out var fitted);
                return root.rotation*fitted.Right.Rotation;
            }
            float limit=60;
            axis=Vector3.RotateTowards(arm,axis,limit*Mathf.Deg2Rad,0);
            normal=Vector3.ProjectOnPlane(normal,axis).normalized;
            if(normal.sqrMagnitude<.01f)normal=Vector3.ProjectOnPlane(Vector3.forward,axis).normalized;
            return root.rotation*Quaternion.LookRotation(axis,normal)*Quaternion.Inverse(localFrames[s]);
        }
        public float ContactWeight(RaccoonActionLibraryV36.Entry entry,float phase,bool left)=>entry.Id=="Interact"&&!left?Envelope(phase):0;
        public Vector3 SurfaceWrist(Animator a,Transform root,bool left,Quaternion rotation)
        {
            var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            Vector3 point=root.TransformPoint(new Vector3(left?-.24f:.22f,.92f,.53f));
            return point-rotation*Vector3.Scale(localPalm[left?0:1],hand.lossyScale);
        }
        public static float Finger(string action,string prop,float phase,int muscle,string name)
        {
            if(prop=="Hammer")
            {
                if(muscle<75)return RaccoonNeutralPoseV39.Finger(name);
                if(name.Contains("Thumb"))return name.Contains("Spread")?-.35f:name.Contains("1 Stretched")?-.55f:-.25f;
                if(name.Contains("Spread"))return 0;
                return name.Contains("1 Stretched")?-.65f:name.Contains("2 Stretched")?-.7f:-.4f;
            }
            if(action=="Neutral")return RaccoonNeutralPoseV39.Finger(name);
            if(action.StartsWith("Swim"))return name.Contains("Thumb")?(name.Contains("Spread")?-.25f:.05f):name.Contains("Spread")?-.08f:.62f;
            bool left=muscle<75;bool grip=(!left&&(prop=="Hammer"||prop=="Rod"))||action=="Melee"||action.StartsWith("Ladder");
            bool flat=action.StartsWith("Swim")||action=="Repair"&&left||action=="Interact"&&!left;
            if(name.Contains("Spread"))return name.Contains("Thumb")?.15f:0;
            if(name.Contains("Thumb"))return grip?-.2f:.15f;
            if(grip)return name.Contains("1 Stretched")?-.35f:name.Contains("2 Stretched")?-.7f:-.5f;
            if(flat)return .75f;
            if(action=="Carry")return -.05f;
            if(prop=="Food"&&!left)return name.Contains("Index")?-.3f:.2f;
            return name.Contains("1 Stretched")?.25f:.5f;
        }
    }
}
