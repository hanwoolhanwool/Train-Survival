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
    public sealed class RaccoonHarpoonV46Tests
    {
#if UNITY_EDITOR
        GameObject root,floor;RaccoonActionMotorV36 motor;RaccoonActionReviewV36 review;Animator a;
        [SetUp]public void Setup()
        {
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/PC_Raccoon_Actions_v36.prefab"));
            floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            motor=root.GetComponent<RaccoonActionMotorV36>();motor.enabled=false;motor.ResetAt(new Vector3(1000,.03f,0));
            review=root.GetComponent<RaccoonActionReviewV36>();a=motor.Rig.Animator;
        }
        [TearDown]public void Cleanup(){Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        void Step(float dt){motor.Advance(dt,Vector2.zero,false);motor.Evaluate(dt);}
        void Context(RaccoonActionReviewV36.ReviewContext c){review.SetContext(c);Step(1f/60);}
        [Test]public void IdleMatchesApprovedRevolverCAndDoesNotChangeIt()
        {
            review.CompareRevolver(RaccoonActionReviewV36.RevolverComparison.LeftShift);
            Context(RaccoonActionReviewV36.ReviewContext.Revolver);
            var bones=Enumerable.Range(0,(int)HumanBodyBones.LastBone).Select(i=>a.GetBoneTransform((HumanBodyBones)i)).Where(t=>t!=null).ToArray();
            var positions=bones.Select(t=>t.position).ToArray();var rotations=bones.Select(t=>t.rotation).ToArray();
            var revolver=review.Props.Single(p=>p.name=="Revolver");var socket=revolver.transform.localPosition;
            Context(RaccoonActionReviewV36.ReviewContext.Harpoon);
            for(int i=0;i<bones.Length;i++)
            {
                Assert.That(Vector3.Distance(bones[i].position,positions[i]),Is.LessThan(.001f),bones[i].name);
                Assert.That(Quaternion.Angle(bones[i].rotation,rotations[i]),Is.LessThan(.2f),bones[i].name);
            }
            Assert.That(revolver.activeSelf,Is.False);
            var launcher=review.Props.Single(p=>p.name=="Harpoon");
            Assert.That(launcher.activeSelf,Is.True);
            var source=AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/Art/Models/Weapon_Harpoon_Launcher.fbx").OfType<Mesh>().First();
            Assert.That(launcher.GetComponent<MeshFilter>().sharedMesh,Is.SameAs(source));
            Context(RaccoonActionReviewV36.ReviewContext.Revolver);
            Assert.That(Vector3.Distance(revolver.transform.localPosition,socket),Is.LessThan(.00001f));
            for(int i=0;i<bones.Length;i++)Assert.That(Quaternion.Angle(bones[i].rotation,rotations[i]),Is.LessThan(.2f),bones[i].name);
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void FireAndRepeatedReelKeepGripAndReturnToMatchingIdle(int fps)
        {
            Context(RaccoonActionReviewV36.ReviewContext.Harpoon);
            var digits=Enumerable.Range((int)HumanBodyBones.RightThumbProximal,15).Select(i=>a.GetBoneTransform((HumanBodyBones)i)).ToArray();
            var grip=digits.Select(t=>t.localRotation).ToArray();var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
            var idleHand=hand.position;var idleRotation=hand.rotation;
            var gun=review.Props.Single(p=>p.name=="Harpoon").transform;var socket=gun.localPosition;var rotation=gun.localRotation;
            var left=a.GetBoneTransform(HumanBodyBones.LeftHand);var idleLeft=left.position;
            float dt=1f/fps;
            foreach(string id in new[]{"HarpoonFire","HarpoonReel"})
            {
                review.Select(Array.FindIndex(review.Library.Entries,e=>e.Id==id));
                float maxCurl=0;var last=hand.position;var lastRotation=hand.rotation;
                for(int frame=0;frame<fps*5;frame++)
                {
                    if(id=="HarpoonReel"&&frame==fps*4)review.ReturnToContext();
                    Step(dt);
                    Assert.That(Vector3.Distance(gun.localPosition,socket),Is.LessThan(.00001f),"Launcher slips");
                    Assert.That(Quaternion.Angle(gun.localRotation,rotation),Is.LessThan(.1f));
                    Assert.That(gun.localScale,Is.EqualTo(Vector3.one*65));
                    Assert.That(Vector3.Distance(last,hand.position),Is.LessThan(2*dt),"Wrist jumps at action boundary");
                    Assert.That(Quaternion.Angle(lastRotation,hand.rotation),Is.LessThan(450*dt));
                    Assert.That(Vector3.Distance(left.position,idleLeft),Is.LessThan(.003f),"Old support hand pose returned");
                    for(int i=0;i<15;i++)
                    {
                        float angle=Quaternion.Angle(digits[i].localRotation,grip[i]);
                        if(i<3||i>5)Assert.That(angle,Is.LessThan(.1f),"Supporting fingers changed");
                        else maxCurl=Mathf.Max(maxCurl,angle);
                    }
                    last=hand.position;lastRotation=hand.rotation;
                }
                Assert.That(maxCurl,Is.GreaterThan(10));Assert.That(review.Active,Is.False);
                Assert.That(Vector3.Distance(hand.position,idleHand),Is.LessThan(.002f));
                Assert.That(Quaternion.Angle(hand.rotation,idleRotation),Is.LessThan(.2f));
                for(int i=0;i<15;i++)Assert.That(Quaternion.Angle(digits[i].localRotation,grip[i]),Is.LessThan(.1f));
            }
        }
#endif
    }
}
