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
    public sealed class RaccoonPropPostureV47Tests
    {
#if UNITY_EDITOR
        GameObject root,floor;RaccoonActionMotorV36 motor;RaccoonActionReviewV36 review;Animator a;
        [SetUp]public void Setup()
        {
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/PC_Raccoon_Actions_v36.prefab"));
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            motor=root.GetComponent<RaccoonActionMotorV36>();motor.enabled=false;motor.ResetAt(new Vector3(1000,.03f,0));
            review=root.GetComponent<RaccoonActionReviewV36>();a=motor.Rig.Animator;
            root.transform.rotation=Quaternion.Euler(0,35,0);
        }
        [TearDown]public void Cleanup(){Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        void Step(float dt){motor.Advance(dt,Vector2.zero,false);motor.Evaluate(dt);}
        void AssertCentred()
        {
            float x=root.transform.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.Hips).position).x;
            foreach(var bone in new[]{HumanBodyBones.Chest,HumanBodyBones.UpperChest,HumanBodyBones.Neck,HumanBodyBones.Head})
                Assert.That(Mathf.Abs(root.transform.InverseTransformPoint(a.GetBoneTransform(bone).position).x-x),Is.LessThan(.005f),review.Entry.Id+" / "+bone+" lateral lean");
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void PropIdlesActionsAndReturnsKeepTorsoCentredAndFeetPlanted(int fps)
        {
            float dt=1f/fps;
            foreach(var c in new[]{RaccoonActionReviewV36.ReviewContext.Food,RaccoonActionReviewV36.ReviewContext.Bundle,RaccoonActionReviewV36.ReviewContext.Fishing})
            {
                review.SetContext(c);for(int i=0;i<fps;i++)Step(dt);AssertCentred();
                var left=a.GetBoneTransform(HumanBodyBones.LeftFoot);var right=a.GetBoneTransform(HumanBodyBones.RightFoot);var lp=left.position;var rp=right.position;
                var head=a.GetBoneTransform(HumanBodyBones.Head);var idleHead=head.position;
                var entries=review.Library.Entries.Select((e,i)=>new{e,i}).Where(x=>review.Compatible(x.e)&&x.e.Id!="Death").ToArray();
                foreach(var item in entries)
                foreach(bool cancel in new[]{false,true})
                {
                    review.Select(item.i);float duration=item.e.Duration;
                    int frames=Mathf.CeilToInt((item.e.Loop?duration*2+.5f:duration+.5f)*fps);
                    var last=head.position;var lastRotation=head.rotation;
                    for(int frame=0;frame<frames;frame++)
                    {
                        if(cancel&&frame==Mathf.CeilToInt(duration*.43f*fps)||item.e.Loop&&!cancel&&frame==Mathf.CeilToInt(duration*1.7f*fps))review.ReturnToContext();
                        Step(dt);AssertCentred();
                        Assert.That(Vector3.Distance(lp,left.position),Is.LessThan(.005f));
                        Assert.That(Vector3.Distance(rp,right.position),Is.LessThan(.005f));
                        Assert.That(Vector3.Distance(last,head.position),Is.LessThan(1.2f*dt),"Head snaps");
                        Assert.That(Quaternion.Angle(lastRotation,head.rotation),Is.LessThan(200*dt),"Head rotation snaps");
                        last=head.position;lastRotation=head.rotation;
                        var prop=review.Props.Single(p=>p.name==review.Entry.Prop);
                        Assert.That(prop.activeSelf,Is.True);
                    }
                    Assert.That(review.Active,Is.False);
                    Assert.That(Vector3.Distance(head.position,idleHead),Is.LessThan(.003f));
                }
            }
        }
        [Test]public void PropContextsAndActionsUseSeparateCorrectedClips()
        {
            foreach(var e in review.Library.Entries.Concat(review.Library.ContextPoses).Where(e=>e.Prop=="Food"||e.Prop=="Bundle"||e.Prop=="Rod"))
            {
                Assert.That(AssetDatabase.GetAssetPath(e.Clip),Does.EndWith("_v47.anim"));
                Assert.That(AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/Raccoon_"+e.Id+"_v36.anim"),Is.Not.Null,"Old clip retained");
            }
        }
#endif
    }
}
