using System;
using System.Linq;
using System.Reflection;
using Game.Gameplay.Debugging;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;
namespace Game.Tests.PlayMode
{
    public sealed class RaccoonHandsV37Tests
    {
#if UNITY_EDITOR
        const string Path="Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/PC_Raccoon_Actions_v36.prefab";
        GameObject root,floor;RaccoonActionMotorV36 motor;RaccoonActionReviewV36 actions;
        [SetUp] public void SetUp()
        {
            root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            motor=root.GetComponent<RaccoonActionMotorV36>();motor.enabled=false;actions=root.GetComponent<RaccoonActionReviewV36>();motor.ResetAt(new Vector3(1000,.03f,0));
        }
        [TearDown]public void TearDown(){Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        void Pose(string id,float seconds,float dt=1f/60)
        {motor.ResetAt(new Vector3(1000,.03f,0));int index=Array.FindIndex(actions.Library.Entries,e=>e.Id==id);actions.SetContext(RaccoonActionReviewV36.ContextFor(actions.Library.Entries[index]));actions.Select(index);for(int i=0;i<Mathf.RoundToInt(seconds/dt);i++){motor.Advance(dt,Vector2.zero,false);motor.Evaluate(dt);}}
        [Test]public void NeutralHandsHangAtSidesWithPalmsInward()
        {
            Pose("Neutral",.6f);var a=motor.Rig.Animator;
            foreach(bool left in new[]{true,false})
            {
                var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);var arm=a.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);var finger=a.GetBoneTransform(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal);
                Assert.That(root.transform.InverseTransformPoint(hand.position).y,Is.LessThan(.78f));
                Assert.That(Vector3.Dot(actions.Hands.PalmNormal(hand,left),left?root.transform.right:-root.transform.right),Is.GreaterThan(.85f));
                Assert.That(Vector3.Angle(hand.position-arm.position,finger.position-hand.position),Is.LessThan(3));
            }
            Assert.That(actions.Weapon.activeSelf,Is.False);
        }
        [Test]public void EveryNonWeaponActionIgnoresCachedShotgunWristRotations()
        {
            var field=typeof(RaccoonActionReviewV36).GetField("weaponRotations",BindingFlags.Instance|BindingFlags.NonPublic);
            foreach(var entry in actions.Library.Entries.Where(e=>!e.KeepWeapon&&e.Id!="Death"))
            {
                Pose(entry.Id,Mathf.Min(entry.Duration*.3f,.6f));var a=motor.Rig.Animator;var left=a.GetBoneTransform(HumanBodyBones.LeftHand);var right=a.GetBoneTransform(HumanBodyBones.RightHand);var expectedLeft=left.rotation;var expectedRight=right.rotation;
                field.SetValue(actions,new[]{Quaternion.Euler(145,82,-130),Quaternion.Euler(-120,-170,100)});
                // Re-evaluate the same action phase after poisoning only the old gun cache.
                motor.Evaluate(1f/60);
                Assert.That(Quaternion.Angle(left.rotation,expectedLeft),Is.LessThan(.5f),entry.Id+" left inherits shotgun orientation");
                Assert.That(Quaternion.Angle(right.rotation,expectedRight),Is.LessThan(.5f),entry.Id+" right inherits shotgun orientation");
            }
        }
        [Test]public void NeutralTorsoIsCentredAndElbowsStaySoftlyBent()
        {
            Pose("Neutral",.6f);var a=motor.Rig.Animator;
            float pelvisX=root.transform.InverseTransformPoint(a.GetBoneTransform(HumanBodyBones.Hips).position).x;
            foreach(var bone in new[]{HumanBodyBones.Chest,HumanBodyBones.UpperChest,HumanBodyBones.Neck,HumanBodyBones.Head})
                Assert.That(Mathf.Abs(root.transform.InverseTransformPoint(a.GetBoneTransform(bone).position).x-pelvisX),Is.LessThan(.005f),bone+" sideways lean");
            float previousAngle=-1;
            foreach(bool left in new[]{true,false})
            {
                var upper=a.GetBoneTransform(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm);
                var lower=a.GetBoneTransform(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm);
                var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                float bend=Vector3.Angle(lower.position-upper.position,hand.position-lower.position);
                Assert.That(bend,Is.InRange(12f,32f),"Arm should hang with a soft elbow, not lock straight or pose at the waist");
                if(previousAngle>=0)Assert.That(Mathf.Abs(bend-previousAngle),Is.LessThan(3f),"Matching relaxation despite unequal limb lengths");
                previousAngle=bend;
                Assert.That(root.transform.InverseTransformPoint(hand.position).y,Is.InRange(.60f,.72f));
            }
        }
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void PalmContactsAndHammerStrokeUseTheirOwnDirections(int fps)
        {
            float dt=1f/fps;Pose("Interact",.36f,dt);var a=motor.Rig.Animator;var hand=a.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.That(Vector3.Dot(actions.Hands.PalmNormal(hand,false),Vector3.down),Is.GreaterThan(.95f));
            Assert.That(Mathf.Abs(root.transform.InverseTransformPoint(actions.Hands.PalmCenter(hand,false)).y-.92f),Is.LessThan(.008f));
            Pose("Repair",RaccoonHammerPoseV42.Duration,dt);var left=a.GetBoneTransform(HumanBodyBones.LeftHand);hand=a.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.That(root.transform.InverseTransformPoint(left.position).y,Is.LessThan(.78f),"Free hand stays out of the striking path");
            var hammer=actions.Props.First(p=>p.name=="Hammer").transform;
            Vector3 face=hammer.TransformPoint(RaccoonHammerPoseV42.Face);float gap=Vector3.Distance(root.transform.InverseTransformPoint(face),RaccoonHammerPoseV42.Contact);
            Assert.That(gap,Is.LessThan(.008f),"Hammer face must reach the vertical repair panel");
            Assert.That(Vector3.Dot(-hammer.up,root.transform.forward),Is.GreaterThan(.99f),"Strike face must be parallel to the panel");
            var lower=a.GetBoneTransform(HumanBodyBones.RightLowerArm);var middle=a.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
            Assert.That(Vector3.Angle(hand.position-lower.position,middle.position-hand.position),Is.LessThan(61));
        }
        [Test]public void UnarmedOneShotReturnsToUnarmedNeutral()
        {Pose("Interact",2);Assert.That(actions.Entry.Id,Is.EqualTo("Neutral"));Assert.That(actions.Weapon.activeSelf,Is.False);}
#endif
    }
}
