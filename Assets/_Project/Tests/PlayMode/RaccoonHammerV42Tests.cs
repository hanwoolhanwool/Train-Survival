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
    public sealed class RaccoonHammerV42Tests
    {
#if UNITY_EDITOR
        GameObject root,floor;RaccoonActionMotorV36 motor;RaccoonActionReviewV36 review;Animator a;
        [SetUp]public void Setup()
        {
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/PC_Raccoon_Actions_v36.prefab"));
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            motor=root.GetComponent<RaccoonActionMotorV36>();motor.enabled=false;motor.ResetAt(new Vector3(1000,.03f,0));
            review=root.GetComponent<RaccoonActionReviewV36>();review.SetContext(RaccoonActionReviewV36.ReviewContext.Hammer);a=motor.Rig.Animator;
            for(int i=0;i<60;i++)Step(1f/60);
        }
        [TearDown]public void Cleanup(){Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        void Step(float dt){motor.Advance(dt,Vector2.zero,false);motor.Evaluate(dt);}
        void Repair()=>review.Select(Array.FindIndex(review.Library.Entries,e=>e.Id=="Repair"));
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void OriginalHammerAlsoPreservesStrokeAndGrip(int fps)
        {
            review.UseOriginalHammer(true);Step(1f/fps);
            var hammer=review.Props.Single(p=>p.name=="Hammer").transform;
            var original=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Models/Weapon_RepairHammer.fbx").OfType<Mesh>().First();
            Assert.That(hammer.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(original),"Use the unchanged source mesh directly");
            Assert.That(hammer.lossyScale.x,Is.EqualTo(110).Within(.001f));
            Assert.That(hammer.lossyScale.y,Is.EqualTo(110).Within(.001f));
            Assert.That(hammer.lossyScale.z,Is.EqualTo(110).Within(.001f));
            RepeatedRepairKeepsGripElbowAndLoopContinuous(fps);
            review.Select(-1);Step(1f/fps);
            RepairReturnsToSameHammerGripAtAnyStrokePhase(fps);
        }
        [Test]public void ComparisonSwitchPreservesTimeAndRestoresExistingGripExactly()
        {
            Repair();for(int i=0;i<50;i++)Step(1f/60);
            var hammer=review.Props.Single(p=>p.name=="Hammer").transform;
            var digits=Enumerable.Range((int)HumanBodyBones.RightThumbProximal,15).Select(i=>a.GetBoneTransform((HumanBodyBones)i)).ToArray();
            var poses=digits.Select(t=>t.localRotation).ToArray();var mesh=hammer.GetComponent<MeshFilter>().sharedMesh;
            Vector3 position=hammer.position;Quaternion rotation=hammer.rotation;float elapsed=review.Elapsed;
            review.UseOriginalHammer(true);motor.Evaluate(1f/60);
            Assert.That(review.Elapsed,Is.EqualTo(elapsed));Assert.That(hammer.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(review.OriginalHammerMesh));
            Assert.That(Quaternion.Angle(digits[0].localRotation,poses[0]),Is.GreaterThan(5),"Original diameter needs its own thumb pose");
            review.UseOriginalHammer(false);motor.Evaluate(1f/60);
            Assert.That(hammer.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(mesh));
            Assert.That(Vector3.Distance(hammer.position,position),Is.LessThan(.0005f));Assert.That(Quaternion.Angle(hammer.rotation,rotation),Is.LessThan(.1f));
            for(int i=0;i<digits.Length;i++)Assert.That(Quaternion.Angle(digits[i].localRotation,poses[i]),Is.LessThan(.1f));
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void ThumbOpposesFingersAndGripSurvivesIdleRepairReturn(int fps)
        {
            var hammer=review.Props.Single(p=>p.name=="Hammer").transform;
            var mesh=hammer.GetComponent<MeshFilter>().sharedMesh;
            var handle=mesh.vertices.Where(v=>v.x<0&&v.x>-.0015f).ToArray();
            Assert.That((handle.Max(v=>v.y)-handle.Min(v=>v.y))*110,Is.LessThan(.055f),"Handle must fit the fingers");
            var digits=Enumerable.Range((int)HumanBodyBones.RightThumbProximal,15).Select(i=>a.GetBoneTransform((HumanBodyBones)i)).ToArray();
            var grip=digits.Select(t=>t.localRotation).ToArray();
            Repair();float dt=1f/fps;
            for(int frame=0;frame<fps*4;frame++)
            {
                if(frame==fps*3)review.ReturnToContext();Step(dt);
                var thumbBase=hammer.InverseTransformPoint(digits[0].position)*110;
                var thumbEnd=hammer.InverseTransformPoint(digits[2].position)*110;
                Assert.That(thumbEnd.x,Is.LessThan(thumbBase.x-.04f),"Thumb stands up the handle instead of crossing the fingers");
                Assert.That(thumbEnd.z-thumbBase.z,Is.GreaterThan(.045f),"Thumb must reach the opposing side");
                for(int i=0;i<digits.Length;i++)Assert.That(Quaternion.Angle(digits[i].localRotation,grip[i]),Is.LessThan(.1f),"Grip changes during the clip: "+digits[i].name);
            }
            Assert.That(review.Active,Is.False);
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void RepeatedRepairKeepsGripElbowAndLoopContinuous(int fps)
        {
            float dt=1f/fps;var hand=a.GetBoneTransform(HumanBodyBones.RightHand);var elbow=a.GetBoneTransform(HumanBodyBones.RightLowerArm);var upper=a.GetBoneTransform(HumanBodyBones.RightUpperArm);var middle=a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            var hammer=review.Props.Single(p=>p.name=="Hammer").transform;
            var socket=hand.InverseTransformPoint(hammer.position);var rotation=Quaternion.Inverse(hand.rotation)*hammer.rotation;
            var foot=a.GetBoneTransform(HumanBodyBones.RightFoot);var planted=foot.position;
            Repair();float duration=review.Entry.Duration;Vector3 last=elbow.position;Quaternion lastRotation=hand.rotation;
            for(int i=0;i<Mathf.CeilToInt(duration*4*fps);i++)
            {
                Step(dt);float t=Mathf.Repeat(review.Elapsed/duration,1);
                Assert.That(Vector3.Angle(hand.position-elbow.position,middle.position-hand.position),Is.LessThan(35),"Wrist bends instead of elbow");
                Assert.That(elbow.position.y-upper.position.y,Is.LessThan(-.10f),"Elbow flips above shoulder");
                Assert.That(Vector3.Distance(last,elbow.position),Is.LessThan(1.5f*dt),"Elbow discontinuity");
                Assert.That(Quaternion.Angle(lastRotation,hand.rotation),Is.LessThan(400*dt),"Hand discontinuity");
                Assert.That(Vector3.Distance(hand.InverseTransformPoint(hammer.position),socket),Is.LessThan(.0001f),"Handle slides in fist");
                Assert.That(Quaternion.Angle(Quaternion.Inverse(hand.rotation)*hammer.rotation,rotation),Is.LessThan(.1f),"Handle rotates inside fist");
                Assert.That(Vector3.Distance(foot.position,planted),Is.LessThan(.008f),"Planted foot moves");
                if(i>fps&&t<.075f)
                {
                    Assert.That(Vector3.Distance(root.transform.InverseTransformPoint(hammer.TransformPoint(RaccoonHammerPoseV42.Face)),RaccoonHammerPoseV42.Contact),Is.LessThan(.008f));
                    Assert.That(Vector3.Dot(-hammer.up,root.transform.forward),Is.GreaterThan(.99f));
                }
                if(i>fps&&(t<.025f||t>.975f))
                {
                    Assert.That(Vector3.Distance(last,elbow.position),Is.LessThan(.04f*dt),"Loop seam position");
                    Assert.That(Quaternion.Angle(lastRotation,hand.rotation),Is.LessThan(10*dt),"Loop seam rotation");
                }
                last=elbow.position;lastRotation=hand.rotation;
            }
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void RepairReturnsToSameHammerGripAtAnyStrokePhase(int fps)
        {
            float dt=1f/fps;var hand=a.GetBoneTransform(HumanBodyBones.RightHand);var baseline=hand.position;var br=hand.rotation;
            foreach(float time in new[]{.06f,.4f,.8f,1.12f})
            {
                Repair();for(int i=0;i<Mathf.CeilToInt(time*fps);i++)Step(dt);review.ReturnToContext();
                for(int i=0;i<Mathf.CeilToInt(.5f*fps);i++)
                {
                    var p=hand.position;var r=hand.rotation;Step(dt);
                    Assert.That(Vector3.Distance(p,hand.position),Is.LessThan(2*dt));
                    Assert.That(Quaternion.Angle(r,hand.rotation),Is.LessThan(450*dt));
                }
                Assert.That(review.Active,Is.False);Assert.That(review.Entry.Id,Is.EqualTo("HammerIdle"));
                Assert.That(Vector3.Distance(hand.position,baseline),Is.LessThan(.003f));Assert.That(Quaternion.Angle(hand.rotation,br),Is.LessThan(.5f));
            }
        }
#endif
    }
}
