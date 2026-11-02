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
    public sealed class RaccoonRevolverV45Tests
    {
#if UNITY_EDITOR
        GameObject root,floor;RaccoonActionMotorV36 motor;RaccoonActionReviewV36 review;Animator a;
        [SetUp]public void Setup()
        {
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/PC_Raccoon_Actions_v36.prefab"));
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            motor=root.GetComponent<RaccoonActionMotorV36>();motor.enabled=false;motor.ResetAt(new Vector3(1000,.03f,0));
            review=root.GetComponent<RaccoonActionReviewV36>();review.SetContext(RaccoonActionReviewV36.ReviewContext.Revolver);a=motor.Rig.Animator;
            for(int i=0;i<30;i++)Step(1f/60);
        }
        [TearDown]public void Cleanup(){Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        void Step(float dt){motor.Advance(dt,Vector2.zero,false);motor.Evaluate(dt);}
        Transform[] Digits()=>Enumerable.Range((int)HumanBodyBones.RightThumbProximal,15).Select(i=>a.GetBoneTransform((HumanBodyBones)i)).ToArray();
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void FireReloadAndCancellationKeepGripAndRecoverIndex(int fps)
        {
            var digits=Digits();var idle=digits.Select(t=>t.localRotation).ToArray();
            var gun=review.Props.Single(p=>p.name=="Revolver").transform;
            var socket=gun.localPosition;var rotation=gun.localRotation;var mesh=gun.GetComponent<MeshFilter>().sharedMesh;
            float dt=1f/fps;
            foreach(var id in new[]{"RevolverFire","RevolverReload"})
            foreach(bool cancel in new[]{false,true})
            {
                review.Select(Array.FindIndex(review.Library.Entries,e=>e.Id==id));
                float duration=review.Entry.Duration,maxCurl=0;
                var last=digits.Select(t=>t.localRotation).ToArray();
                for(int frame=0;frame<Mathf.CeilToInt((duration+.5f)*fps);frame++)
                {
                    if(cancel&&frame==Mathf.CeilToInt(.12f*fps))review.ReturnToContext();
                    Step(dt);
                    if(id=="RevolverReload")Assert.That(Vector3.Distance(
                        review.Hands.PalmCenter(a.GetBoneTransform(HumanBodyBones.LeftHand),true),
                        review.Hands.PalmCenter(a.GetBoneTransform(HumanBodyBones.RightHand),false)),Is.GreaterThan(.12f),"Reload hands overlap");
                    Assert.That(Vector3.Distance(gun.localPosition,socket),Is.LessThan(.00001f),"Weapon slides in hand");
                    Assert.That(Quaternion.Angle(gun.localRotation,rotation),Is.LessThan(.1f));
                    Assert.That(gun.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
                    for(int i=0;i<15;i++)
                    {
                        float change=Quaternion.Angle(digits[i].localRotation,idle[i]);
                        if(i<3||i>5||id=="RevolverReload")Assert.That(change,Is.LessThan(.1f),"Supporting fingers changed");
                        else maxCurl=Mathf.Max(maxCurl,change);
                        Assert.That(Quaternion.Angle(last[i],digits[i].localRotation),Is.LessThan(1600*dt),"Finger jumps between frames");
                        last[i]=digits[i].localRotation;
                    }
                }
                if(id=="RevolverFire")Assert.That(maxCurl,Is.GreaterThan(15),"Index never reaches trigger pose");
                Assert.That(review.Active,Is.False);
                for(int i=0;i<15;i++)Assert.That(Quaternion.Angle(digits[i].localRotation,idle[i]),Is.LessThan(.1f),"Index did not recover");
            }
        }
        [Test]public void ComparisonPreservesTimeUntouchedFingersAndWeaponOffset()
        {
            var digits=Digits();var gun=review.Props.Single(p=>p.name=="Revolver").transform;
            review.Select(Array.FindIndex(review.Library.Entries,e=>e.Id=="RevolverFire"));
            for(int frame=0;frame<8;frame++)Step(1f/60);
            float time=review.Elapsed;int selection=review.Selected;
            root.transform.rotation=Quaternion.Euler(0,37,0);
            review.CompareRevolver(RaccoonActionReviewV36.RevolverComparison.Original);motor.Evaluate(1f/60);
            var original=digits.Select(t=>t.localRotation).ToArray();
            var originalSocket=gun.localPosition;
            review.CompareRevolver(RaccoonActionReviewV36.RevolverComparison.Current);motor.Evaluate(1f/60);
            var current=digits.Select(t=>t.localRotation).ToArray();var before=gun.position;var angle=gun.rotation;
            var scale=gun.localScale;var mesh=gun.GetComponent<MeshFilter>().sharedMesh;
            review.CompareRevolver(RaccoonActionReviewV36.RevolverComparison.LeftShift);motor.Evaluate(1f/60);
            Assert.That(Vector3.Distance(gun.position-before,-root.transform.right*.012f),Is.LessThan(.0001f),"Left is player-relative, including when turned");
            Assert.That(Quaternion.Angle(gun.rotation,angle),Is.LessThan(.1f));
            Assert.That(gun.localScale,Is.EqualTo(scale));Assert.That(gun.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
            for(int i=0;i<15;i++)if(i<3||i>=6&&i!=9&&i!=12)
                Assert.That(Quaternion.Angle(digits[i].localRotation,current[i]),Is.LessThan(.1f),"C changed an untouched joint");
            review.CompareRevolver(RaccoonActionReviewV36.RevolverComparison.Original);motor.Evaluate(1f/60);
            Assert.That(Vector3.Distance(gun.localPosition,originalSocket),Is.LessThan(.00001f));
            for(int i=0;i<15;i++)Assert.That(Quaternion.Angle(digits[i].localRotation,original[i]),Is.LessThan(.1f),"Original fingers must be restored");
            review.CompareRevolver(RaccoonActionReviewV36.RevolverComparison.Current);motor.Evaluate(1f/60);
            Assert.That(Vector3.Distance(gun.position,before),Is.LessThan(.0001f));
            for(int i=0;i<15;i++)Assert.That(Quaternion.Angle(digits[i].localRotation,current[i]),Is.LessThan(.1f));
            Assert.That(review.Elapsed,Is.EqualTo(time));Assert.That(review.Selected,Is.EqualTo(selection));
        }
        [Test]public void RefinedIndexFollowsBarrelAndLowerFingersMoveOnlySlightly()
        {
            var digits=Digits();
            review.CompareRevolver(RaccoonActionReviewV36.RevolverComparison.Current);motor.Evaluate(1f/60);
            var ring=digits[11].position;var little=digits[14].position;
            review.CompareRevolver(RaccoonActionReviewV36.RevolverComparison.LeftShift);motor.Evaluate(1f/60);
            Assert.That(Vector3.Angle(digits[4].position-digits[3].position,root.transform.forward),Is.LessThan(32),"Index knuckle still hooks sideways");
            Assert.That(Vector3.Angle(digits[5].position-digits[4].position,root.transform.forward),Is.LessThan(12),"Index does not follow the barrel");
            foreach(var delta in new[]{digits[11].position-ring,digits[14].position-little})
                Assert.That(Vector3.Dot(delta,root.transform.up),Is.InRange(-.015f,-.002f),"Lower fingers should move down only a few millimetres");
        }
        [Test]public void SwitchingBackRestoresHammerAndUnarmedHands()
        {
            var digits=Digits();
            foreach(var context in new[]{RaccoonActionReviewV36.ReviewContext.Hammer,RaccoonActionReviewV36.ReviewContext.Unarmed})
            {
                review.SetContext(context);Step(1f/60);var baseline=digits.Select(t=>t.localRotation).ToArray();
                review.SetContext(RaccoonActionReviewV36.ReviewContext.Revolver);Step(1f/60);
                review.SetContext(context);Step(1f/60);
                for(int i=0;i<15;i++)Assert.That(Quaternion.Angle(digits[i].localRotation,baseline[i]),Is.LessThan(.1f),"Revolver grip leaked to another context");
            }
        }
#endif
    }
}
