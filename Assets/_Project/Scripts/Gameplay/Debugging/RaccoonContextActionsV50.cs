using System;
using System.Collections.Generic;
using Game.Gameplay.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using Context = Game.Gameplay.Debugging.RaccoonActionReviewV36.ReviewContext;

namespace Game.Gameplay.Debugging
{
    /// <summary>
    /// Isolated animation review. Samples the preserved body and equipment solvers separately,
    /// composes their skeletal poses, then authors environmental contact and transition motion.
    /// Does not modify production movement, inventory, attacks, or the source animation assets.
    /// </summary>
    public sealed class RaccoonContextActionsV50 : MonoBehaviour
    {
        public enum Situation { Ground, Surface, Underwater, Ladder, Grabbed, Carried, Interact, Pickup, Melee }
        public enum Hands { BothFree, RightFree, Restrained }
        public enum Motion { Idle, Forward, Back, Left, Right, Up, Down }
        public enum HandTask { None, Interact, Pickup }
        public GameObject SourcePrefab;
        public RaccoonActionReviewV36 Display;
        public Mesh RebuiltSkin;
        public Mesh PreviousRebuiltSkin;
        public string PreviousSkinLabel;
        public string RebuiltSkinLabel;
        public string SkinReviewTitle;
        public bool UseRebuiltSkin = true;
        public bool AllowSkinComparison = true;
        public bool GroundLocomotion;
        public RuntimeAnimatorController GroundLocomotionController;
        public bool GroundRun;
        // Retained for old scene serialization; locomotion now shares one gaze frame.
        [HideInInspector] public float WalkHeadPitchOffset;
        public float RevolverAimPitchOffset;
        public float ShotgunAimPitchOffset;
        public Transform LocomotionFloor;
        public Vector2 GroundVelocity => locomotionActive?locomotionMotor.ActualVelocity:Vector2.zero;
        RaccoonActionMotorV36 locomotionMotor;
        bool locomotionActive;
        public bool ShoulderAimV71;
        public bool ContextAimV72;
        public bool ArmpitVolumeV73;
        public GameObject MachetePrefab;
        public GameObject FishingPrefab;
        RaccoonFishingReview fishing;
        public RaccoonFishingReview FishingReview=>fishing;
        public bool CanFish=>Candidate&&Equipment==Context.Fishing&&State==Situation.Ground&&!InteractionBusy;
        public void StartFishingMotion(RaccoonFishingReview.Motion motion)
        {
            if(!CanFish)return;
            if(Direction!=Motion.Idle)Configure(Equipment,State,Motion.Idle);
            if(motion==RaccoonFishingReview.Motion.Carry||fishing?.Current==RaccoonFishingReview.Motion.Carry)CaptureTransition();
            fishing?.Select(motion);Paused=false;
            if(motion==RaccoonFishingReview.Motion.Hook)Notice="끌어올리기 → 장력 유지 → 대기";
            if(motion==RaccoonFishingReview.Motion.Land)Notice="낚아 올리기 → 줄 잡기 → 완료 자세 유지";
            if(motion==RaccoonFishingReview.Motion.Carry)Notice="낚시 전 · 낚싯대 들기";
            if(motion==RaccoonFishingReview.Motion.Wait)Notice="입질 대기";
            if(motion==RaccoonFishingReview.Motion.Cast)Notice="낚싯줄 던지기 → 입질 대기";
            if(motion==RaccoonFishingReview.Motion.Reel)Notice="릴 감기 · 반복";
        }
        public bool MacheteHammerGripV75;
        public bool MacheteSlashV81;
        public bool MacheteSlashV82;
        public bool MacheteNaturalV84;
        public GameObject MacheteModel {get;private set;}
        float macheteTime=99;
        public float MacheteAttackDuration=>MacheteNaturalV84?RaccoonMacheteMotionV84.Duration:MacheteSlashV82?RaccoonMacheteSlashV82.Duration:MacheteSlashV81?RaccoonMacheteSlashV81.Duration:RaccoonMachetePoseV74.Duration;
        public bool MacheteAttacking=>Equipment==Context.Machete&&macheteTime<MacheteAttackDuration;
        public bool CanMacheteAttack=>Candidate&&Equipment==Context.Machete&&MachetePrefab!=null&&!ShouldStow()&&!InteractionBusy&&!MacheteAttacking;
        Context SamplerEquipment=>Equipment==Context.Machete?Context.Hammer:Equipment;
        public bool StartMacheteAttack()
        {
            Initialize();if(!CanMacheteAttack)return false;
            macheteTime=0;Paused=false;Notice="마체테 · 베기 후 준비 자세 복귀";return true;
        }
        public bool PreviewMacheteRevision(bool latest)
        {
            Initialize();if(!CanMacheteAttack)return false;
            MacheteSlashV81=true;MacheteSlashV82=latest;
            return StartMacheteAttack();
        }
        public Context Equipment = Context.Shotgun;
        public Situation State = Situation.Surface;
        public Motion Direction = Motion.Forward;
        public Hands Freedom = Hands.BothFree;
        public bool Candidate = true, Paused, ShowGuides = true, TwoHandTask;
        [Range(.1f, 1.5f)] public float PlaybackSpeed = 1;
        [Range(-60,60)] public float AimYaw;
        [Range(-30,30)] public float AimPitch;
        public float Clock { get; private set; }
        public int Shots { get; private set; }
        public int Reloads { get; private set; }
        public int Inserts { get; private set; }
        public string Notice { get; private set; } = "새 후보 · 장비와 몸 동작을 함께 검토";
        public bool IsGun => Equipment==Context.Shotgun||Equipment==Context.Revolver||Equipment==Context.Harpoon||Equipment==Context.RiflePreview;
        public bool LongGun => Equipment==Context.Shotgun||Equipment==Context.RiflePreview;
        public bool Restricted => State==Situation.Grabbed||State==Situation.Carried;
        public bool UsesBothHands => LongGun || Reloading;
        public bool ArmedInteraction => TaskState&&groundTask==HandTask.Interact&&IsGun&&!TwoHandTask;
        HandTask groundTask;
        bool TaskState => State==Situation.Ground&&groundTask!=HandTask.None;
        bool GroundPickup => TaskState&&groundTask==HandTask.Pickup;
        bool GroundInteractionOverlay => TaskState&&groundTask==HandTask.Interact&&!TwoHandTask;
        float TaskDuration => TwoHandTask&&Equipment!=Context.Unarmed?2.4f:GroundPickup?RaccoonArmedInteractionV52.PickupDuration:RaccoonArmedInteractionV52.Duration;
        public bool CanStartTask => Candidate&&(State==Situation.Ground||Swimming)&&!InteractionBusy&&!MacheteAttacking;
        public HandTask ActiveTask => !InteractionBusy?HandTask.None:Swimming?SwimmingTask:groundTask;
        public bool Swimming => State==Situation.Surface||State==Situation.Underwater;
        public HandTask SwimmingTask { get; private set; }
        public bool CanStartSwimmingTask => Candidate&&Swimming&&SwimmingTask==HandTask.None&&!MacheteAttacking;
        public bool InteractionBusy => (TaskState&&taskTime<TaskDuration)||SwimmingTask!=HandTask.None;
        public Vector3 SwimmingContactPoint { get; private set; }
        public float SwimmingContactError { get; private set; }
        public float TaskWristAngle => (SwimmingTask!=HandTask.None?swimmingTaskLeft:Equipment!=Context.Unarmed&&!ShouldStow())?armedInteraction?.WristAngle??0:unarmedInteraction?.WristAngle??0;
        public float InteractionContactError => Equipment==Context.Unarmed?unarmedInteraction?.ContactError??0:armedInteraction?.ContactError??0;
        public bool CanFire => IsGun && !(Restricted && (Freedom==Hands.Restrained || (LongGun&&Freedom!=Hands.BothFree)))
            && !(State==Situation.Ladder&&Equipment==Context.RiflePreview)
            && !InteractionBusy&&State!=Situation.Melee;
        public bool CanReload => CanFire && (!Restricted||Freedom==Hands.BothFree);
        public bool Reloading => ready && (Equipment==Context.Shotgun ? weaponCombat.Reloading : loading);
        public int Rounds => !ready?0:Equipment==Context.Shotgun?weaponCombat.Rounds:rounds;
        public int Capacity => Equipment==Context.Shotgun?2:Equipment==Context.Revolver?6:Equipment==Context.Harpoon?1:12;
        public float ReloadProgress => !Reloading?0:Equipment==Context.Shotgun?weaponCombat.ReloadPhase:loadTime/LoadDuration;
        public float LoadDuration => Equipment==Context.Revolver?2.5f:Equipment==Context.Harpoon?2.4f:3.2f;
        public float SupportError { get; private set; }
        public float HeadLookError { get; private set; }
        public Animator OutputAnimator => outputAnimator;
        public RaccoonActionReviewV36 WeaponSource => weapon;
        public RaccoonActionReviewV36 BodySource => body;
        public float BodyPhase => body.Active?Mathf.Repeat(body.Elapsed/body.Entry.Duration,1):0;
        RaccoonActionReviewV36 body,weapon;
        RaccoonActionRigV36 bodyRig,weaponRig;
        RaccoonWeaponPoseV36 weaponCombat;
        Animator outputAnimator;
        RaccoonArmedInteractionV52 armedInteraction,unarmedInteraction;
        RaccoonActionHandsV37 backwardHands;
        RaccoonFreeHandPose freeHands;
        RaccoonLadderSkinV58 ladderSkin;
        RaccoonArmpitVolumeV73 armpitVolume;
        Transform[] output,bodyTransforms,weaponTransforms;
        bool[] armLeft,armRight,skeleton;
        Vector3[] blendPositions;
        Quaternion[] blendRotations;
        bool ready,loading,committed;
        int rounds;
        float loadTime,fireTime=10,transition=1,taskTime;
        float leftBlend=1,rightBlend=1;
        float swimmingTaskTime;
        bool swimmingTaskLeft;
        Quaternion swimmingContactFrame;
        Quaternion neutralHead,lookFrame;
        bool lookReady;
        GameObject supportGuide,pickupGuide;
        readonly Dictionary<Context,GameObject> stowed = new Dictionary<Context,GameObject>();
        string bodyId="";
        Vector3 actorOrigin;
        static readonly string[] EquipmentNames={"맨손","샷건","해머","리볼버","작살총","음식","꾸러미","낚싯대","소총·임시","마체테"};
        static readonly Situation[] StateChoices={Situation.Ground,Situation.Surface,Situation.Underwater,Situation.Ladder,Situation.Grabbed,Situation.Carried,Situation.Melee};
        static readonly string[] StateNames={"일반","수영","잠수","사다리","붙잡힘","들려가기","근접"};
        static readonly string[] MotionNames={"제자리","전진","후진","왼쪽","오른쪽","상승","하강"};
        public void Initialize()
        {
            if(ready){EnsureTaskSolvers();return;}
            if(SourcePrefab==null||Display==null)throw new InvalidOperationException("v50 requires the preserved review prefab and an output actor.");
            actorOrigin=Display.transform.position;
            body=CreateSampler("v50 body sampler",out bodyRig);
            if(GroundLocomotionController!=null)
            {
                bodyRig.Animator.runtimeAnimatorController=GroundLocomotionController;
                bodyRig.Initialize(body.transform);
                bodyRig.Animator.enabled=false;
            }
            weapon=CreateSampler("v50 weapon sampler",out weaponRig);
            weaponCombat=weapon.GetComponent<RaccoonWeaponPoseV36>();
            outputAnimator=Display.GetComponentInChildren<Animator>(true);
            foreach(var b in Display.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            foreach(var c in Display.GetComponentsInChildren<Collider>(true))c.enabled=false;
            foreach(var a in Display.GetComponentsInChildren<AudioSource>(true))a.enabled=false;
            outputAnimator.enabled=false;
            output=Display.GetComponentsInChildren<Transform>(true);
            bodyTransforms=body.GetComponentsInChildren<Transform>(true);
            weaponTransforms=weapon.GetComponentsInChildren<Transform>(true);
            if(output.Length!=bodyTransforms.Length||output.Length!=weaponTransforms.Length)throw new InvalidOperationException("Source and display hierarchy differ.");
            armLeft=new bool[output.Length];armRight=new bool[output.Length];skeleton=new bool[output.Length];
            blendPositions=new Vector3[output.Length];blendRotations=new Quaternion[output.Length];
            var ls=outputAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            var rs=outputAnimator.GetBoneTransform(HumanBodyBones.RightShoulder);
            var hips=outputAnimator.GetBoneTransform(HumanBodyBones.Hips);
            for(int i=1;i<output.Length;i++)
            {
                if(output[i].name!=bodyTransforms[i].name||output[i].name!=weaponTransforms[i].name)throw new InvalidOperationException("Pose correspondence mismatch: "+output[i].name);
                armLeft[i]=output[i]==ls||output[i].IsChildOf(ls);
                armRight[i]=output[i]==rs||output[i].IsChildOf(rs);
                skeleton[i]=(output[i]==hips||output[i].IsChildOf(hips))&&output[i].GetComponent<Renderer>()==null;
            }
            supportGuide=Guide("구속·운반 접촉 위치",new Vector3(.6f,.14f,.3f),new Color(.18f,.55f,.61f));
            pickupGuide=Guide("줍기 접촉 대상",Vector3.one*.07f,new Color(.76f,.53f,.22f));
            ready=true;rounds=Capacity;
            weapon.SetContext(SamplerEquipment);weapon.UseOriginalHammer(true);
            body.SetContext(Context.Unarmed);
            bodyRig.Animator.enabled=true;
            body.Tick(1f/60);bodyRig.Tick(1f/60,Vector2.zero,false,true);
            neutralHead=Quaternion.Inverse(body.transform.rotation)*bodyRig.Animator.GetBoneTransform(HumanBodyBones.Head).rotation;
            bodyRig.Animator.enabled=false;
            EnsureTaskSolvers();
            // Preserve old serialized scene values without exposing actions as environments.
            if(State==Situation.Interact||State==Situation.Pickup)
            {groundTask=State==Situation.Pickup?HandTask.Pickup:HandTask.Interact;State=Situation.Ground;}
            SelectBody();
            for(int i=0;i<20;i++)Sample(1f/60);
            CaptureTransition();
            var camera=FindFirstObjectByType<ViewLabOrbitCamera>();
            if(camera!=null)camera.Frame(Display.transform,3.5f,1.05f);
        }
        RaccoonActionReviewV36 CreateSampler(string name,out RaccoonActionRigV36 rig)
        {
            var go=Instantiate(SourcePrefab,actorOrigin,Display.transform.rotation);go.name=name;
            foreach(var b in go.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.enabled=false;
            foreach(var c in go.GetComponentsInChildren<Collider>(true))c.enabled=false;
            foreach(var a in go.GetComponentsInChildren<AudioSource>(true)){a.enabled=true;a.mute=true;}
            var review=go.GetComponent<RaccoonActionReviewV36>();
            // Only the rig evaluator is invoked explicitly; no hidden input or UI runs.
            rig=go.GetComponentInChildren<RaccoonActionRigV36>();rig.Initialize(go.transform);
            rig.Animator.enabled=false;
            go.GetComponent<RaccoonWeaponPoseV36>().ResetState();
            return review;
        }
        GameObject Guide(string name,Vector3 scale,Color color)
        {
            var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(transform);
            g.transform.localScale=scale;g.GetComponent<Collider>().enabled=false;
            var material=new Material(Display.WorkTarget.GetComponent<Renderer>().sharedMaterial);material.color=color;
            g.GetComponent<Renderer>().sharedMaterial=material;g.SetActive(false);return g;
        }
        public void Configure(Context equipment,Situation state,Motion motion,Hands freedom=Hands.BothFree)
        {
            Initialize();
            HandTask requestedTask=state==Situation.Interact?HandTask.Interact:state==Situation.Pickup?HandTask.Pickup:HandTask.None;
            if(requestedTask!=HandTask.None)state=Situation.Ground;
            bool sameGround=State==Situation.Ground&&state==Situation.Ground&&equipment==Equipment&&freedom==Freedom;
            if(sameGround&&requestedTask==HandTask.Interact&&!TwoHandTask)
            {
                // Start only the arm layer: do not select/reset the body or capture
                // a frozen full-body pose while the locomotion sampler is running.
                Direction=motion;CancelReload();groundTask=HandTask.Interact;taskTime=0;
                if(Equipment==Context.Fishing&&fishing!=null&&fishing.Current!=RaccoonFishingReview.Motion.Carry)
                    fishing.Select(RaccoonFishingReview.Motion.Carry);
                Notice="이동 유지 · 한 손 상호작용";
                return;
            }
            if(sameGround&&requestedTask==HandTask.None&&GroundInteractionOverlay&&InteractionBusy)
            {
                // Direction/speed buttons must not cancel or restart the arm action.
                Direction=motion;return;
            }
            // Ground direction/speed changes already blend in the locomotion motor.
            // A second frozen-pose crossfade stops the feet, then rushes them ahead.
            bool continuousGround=GroundLocomotion&&Candidate&&locomotionActive&&
                State==Situation.Ground&&state==Situation.Ground&&equipment==Equipment&&freedom==Freedom&&
                groundTask==HandTask.None&&!InteractionBusy&&!MacheteAttacking;
            if(!continuousGround)CaptureTransition();
            // Legacy callers still work, but State always stores the underlying environment.
            bool changedEquipment=equipment!=Equipment;
            bool changedState=state!=State;
            if(changedEquipment||(state!=Situation.Surface&&state!=Situation.Underwater))SwimmingTask=HandTask.None;
            bool lostHands=(state==Situation.Grabbed||state==Situation.Carried)&&freedom!=Hands.BothFree;
            if(changedEquipment||lostHands||requestedTask!=HandTask.None)CancelReload();
            Equipment=equipment;State=state;Direction=motion;Freedom=freedom;
            if(equipment==Context.Fishing&&(changedEquipment||changedState||state!=Situation.Ground||motion!=Motion.Idle||requestedTask!=HandTask.None))
                fishing?.Select(RaccoonFishingReview.Motion.Carry);
            groundTask=requestedTask;
            if(changedEquipment){weapon.SetContext(SamplerEquipment);rounds=Capacity;Shots=Reloads=Inserts=0;fireTime=10;}
            macheteTime=equipment==Context.Machete&&state==Situation.Melee?0:99;
            taskTime=0;if(!continuousGround)SelectBody();
            Notice=Restricted&&Freedom==Hands.Restrained?"무기 손 제압 · 발사/장전 불가":State==Situation.Ladder&&Equipment==Context.RiflePreview?"소총 사다리 정책 미정 · 발사/장전 비활성":"수정 후보 · 원본은 보존됨";
        }
        public void PreviewSelection(Context equipment,Situation state,Motion motion,Hands freedom=Hands.BothFree)
        {
            // Selecting an environment is an explicit request to see that pose.
            // Resume a frozen comparison and leave the ground-only equipment preview.
            Configure(equipment,state,motion,freedom);
            Candidate=true;Paused=false;
        }
        void SelectBody()
        {
            string id="";
            switch(State)
            {
                case Situation.Surface:case Situation.Underwater:
                    id=Direction==Motion.Idle?"SwimIdle":Direction==Motion.Back?"SwimBack":Direction==Motion.Left?"SwimLeft":Direction==Motion.Right?"SwimRight":"SwimMove";break;
                case Situation.Ladder:id=Direction==Motion.Up||Direction==Motion.Forward?"LadderUp":Direction==Motion.Down||Direction==Motion.Back?"LadderDown":"LadderHold";break;
                case Situation.Grabbed:id="Grabbed";break;
                case Situation.Carried:id="Carried";break;
                // The v53 ground pickup body is retained independently of the environment.
                case Situation.Ground:id=GroundPickup?"Pickup":"";break;
                case Situation.Melee:id=IsGun||Equipment==Context.Machete?"":"Melee";break;
            }
            bodyId=id;body.Select(string.IsNullOrEmpty(id)?-1:Array.FindIndex(body.Library.Entries,e=>e.Id==id));
        }
        public void RestartBody(){Initialize();if(TaskState)CancelReload();SwimmingTask=HandTask.None;CaptureTransition();taskTime=0;if(Equipment==Context.Machete&&State==Situation.Melee)macheteTime=0;SelectBody();}
        void EnsureTaskSolvers()
        {
            // Unity hot reload can retain ready/Animator while discarding plain C# helpers.
            // Rebuild the helpers from the live avatar before any action samples them.
            if(armedInteraction==null)armedInteraction=new RaccoonArmedInteractionV52(outputAnimator,Display.transform);
            if(unarmedInteraction==null)unarmedInteraction=new RaccoonArmedInteractionV52(outputAnimator,Display.transform,false);
            if(backwardHands==null)backwardHands=new RaccoonActionHandsV37(outputAnimator);
            if(freeHands==null)freeHands=new RaccoonFreeHandPose(outputAnimator,Display.transform);
            if(fishing==null&&FishingPrefab!=null)
            {
                // A plain helper can be lost on hot reload while its prop survives.
                // Retire only this review's old generated prop before rebuilding it.
                for(int i=transform.childCount-1;i>=0;i--)
                {
                    var child=transform.GetChild(i);
                    if(child.name!="Fishing · 동작 검토")continue;
                    child.gameObject.SetActive(false);Destroy(child.gameObject);
                }
                fishing=new RaccoonFishingReview(FishingPrefab,outputAnimator,Display.transform,transform);
            }
            if(ladderSkin==null)ladderSkin=new RaccoonLadderSkinV58(outputAnimator,Display.transform,body.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);
        }
        public bool StartTask(HandTask task)
        {
            Initialize();
            if(!CanStartTask||(task!=HandTask.Interact&&task!=HandTask.Pickup))return false;
            bool started;
            if(Swimming)started=StartSwimmingTask(task);
            else
            {
                Configure(Equipment,task==HandTask.Interact?Situation.Interact:Situation.Pickup,Direction,Freedom);
                Notice=GroundInteractionOverlay?"이동 유지 · 한 손 상호작용":task==HandTask.Interact?"일반 · 상호작용":"일반 · 줍기";
                started=true;
            }
            // An explicit preview button should also play when the viewer was paused.
            if(started)Paused=false;
            return started;
        }
        public bool StartSwimmingTask(HandTask task,Vector3? worldContact=null)
        {
            Initialize();
            if(!CanStartSwimmingTask||(task!=HandTask.Interact&&task!=HandTask.Pickup))return false;
            CancelReload();
            // Do not select a new body clip or capture a full-body transition here.
            // The existing swim phase, feet, torso and unused arm must keep advancing.
            swimmingTaskLeft=Equipment!=Context.Unarmed&&!ShouldStow();
            swimmingContactFrame=lookFrame;
            var upper=outputAnimator.GetBoneTransform(swimmingTaskLeft?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=outputAnimator.GetBoneTransform(swimmingTaskLeft?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=outputAnimator.GetBoneTransform(swimmingTaskLeft?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            float reach=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position);
            // Leave reach reserve for the continuing shoulder stroke and palm offset.
            // A chest-relative target can fall outside the arm during sideways swimming.
            SwimmingContactPoint=worldContact??(upper.position+lookFrame*new Vector3(swimmingTaskLeft?-.10f:.10f,task==HandTask.Pickup?-.35f:-.18f,.90f)*reach*.80f);
            SwimmingTask=task;swimmingTaskTime=0;SwimmingContactError=0;
            Notice=task==HandTask.Pickup?"수영 유지 · 한 손으로 줍기":"수영 유지 · 한 손 상호작용";
            return true;
        }
        public bool Fire()
        {
            Initialize();if(!CanFire){Notice="현재 손·행동 조건에서는 사격할 수 없습니다.";return false;}
            if(Reloading||fireTime<(Equipment==Context.Shotgun?.65f:.45f)||Rounds<=0){Notice=Rounds<=0?"탄약 없음 · 장전하세요":"무기 동작 진행 중";return false;}
            if(Equipment==Context.Shotgun){if(!weaponCombat.Fire())return false;}
            else
            {
                string id=Equipment==Context.Revolver?"RevolverFire":Equipment==Context.Harpoon?"HarpoonFire":"RifleFire";
                weapon.Select(Array.FindIndex(weapon.Library.Entries,e=>e.Id==id));rounds--;
            }
            Shots++;fireTime=0;Notice="총기 발사";return true;
        }
        public bool Reload()
        {
            Initialize();if(!CanReload){Notice="장전에 필요한 손을 사용할 수 없습니다.";return false;}
            if(Reloading||Rounds>=Capacity||fireTime<.65f)return false;
            if(Equipment==Context.Shotgun){if(!weaponCombat.Reload())return false;}
            else
            {
                string id=Equipment==Context.Revolver?"RevolverReload":Equipment==Context.RiflePreview?"RifleReload":null;
                if(id!=null)weapon.Select(Array.FindIndex(weapon.Library.Entries,e=>e.Id==id));
                loading=true;committed=false;loadTime=0;
            }
            Reloads++;Notice="탄약 보충 장전";return true;
        }
        public void CancelReload()
        {
            if(!ready)return;
            if(Reloading)CaptureTransition();
            if(Equipment==Context.Shotgun)weaponCombat.CancelReviewReload();
            else if(loading){loading=false;weapon.Select(-1);}
            Notice="장전 중단 · 이미 반영한 탄약 유지";
        }
        public void SetComparison(bool candidate){Initialize();CaptureTransition();Candidate=candidate;if(!candidate)SwimmingTask=HandTask.None;}
        void CaptureTransition()
        {
            if(output==null)return;
            for(int i=1;i<output.Length;i++){blendPositions[i]=output[i].localPosition;blendRotations[i]=output[i].localRotation;}
            transition=0;
        }
        public void Step(float dt)
        {
            Initialize();if(dt<=0)return;dt=Mathf.Min(dt,.05f);
            bool interactionWasBusy=InteractionBusy;
            Clock+=dt;taskTime+=dt;fireTime+=dt;transition=Mathf.Min(1,transition+dt/.35f);
            macheteTime+=dt;
            if(SwimmingTask!=HandTask.None)
            {
                swimmingTaskTime+=dt;
                if(swimmingTaskTime>=(SwimmingTask==HandTask.Pickup?RaccoonArmedInteractionV52.PickupDuration:RaccoonArmedInteractionV52.Duration))SwimmingTask=HandTask.None;
            }
            if(interactionWasBusy&&!InteractionBusy)
            {
                if(GroundInteractionOverlay)groundTask=HandTask.None;
                Notice="작업 완료 · 원래 손 자세 복귀";
            }
            if(loading)
            {
                loadTime+=dt;
                if(!committed&&loadTime>=LoadDuration*.78f){Inserts+=Capacity-rounds;rounds=Capacity;committed=true;}
                if(loadTime>=LoadDuration)loading=false;
            }
            Sample(dt);
        }
        void Sample(float dt)
        {
            // The existing wrist deformation guard requires an enabled Animator. Enable
            // only for explicit evaluation, then disable to prevent Unity's later pass
            // from replacing the composed pose (including while the preview is paused).
            bodyRig.Animator.enabled=true;
            bool movingBody=SampleLocomotion(dt);
            if(!movingBody){body.Tick(dt);bodyRig.Tick(dt,Vector2.zero,false,true);}
            bodyRig.Animator.enabled=false;
            weaponRig.Animator.enabled=true;
            weaponCombat.Step(dt);weapon.Tick(dt);weaponRig.Tick(dt,movingBody?GroundVelocity:Vector2.zero,movingBody&&GroundRun&&LongGun,true);
            weaponRig.Animator.enabled=false;
            // Copy the full authored body before applying equipment-specific arm chains.
            for(int i=1;i<output.Length;i++)Copy(output[i],Candidate?bodyTransforms[i]:weaponTransforms[i]);
            if(Candidate)
            {
                // v84 starts from the ordinary arm pose, then turns the torso.
                // Solving the resting free hand against an already turned chest
                // changes its elbow plane underneath the authored joint motion.
                bool naturalMachete=MacheteNaturalV84&&MacheteAttacking;
                if(!naturalMachete)AuthorBody();
                bool stow=ShouldStow();
                float rightTarget=stow?0:1;
                float leftTarget=stow?0:LongGun||Reloading||State==Situation.Ground||Equipment==Context.Bundle?1:0;
                if(Equipment==Context.Machete)leftTarget=0;
                if(TaskState)leftTarget=stow?0:1;
                bool fishingCarry=Equipment==Context.Fishing&&(fishing==null||fishing.Current==RaccoonFishingReview.Motion.Carry||State!=Situation.Ground||Direction!=Motion.Idle||InteractionBusy);
                if(Equipment==Context.Fishing){leftTarget=0;if(fishingCarry)rightTarget=0;}
                // Pickup keeps the crouching body but replaces the old baked arm reach.
                // Rebuild both arms from their neutral/equipment baseline on every frame.
                if(GroundPickup&&Equipment==Context.Unarmed)rightTarget=leftTarget=1;
                rightBlend=Mathf.MoveTowards(rightBlend,rightTarget,dt*5);
                leftBlend=Mathf.MoveTowards(leftBlend,leftTarget,dt*5);
                for(int i=1;i<output.Length;i++)
                {
                    float w=armRight[i]?rightBlend:armLeft[i]?leftBlend:0;
                    if(w>0){output[i].localPosition=Vector3.Lerp(output[i].localPosition,weaponTransforms[i].localPosition,w);output[i].localRotation=Quaternion.Slerp(output[i].localRotation,weaponTransforms[i].localRotation,w);}
                }
                AimEquipment(dt);
                AuthorGroundFreeHands(fishingCarry);
                if(naturalMachete)AuthorBody();
                if(fishing!=null)
                {
                    if(fishingCarry&&!stow)fishing.ApplyCarry(dt,lookFrame,State,BodyPhase);
                    else fishing.Apply(dt,lookFrame,Equipment==Context.Fishing&&!stow,
                        State==Situation.Ground&&Direction==Motion.Idle&&!InteractionBusy&&GroundVelocity.sqrMagnitude<.000001f);
                }
                if(Equipment==Context.Machete&&rightBlend>0)
                {
                    if(MacheteNaturalV84)RaccoonMacheteMotionV84.ApplyArms(outputAnimator,lookFrame,macheteTime,MacheteSlashV82,rightBlend);
                    else if(MacheteSlashV82)RaccoonMacheteSlashV82.ApplyArms(outputAnimator,backwardHands,lookFrame,macheteTime,rightBlend);
                    else if(MacheteSlashV81)RaccoonMacheteSlashV81.ApplyArms(outputAnimator,backwardHands,lookFrame,macheteTime,rightBlend);
                    else if(MacheteHammerGripV75)RaccoonMachetePoseV74.ApplyHammerAttack(outputAnimator,lookFrame,macheteTime,rightBlend);
                    else RaccoonMachetePoseV74.Apply(outputAnimator,Display.transform,backwardHands,lookFrame,macheteTime,rightBlend);
                }
                if(State==Situation.Underwater&&Direction==Motion.Back)AuthorUnderwaterBackward();
                if(TaskState)AuthorTask();
                if(SwimmingTask!=HandTask.None)
                {
                    var task=swimmingTaskLeft?armedInteraction:unarmedInteraction;
                    task.ApplySwimming(swimmingTaskTime,SwimmingTask==HandTask.Pickup,SwimmingContactPoint,swimmingContactFrame);
                    SwimmingContactError=task.ContactError;
                }
                else SwimmingContactError=0;
                if(Equipment==Context.Harpoon&&loading)AuthorHarpoonReload();
                SetProps(stow);
            }
            else {SetProps(false);if(fishing!=null)fishing.Model.SetActive(false);}
            float blend=Mathf.SmoothStep(0,1,transition);
            for(int i=1;i<output.Length;i++)if(skeleton[i]&&blend<1)
            {output[i].localPosition=Vector3.Lerp(blendPositions[i],output[i].localPosition,blend);output[i].localRotation=Quaternion.Slerp(blendRotations[i],output[i].localRotation,blend);}
            // Local bone crossfades can tilt a world-space gaze as the torso settles.
            // Resolve gaze after the final ground pose, with no transition-only gesture.
            if(Candidate&&State==Situation.Ground&&(!InteractionBusy||GroundInteractionOverlay))ApplyHeadLook();
            // Mesh-only correctives run on the composed output, not on a previous source pose.
            outputAnimator.enabled=true;
            var wrist=outputAnimator.GetComponent<RaccoonWristDeformation>();
            if(wrist!=null)
            {
                wrist.PreserveTwistPath=Candidate&&Equipment==Context.Shotgun&&(GroundInteractionOverlay||transition<1);
                wrist.Apply();
            }
            var elbows=outputAnimator.GetComponent<RaccoonElbowCorrectives>();if(elbows!=null)elbows.Apply();
            outputAnimator.enabled=false;
            ApplyReviewSkin(dt);
            // Follow the final blended wrist, including equipment/state transitions.
            UpdateMachete();
            if(fishing!=null&&fishing.Model.activeSelf)fishing.FollowHand();
            HeadLookError=Candidate?Quaternion.Angle(outputAnimator.GetBoneTransform(HumanBodyBones.Head).rotation,lookFrame*neutralHead):0;
            UpdateGuides();
            SupportError=0;
            if(Candidate&&LongGun&&!Reloading&&!InteractionBusy&&rightBlend>.99f&&leftBlend>.99f&&transition>=1)
            {
                var a=weaponRig.Animator;var rh=a.GetBoneTransform(HumanBodyBones.RightHand);var lh=a.GetBoneTransform(HumanBodyBones.LeftHand);
                Vector3 expected=rh.InverseTransformPoint(lh.position);
                var orh=outputAnimator.GetBoneTransform(HumanBodyBones.RightHand);var olh=outputAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
                SupportError=Vector3.Distance(orh.TransformPoint(expected),olh.position);
            }
        }
        static void Copy(Transform to,Transform from){to.localPosition=from.localPosition;to.localRotation=from.localRotation;to.localScale=from.localScale;}
        void AuthorGroundFreeHands(bool fishingCarry)
        {
            if(State!=Situation.Ground||(InteractionBusy&&!GroundInteractionOverlay)||Reloading||
                (MacheteAttacking&&!MacheteSlashV81&&!MacheteSlashV82&&!MacheteNaturalV84))return;
            bool unarmed=Equipment==Context.Unarmed;
            bool oneHand=Equipment==Context.Hammer||Equipment==Context.Revolver||
                Equipment==Context.Harpoon||Equipment==Context.Food||Equipment==Context.Machete||
                (Equipment==Context.Fishing&&fishingCarry);
            if(!unarmed&&!oneHand)return;
            // Read swing from the body clip, never from a held weapon's grip.
            var a=bodyRig.Animator;var r=body.transform;
            Vector3 l=r.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.LeftHand).position);
            Vector3 h=r.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.RightHand).position);
            float swing=locomotionActive?Mathf.Clamp((h.z-l.z)*.5f,-.16f,.16f)*bodyRig.LocomotionWeight:0;
            // The retained run has baked shotgun arms. Derive the free-arm rhythm
            // from the opposite leg instead, while preserving its approved stride.
            float run=locomotionActive?bodyRig.RunBlend:0;
            Vector3 leftKnee=r.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.LeftLowerLeg).position);
            Vector3 rightKnee=r.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.RightLowerLeg).position);
            float runSwing=Mathf.Clamp((leftKnee.z-rightKnee.z)*.65f,-.16f,.16f);
            swing=Mathf.Lerp(swing,runSwing,run);
            freeHands.Apply(true,unarmed,swing,.025f*Mathf.Clamp01(Mathf.Abs(GroundVelocity.x)),run);
        }
        bool SampleLocomotion(float dt)
        {
            bool available=GroundLocomotion&&Candidate&&State==Situation.Ground&&
                (!InteractionBusy||GroundInteractionOverlay)&&(!MacheteAttacking||MacheteSlashV81||MacheteSlashV82||MacheteNaturalV84);
            if(!available)
            {
                if(locomotionActive)EndLocomotion();
                return false;
            }
            var input=Direction==Motion.Forward?Vector2.up:Direction==Motion.Back?Vector2.down:
                Direction==Motion.Left?Vector2.left:Direction==Motion.Right?Vector2.right:Vector2.zero;
            // Keep the planted-foot solver alive through the v81 slash. A moving
            // attack settles through the existing stop steps, then resumes input.
            if((MacheteSlashV81||MacheteSlashV82||MacheteNaturalV84)&&MacheteAttacking)input=Vector2.zero;
            if(!locomotionActive)
            {
                CaptureTransition();
                body.GetComponent<CharacterController>().enabled=true;
                locomotionMotor=body.GetComponent<RaccoonActionMotorV36>();
                locomotionMotor.Initialize();locomotionMotor.Automatic=false;
                locomotionMotor.ResetAt(Display.transform.position);
                // This context unlocks the original motor; BodyOnly below removes
                // its equipment pose so the clip's own torso and free arms survive.
                body.SetContext(Context.Shotgun);
                bodyRig.LocomotionBodyOnly=true;
                locomotionActive=true;
            }
            // Equipment and free hands are composed after the body-only run sample.
            // The review floor follows the actor. Publish its transform before
            // CharacterController.Move, including manual frame-by-frame previews
            // that do not run a FixedUpdate between samples.
            if(LocomotionFloor!=null)Physics.SyncTransforms();
            bodyRig.LocomotionInput=input;
            locomotionMotor.Advance(dt,input,GroundRun);
            locomotionMotor.Evaluate(dt);
            Display.transform.position=body.transform.position;
            weapon.transform.position=body.transform.position;
            if(LocomotionFloor!=null)
            {
                var p=LocomotionFloor.position;p.x=body.transform.position.x;p.z=body.transform.position.z;
                LocomotionFloor.position=p;
            }
            // Keep this sampler alive at rest. Rebind/reset on each stop restarts the
            // gait and adds a second frozen-pose transition when movement resumes.
            return true;
        }
        void EndLocomotion()
        {
            CaptureTransition();
            locomotionMotor.ResetAt(Display.transform.position);
            bodyRig.LocomotionBodyOnly=false;
            body.SetContext(Context.Unarmed);SelectBody();
            body.GetComponent<CharacterController>().enabled=false;
            locomotionActive=false;
        }
        bool ShouldStow()
        {
            if(Restricted&&(Freedom==Hands.Restrained||(LongGun&&Freedom!=Hands.BothFree)))return true;
            if(State==Situation.Ladder&&Equipment==Context.RiflePreview)return true;
            if(Equipment==Context.Unarmed)return true;
            if(TaskState&&TwoHandTask)return InteractionBusy;
            if(Equipment==Context.Fishing)return State==Situation.Melee;
            if(Equipment==Context.Food||Equipment==Context.Bundle)
                return TaskState?InteractionBusy:State!=Situation.Ground;
            return false;
        }
        public void SetSkinRevision(bool rebuilt)
        {
            Initialize();UseRebuiltSkin=rebuilt||!AllowSkinComparison;ApplyReviewSkin(1);
            if(ShoulderAimV71||ContextAimV72||ArmpitVolumeV73)Sample(0);
        }
        void ApplyReviewSkin(float dt)
        {
            if(!AllowSkinComparison)UseRebuiltSkin=true;
            if(Candidate&&UseRebuiltSkin&&RebuiltSkin!=null)
                outputAnimator.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh=RebuiltSkin;
            else if(Candidate&&!UseRebuiltSkin&&PreviousRebuiltSkin!=null)
                outputAnimator.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh=PreviousRebuiltSkin;
            else ladderSkin.Apply(Candidate&&State==Situation.Ladder&&IsGun&&!ShouldStow(),dt,!Candidate);
            if(Candidate&&UseRebuiltSkin&&ArmpitVolumeV73&&RebuiltSkin!=null)
            {
                if(armpitVolume==null)
                {
                    var sourceSkin=SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>();
                    var map=SourcePrefab.transform.worldToLocalMatrix*sourceSkin.transform.localToWorldMatrix;
                    armpitVolume=new RaccoonArmpitVolumeV73(RebuiltSkin,outputAnimator.GetComponentInChildren<SkinnedMeshRenderer>(),Display.transform,map);
                }
                armpitVolume.Apply();
            }
        }
        void AuthorBody()
        {
            var hips=outputAnimator.GetBoneTransform(HumanBodyBones.Hips);
            if(MacheteAttacking)
            {
                if(MacheteNaturalV84)RaccoonMacheteMotionV84.ApplyBody(outputAnimator,lookFrame,macheteTime,MacheteSlashV82);
                else if(MacheteSlashV82)RaccoonMacheteSlashV82.ApplyBody(outputAnimator,lookFrame,macheteTime,State==Situation.Ground);
                else if(MacheteSlashV81)RaccoonMacheteSlashV81.ApplyBody(outputAnimator,lookFrame,macheteTime);
                else
                {
                    var chest=outputAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
                    chest.rotation=Quaternion.AngleAxis(RaccoonMachetePoseV74.TorsoTurn(macheteTime),Display.transform.up)*chest.rotation;
                }
            }
            if(State==Situation.Underwater)
            {
                float pitch=Direction==Motion.Back?-14:Direction==Motion.Up?22:Direction==Motion.Down?65:48;
                hips.rotation=Quaternion.AngleAxis(pitch,Display.transform.right)*hips.rotation;
                hips.position+=Display.transform.up*(.28f+.012f*Mathf.Sin(Clock*2));
            }
            if(State==Situation.Surface)hips.position+=Display.transform.up*.18f;
            if(State==Situation.Carried)hips.position+=Display.transform.up*(.30f+.018f*Mathf.Sin(Clock*2.8f));
            if(State==Situation.Melee&&IsGun)
            {
                float p=Mathf.Clamp01(taskTime/1.05f),k=Mathf.Sin(Mathf.PI*p);k*=k;
                var foot=outputAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
                Solve(false,true,foot.position+Display.transform.TransformVector(new Vector3(0,.36f*k,.40f*k)),foot.rotation);
            }
        }
        void AuthorUnderwaterBackward()
        {
            // Push water toward the facing direction while the body travels backward.
            // This is a separate reverse stroke, not a reversed forward-swim playback.
            var root=Display.transform;
            foreach(bool left in new[]{true,false})
            {
                float side=left?-1:1;
                float phase=Mathf.Repeat(BodyPhase+(left?0:.5f),1);
                float push=phase<.42f?Mathf.SmoothStep(0,1,phase/.42f):1-Mathf.SmoothStep(0,1,(phase-.42f)/.58f);
                var thigh=outputAnimator.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                var calf=outputAnimator.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                var foot=outputAnimator.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                float legReach=Vector3.Distance(thigh.position,calf.position)+Vector3.Distance(calf.position,foot.position);
                var kick=thigh.position+root.TransformVector(new Vector3(side*.025f,-.78f-.08f*push,.23f+.25f*push))*legReach*.88f;
                var footRotation=Quaternion.AngleAxis(-8+16*push,root.right)*foot.rotation;
                Solve(left,true,kick,footRotation);

                // Equipment owns its grip arm(s); only the remaining free arm sculls.
                float free=1-(left?leftBlend:rightBlend);
                if(!left&&Equipment==Context.Fishing&&!ShouldStow())free=0;
                if(free<=0)continue;
                var upper=outputAnimator.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                var lower=outputAnimator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                var hand=outputAnimator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                float armReach=Vector3.Distance(upper.position,lower.position)+Vector3.Distance(lower.position,hand.position);
                var stroke=upper.position+root.TransformVector(new Vector3(side*.14f,-.30f+.06f*push,.35f+.38f*push))*armReach;
                var oldRotation=hand.rotation;
                Solve(left,false,Vector3.Lerp(hand.position,stroke,free),oldRotation);
                hand.rotation=Quaternion.Slerp(oldRotation,backwardHands.Rotation(outputAnimator,root,body.Entry,BodyPhase,left),free);
            }
        }
        void ApplyHeadLook()
        {
            var head=outputAnimator.GetBoneTransform(HumanBodyBones.Head);
            var neck=outputAnimator.GetBoneTransform(HumanBodyBones.Neck);
            // Idle, walk and run share the same user-directed gaze. Sampling the
            // source head and fading a separate walk correction produced a head lift
            // whenever the source blend tree passed through its idle pose.
            var target=lookFrame*neutralHead;
            if(neck!=null)neck.rotation=Quaternion.Slerp(Quaternion.identity,target*Quaternion.Inverse(head.rotation),.45f)*neck.rotation;
            head.rotation=target;
        }
        void AimEquipment(float dt)
        {
            // A single facing frame drives the gaze and the accepted equipment pose in
            // every context, including empty hands and weapons temporarily stowed.
            // Zero input always means character-forward, including armed ladder poses.
            // Side aim is explicit input, not an equipment-specific facing offset.
            float yaw=Mathf.Clamp(AimYaw,-60,60);
            var target=Display.transform.rotation*Quaternion.Euler(Mathf.Clamp(AimPitch,-30,30),yaw,0);
            if(!lookReady){lookFrame=target;lookReady=true;}
            else lookFrame=Quaternion.RotateTowards(lookFrame,target,240*dt);
            bool ladderTurn=State==Situation.Ladder&&IsGun&&!ShouldStow();
            bool groundAimFollow=ShoulderAimV71&&(UseRebuiltSkin||ContextAimV72)&&State==Situation.Ground&&IsGun;
            bool contextTurn=ContextAimV72&&(UseRebuiltSkin||ArmpitVolumeV73)&&(Swimming||Restricted);
            var freeHand=outputAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            var freeHandPosition=freeHand.position;var freeHandRotation=freeHand.rotation;
            var freeShoulder=outputAnimator.GetBoneTransform(HumanBodyBones.LeftShoulder);
            var freeShoulderPosition=freeShoulder.position;var freeShoulderRotation=freeShoulder.rotation;
            if(ladderTurn||groundAimFollow||contextTurn)
            {
                // Distribute the aim through the torso before restoring the shared
                // weapon frame. Ground review v71 also follows elevation; its weight
                // fades with the weapon arm during stowing instead of snapping.
                // Swimming/capture yaw is independent of equipment and arm freedom:
                // the authored body lean remains, and the hips/legs are not rotated.
                Vector3 facing=Quaternion.Inverse(Display.transform.rotation)*(lookFrame*Vector3.forward);
                float turn=Mathf.Atan2(facing.x,facing.z)*Mathf.Rad2Deg;
                if(groundAimFollow)turn*=rightBlend;
                var spine=outputAnimator.GetBoneTransform(HumanBodyBones.Spine);
                var middleChest=outputAnimator.GetBoneTransform(HumanBodyBones.Chest);
                var upperChest=outputAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
                float pitch=groundAimFollow?-Mathf.Asin(Mathf.Clamp(facing.y,-1,1))*Mathf.Rad2Deg*rightBlend:0;
                var pitchAxis=Display.transform.rotation*Quaternion.AngleAxis(turn,Vector3.up)*Vector3.right;
                spine.rotation=Quaternion.AngleAxis(turn*(groundAimFollow||contextTurn?.20f:.18f),Display.transform.up)*spine.rotation;
                spine.rotation=Quaternion.AngleAxis(pitch*.10f,pitchAxis)*spine.rotation;
                middleChest.rotation=Quaternion.AngleAxis(turn*(groundAimFollow||contextTurn?.30f:.22f),Display.transform.up)*middleChest.rotation;
                middleChest.rotation=Quaternion.AngleAxis(pitch*.20f,pitchAxis)*middleChest.rotation;
                upperChest.rotation=Quaternion.AngleAxis(turn*(groundAimFollow||contextTurn?.35f:.25f),Display.transform.up)*upperChest.rotation;
                upperChest.rotation=Quaternion.AngleAxis(pitch*.30f,pitchAxis)*upperChest.rotation;
            }
            var chest=outputAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            var sourceChest=weaponRig.Animator.GetBoneTransform(HumanBodyBones.UpperChest);
            // The meshes' barrels slope below their transform's aiming axis.
            // Rotate the complete held pose, keeping fingers and
            // reload contacts in the same frame instead of tilting only the prop.
            float barrelPitch=Equipment==Context.Revolver?RevolverAimPitchOffset:
                Equipment==Context.Shotgun?ShotgunAimPitchOffset:0;
            var equipmentFrame=lookFrame*Quaternion.Euler(barrelPitch,0,0);
            var q=equipmentFrame*Quaternion.Inverse(weapon.transform.rotation);
            foreach(bool left in new[]{true,false})
            {
                float w=left?leftBlend:rightBlend;if(w<=0||Equipment==Context.Unarmed)continue;
                var shoulder=outputAnimator.GetBoneTransform(left?HumanBodyBones.LeftShoulder:HumanBodyBones.RightShoulder);
                var source=weaponRig.Animator.GetBoneTransform(left?HumanBodyBones.LeftShoulder:HumanBodyBones.RightShoulder);
                // Retain the accepted weapon-space pose instead of inheriting a swim body's
                // forward pitch. Both arm chains share exactly one rigid frame.
                shoulder.position=Vector3.Lerp(shoulder.position,chest.position+q*(source.position-sourceChest.position),w);
                shoulder.rotation=Quaternion.Slerp(shoulder.rotation,q*source.rotation,w);
            }
            ApplyHeadLook();
            if(ladderTurn&&leftBlend<1)
            {
                float free=1-leftBlend;
                // Keep the unoccupied arm's ladder frame. Rotating its shoulder away
                // first can put the contact outside the elbow solver's reach.
                freeShoulder.position=Vector3.Lerp(freeShoulder.position,freeShoulderPosition,free);
                freeShoulder.rotation=Quaternion.Slerp(freeShoulder.rotation,freeShoulderRotation,free);
                if(leftBlend>0)
                    Solve(true,false,Vector3.Lerp(freeHand.position,freeHandPosition,free),Quaternion.Slerp(freeHand.rotation,freeHandRotation,free));
            }
            if(State==Situation.Melee&&Equipment==Context.Hammer)
            {
                float p=Mathf.Clamp01(taskTime/1.15f),e=Mathf.Sin(p*Mathf.PI);e*=e;
                var hand=outputAnimator.GetBoneTransform(HumanBodyBones.RightHand);
                Solve(false,false,hand.position+Display.transform.TransformVector(new Vector3(-.05f*e,.32f*e,.16f*e)),Quaternion.AngleAxis(-28*e,Display.transform.right)*hand.rotation);
            }
        }
        void AuthorTask()
        {
            if(Equipment==Context.Unarmed){unarmedInteraction.Apply(taskTime,GroundPickup);return;}
            if(!ShouldStow())
            {
                armedInteraction.Apply(taskTime,GroundPickup,Equipment==Context.Shotgun&&GroundInteractionOverlay);
                return;
            }
            if(!InteractionBusy)return;
            // A stowed item leaves the dominant hand free; use the same visible reach,
            // grasp and recovery instead of a small anonymous offset on the other arm.
            unarmedInteraction.Apply(taskTime,GroundPickup);
            if(TwoHandTask)armedInteraction.Apply(taskTime,GroundPickup);
        }
        void AuthorHarpoonReload()
        {
            float p=ReloadProgress,e=Mathf.Sin(Mathf.PI*Mathf.Clamp01(p));e*=e;
            var left=outputAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            var right=outputAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 target=right.position+Display.transform.TransformVector(new Vector3(-.05f,-.03f,.08f));
            Solve(true,false,Vector3.Lerp(left.position,target,e),Quaternion.Slerp(left.rotation,right.rotation,e*.3f));
        }
        void Solve(bool left,bool leg,Vector3 target,Quaternion rotation)
        {
            var a=outputAnimator;
            var upper=a.GetBoneTransform(leg?(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg):(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm));
            var lower=a.GetBoneTransform(leg?(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg):(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm));
            var end=a.GetBoneTransform(leg?(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot):(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand));
            RaccoonEquipmentPose.SolveArm(upper,lower,end,target,upper.position+Display.transform.TransformVector(leg?Vector3.forward:new Vector3(left?-.4f:.4f,-.3f,-.15f)));end.rotation=rotation;
        }
        void SetProps(bool stow)
        {
            Display.Weapon.SetActive(!stow&&(Equipment==Context.Shotgun||Equipment==Context.RiflePreview));
            string name=Equipment==Context.Hammer?"Hammer":Equipment==Context.Revolver?"Revolver":Equipment==Context.Harpoon?"Harpoon":Equipment==Context.Food?"Food":Equipment==Context.Bundle?"Bundle":Equipment==Context.Fishing?"Rod":"";
            for(int i=0;i<Display.Props.Length;i++)
            {
                var p=Display.Props[i];p.SetActive(!stow&&p.name==name&&!(Candidate&&FishingPrefab!=null&&p.name=="Rod"));
                var source=Array.Find(weapon.Props,x=>x.name==p.name);
                if(source!=null){Copy(p.transform,source.transform);var mf=p.GetComponent<MeshFilter>();var sf=source.GetComponent<MeshFilter>();if(mf!=null&&sf!=null)mf.sharedMesh=sf.sharedMesh;}
            }
            UpdateStowed(stow);
            // Accessories outside the skeleton follow the final hand/gun pose through one rigid mapping.
            var wr=weaponRig.Animator.GetBoneTransform(HumanBodyBones.RightHand);
            var dr=outputAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            Quaternion delta=dr.rotation*Quaternion.Inverse(wr.rotation);
            var wc=Display.GetComponent<RaccoonWeaponPoseV36>();
            MapAccessory(wc.Shell,weaponCombat.Shell,wr,dr,delta,!stow&&Equipment==Context.Shotgun);
            MapAccessory(wc.Flash,weaponCombat.Flash,wr,dr,delta,!stow&&Equipment==Context.Shotgun);
            if(Candidate&&Equipment==Context.Shotgun&&wc.Flash!=null&&wc.Flash.gameObject.activeSelf)
            {
                var gun=Display.Weapon.transform;
                var barrel=Quaternion.AngleAxis(ShotgunAimPitchOffset,Vector3.right)*Vector3.up;
                wc.Flash.rotation=Quaternion.LookRotation(gun.TransformDirection(barrel),gun.forward);
            }
            if(wc.Mechanics!=null&&weaponCombat.Mechanics!=null)
            {
                MapAccessory(wc.Mechanics.Casing,weaponCombat.Mechanics.Casing,wr,dr,delta,!stow&&Equipment==Context.Shotgun);
                if(wc.Mechanics.Pump!=null&&weaponCombat.Mechanics.Pump!=null)Copy(wc.Mechanics.Pump,weaponCombat.Mechanics.Pump);
            }
        }
        void UpdateStowed(bool show)
        {
            foreach(var pair in stowed)pair.Value.SetActive(false);
            if(!show||Equipment==Context.Unarmed||Equipment==Context.Machete)return;
            // Inventory presentation only: these copies never have colliders or inventory logic.
            GameObject prop=LongGun?Display.Weapon:Array.Find(Display.Props,x=>x.name==(Equipment==Context.Hammer?"Hammer":Equipment==Context.Revolver?"Revolver":Equipment==Context.Harpoon?"Harpoon":Equipment==Context.Food?"Food":Equipment==Context.Bundle?"Bundle":"Rod"));
            if(Equipment==Context.Fishing&&FishingPrefab!=null)prop=FishingPrefab;
            if(prop==null)return;
            if(!stowed.TryGetValue(Equipment,out var copy))
            {
                copy=Instantiate(prop,transform);copy.name="수납 표시 · "+Equipment;
                foreach(var c in copy.GetComponentsInChildren<Collider>(true))c.enabled=false;
                foreach(var b in copy.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
                copy.transform.localScale=prop.transform.lossyScale;stowed.Add(Equipment,copy);
            }
            bool packed=Equipment==Context.Food||Equipment==Context.Bundle;
            copy.SetActive(!packed);
            var hips=outputAnimator.GetBoneTransform(HumanBodyBones.Hips);
            copy.transform.position=hips.position+Display.transform.TransformVector(LongGun||Equipment==Context.Fishing?new Vector3(.16f,.20f,-.25f):new Vector3(.33f,.03f,-.08f));
            copy.transform.rotation=Display.transform.rotation*Quaternion.Euler(LongGun||Equipment==Context.Harpoon?0:90,0,25);
            if(Equipment==Context.Fishing)
            {
                var chest=outputAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
                Vector3 up=(chest.position-hips.position).normalized;
                var frame=Quaternion.LookRotation(Vector3.Cross(Display.transform.right,up),up);
                copy.transform.position=chest.position+frame*new Vector3(.30f,-.36f,-.28f);
                copy.transform.rotation=frame*Quaternion.Euler(-75,55,15);
                RaccoonFishingReview.UpdatePackedFloat(copy.transform);
            }
        }
        static void MapAccessory(Transform dst,Transform src,Transform from,Transform to,Quaternion delta,bool visible)
        {
            if(dst==null||src==null)return;dst.gameObject.SetActive(visible&&src.gameObject.activeSelf);
            dst.SetPositionAndRotation(to.position+delta*(src.position-from.position),delta*src.rotation);
        }
        void UpdateGuides()
        {
            Display.Ladder.SetActive(Candidate&&ShowGuides&&State==Situation.Ladder);
            // Repeatable moving reference shows climbing without teleporting the actor at a loop boundary.
            if(Display.Ladder.activeSelf)Display.Ladder.transform.localPosition=body.Ladder.transform.localPosition;
            Display.Water.SetActive(Candidate&&ShowGuides&&(State==Situation.Surface||State==Situation.Underwater));
            if(Display.Water.activeSelf)Display.Water.transform.localPosition=new Vector3(0,State==Situation.Underwater?2.05f:1.17f,0);
            Display.WorkTarget.SetActive(Candidate&&ShowGuides&&TaskState&&!GroundPickup&&taskTime<RaccoonArmedInteractionV52.Duration);
            supportGuide.SetActive(Candidate&&ShowGuides&&Restricted);
            supportGuide.transform.SetPositionAndRotation(Display.transform.TransformPoint(new Vector3(0,State==Situation.Carried?1.15f:1.0f,-.12f)),Display.transform.rotation);
            bool groundLeft=Equipment!=Context.Unarmed&&!ShouldStow();
            pickupGuide.SetActive(Candidate&&ShowGuides&&GroundPickup&&taskTime<1.48f);
            pickupGuide.transform.SetPositionAndRotation(Display.transform.TransformPoint(new Vector3(groundLeft?-.23f:.23f,.465f,.4f)),Display.transform.rotation);
            if(GroundPickup&&RaccoonArmedInteractionV52.HoldingObject(taskTime))
            {
                var grip=groundLeft?armedInteraction:unarmedInteraction;
                pickupGuide.transform.SetPositionAndRotation(grip.PalmPosition+grip.PalmRotation*Vector3.up*.035f,grip.PalmRotation);
            }
            if(Candidate&&ShowGuides&&SwimmingTask!=HandTask.None)
            {
                bool pickup=SwimmingTask==HandTask.Pickup;
                Display.WorkTarget.SetActive(!pickup);
                if(!pickup)
                {
                    Display.WorkTarget.transform.SetPositionAndRotation(SwimmingContactPoint-swimmingContactFrame*Vector3.up*.04f,swimmingContactFrame);
                    Display.WorkTarget.transform.localScale=new Vector3(.38f,.08f,.30f);
                }
                pickupGuide.SetActive(pickup&&swimmingTaskTime<1.48f);
                if(pickup)
                {
                    pickupGuide.transform.SetPositionAndRotation(SwimmingContactPoint-swimmingContactFrame*Vector3.up*.035f,swimmingContactFrame);
                    if(RaccoonArmedInteractionV52.HoldingObject(swimmingTaskTime))
                    {
                        var grip=swimmingTaskLeft?armedInteraction:unarmedInteraction;
                        pickupGuide.transform.SetPositionAndRotation(grip.PalmPosition+grip.PalmRotation*Vector3.up*.035f,grip.PalmRotation);
                    }
                }
            }
        }
        void Start()=>Initialize();
        void Update()
        {
            Initialize();var k=Keyboard.current;
            if(k!=null){if(k.fKey.wasPressedThisFrame){if(Equipment==Context.Machete)StartMacheteAttack();else if(Equipment==Context.Fishing)StartFishingMotion(RaccoonFishingReview.Motion.Land);else Fire();}if(k.rKey.wasPressedThisFrame)Reload();if(k.spaceKey.wasPressedThisFrame)Paused=!Paused;}
        }
        void LateUpdate(){if(!Paused)Step(Time.deltaTime*PlaybackSpeed);}
        GUIStyle label,button,title;
        Vector2 scroll;
        void OnGUI()
        {
            if(!ready)return;
            if(label==null){var font=Font.CreateDynamicFontFromOSFont("Malgun Gothic",15);label=new GUIStyle(GUI.skin.label){font=font,fontSize=15,wordWrap=true};button=new GUIStyle(GUI.skin.button){font=font,fontSize=14};title=new GUIStyle(label){fontSize=18,fontStyle=FontStyle.Bold};}
            GUI.Box(new Rect(12,12,342,Screen.height-24),GUIContent.none);
            GUILayout.BeginArea(new Rect(24,22,318,Screen.height-44));scroll=GUILayout.BeginScrollView(scroll);
            bool scarfFinish=RebuiltSkin!=null&&RebuiltSkin.name.Contains("ScarfFinish_v63");
            bool collarRepair=RebuiltSkin!=null&&RebuiltSkin.name.Contains("CollarRepair_v62");
            bool closedNeck=RebuiltSkin!=null&&RebuiltSkin.name.Contains("ClosedNeck_v61");
            GUILayout.Label(!string.IsNullOrEmpty(SkinReviewTitle)?SkinReviewTitle:scarfFinish?"전체 상태 · 스카프 마감 v63":collarRepair?"전체 상태 · 목 주변 정리 v62":closedNeck?"전체 상태 · 턱·목 마감 비교 v61":PreviousRebuiltSkin!=null?"전체 상태 · 턱 연결 비교 v60":RebuiltSkin!=null?"전체 상태 · 메시 비교 v59":"상태별 무장 동작 · 후보 v50",title);
            GUILayout.Label("원본 보존 · 검토 전용 / 실제 전투 아님",label);
            GUILayout.BeginHorizontal();if(GUILayout.Button(Paused?"▶ 재생":"Ⅱ 일시정지",button,GUILayout.Height(30)))Paused=!Paused;
            if(GUILayout.Button("처음부터 재생",button,GUILayout.Height(30))){RestartBody();Paused=false;}GUILayout.EndHorizontal();
            if(RebuiltSkin!=null&&AllowSkinComparison)
            {
                GUILayout.Label(ArmpitVolumeV73?"겨드랑이 볼륨 비교 · 같은 자세 유지":ContextAimV72?"상체 회전 비교 · 같은 시선 유지":ShoulderAimV71?"조준·옷 변형 비교 · 같은 시선 유지":"메시 연결 비교 · 같은 자세 유지",label);
                GUILayout.BeginHorizontal();
                string previousLabel=!string.IsNullOrEmpty(PreviousSkinLabel)?PreviousSkinLabel:scarfFinish?"수정 전 v62":collarRepair?"수정 전 v60":closedNeck?"수정 전 v60":PreviousRebuiltSkin!=null?"기존 연결 v59":"기존 연결";
                if(GUILayout.Button(previousLabel,button))SetSkinRevision(false);
                if(GUILayout.Button(!string.IsNullOrEmpty(RebuiltSkinLabel)?RebuiltSkinLabel:scarfFinish?"색상·경계 수정 v63":collarRepair?"목 주변 정리 v62":closedNeck?"턱·목 마감 v61":PreviousRebuiltSkin!=null?"턱 수정 v60":"수정 연결",button))SetSkinRevision(true);
                GUILayout.EndHorizontal();
                GUILayout.Label(UseRebuiltSkin&&!string.IsNullOrEmpty(RebuiltSkinLabel)?"현재: "+RebuiltSkinLabel:!UseRebuiltSkin&&!string.IsNullOrEmpty(PreviousSkinLabel)?"현재: "+PreviousSkinLabel:scarfFinish?(UseRebuiltSkin?"현재: 색상·경계 수정 v63":"현재: 수정 전 v62"):collarRepair?(UseRebuiltSkin?"현재: 목 주변 정리 v62":"현재: 수정 전 v60"):closedNeck?(UseRebuiltSkin?"현재: 턱·목 마감 v61":"현재: 수정 전 v60"):PreviousRebuiltSkin!=null?(UseRebuiltSkin?"현재: 턱 수정 v60":"현재: 기존 연결 v59"):(UseRebuiltSkin?"현재: 수정 연결 v59":"현재: 기존 연결 v58"),label);
            }
            else if(RebuiltSkin!=null)GUILayout.Label("현재: "+RebuiltSkinLabel,label);
            GUILayout.Space(8);GUILayout.Label("1. 상태 선택 · 누르면 재생",label);
            for(int row=0;row<3;row++){GUILayout.BeginHorizontal();for(int col=0;col<3;col++){int i=row*3+col;if(i>=StateChoices.Length)break;if(GUILayout.Button((State==StateChoices[i]?"● ":"")+StateNames[i],button,GUILayout.Height(30)))PreviewSelection(Equipment,StateChoices[i],Direction,Freedom);}GUILayout.EndHorizontal();}
            GUILayout.Space(8);GUILayout.Label("2. 장비",label);
            int equipmentCount=MachetePrefab!=null?EquipmentNames.Length:EquipmentNames.Length-1;
            for(int row=0;row<(equipmentCount+2)/3;row++){GUILayout.BeginHorizontal();for(int col=0;col<3;col++){int i=row*3+col;if(i>=equipmentCount)break;if(GUILayout.Button((Equipment==(Context)i?"● ":"")+EquipmentNames[i],button,GUILayout.Height(30)))PreviewSelection((Context)i,State,Direction,Freedom);}GUILayout.EndHorizontal();}
            GUILayout.Label("3. 이동",label);
            if(GroundLocomotion&&State==Situation.Ground)
            {
                GUILayout.BeginHorizontal();
                if(GUILayout.Button((!GroundRun?"● ":"")+"걷기",button)){GroundRun=false;Paused=false;}
                if(GUILayout.Button((GroundRun?"● ":"")+"정면 달리기",button)){GroundRun=true;PreviewSelection(Equipment,State,Motion.Forward,Freedom);}
                GUILayout.EndHorizontal();
            }
            for(int row=0;row<3;row++){GUILayout.BeginHorizontal();for(int col=0;col<3;col++){int i=row*3+col;if(i>=7)break;if(GUILayout.Button((Direction==(Motion)i?"● ":"")+MotionNames[i],button,GUILayout.Height(28)))PreviewSelection(Equipment,State,(Motion)i,Freedom);}GUILayout.EndHorizontal();}
            if(Restricted){GUILayout.Label("자유로운 손",label);GUILayout.BeginHorizontal();string[] n={"양손","오른손","제압됨"};for(int i=0;i<3;i++)if(GUILayout.Button((Freedom==(Hands)i?"● ":"")+n[i],button))PreviewSelection(Equipment,State,Direction,(Hands)i);GUILayout.EndHorizontal();}
            GUILayout.Space(8);GUILayout.Label("4. 동작 실행",label);
            if(Equipment==Context.Fishing&&fishing!=null)
            {
                GUI.enabled=CanFish;
                if(GUILayout.Button((fishing.Current==RaccoonFishingReview.Motion.Carry?"● ":"")+"낚시 전 · 낚싯대 들기",button,GUILayout.Height(32)))StartFishingMotion(RaccoonFishingReview.Motion.Carry);
                if(State!=Situation.Ground)GUILayout.Label("현재 상태: 휴대만 가능 · 낚시는 일반 상태에서",label);
                GUILayout.BeginHorizontal();
                if(GUILayout.Button("낚싯줄 던지기",button,GUILayout.Height(32)))StartFishingMotion(RaccoonFishingReview.Motion.Cast);
                if(GUILayout.Button("입질 대기",button,GUILayout.Height(32)))StartFishingMotion(RaccoonFishingReview.Motion.Wait);
                GUILayout.EndHorizontal();GUILayout.BeginHorizontal();
                if(GUILayout.Button("릴 감기 · 반복",button,GUILayout.Height(32)))StartFishingMotion(RaccoonFishingReview.Motion.Reel);
                if(GUILayout.Button("끌어올리기",button,GUILayout.Height(32)))StartFishingMotion(RaccoonFishingReview.Motion.Hook);
                GUILayout.EndHorizontal();
                if(GUILayout.Button("낚아 올리기 · 완료 [F]",button,GUILayout.Height(34)))StartFishingMotion(RaccoonFishingReview.Motion.Land);
                GUI.enabled=true;
            }
            if(Equipment==Context.Machete)
            {
                GUI.enabled=CanMacheteAttack;
                if(MacheteSlashV81||MacheteSlashV82)
                {
                    GUILayout.Label("마체테 v85 · 대각선 베기",label);
                    if(GUILayout.Button("마체테 v85",button,GUILayout.Height(34)))PreviewMacheteRevision(false);
                }
                if(GUILayout.Button("마체테 베기 [F]",button,GUILayout.Height(34)))StartMacheteAttack();
                GUI.enabled=true;
            }
            GUI.enabled=CanStartTask;GUILayout.BeginHorizontal();
            if(GUILayout.Button("상호작용",button,GUILayout.Height(34)))StartTask(HandTask.Interact);
            if(GUILayout.Button("줍기",button,GUILayout.Height(34)))StartTask(HandTask.Pickup);
            GUILayout.EndHorizontal();GUI.enabled=true;
            if(State==Situation.Ground||Swimming)
            {
                string environment=State==Situation.Ground?"일반":State==Situation.Surface?"수영":"잠수";
                GUILayout.Label(environment+" + "+(ActiveTask==HandTask.None?"동작 대기":ActiveTask==HandTask.Interact?"상호작용":"줍기"),label);
                if(State==Situation.Ground)
                {GUI.enabled=!InteractionBusy;TwoHandTask=GUILayout.Toggle(TwoHandTask,"양손 작업 / 큰 물건",button);GUI.enabled=true;}
            }
            else GUILayout.Label("상호작용·줍기: 일반 / 수영 / 잠수에서 실행",label);
            GUILayout.Space(8);GUILayout.BeginHorizontal();GUI.enabled=CanFire;if(GUILayout.Button("총기 발사 [F]",button,GUILayout.Height(34)))Fire();GUI.enabled=CanReload;if(GUILayout.Button("탄약 장전 [R]",button,GUILayout.Height(34)))Reload();GUI.enabled=true;GUILayout.EndHorizontal();
            if(IsGun)GUILayout.Label("탄약 "+Rounds+" / "+Capacity+(Reloading?" · 장전 "+Mathf.RoundToInt(ReloadProgress*100)+"%":""),label);
            if(Reloading&&GUILayout.Button("장전 중단",button))CancelReload();
            GUILayout.Label(Notice,label);
            GUILayout.Label("시선·조준 좌우",label);AimYaw=GUILayout.HorizontalSlider(AimYaw,-60,60);
            GUILayout.Label("시선·조준 상하",label);AimPitch=GUILayout.HorizontalSlider(AimPitch,-30,30);
            GUILayout.Space(8);
            GUILayout.Label("현재 상황과 무기 동작 결합",label);
            ShowGuides=GUILayout.Toggle(ShowGuides,"수면·사다리·접촉 기준 표시",button);
            GUILayout.Label("재생 속도 "+PlaybackSpeed.ToString("0.0")+"×",label);PlaybackSpeed=GUILayout.HorizontalSlider(PlaybackSpeed,.1f,1.5f);
            GUILayout.Label("우클릭 회전 · 휠 확대 · 가운데 버튼 이동\nF 발사 · R 장전 · Space 일시정지\n작살 장전: 손동작 후보 / 소총: 대체 모델",label);
            GUILayout.EndScrollView();GUILayout.EndArea();
        }
        void OnDestroy()
        {
            fishing?.Dispose();
            if(MacheteModel!=null)Destroy(MacheteModel);
            if(armpitVolume!=null)armpitVolume.Dispose();
            if(ladderSkin!=null)ladderSkin.Dispose();
            if(body!=null)Destroy(body.gameObject);if(weapon!=null)Destroy(weapon.gameObject);
            foreach(var g in new[]{supportGuide,pickupGuide})if(g!=null){Destroy(g.GetComponent<Renderer>().sharedMaterial);Destroy(g);}
        }
        void UpdateMachete()
        {
            if(MachetePrefab==null)return;
            if(MacheteModel==null){MacheteModel=Instantiate(MachetePrefab,transform);MacheteModel.name=MacheteHammerGripV75?"Machete v75":"Machete v74";}
            MacheteModel.SetActive(Equipment==Context.Machete);
            if(Equipment!=Context.Machete)return;
            if(Candidate&&ShouldStow())
            {
                var hips=outputAnimator.GetBoneTransform(HumanBodyBones.Hips);
                MacheteModel.transform.SetPositionAndRotation(hips.position+Display.transform.TransformVector(new Vector3(.29f,-.02f,-.16f)),Display.transform.rotation*Quaternion.Euler(175,0,-12));
            }
            else
            {
                RaccoonMachetePoseV74.Place(MacheteModel.transform,outputAnimator.GetBoneTransform(HumanBodyBones.RightHand),backwardHands);
            }
        }
    }
}



