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
    public sealed class RaccoonSwimV40Tests
    {
#if UNITY_EDITOR
        GameObject root,floor;RaccoonActionMotorV36 motor;RaccoonActionReviewV36 actions;
        [SetUp]public void SetUp()
        {
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/PC_Raccoon_Actions_v36.prefab"));
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            motor=root.GetComponent<RaccoonActionMotorV36>();motor.enabled=false;actions=root.GetComponent<RaccoonActionReviewV36>();motor.ResetAt(new Vector3(1000,.03f,0));
        }
        [TearDown]public void TearDown(){Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        void Step(float dt){motor.Advance(dt,Vector2.zero,false);motor.Evaluate(dt);}
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void FiveDirectionsLoopWithoutHandSnapsOrGroundedFeet(int fps)
        {
            var entries=actions.Library.Entries.Where(e=>e.Id.StartsWith("Swim")).ToArray();
            CollectionAssert.AreEquivalent(new[]{"SwimIdle","SwimMove","SwimLeft","SwimRight","SwimBack"},entries.Select(e=>e.Id));
            float dt=1f/fps;
            foreach(var entry in entries)
            {
                motor.ResetAt(new Vector3(1000,.03f,0));actions.SetContext(RaccoonActionReviewV36.ReviewContext.Unarmed);actions.Select(Array.IndexOf(actions.Library.Entries,entry));
                int count=Mathf.RoundToInt(entry.Duration*fps);for(int i=0;i<count;i++)Step(dt);
                var a=motor.Rig.Animator;var hands=new[]{a.GetBoneTransform(HumanBodyBones.LeftHand),a.GetBoneTransform(HumanBodyBones.RightHand)};
                var initial=hands.Select(h=>h.position).ToArray();var previous=(Vector3[])initial.Clone();var rotation=hands.Select(h=>h.rotation).ToArray();
                for(int i=0;i<count;i++)
                {
                    Step(dt);
                    for(int side=0;side<2;side++)
                    {
                        Assert.That(Vector3.Distance(hands[side].position,previous[side])/dt,Is.LessThan(3f),entry.Id+" abrupt hand motion");
                        Assert.That(Quaternion.Angle(hands[side].rotation,rotation[side])/dt,Is.LessThan(600f),entry.Id+" wrist flip");
                        previous[side]=hands[side].position;rotation[side]=hands[side].rotation;
                        var foot=a.GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                        Assert.That(root.transform.InverseTransformPoint(foot.position).y,Is.GreaterThan(.22f),entry.Id+" foot stuck to floor");
                    }
                    Assert.That(actions.Weapon.activeSelf,Is.False);Assert.That(actions.Context,Is.EqualTo(RaccoonActionReviewV36.ReviewContext.Unarmed));
                }
                for(int side=0;side<2;side++)Assert.That(Vector3.Distance(initial[side],hands[side].position),Is.LessThan(.004f),entry.Id+" loop seam");
                actions.ReturnToContext();for(int i=0;i<fps;i++)Step(dt);
                Assert.That(actions.Active,Is.False);Assert.That(actions.Entry.Id,Is.EqualTo("Neutral"));Assert.That(actions.Water.activeSelf,Is.False);
            }
        }
        [Test]public void SideAndBackClipsHaveDistinctBodyAndHandPaths()
        {
            var samples=new System.Collections.Generic.Dictionary<string,Vector3[]>();
            foreach(string id in new[]{"SwimIdle","SwimMove","SwimLeft","SwimRight","SwimBack"})
            {
                motor.ResetAt(new Vector3(1000,.03f,0));actions.SetContext(RaccoonActionReviewV36.ReviewContext.Unarmed);actions.Select(Array.FindIndex(actions.Library.Entries,e=>e.Id==id));
                for(int i=0;i<30;i++)Step(1f/60);var a=motor.Rig.Animator;
                samples[id]=new[]{HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.Head,HumanBodyBones.Hips}.Select(b=>root.transform.InverseTransformPoint(a.GetBoneTransform(b).position)).ToArray();
            }
            Assert.That(samples["SwimMove"][2].z-samples["SwimIdle"][2].z,Is.GreaterThan(.07f),"Forward swimming leans into travel");
            Assert.That(Vector3.Distance(samples["SwimLeft"][0],samples["SwimRight"][0]),Is.GreaterThan(.06f));
            Assert.That(Vector3.Distance(samples["SwimBack"][0],samples["SwimMove"][0]),Is.GreaterThan(.04f));
        }
#endif
    }
}
