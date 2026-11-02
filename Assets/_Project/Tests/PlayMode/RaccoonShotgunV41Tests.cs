using System;
using System.Linq;
using Game.Gameplay.Debugging;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;
namespace Game.Tests.PlayMode
{
    public sealed class RaccoonShotgunV41Tests
    {
#if UNITY_EDITOR
        GameObject root,floor;RaccoonActionMotorV36 motor;RaccoonActionReviewV36 actions;
        [SetUp]public void SetUp()
        {
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/PC_Raccoon_Actions_v36.prefab"));
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(50,1,50);
            motor=root.GetComponent<RaccoonActionMotorV36>();motor.enabled=false;actions=root.GetComponent<RaccoonActionReviewV36>();motor.ResetAt(new Vector3(1000,.03f,0));actions.SetContext(RaccoonActionReviewV36.ReviewContext.Shotgun);for(int i=0;i<60;i++)Step(1f/60);
        }
        [TearDown]public void TearDown(){Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        void Step(float dt){motor.Advance(dt,Vector2.zero,false);motor.Evaluate(dt);}
        void Select(string id)=>actions.Select(Array.FindIndex(actions.Library.Entries,e=>e.Id==id));
        Vector3 SupportInGun()=>actions.Weapon.transform.InverseTransformPoint(motor.Rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
        [TestCase(30,false)][TestCase(60,false)][TestCase(120,false)]
        [TestCase(30,true)][TestCase(60,true)][TestCase(120,true)]
        public void HitRecoveryIsContinuousInRaisedAndLoweredStates(int fps,bool lowered)
        {
            float dt=1f/fps;var a=motor.Rig.Animator;
            if(lowered){Select("Holster");for(int i=0;i<fps*2;i++)Step(dt);}
            var tracked=new[]{HumanBodyBones.Head,HumanBodyBones.Chest,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Select(a.GetBoneTransform).ToArray();
            var support=SupportInGun();
            foreach(string id in new[]{"HitFront","HitBack","HitLeft","HitRight"})
            {
                Quaternion neutral=tracked[1].rotation;float response=0;bool ended=false;
                Select(id);float duration=actions.Entry.Duration;
                for(int i=0;i<Mathf.CeilToInt((duration+.15f)*fps);i++)
                {
                    var p=tracked.Select(t=>t.position).ToArray();var q=tracked.Select(t=>t.rotation).ToArray();
                    Step(dt);response=Mathf.Max(response,Quaternion.Angle(neutral,tracked[1].rotation));
                    if(i*dt>duration*.70f)for(int b=0;b<tracked.Length;b++)
                    {
                        Assert.That(Vector3.Distance(p[b],tracked[b].position),Is.LessThan(.12f*dt),id+" recovery position: "+tracked[b].name);
                        Assert.That(Quaternion.Angle(q[b],tracked[b].rotation),Is.LessThan(45*dt),id+" recovery rotation: "+tracked[b].name);
                    }
                    Assert.That(Vector3.Distance(SupportInGun(),support)*actions.Weapon.transform.lossyScale.x,Is.LessThan(.008f),id+" grip");
                    ended|=!actions.Active;
                }
                Assert.That(response,Is.GreaterThan(4),id+" reaction must remain visible");
                Assert.That(ended,Is.True);Assert.That(actions.ShotgunLowered,Is.EqualTo(lowered));
                // Interrupting a reaction must retain the selected weapon stance too.
                foreach(float cancelAt in new[]{.05f,.2f,.7f})
                {
                Select(id);for(int i=0;i<Mathf.CeilToInt(cancelAt*fps);i++)Step(dt);
                actions.ReturnToContext();
                for(int i=0;i<Mathf.CeilToInt(.5f*fps);i++)
                {
                    var p=tracked.Select(t=>t.position).ToArray();var q=tracked.Select(t=>t.rotation).ToArray();Step(dt);
                    for(int b=0;b<tracked.Length;b++)
                    {
                        Assert.That(Vector3.Distance(p[b],tracked[b].position),Is.LessThan(.8f*dt),id+" interrupted position");
                        Assert.That(Quaternion.Angle(q[b],tracked[b].rotation),Is.LessThan(180*dt),id+" interrupted rotation");
                    }
                }
                Assert.That(actions.Active,Is.False);Assert.That(actions.ShotgunLowered,Is.EqualTo(lowered));
                }
            }
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void HitsPreserveRigidSupportAndReturnWithoutFootSliding(int fps)
        {
            float dt=1f/fps;var support=SupportInGun();var a=motor.Rig.Animator;
            foreach(string id in new[]{"HitFront","HitBack","HitLeft","HitRight"})
            {
                var foot=a.GetBoneTransform(HumanBodyBones.LeftFoot);Vector3 ground=foot.position;Select(id);
                for(int i=0;i<fps;i++)
                {
                    Step(dt);
                    Assert.That(Vector3.Distance(SupportInGun(),support)*actions.Weapon.transform.lossyScale.x,Is.LessThan(.008f),id+" support slips off weapon");
                    Assert.That(Vector3.Distance(foot.position,ground),Is.LessThan(.018f),id+" planted foot slides");
                    Assert.That(actions.Weapon.activeSelf,Is.True);
                }
                Assert.That(actions.Active,Is.False);
            }
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void LowerRaiseAndRespawnKeepWeaponVisibleAndBothHandsAttached(int fps)
        {
            float dt=1f/fps;var support=SupportInGun();
            foreach(string id in new[]{"Holster","Equip","Respawn"})
            {
                Select(id);float duration=actions.Entry.Duration;
                for(int i=0;i<Mathf.CeilToInt((duration+.3f)*fps);i++)
                {
                    Step(dt);Assert.That(actions.Weapon.activeSelf,Is.True,id+" weapon vanished");
                    Assert.That(Vector3.Distance(SupportInGun(),support)*actions.Weapon.transform.lossyScale.x,Is.LessThan(.008f),id+" support detached");
                }
                Assert.That(actions.Active,Is.False);Assert.That(actions.ShotgunLowered,Is.EqualTo(id=="Holster"));
                Assert.That(actions.CanUseShotgun,Is.EqualTo(id!="Holster"));
            }
        }
        [Test]public void DeathReleasesWeaponAndLeavesItAboveGround()
        {
            Select("ShotgunDeath");Step(.05f);Assert.That(actions.Weapon.activeSelf,Is.True);
            for(int i=0;i<180;i++)Step(1f/60);
            Assert.That(actions.Weapon.activeSelf,Is.False);Assert.That(actions.DroppedShotgunVisible,Is.True);
            var dropped=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Shotgun released review");
            float lowest=dropped.GetComponentsInChildren<Renderer>().Min(r=>r.bounds.min.y);
            Assert.That(lowest,Is.InRange(root.transform.position.y+.01f,root.transform.position.y+.15f));
            actions.Select(-1);Assert.That(actions.DroppedShotgunVisible,Is.False);
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void WalkStrafeBackRunAndJumpRetainSupportContact(int fps)
        {
            float dt=1f/fps;var support=SupportInGun();
            foreach(var input in new[]{Vector2.up,Vector2.left,Vector2.right,Vector2.down})
            for(int i=0;i<fps;i++)
            {
                motor.Advance(dt,input,false);motor.Evaluate(dt);
                Assert.That(Vector3.Distance(SupportInGun(),support)*actions.Weapon.transform.lossyScale.x,Is.LessThan(.012f),input.ToString());
            }
            for(int i=0;i<fps*3;i++)
            {
                motor.Advance(dt,Vector2.up,true,i==fps);motor.Evaluate(dt);
                Assert.That(Vector3.Distance(SupportInGun(),support)*actions.Weapon.transform.lossyScale.x,Is.LessThan(.012f),"run/jump support");
            }
        }
#endif
    }
}
