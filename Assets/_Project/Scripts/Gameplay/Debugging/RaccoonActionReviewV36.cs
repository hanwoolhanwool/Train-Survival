using UnityEngine;
using Game.Gameplay.Player;
namespace Game.Gameplay.Debugging
{
    public sealed class RaccoonActionReviewV36 : MonoBehaviour
    {
        public enum ReviewContext { Unarmed, Shotgun, Hammer, Revolver, Harpoon, Food, Bundle, Fishing, RiflePreview, Machete }
        public enum RevolverComparison { Original, Current, LeftShift }
        public RevolverComparison RevolverVersion {get;private set;}=RevolverComparison.LeftShift;
        public void CompareRevolver(RevolverComparison version){RevolverVersion=version;}
        static readonly string[] contextNames={"맨손","샷건","해머","리볼버","작살총","음식","꾸러미","낚싯대","소총 (임시)"};
        static readonly string[] contextIds={"Neutral","","HammerIdle","RevolverIdle","HarpoonIdle","FoodIdle","Carry","FishingWait",""};
        public ReviewContext Context {get;private set;}=ReviewContext.Unarmed;
        public string ContextLabel=>contextNames[(int)Context];
        public bool UsesShotgunBase=>Context==ReviewContext.Shotgun||Context==ReviewContext.RiflePreview;
        public bool CanUseShotgun=>Context==ReviewContext.Shotgun&&!Active&&!ShotgunLowered;
        public bool Returning {get;private set;}
        public float HitReturnBlend=>Returning?1-Mathf.SmoothStep(0,1,returnElapsed/.28f):1;
        public bool ShotgunLowered {get;private set;}
        int queuedRaise=-1;
        RaccoonShotgunActionsV41 shotgunActions;
        RaccoonDeathCandidateV49 softDeath;
        public bool SoftDeath=>Active&&Entry.Id==RaccoonDeathCandidateV49.Id;
        public bool DeathItemReleased=>softDeath!=null&&softDeath.Released;
        public RaccoonDeathCandidateV49 DeathCandidate=>softDeath;
        public void ApplySoftDeath()=>softDeath?.Apply(Elapsed);
        public float ShotgunGripError=>shotgunActions==null?0:shotgunActions.GripError;
        public bool DroppedShotgunVisible=>shotgunActions!=null&&shotgunActions.DroppedVisible;
        public bool SpecialShotgunAction=>Context==ReviewContext.Shotgun&&Active&&RaccoonShotgunActionsV41.IsAction(Entry.Id);
        public bool ShotgunHit=>SpecialShotgunAction&&Entry.Id.StartsWith("Hit");
        float returnElapsed,returnWeight;
        int lastSelection=-1;
        public RaccoonActionLibraryV36 Library;
        public GameObject Weapon;
        public Mesh OriginalHammerMesh,FittedHammerMesh;
        public Mesh WristBeforeMesh,WristAfterMesh;
        [SerializeField] private bool correctedWrist=true;
        public bool CorrectedWrist=>correctedWrist;
        public void CompareWrist(bool corrected)
        {
            var mesh=corrected?WristAfterMesh:WristBeforeMesh;
            if(mesh==null)return;
            var skin=GetComponentInChildren<SkinnedMeshRenderer>();
            if(skin==null)return;
            // Both versions share bones and corrective shapes. Switching the surface
            // must not restart the action or change its current pose or camera.
            var weights=new float[skin.sharedMesh.blendShapeCount];
            for(int i=0;i<weights.Length;i++)weights[i]=skin.GetBlendShapeWeight(i);
            skin.sharedMesh=mesh;
            for(int i=0;i<Mathf.Min(weights.Length,mesh.blendShapeCount);i++)skin.SetBlendShapeWeight(i,weights[i]);
            correctedWrist=corrected;
        }
        public bool OriginalHammerGrip {get;private set;}
        float HammerClearance=>OriginalHammerGrip?RaccoonHammerGripV44.PalmClearance:0;
        public void UseOriginalHammer(bool original){OriginalHammerGrip=original;}
        public GameObject[] Props;
        public GameObject Ladder,Water,WorkTarget;
        public bool ShowWater=true;
        public int Selected {get;private set;}=-1;
        public float Elapsed {get;private set;}
        public float Weight {get;private set;}
        public bool Active=>Selected>=0;
        public RaccoonActionLibraryV36.Entry ContextEntry=>Library.ContextPoses==null?null:System.Array.Find(Library.ContextPoses,e=>e.Id==contextIds[(int)Context]);
        public RaccoonActionLibraryV36.Entry Entry=>Active?Library.Entries[Selected]:ContextEntry;
        public bool HasFullBodyPose=>!UsesShotgunBase||(Active&&!Entry.UpperBody&&!ShotgunHit);
        public bool LocksMovement=>!UsesShotgunBase||(Active&&!Entry.UpperBody);
        public bool AllowsWeapon=>UsesShotgunBase&&(!Active||Entry.KeepWeapon);
        public static ReviewContext ContextFor(RaccoonActionLibraryV36.Entry e)
        {
            if(e.Id.StartsWith("Rifle"))return ReviewContext.RiflePreview;
            if(e.KeepWeapon)return ReviewContext.Shotgun;
            switch(e.Prop){case "Hammer":return ReviewContext.Hammer;case "Revolver":return ReviewContext.Revolver;case "Harpoon":return ReviewContext.Harpoon;case "Food":return ReviewContext.Food;case "Bundle":return ReviewContext.Bundle;case "Rod":return ReviewContext.Fishing;default:return ReviewContext.Unarmed;}
        }
        public bool Compatible(RaccoonActionLibraryV36.Entry e)=>e.Id==RaccoonDeathCandidateV49.Id||((e.Id!="Death"&&e.Id!="ShotgunDeath")&&ContextFor(e)==Context);
        public void SetContext(ReviewContext context)
        {
            Context=context;ShotgunLowered=false;queuedRaise=-1;lastSelection=-1;scroll=Vector2.zero;Select(-1);
        }
        private Vector3[] baseHands,baseFeet;
        private Quaternion[] weaponRotations,footRotations;
        private RaccoonActionHandsV37 hands;
        public RaccoonActionHandsV37 Hands=>hands;
        private Vector3 baseHip;
        public void CacheContacts(Animator a)
        {
            var r=transform;
            baseHands=new[]{r.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.LeftHand).position),r.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.RightHand).position)};
            baseFeet=new[]{r.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.LeftFoot).position),r.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.RightFoot).position)};
            weaponRotations=new[]{Quaternion.Inverse(r.rotation)*a.GetBoneTransform(HumanBodyBones.LeftHand).rotation,Quaternion.Inverse(r.rotation)*a.GetBoneTransform(HumanBodyBones.RightHand).rotation};
            hands=new RaccoonActionHandsV37(a);
            footRotations=new[]{Quaternion.Inverse(r.rotation)*a.GetBoneTransform(HumanBodyBones.LeftFoot).rotation,Quaternion.Inverse(r.rotation)*a.GetBoneTransform(HumanBodyBones.RightFoot).rotation};
            baseHip=r.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.Hips).position);
            if(shotgunActions!=null)shotgunActions.Dispose();
            shotgunActions=new RaccoonShotgunActionsV41(a,r,this);
            if(softDeath!=null)softDeath.Dispose();softDeath=new RaccoonDeathCandidateV49(a,r);
        }
        void OnDestroy(){if(shotgunActions!=null)shotgunActions.Dispose();if(softDeath!=null)softDeath.Dispose();}
        public void ApplyShotgunActions(Vector2 velocity,float run,float jump)=>shotgunActions?.Apply(velocity,run,jump);
        public float ContactError{get;private set;}
        RaccoonActionTargetsV36.Goals Goals(Animator a,RaccoonActionLibraryV36.Entry entry,float phase)
        {
            var goals=entry.Prop=="Harpoon"?RaccoonHarpoonGripV46.Targets(entry,phase,baseHands,baseFeet):RaccoonActionTargetsV36.Evaluate(entry,phase,baseHands,baseFeet);
            if(entry.Id=="Neutral")
            {goals.Left=RaccoonNeutralPoseV39.HandTarget(a,transform,true);goals.Right=RaccoonNeutralPoseV39.HandTarget(a,transform,false);}
            if(entry.Prop=="Hammer")goals.Left=RaccoonNeutralPoseV39.HandTarget(a,transform,true);
            if(entry.Id=="RevolverReload"&&RevolverVersion!=RevolverComparison.Original)goals.Left=RaccoonRevolverGripV45.ReloadLeft(phase);
            if(entry.Id=="Repair")goals.Right=transform.InverseTransformPoint(hands.HammerWrist(a,transform,hands.Rotation(a,transform,entry,phase,false),phase,HammerClearance));
            return goals;
        }
        public void ApplyContacts(Animator a)
        {
            if(Entry==null||Entry.UpperBody||Entry.Id=="Death"||SpecialShotgunAction||baseHands==null)return;
            float phase=!Active?0:Entry.Loop?Mathf.Repeat(Elapsed/Entry.Duration,1):Mathf.Clamp01(Elapsed/Entry.Duration);
            float poseWeight=Active?Weight:1;
            float contactWeight=poseWeight;
            var g=Goals(a,Entry,phase);
            if(Active&&!UsesShotgunBase&&ContextEntry!=null)
            {
                // Blend exact contact targets as well as clip curves. Otherwise the final
                // frame snaps from the retargeted clip to the corrected idle hand position.
                var baseline=Goals(a,ContextEntry,0);
                g.Left=Vector3.Lerp(baseline.Left,g.Left,Weight);g.Right=Vector3.Lerp(baseline.Right,g.Right,Weight);
                g.LeftFoot=Vector3.Lerp(baseline.LeftFoot,g.LeftFoot,Weight);g.RightFoot=Vector3.Lerp(baseline.RightFoot,g.RightFoot,Weight);
                g.Drop=Mathf.Lerp(baseline.Drop,g.Drop,Weight);poseWeight=1;
            }
            var hip=a.GetBoneTransform(HumanBodyBones.Hips);hip.position=Vector3.Lerp(hip.position,transform.TransformPoint(baseHip-Vector3.up*g.Drop),poseWeight);
            ContactError=0;
            for(int side=0;side<2;side++)for(int limb=0;limb<2;limb++)
            {
                bool left=side==0,leg=limb==0;
                var upper=a.GetBoneTransform(leg?(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg):(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm));
                var lower=a.GetBoneTransform(leg?(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg):(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm));
                var end=a.GetBoneTransform(leg?(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot):(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand));
                Vector3 target=transform.TransformPoint(leg?(left?g.LeftFoot:g.RightFoot):(left?g.Left:g.Right));target=Vector3.Lerp(end.position,target,poseWeight);
                Vector3 pole=upper.position+transform.TransformVector(leg?new Vector3(0,0,.7f):new Vector3(left?-.35f:.35f,-.35f,-.18f));
                if(!leg)
                {
                    float relaxed=Entry.Id=="Neutral"?1:Context==ReviewContext.Unarmed?1-Weight:0;
                    pole=Vector3.Lerp(pole,upper.position+transform.TransformVector(RaccoonNeutralPoseV39.PoleOffset(left)),relaxed);
                }
                if(!leg&&Entry.Prop=="Hammer")pole=upper.position+transform.TransformVector(left?RaccoonNeutralPoseV39.PoleOffset(true):RaccoonHammerPoseV42.Pole);
                if(!leg&&Entry.Id.StartsWith("Swim"))pole=Vector3.Lerp(pole,upper.position+transform.TransformVector(RaccoonSwimPoseV40.ElbowPole(left)),contactWeight);
                Quaternion authored=end.rotation;
                float error=RaccoonEquipmentPose.SolveArm(upper,lower,end,target,pole);
                if(leg)
                {
                    var rotation=footRotations[side];
                    if(Entry.Id.StartsWith("Swim"))rotation=Quaternion.Slerp(Quaternion.identity,RaccoonSwimPoseV40.FootOffset(Entry.Id,phase,left),contactWeight)*rotation;
                    end.rotation=Quaternion.Slerp(authored,transform.rotation*rotation,poseWeight);
                }
                else if(Entry.KeepWeapon)
                {
                    end.rotation=Quaternion.Slerp(authored,transform.rotation*weaponRotations[side],poseWeight);
                    var middle=a.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
                    Vector3 palm=(middle.position-end.position).normalized,arm=(end.position-lower.position).normalized;
                    end.rotation=Quaternion.FromToRotation(palm,Vector3.RotateTowards(arm,palm,32*Mathf.Deg2Rad,0))*end.rotation;
                }
                else
                {
                    Quaternion desired=HandRotation(a,phase,left);
                    float contact=hands.ContactWeight(Entry,phase,left)*contactWeight;
                    if(contact>0)for(int solve=0;solve<3;solve++)
                    {Vector3 fitted=Vector3.Lerp(target,hands.SurfaceWrist(a,transform,left,desired),contact);error=RaccoonEquipmentPose.SolveArm(upper,lower,end,fitted,pole);desired=HandRotation(a,phase,left);}
                    end.rotation=Quaternion.Slerp(authored,desired,poseWeight);
                }
                ContactError=Mathf.Max(ContactError,error);
            }
            var wrist=a.GetComponent<RaccoonWristDeformation>();if(wrist!=null)wrist.Apply();
            FitProps(a);
            if(Entry.Prop=="Harpoon")RaccoonHarpoonGripV46.Apply(a,Props,Entry.Id,phase,Weight,HitReturnBlend);
            if(Entry.Prop=="Revolver")
            {
                if(RevolverVersion==RevolverComparison.Original)RaccoonRevolverGripV45.ApplyOriginalSocket(a,Props);
                else RaccoonRevolverGripV45.Apply(a,Props,Entry.Id,phase,HitReturnBlend,RevolverVersion==RevolverComparison.LeftShift);
            }
            if(Entry.Prop=="Hammer")
            {if(OriginalHammerGrip)RaccoonHammerGripV44.Apply(a);else RaccoonHammerGripV43.Apply(a);}
        }
        Quaternion HandRotation(Animator a,float phase,bool left)
        {
            var desired=hands.Rotation(a,transform,Entry,phase,left);
            return Active&&!UsesShotgunBase&&ContextEntry!=null?Quaternion.Slerp(hands.Rotation(a,transform,ContextEntry,0,left),desired,Weight):desired;
        }
        private void FitProps(Animator a)
        {
            var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
            var index=a.GetBoneTransform(HumanBodyBones.RightIndexProximal);var little=a.GetBoneTransform(HumanBodyBones.RightLittleProximal);
            Vector3 axis=(index.position-little.position).normalized;
            Vector3 palm=(a.GetBoneTransform(HumanBodyBones.RightMiddleProximal).position-hand.position).normalized;
            Vector3 normal=hands.PalmNormal(hand,false);
            Vector3 centre=hands.PalmCenter(hand,false)+palm*.018f;
            foreach(var p in Props)
            {
                if(!p.activeSelf)continue;var tr=p.transform;
                if(p.name=="Hammer")
                {
                    var mesh=OriginalHammerGrip?OriginalHammerMesh:FittedHammerMesh;
                    if(mesh!=null)p.GetComponent<MeshFilter>().sharedMesh=mesh;
                    Quaternion rotation=hands.HammerRotation(hand.rotation);float scale=RaccoonHammerPoseV42.Scale;
                    tr.rotation=rotation;tr.localScale=Vector3.one*(scale/hand.lossyScale.x);
                    tr.position=hands.HammerGripCenter(hand,HammerClearance)-rotation*(RaccoonHammerPoseV42.Grip*scale);
                }
                if(p.name=="Food")tr.position=centre;
                if(p.name=="Bundle")
                {tr.rotation=transform.rotation*Quaternion.Euler(-90,0,0);tr.position=(hand.position+a.GetBoneTransform(HumanBodyBones.LeftHand).position)*.5f+transform.forward*.025f;}
                if(p.name=="Rod")
                {tr.rotation=Quaternion.LookRotation(normal,axis);tr.position=centre+axis*.36f;tr.localScale=new Vector3(.012f,.48f,.012f)/hand.lossyScale.x;}
            }
        }
        public void Select(int index)
        {
            if(index>=Library.Entries.Length||index< -1)return;
            if(index>=0&&!Compatible(Library.Entries[index]))return;
            bool wasSoftDeath=SoftDeath;
            if(softDeath!=null)softDeath.Dispose();
            if(wasSoftDeath)
            {Selected=-1;SetProps();var previousMotor=GetComponent<RaccoonActionMotorV36>();if(previousMotor!=null)previousMotor.Evaluate(1f/60);}
            if(index>=0&&Library.Entries[index].Id==RaccoonDeathCandidateV49.Id)
            {
                GameObject held=Weapon!=null&&Weapon.activeSelf?Weapon:null;
                if(held==null&&Props!=null)foreach(var p in Props)if(p!=null&&p.activeSelf){held=p;break;}
                if(softDeath!=null)softDeath.Begin(held);
            }
            queuedRaise=-1;
            if(index>=0&&Context==ReviewContext.Shotgun&&Library.Entries[index].Id=="Equip"&&!ShotgunLowered)
            {queuedRaise=index;index=System.Array.FindIndex(Library.Entries,e=>e.Id=="Holster");}
            bool fromDeath=Active&&(Entry.Id=="Death"||Entry.Id=="ShotgunDeath");
            Selected=index;Elapsed=0;Weight=0;Returning=false;
            if(shotgunActions!=null)shotgunActions.Reset();
            if(shotgunActions!=null&&SpecialShotgunAction&&!fromDeath&&Entry.Id!="Respawn")shotgunActions.PlantCurrentFeet();
            if(index>=0)lastSelection=index;
            var motor=GetComponent<RaccoonActionMotorV36>();if(motor!=null)motor.Automatic=false;
            var combat=GetComponent<RaccoonWeaponPoseV36>();if(combat!=null)combat.ResetState();
            SetProps();
        }
        public void ReturnToContext()
        {
            if(SoftDeath){Select(-1);return;}
            if(!Active){if(ShotgunLowered)Select(System.Array.FindIndex(Library.Entries,e=>e.Id=="Equip"));return;}
            if(Returning)return;
            Returning=true;returnElapsed=0;returnWeight=Weight;
        }
        public void Tick(float dt)
        {
            if(!Active){Weight=0;SetProps();return;}
            // On a manual hit cancellation, fade the current offset once. Advancing
            // its recovery curve as well would accelerate the return twice.
            if(!Returning||!(ShotgunHit||Entry.Id=="Repair"))Elapsed+=dt;
            if(Returning)
            {
                float returnDuration=Entry.Id=="Repair"?.45f:.28f;
                returnElapsed+=dt;Weight=returnWeight*(1-Mathf.SmoothStep(0,1,returnElapsed/returnDuration));
                if(returnElapsed>=returnDuration){if(!ShotgunHit)ShotgunLowered=false;Select(-1);}
                SetProps();return;
            }
            bool hold=Entry.Loop||Entry.Id=="Death"||Entry.Id=="ShotgunDeath"||SoftDeath;
            Weight=Mathf.SmoothStep(0,1,Elapsed/(Entry.Id=="Repair"?.32f:.16f));
            bool staged=SpecialShotgunAction&&(Entry.Id=="Holster"||Entry.Id=="Equip"||Entry.Id=="Respawn");
            if(staged)Weight=1;
            else if(!hold)Weight*=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(Entry.Duration-.24f,Entry.Duration,Elapsed));
            if(!hold&&Elapsed>=Entry.Duration)
            {
                if(SpecialShotgunAction){if(Entry.Id=="Holster")ShotgunLowered=true;else if(Entry.Id=="Equip"||Entry.Id=="Respawn")ShotgunLowered=false;}
                int next=queuedRaise;Select(-1);if(next>=0)Select(next);
            }
            SetProps();
        }
        private Vector2 scroll;
        private void Start(){if(Library!=null)SetProps();CompareWrist(correctedWrist);}
        private GUIStyle textStyle,buttonStyle;
        private void OnGUI()
        {
            if(Library==null)return;
            if(textStyle==null){var font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",16);textStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=16};buttonStyle=new GUIStyle(GUI.skin.button){font=font,fontSize=15,alignment=TextAnchor.MiddleLeft};}
            GUI.Box(new Rect(12,14,344,Screen.height-28),GUIContent.none);
            GUI.Label(new Rect(24,24,320,28),"1. 장비 상태 선택",textStyle);
            for(int i=0;i<contextNames.Length;i++)
                if(GUI.Button(new Rect(24+i%3*107,58+i/3*34,103,30),((int)Context==i?"● ":"")+contextNames[i],buttonStyle))SetContext((ReviewContext)i);
            GUI.Label(new Rect(24,164,320,28),"현재: "+ContextLabel+" · "+(Returning?"복귀 중":Active?"동작 재생":ShotgunLowered?"낮춰 들기":"기본 자세"),textStyle);
            if(GUI.Button(new Rect(24,198,155,30),"기본 자세로 복귀",buttonStyle))ReturnToContext();
            GUI.enabled=lastSelection>=0;
            if(GUI.Button(new Rect(187,198,155,30),"동작 다시 재생",buttonStyle))Select(lastSelection);
            GUI.enabled=true;
            bool wristComparison=WristBeforeMesh!=null&&WristAfterMesh!=null;
            if(wristComparison)
            {
                GUI.Label(new Rect(24,236,320,28),"2. 손목 수정 전·후 비교",textStyle);
                if(GUI.Button(new Rect(24,270,155,30),(!CorrectedWrist?"● ":"")+"A · 수정 전",buttonStyle))CompareWrist(false);
                if(GUI.Button(new Rect(187,270,155,30),(CorrectedWrist?"● ":"")+"B · 수정 후",buttonStyle))CompareWrist(true);
                GUI.Label(new Rect(24,304,320,28),"손목만 전환 · 자세/시점 유지",textStyle);
            }
            float actionTop=wristComparison?338:236;
            GUI.Label(new Rect(24,actionTop,320,28),(wristComparison?"3. ":"2. ")+"이 상태에서 동작 확인",textStyle);
            int count=Context==ReviewContext.Revolver?3:Context==ReviewContext.Shotgun||Context==ReviewContext.Hammer?2:0;foreach(var e in Library.Entries)if(e.Id!="Neutral"&&Compatible(e))count++;
            scroll=GUI.BeginScrollView(new Rect(24,actionTop+34,320,Mathf.Max(80,Screen.height-actionTop-62)),scroll,new Rect(0,0,296,count*35));
            int row=0;
            if(Context==ReviewContext.Revolver)
            {
                if(GUI.Button(new Rect(0,row++*35,294,31),(RevolverVersion==RevolverComparison.Original?"● ":"")+"A · 최초 파지 (수정 전)",buttonStyle))CompareRevolver(RevolverComparison.Original);
                if(GUI.Button(new Rect(0,row++*35,294,31),(RevolverVersion==RevolverComparison.Current?"● ":"")+"B · 현재 파지 (이동 전)",buttonStyle))CompareRevolver(RevolverComparison.Current);
                if(GUI.Button(new Rect(0,row++*35,294,31),(RevolverVersion==RevolverComparison.LeftShift?"● ":"")+"C · 왼쪽 1.2cm · 손가락 보정",buttonStyle))CompareRevolver(RevolverComparison.LeftShift);
            }
            if(Context==ReviewContext.Hammer)
            {
                if(GUI.Button(new Rect(0,row++*35,294,31),(!OriginalHammerGrip?"● ":"")+"A · 현재 버전 (가는 손잡이)",buttonStyle))UseOriginalHammer(false);
                if(GUI.Button(new Rect(0,row++*35,294,31),(OriginalHammerGrip?"● ":"")+"B · 원본 망치 (손만 수정)",buttonStyle))UseOriginalHammer(true);
            }
            if(Context==ReviewContext.Shotgun)
            {
                var motor=GetComponent<RaccoonActionMotorV36>();GUI.enabled=CanUseShotgun;
                if(GUI.Button(new Rect(0,row++*35,294,31),"샷건 사격 · 좌클릭",buttonStyle)){motor.Automatic=false;motor.Combat.Fire();}
                if(GUI.Button(new Rect(0,row++*35,294,31),"샷건 장전 · R",buttonStyle)){motor.Automatic=false;motor.Combat.Reload();}
                GUI.enabled=true;
            }
            var order=new int[Library.Entries.Length];for(int i=0;i<order.Length;i++)order[i]=i;
            if(Context==ReviewContext.Unarmed)System.Array.Sort(order,(a,b)=>
            {int categoryA=Library.Entries[a].Id.StartsWith("Swim")?0:1,categoryB=Library.Entries[b].Id.StartsWith("Swim")?0:1;return categoryA!=categoryB?categoryA.CompareTo(categoryB):a.CompareTo(b);});
            foreach(int i in order)
            {
                if(Library.Entries[i].Id=="Neutral"||!Compatible(Library.Entries[i]))continue;
                if(GUI.Button(new Rect(0,row++*35,294,31),(Selected==i?"▶ ":"")+Library.Entries[i].Label,buttonStyle))Select(i);
            }
            GUI.EndScrollView();
            if(Active&&Entry.Id.StartsWith("Swim")&&GUI.Button(new Rect(384,198,180,30),ShowWater?"수면 숨기기":"수면 표시",buttonStyle))ShowWater=!ShowWater;
        }
        public void PrepareAnimator(Animator a)
        {
            if(SoftDeath)return;
            int upper=a.GetLayerIndex("Actions Upper V36"),full=a.GetLayerIndex("Actions Full V36"),context=a.GetLayerIndex("Review Context V38");
            a.SetLayerWeight(context,UsesShotgunBase?0:1);
            if(!UsesShotgunBase&&ContextEntry!=null)a.Play(Context==ReviewContext.Harpoon?"RevolverIdle":ContextEntry.Id,context,0);
            a.SetLayerWeight(upper,Active&&Entry.UpperBody?Weight:0);
            // Hits are offsets on the live equipment pose, not a second absolute gun pose.
            a.SetLayerWeight(full,Active&&!Entry.UpperBody&&!ShotgunHit&&Entry.Id!="HarpoonReel"?Weight:0);
            if(!UsesShotgunBase||(Active&&!Entry.KeepWeapon))a.SetLayerWeight(a.GetLayerIndex("Hold"),0);
            if(Active&&!ShotgunHit&&Entry.Id!="HarpoonReel")a.Play(Entry.Id=="HarpoonFire"?"RevolverFire":Entry.Id,Entry.UpperBody?upper:full,Entry.Loop?Elapsed/Entry.Duration:Mathf.Min(.99999f,Elapsed/Entry.Duration));
        }
        private void SetProps()
        {
            if(SoftDeath)
            {
                if(DeathItemReleased){if(Weapon!=null)Weapon.SetActive(false);if(Props!=null)foreach(var p in Props)if(p!=null)p.SetActive(false);}
                if(Ladder!=null)Ladder.SetActive(false);if(Water!=null)Water.SetActive(false);if(WorkTarget!=null)WorkTarget.SetActive(false);return;
            }
            if(Weapon!=null)Weapon.SetActive(AllowsWeapon&&(!Active||Entry.Id!="ShotgunDeath"||Elapsed<.20f));
            if(Props!=null)foreach(var p in Props)if(p!=null)p.SetActive(Entry!=null&&p.name==Entry.Prop);
            if(Ladder!=null)Ladder.SetActive(Active&&(Entry.Id.StartsWith("Ladder")||Entry.Id=="Mantle"));
            if(Ladder!=null&&Active&&Entry.Id.StartsWith("Ladder")){float phase=Entry.Id=="LadderHold"?0:Mathf.Repeat((Entry.Id=="LadderDown"?-1:1)*Elapsed/Entry.Duration,1);Ladder.transform.localPosition=Vector3.down*(phase*.26f);}
            if(Water!=null)
            {Water.SetActive(ShowWater&&Active&&Entry.Id.StartsWith("Swim"));if(Water.activeSelf)Water.transform.localPosition=new Vector3(0,1.03f,0);}
            if(WorkTarget!=null)
            {
                bool repair=Active&&Entry.Id=="Repair";
                WorkTarget.SetActive(Active&&(repair||Entry.Id=="Interact"));
                WorkTarget.transform.localPosition=repair?new Vector3(.36f,1.28f,.74f):new Vector3(0,.87f,.72f);
                WorkTarget.transform.localScale=repair?new Vector3(.32f,.30f,.08f):new Vector3(1.1f,.1f,.5f);
            }
        }
    }
}


