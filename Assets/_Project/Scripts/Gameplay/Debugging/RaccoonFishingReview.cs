using System;
using UnityEngine;
using Game.Gameplay.Player;

namespace Game.Gameplay.Debugging
{
    // Review motion authored for the user's Fishing model. The original clips stay intact.
    public sealed class RaccoonFishingReview
    {
        public enum Motion { Wait, Cast, Reel, Pull, Hook, Land, Carry }
        public Motion Current { get; private set; }=Motion.Carry;
        public GameObject Model { get; private set; }
        public float RightContactError { get; private set; }
        public float LeftContactError { get; private set; }
        public float RightWristAngle { get; private set; }
        public float LeftWristAngle { get; private set; }
        readonly Animator animator;
        readonly Transform root, reel, reelGrip, tip, spool, fishingFloat;
        public Transform Float=>fishingFloat;
        readonly RaccoonActionHandsV37 hands;
        readonly Quaternion rightInRod, leftPalmFrame;
        readonly Quaternion[] leftFingers=new Quaternion[15];
        readonly SkinnedMeshRenderer rodSkin;
        readonly int bendIndex;
        readonly LineRenderer line;
        readonly Material lineMaterial;
        float time, clock, crankAngle, blend=1;
        Vector3 oldGrip, grip;
        Quaternion oldRotation=Quaternion.identity, rodRotation=Quaternion.identity;
        float bend, oldBend;
        bool posed;
        Quaternion lineFacing;
        Vector3 landingHand, oldLeftPoint, lastLeftPoint, oldLineEnd, lastLineEnd;
        bool leftPosed, linePosed;
        Vector3 castReleasePoint;
        bool castReleased;
        // Guide centers and their exported Rod_Bend offsets, in rod-local space.
        static readonly Vector3[] Guides={
            new Vector3(.006146f,-.041023f,.484364f),
            new Vector3(.009235f,-.076489f,.738685f),
            new Vector3(.008854f,-.148726f,.958387f)};
        static readonly Vector3[] GuideBend={
            new Vector3(0,-.017513f,-.000908f),
            new Vector3(0,-.069987f,-.007247f),
            new Vector3(0,-.143477f,-.021268f)};

        public RaccoonFishingReview(GameObject prefab, Animator animator, Transform root, Transform parent)
        {
            this.animator=animator;this.root=root;hands=new RaccoonActionHandsV37(animator);
            Model=UnityEngine.Object.Instantiate(prefab,parent);Model.name="Fishing · 동작 검토";
            reel=Model.transform.Find("ReelHandle");reelGrip=reel.Find("ReelGrip");tip=Model.transform.Find("RodTip");
            spool=Model.transform.Find("ReelSpool");
            fishingFloat=Model.transform.Find("FishingFloat");
            var packedLine=Model.transform.Find("PackedFloatLine");
            if(packedLine!=null)packedLine.GetComponent<LineRenderer>().enabled=false;
            rodSkin=Model.GetComponentInChildren<SkinnedMeshRenderer>();
            bendIndex=rodSkin!=null?rodSkin.sharedMesh.GetBlendShapeIndex("Rod_Bend"):-1;
            if(bendIndex<0&&rodSkin!=null)
                for(int i=0;i<rodSkin.sharedMesh.blendShapeCount;i++)if(rodSkin.sharedMesh.GetBlendShapeName(i).EndsWith("Rod_Bend"))bendIndex=i;
            var right=animator.GetBoneTransform(HumanBodyBones.RightHand);
            rightInRod=Quaternion.Inverse(right.rotation)*hands.HammerRotation(right.rotation)*Quaternion.Euler(0,90,0);
            var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            var mid=animator.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
            leftPalmFrame=Quaternion.LookRotation(left.InverseTransformDirection(mid.position-left.position),left.InverseTransformDirection(hands.PalmNormal(left,true)));
            CacheLeftGrip();
            var lineObject=new GameObject("낚싯줄 · 연출");lineObject.transform.SetParent(Model.transform,false);
            line=lineObject.AddComponent<LineRenderer>();line.positionCount=16;line.startWidth=.0018f;line.endWidth=.0015f;line.useWorldSpace=true;
            var material=Model.GetComponentInChildren<Renderer>().sharedMaterial;
            lineMaterial=new Material(material);lineMaterial.name="Fishing line";lineMaterial.mainTexture=null;
            if(lineMaterial.HasProperty("_BaseMap"))lineMaterial.SetTexture("_BaseMap",null);
            if(lineMaterial.HasProperty("_BaseColor"))lineMaterial.SetColor("_BaseColor",new Color(.65f,.75f,.75f));
            line.sharedMaterial=lineMaterial;line.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void CacheLeftGrip()
        {
            var transforms=animator.GetComponentsInChildren<Transform>();
            var positions=new Vector3[transforms.Length];var rotations=new Quaternion[transforms.Length];
            for(int i=0;i<transforms.Length;i++){positions[i]=transforms[i].localPosition;rotations[i]=transforms[i].localRotation;}
            var handler=new HumanPoseHandler(animator.avatar,animator.transform);var pose=new HumanPose();handler.GetHumanPose(ref pose);
            for(int i=0;i<HumanTrait.MuscleCount;i++)
            {
                var n=HumanTrait.MuscleName[i];if(!n.StartsWith("Left"))continue;
                bool finger=n.Contains("Thumb")||n.Contains("Index")||n.Contains("Middle")||n.Contains("Ring")||n.Contains("Little");
                if(!finger)continue;
                pose.muscles[i]=n.Contains("Spread")?0:n.Contains("Thumb")?-.25f:n.Contains("1 Stretched")?-.30f:n.Contains("2 Stretched")?-.65f:-.45f;
            }
            handler.SetHumanPose(ref pose);
            for(int i=0;i<15;i++)leftFingers[i]=animator.GetBoneTransform((HumanBodyBones)((int)HumanBodyBones.LeftThumbProximal+i)).localRotation;
            for(int i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];}
            handler.Dispose();
        }

        public void Select(Motion motion,bool captureDisplayedPose=true)
        {
            oldGrip=grip;oldRotation=rodRotation;oldBend=bend;
            oldLeftPoint=lastLeftPoint;oldLineEnd=lastLineEnd;
            // Automatic completion runs after the body sampler has reset the arms.
            // Its live bones are not the previous fishing pose; use the cached contact.
            if(posed&&captureDisplayedPose)
            {
                oldLeftPoint=hands.PalmCenter(animator.GetBoneTransform(HumanBodyBones.LeftHand),true);
                leftPosed=true;
            }
            Current=motion;time=0;blend=0;
            castReleased=false;
        }
        static float S(float x)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(x));
        static float Curve(float t,float[] times,float[] values)
        {
            int i=0;while(i<times.Length-2&&t>times[i+1])i++;
            return Mathf.Lerp(values[i],values[i+1],S(Mathf.InverseLerp(times[i],times[i+1],t)));
        }
        public const float CastDuration=2.35f;
        static readonly float[] CastTimes={0,.36f,.72f,1.00f,1.30f,CastDuration};
        static readonly float[] CastPitch={-58,-82,-100,-38,-38,-58};
        static readonly float[] CastSide={.10f,.34f,.46f,.24f,.20f,.10f};
        static readonly float[] CastRoll={-35,-60,-75,-35,-35,-35};
        static readonly float[] CastYaw={0,35,50,8,0,0};
        static readonly float[] CastReach={.40f,.36f,.29f,.47f,.46f,.40f};
        static readonly float[] CastHeight={-.38f,-.18f,-.10f,-.25f,-.29f,-.38f};
        static readonly float[] CastTurn={0,-8,-14,12,7,0};
        static readonly float[] CastLean={0,-3,-7,8,5,0};
        static readonly float[] CastBend={5,22,42,92,20,5};
        // A brief give, decisive hook set, elastic rebound, then a controlled hold.
        // Keep both hands in the accepted grip throughout the strike.
        public const float HookDuration=1.95f;
        static readonly float[] HookTimes={0,.20f,.46f,.67f,1.08f,HookDuration};
        static readonly float[] HookPitch={-58,-45,-88,-77,-80,-58};
        static readonly float[] HookHeight={-.38f,-.40f,-.15f,-.22f,-.20f,-.38f};
        static readonly float[] HookReach={.40f,.40f,.37f,.39f,.38f,.40f};
        static readonly float[] HookSide={.10f,.12f,.18f,.16f,.17f,.10f};
        static readonly float[] HookRoll={-35,-35,-58,-50,-52,-35};
        static readonly float[] HookBend={6,12,96,55,68,6};
        static readonly float[] HookLean={0,3,-10,-6,-7,0};
        // Lift the catch clear, transfer the free hand from the reel to the line,
        // then hold the completed pose until the reviewer selects another action.
        public const float LandDuration=3.1f;
        static readonly float[] LandTimes={0,.42f,1.05f,1.75f,2.35f,LandDuration};
        static readonly float[] LandPitch={-58,-77,-100,-84,-82,-82};
        static readonly float[] LandHeight={-.38f,-.24f,-.08f,-.09f,-.12f,-.12f};
        static readonly float[] LandReach={.40f,.37f,.29f,.35f,.36f,.36f};
        static readonly float[] LandSide={.10f,.18f,.46f,.36f,.34f,.34f};
        static readonly float[] LandRoll={-35,-52,-75,-63,-60,-60};
        static readonly float[] LandYaw={0,0,40,12,8,8};
        static readonly float[] LandTurn={0,-4,-12,-5,0,0};
        static readonly float[] LandLean={0,-5,-8,-4,-2,-2};
        static readonly float[] LandBend={6,90,72,24,8,8};

        public void Apply(float dt,Quaternion facing,bool active,bool animate)
        {
            Model.SetActive(active);if(!active){posed=false;leftPosed=false;linePosed=false;Current=Motion.Carry;time=0;blend=1;return;}
            line.enabled=true;
            if(animate){time+=dt;clock+=dt;blend=Mathf.Min(1,blend+dt/.25f);}
            if(Current==Motion.Land)time=Mathf.Min(time,LandDuration);
            if((Current==Motion.Cast&&time>=CastDuration)||(Current==Motion.Pull&&time>=1.8f)||
                (Current==Motion.Hook&&time>=HookDuration))Select(Motion.Wait,false);
            float pitch=-58,reach=.40f,height=-.38f,turn=0,side=.10f,yaw=0,roll=-35,lean=0;
            float bob=Mathf.Sin(clock*Mathf.PI*2/2.8f);
            bend=6+2*bob;pitch+=bob; height+=.004f*bob;
            if(animate&&Current==Motion.Cast)
            {
                pitch=Curve(time,CastTimes,CastPitch);reach=Curve(time,CastTimes,CastReach);
                height=Curve(time,CastTimes,CastHeight);turn=Curve(time,CastTimes,CastTurn);bend=Curve(time,CastTimes,CastBend);
                side=Curve(time,CastTimes,CastSide);yaw=Curve(time,CastTimes,CastYaw);
                roll=Curve(time,CastTimes,CastRoll);
                lean=Curve(time,CastTimes,CastLean);
            }
            else if(animate&&Current==Motion.Reel)
            {
                crankAngle+=dt*360/1.1f;
                pitch=-60+2*Mathf.Sin(time*Mathf.PI*2/2.2f);bend=28+7*Mathf.Sin(time*Mathf.PI*2/1.1f);
            }
            else if(animate&&Current==Motion.Pull)
            {
                float e=time<.4f?S(time/.4f):1-S((time-.4f)/1.4f);
                pitch-=14*e;height+=.06f*e;reach-=.03f*e;bend+=55*e;
            }
            else if(animate&&Current==Motion.Hook)
            {
                pitch=Curve(time,HookTimes,HookPitch);height=Curve(time,HookTimes,HookHeight);
                reach=Curve(time,HookTimes,HookReach);roll=Curve(time,HookTimes,HookRoll);
                bend=Curve(time,HookTimes,HookBend);lean=Curve(time,HookTimes,HookLean);
                side=Curve(time,HookTimes,HookSide);
            }
            else if(animate&&Current==Motion.Land)
            {
                pitch=Curve(time,LandTimes,LandPitch);height=Curve(time,LandTimes,LandHeight);
                reach=Curve(time,LandTimes,LandReach);side=Curve(time,LandTimes,LandSide);
                roll=Curve(time,LandTimes,LandRoll);yaw=Curve(time,LandTimes,LandYaw);
                turn=Curve(time,LandTimes,LandTurn);lean=Curve(time,LandTimes,LandLean);
                bend=Curve(time,LandTimes,LandBend);
            }
            var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest);
            var head=animator.GetBoneTransform(HumanBodyBones.Head);var gaze=head.rotation;
            var spine=animator.GetBoneTransform(HumanBodyBones.Spine);
            // Share the effort through the torso while keeping the feet planted.
            spine.rotation=Quaternion.AngleAxis(lean*.4f,facing*Vector3.right)*spine.rotation;
            spine.rotation=Quaternion.AngleAxis(turn*.4f,facing*Vector3.up)*spine.rotation;
            chest.rotation=Quaternion.AngleAxis(lean*.6f,facing*Vector3.right)*chest.rotation;
            chest.rotation=Quaternion.AngleAxis(turn*.6f,facing*Vector3.up)*chest.rotation;
            if(Current==Motion.Hook||Current==Motion.Cast||Current==Motion.Land)head.rotation=gaze;
            grip=chest.position+facing*new Vector3(side,height,reach);
            // The grip stays ahead of the abdomen. Rolling the shaft in the palm
            // lets the knuckles continue the forearm instead of folding back on it.
            rodRotation=facing*Quaternion.Euler(pitch,yaw,roll);
            if(posed&&blend<1){float w=S(blend);grip=Vector3.Lerp(oldGrip,grip,w);rodRotation=Quaternion.Slerp(oldRotation,rodRotation,w);bend=Mathf.Lerp(oldBend,bend,w);}
            posed=true;
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);var rotation=rodRotation*Quaternion.Inverse(rightInRod);
            hand.rotation=rotation;
            Vector3 palmOffset=hands.HammerGripCenter(hand,.008f)-hand.position;
            Model.transform.SetPositionAndRotation(grip,rodRotation);
            reel.localRotation=Quaternion.AngleAxis(crankAngle,Vector3.right);
            if(spool!=null)spool.localRotation=Quaternion.AngleAxis(crankAngle,Vector3.right);
            if(rodSkin!=null&&bendIndex>=0)rodSkin.SetBlendShapeWeight(bendIndex,bend);
            Vector3 leftPoint=reelGrip.position;
            float release=animate&&Current==Motion.Cast?S(time/.42f)*(1-S((time-1.30f)/.80f)):0;
            Vector3 freeLeft=chest.position+facing*new Vector3(-.25f,-.30f,.35f);
            landingHand=chest.position+facing*new Vector3(-.24f,-.12f,.40f);
            if(animate&&Current==Motion.Land)
            {
                release=S((time-.45f)/1.25f);
                freeLeft=landingHand;
            }
            leftPoint=Vector3.Lerp(leftPoint,freeLeft,release);
            var left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            // Do not pull the shared prop back into the torso to satisfy the left
            // hand. Fit that hand's orientation to its forearm and the reel instead.
            Solve(false,grip-palmOffset,rotation,facing);RaccoonHammerGripV44.Apply(animator);
            Model.transform.SetPositionAndRotation(hands.HammerGripCenter(hand,.008f),rodRotation);
            RightContactError=Vector3.Distance(Model.transform.position,grip);
            leftPoint=Vector3.Lerp(reelGrip.position,freeLeft,release);
            if(leftPosed&&blend<1)leftPoint=Vector3.Lerp(oldLeftPoint,leftPoint,S(blend));
            lastLeftPoint=leftPoint;leftPosed=true;
            SolveReelHand(leftPoint,rodRotation,facing);
            for(int i=0;i<15;i++)
            {
                var finger=animator.GetBoneTransform((HumanBodyBones)((int)HumanBodyBones.LeftThumbProximal+i));
                float open=Current==Motion.Land?release*(1-S((time-1.55f)/.55f)):release;
                finger.localRotation=Quaternion.Slerp(leftFingers[i],finger.localRotation,open);
            }
            LeftContactError=Vector3.Distance(hands.PalmCenter(left,true),leftPoint);
            RightWristAngle=WristAngle(false);LeftWristAngle=WristAngle(true);
            lineFacing=facing;UpdateLine(facing);
        }

        public void ApplyCarry(float dt,Quaternion facing,RaccoonContextActionsV50.Situation state,float phase)
        {
            if(Current!=Motion.Carry)Select(Motion.Carry);
            Model.SetActive(true);line.enabled=true;leftPosed=false;
            clock+=dt;blend=Mathf.Min(1,blend+dt/.25f);
            float side=.32f,height=-.47f,reach=.26f,pitch=-68,roll=-35,yaw=8;
            var chest=animator.GetBoneTransform(HumanBodyBones.UpperChest);
            Vector3 shoulderRight=animator.GetBoneTransform(HumanBodyBones.RightShoulder).position-animator.GetBoneTransform(HumanBodyBones.LeftShoulder).position;
            Vector3 carryForward=Vector3.Cross(shoulderRight,root.up).normalized;
            Quaternion frame=carryForward.sqrMagnitude>.01f?Quaternion.LookRotation(carryForward,root.up):root.rotation;
            if(state==RaccoonContextActionsV50.Situation.Surface||state==RaccoonContextActionsV50.Situation.Underwater)
            {
                // Carry with the swimming torso instead of leaving the arm upright.
                var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                Vector3 up=(chest.position-hips.position).normalized;
                Vector3 forward=Vector3.Cross(shoulderRight,up).normalized;
                if(forward.sqrMagnitude>.01f)frame=Quaternion.LookRotation(forward,up);
                height=-.22f;reach=.33f;pitch=-18;roll=-30;yaw=4;
                if(state==RaccoonContextActionsV50.Situation.Underwater){height=-.18f;reach=.36f;pitch=-10;}
                height+=.012f*Mathf.Sin(phase*Mathf.PI*2);
            }
            else if(state==RaccoonContextActionsV50.Situation.Ladder)
            {side=.36f;height=-.32f;reach=.22f;pitch=-80;roll=-50;}
            else if(state==RaccoonContextActionsV50.Situation.Grabbed)
            {side=.34f;height=-.30f;reach=.25f;pitch=-55;}
            else if(state==RaccoonContextActionsV50.Situation.Carried)
            {side=.34f;height=-.24f;reach=.30f;pitch=-38;roll=-30;}
            grip=chest.position+frame*new Vector3(side,height,reach);
            rodRotation=frame*Quaternion.Euler(pitch,yaw,roll);bend=0;
            if(posed&&blend<1)
            {float w=S(blend);grip=Vector3.Lerp(oldGrip,grip,w);rodRotation=Quaternion.Slerp(oldRotation,rodRotation,w);bend=Mathf.Lerp(oldBend,0,w);}
            posed=true;
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            var rotation=rodRotation*Quaternion.Inverse(rightInRod);
            RaccoonHammerGripV44.Apply(animator);
            var forearm=animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
            var knuckle=animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            // A carried rod may settle in the palm. Keep its grip fixed, but allow
            // its angle to follow the forearm instead of folding the wrist to aim it.
            for(int pass=0;pass<8;pass++)
            {
                hand.rotation=rotation;
                Vector3 offset=hands.HammerGripCenter(hand,.008f)-hand.position;
                Solve(false,grip-offset,rotation,frame);
                Vector3 fingers=(knuckle.position-hand.position).normalized;
                Vector3 arm=(hand.position-forearm.position).normalized;
                float excess=Mathf.Max(0,Vector3.Angle(fingers,arm)-22);
                if(pass<7&&excess>.01f)
                    rotation=Quaternion.FromToRotation(fingers,Vector3.RotateTowards(fingers,arm,excess*Mathf.Deg2Rad,0))*rotation;
            }
            rodRotation=rotation*rightInRod;
            Model.transform.SetPositionAndRotation(hands.HammerGripCenter(hand,.008f),rodRotation);
            if(rodSkin!=null&&bendIndex>=0)rodSkin.SetBlendShapeWeight(bendIndex,bend);
            RightContactError=Vector3.Distance(Model.transform.position,grip);LeftContactError=0;
            RightWristAngle=WristAngle(false);LeftWristAngle=WristAngle(true);
            // The body animation exclusively owns the free hand: walking, swimming,
            // ladder contacts and interactions must never inherit a reel grip.
            lineFacing=facing;
        }

        void Solve(bool left,Vector3 position,Quaternion rotation,Quaternion facing)
        {
            var upper=animator.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
            var lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            // Keep elbows outside the torso, rather than folding them beneath
            // a high wrist and forcing the wrist to turn backwards.
            RaccoonEquipmentPose.SolveArm(upper,lower,hand,position,upper.position+facing*new Vector3(left?-.5f:.5f,-.25f,0));hand.rotation=rotation;
        }
        void SolveReelHand(Vector3 target,Quaternion rod,Quaternion facing)
        {
            var lower=animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            var hand=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            for(int pass=0;pass<8;pass++)
            {
                Vector3 axis=(hand.position-lower.position).normalized;
                Vector3 normal=Vector3.ProjectOnPlane(rod*Vector3.right,axis).normalized;
                if(normal.sqrMagnitude<.001f)normal=Vector3.ProjectOnPlane(facing*Vector3.up,axis).normalized;
                Quaternion rotation=Quaternion.LookRotation(axis,normal)*Quaternion.Inverse(leftPalmFrame);
                hand.rotation=rotation;
                Vector3 offset=hands.PalmCenter(hand,true)-hand.position;
                Solve(true,target-offset,rotation,facing);
            }
        }
        float WristAngle(bool left)
        {
            var lower=animator.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
            var hand=animator.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
            var knuckle=animator.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
            return Vector3.Angle(hand.position-lower.position,knuckle.position-hand.position);
        }
        void UpdateLine(Quaternion facing)
        {
            var model=Model.transform;
            float weight=bend/100;
            // A fixed tangent exit keeps the free line from orbiting with the spool.
            line.SetPosition(0,model.TransformPoint(new Vector3(-.06542f,-.00169865f,.23775053f)));
            for(int i=0;i<Guides.Length;i++)
                line.SetPosition(i+1,model.TransformPoint(Guides[i]+GuideBend[i]*weight));
            Vector3 start=tip.position+model.TransformVector(new Vector3(0,-.17397f,-.028395f)*weight);
            Vector3 waterEnd=root.position+facing*new Vector3(.12f,.12f,3);
            waterEnd.y+=.009f*Mathf.Sin(clock*2.5f);
            Vector3 hangDirection=(Vector3.down+root.right*(.07f*Mathf.Sin(clock*2.9f))+root.forward*(.05f*Mathf.Sin(clock*2.3f))).normalized;
            Vector3 end=waterEnd;
            if(Current==Motion.Carry)end=start+hangDirection*.28f;
            if(Current==Motion.Cast)
            {
                // Keep the float beside the tip during the wind-up, then send it
                // along one continuous cast arc instead of teleporting it outward.
                if(time<.82f)end=start+hangDirection*.28f;
                else
                {
                    if(!castReleased){castReleasePoint=start+hangDirection*.28f;castReleased=true;}
                    float flight=Mathf.Clamp01((time-.82f)/.80f);
                    end=Vector3.Lerp(castReleasePoint,waterEnd,flight)+Vector3.up*(Mathf.Sin(flight*Mathf.PI)*.45f);
                }
            }
            if(Current==Motion.Hook||Current==Motion.Pull)
            {
                float tug=Current==Motion.Hook?S((time-.20f)/.26f)*(1-S((time-1.08f)/.87f)):
                    S(time/.4f)*(1-S((time-.4f)/1.4f));
                end+=(Vector3.up*.12f-facing*Vector3.forward*.20f)*tug;
            }
            if(Current==Motion.Land)
            {
                float lift=S((time-.35f)/1.45f);
                Vector3 landed=landingHand-Vector3.up*.18f;
                end=Vector3.Lerp(end,landed,lift);
                end+=facing*Vector3.right*(Mathf.Sin(lift*Mathf.PI)*.22f);
            }
            if(linePosed&&blend<1)end=Vector3.Lerp(oldLineEnd,end,S(blend));
            lastLineEnd=end;linePosed=true;
            float sag=Current==Motion.Carry?0:Current==Motion.Hook?Mathf.Lerp(.12f,.018f,S(time/.40f)*(1-S((time-1.08f)/.87f))):.12f;
            for(int i=4;i<line.positionCount;i++)
            {
                float t=(i-4)/(float)(line.positionCount-5);
                Vector3 point=Vector3.Lerp(start,end,t)-Vector3.up*Mathf.Sin(t*Mathf.PI)*sag;
                if(Current==Motion.Land)
                {
                    float hold=S((time-1.55f)/.55f);
                    const float graspT=9f/11f;
                    Vector3 held=t<graspT?Vector3.Lerp(start,landingHand,t/graspT):Vector3.Lerp(landingHand,end,(t-graspT)/(1-graspT));
                    point=Vector3.Lerp(point,held,hold);
                }
                line.SetPosition(i,point);
            }
            if(fishingFloat!=null)
            {
                // The mesh origin is its upper line eye, so the string terminates
                // exactly on the attachment in carry, flight and the landed pose.
                Vector3 up=Current==Motion.Carry?-hangDirection:Vector3.up;
                if(Current==Motion.Cast&&time>=.82f&&time<1.62f)
                    up=Vector3.RotateTowards(Vector3.up,(start-end).normalized,35*Mathf.Deg2Rad,0);
                fishingFloat.SetPositionAndRotation(end,Quaternion.FromToRotation(Vector3.up,up));
            }
        }
        public static void UpdatePackedFloat(Transform model)
        {
            var floatObject=model.Find("FishingFloat");var rodTip=model.Find("RodTip");
            var lineObject=model.Find("PackedFloatLine");
            if(floatObject==null||rodTip==null||lineObject==null)return;
            floatObject.SetPositionAndRotation(rodTip.position-Vector3.up*.28f,Quaternion.identity);
            var packedLine=lineObject.GetComponent<LineRenderer>();
            packedLine.enabled=true;packedLine.useWorldSpace=true;
            packedLine.SetPosition(0,rodTip.position);packedLine.SetPosition(1,floatObject.position);
        }
        public void Dispose(){if(Model!=null)UnityEngine.Object.Destroy(Model);if(lineMaterial!=null)UnityEngine.Object.Destroy(lineMaterial);}
        public void FollowHand()
        {
            var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
            Model.transform.SetPositionAndRotation(hands.HammerGripCenter(hand,.008f),hand.rotation*rightInRod);
            if(line.enabled)UpdateLine(lineFacing);
        }
    }
}
