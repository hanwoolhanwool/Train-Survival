using Game.Gameplay.Debugging;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonCombatV33Tests
    {
#if UNITY_EDITOR
        private const string Path="Assets/_Project/Art/Characters/Raccoon/CombatCandidate_v33/PC_Raccoon_Combat_v33.prefab";
        [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,1)]
        [TestCase(30,2)] [TestCase(60,2)] [TestCase(120,2)]
        public void FireReloadAndReturnStayContinuous(int fps,int rounds)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(40,1,40);
            try
            {
                var lab=go.GetComponent<RaccoonCombatMotorV33>();lab.enabled=false;lab.ResetAt(new Vector3(1000,.03f,0));
                var combat=lab.Combat;var a=lab.Rig.Animator;float dt=1f/fps;
                Vector2 v=rounds==1?Vector2.zero:Vector2.left;
                for(int i=0;i<fps;i++){lab.Advance(dt,v,false);lab.Evaluate(dt);}
                Assert.That(combat.Reload(),Is.False,"Full magazine should not reload");
                Assert.That(combat.Fire(),Is.True);Assert.That(combat.Fire(),Is.False,"Fire interval");Assert.That(combat.Reload(),Is.False,"Recoil must finish first");
                float grip=0,reach=0,maxHandSpeed=0,maxHeadRate=0;int shellPulses=0;bool shellBefore=false;
                var left=a.GetBoneTransform(HumanBodyBones.LeftHand);var right=a.GetBoneTransform(HumanBodyBones.RightHand);var head=a.GetBoneTransform(HumanBodyBones.Head);
                Vector3 last=go.transform.InverseTransformPoint(left.position);Quaternion lastHead=head.rotation;
                for(int i=0;i<fps*2;i++)
                {
                    if(rounds==2&&i==fps)Assert.That(combat.Fire(),Is.True);
                    lab.Advance(dt,v,false);lab.Evaluate(dt);grip=Mathf.Max(grip,combat.GripError);reach=Mathf.Max(reach,combat.ReachError);
                }
                Assert.That(combat.Rounds,Is.EqualTo(2-rounds));Assert.That(combat.Reload(),Is.True);Assert.That(combat.Fire(),Is.False,"No firing while loading");
                last=go.transform.InverseTransformPoint(left.position);lastHead=head.rotation;
                for(int i=0;i<fps*4;i++)
                {
                    lab.Advance(dt,v,false);lab.Evaluate(dt);
                    var p=go.transform.InverseTransformPoint(left.position);maxHandSpeed=Mathf.Max(maxHandSpeed,(p-last).magnitude/dt);last=p;
                    maxHeadRate=Mathf.Max(maxHeadRate,Quaternion.Angle(lastHead,head.rotation)/dt);lastHead=head.rotation;
                    reach=Mathf.Max(reach,combat.ReachError);
                    bool visible=combat.Shell.gameObject.activeSelf;if(visible&&!shellBefore)shellPulses++;shellBefore=visible;
                    Assert.That(float.IsFinite(left.position.x)&&float.IsFinite(right.position.x),Is.True);
                    if(i*dt<2.75f)Assert.That(combat.Rounds,Is.EqualTo(2-rounds),"Rounds appear only after reload completes");
                }
                Assert.That(combat.Reloading,Is.False);Assert.That(combat.Rounds,Is.EqualTo(2));Assert.That(combat.Shots,Is.EqualTo(rounds));
                Assert.That(shellPulses,Is.EqualTo(rounds),"One shell visual per missing round");
                Assert.That(grip,Is.LessThan(.003f));Assert.That(reach,Is.LessThan(.005f));
                Assert.That(maxHandSpeed,Is.LessThan(3f),"Support hand path should not teleport");Assert.That(maxHeadRate,Is.LessThan(180));
                Debug.Log($"COMBAT_V33 fps={fps} rounds={rounds} grip={grip} reach={reach} handSpeed={maxHandSpeed} headRate={maxHeadRate} shells={shellPulses}");
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(floor);}
        }
        [Test]
        public void CombatCanOverlapJumpAndRunning()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,15);floor.transform.localScale=new Vector3(40,1,60);
            try
            {
                var lab=go.GetComponent<RaccoonCombatMotorV33>();lab.enabled=false;lab.ResetAt(new Vector3(1000,.03f,0));float reach=0,grip=0;
                for(int i=0;i<420;i++)
                {
                    if(i==75||i==180)Assert.That(lab.Combat.Fire(),Is.True);
                    if(i==240)Assert.That(lab.Combat.Reload(),Is.True);
                    lab.Advance(1f/60,Vector2.up,true,i==60);lab.Evaluate(1f/60);
                    reach=Mathf.Max(reach,lab.Combat.ReachError);
                    if(!lab.Combat.Reloading)grip=Mathf.Max(grip,lab.Combat.GripError);
                }
                Assert.That(lab.LaunchCount,Is.EqualTo(1));Assert.That(lab.LandingCount,Is.EqualTo(1));
                Assert.That(lab.Combat.Rounds,Is.EqualTo(2));Assert.That(lab.Combat.Reloading,Is.False);
                Assert.That(reach,Is.LessThan(.005f));Assert.That(grip,Is.LessThan(.003f));
                Debug.Log($"COMBAT_V33_OVERLAP grip={grip} reach={reach}");
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(floor);}
        }
        [Test]
        public void NoCombatPreservesGroundAndJump()
        {
            var current=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            var original=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/JumpCandidate_v32/PC_Raccoon_Jump_v32.prefab"));
            try
            {
                current.GetComponent<RaccoonCombatMotorV33>().enabled=false;original.GetComponent<RaccoonJumpMotorV32>().enabled=false;
                var a=current.GetComponentInChildren<RaccoonCombatRigV33>();var b=original.GetComponentInChildren<RaccoonJumpRigV32>();
                a.Initialize(current.transform);b.Initialize(original.transform);float peak=0;
                for(int i=0;i<600;i++)
                {
                    Vector2 v=i<120?Vector2.zero:i<240?Vector2.left*.45f:i<360?Vector2.down*.55f:i<480?Vector2.up*2.5f:Vector2.up*.8f;
                    float jump=i>=250&&i<310?Mathf.Sin((i-250)/60f*Mathf.PI):0;
                    a.Tick(1f/60,v,v.y>1,false,jump,2);b.Tick(1f/60,v,v.y>1,false,jump,2);
                    foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand})
                        peak=Mathf.Max(peak,Vector3.Distance(a.Animator.GetBoneTransform(bone).position,b.Animator.GetBoneTransform(bone).position));
                }
                Assert.That(peak,Is.LessThan(.0001f));Debug.Log("COMBAT_V33_PRESERVED maxDifference="+peak);
            }
            finally{Object.DestroyImmediate(current);Object.DestroyImmediate(original);}
        }
#endif
    }
}
