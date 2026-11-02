using System;
using System.Linq;
using Game.Gameplay.Debugging;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;
using Context=Game.Gameplay.Debugging.RaccoonActionReviewV36.ReviewContext;
namespace Game.Tests.PlayMode
{
    public sealed class RaccoonStatesV38Tests
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
        void Select(string id){actions.Select(Array.FindIndex(actions.Library.Entries,e=>e.Id==id));}
        [TestCase(30)][TestCase(60)][TestCase(120)]
        public void UnarmedReturnNeverExposesShotgunPose(int fps)
        {
            float dt=1f/fps;actions.SetContext(Context.Unarmed);for(int i=0;i<fps;i++)Step(dt);
            var a=motor.Rig.Animator;var left=a.GetBoneTransform(HumanBodyBones.LeftHand);var right=a.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 idleLeft=left.position,idleRight=right.position;
            foreach(string id in new[]{"Interact","Pickup","Melee"})
            {
                Select(id);float duration=actions.Entry.Duration;Vector3 previous=right.position;float maxReturnStep=0;
                for(int i=0;i<Mathf.CeilToInt((duration+.5f)*fps);i++)
                {
                    Step(dt);
                    Assert.That(a.GetLayerWeight(a.GetLayerIndex("Hold")),Is.Zero,id+" leaked Hold layer");
                    Assert.That(a.GetLayerWeight(a.GetLayerIndex("Review Context V38")),Is.EqualTo(1));
                    Assert.That(actions.Weapon.activeSelf,Is.False);
                    // Measure the final settling boundary, not the earlier authored strike recovery.
                    if(i*dt>duration-.1f)maxReturnStep=Mathf.Max(maxReturnStep,Vector3.Distance(previous,right.position));
                    previous=right.position;
                }
                Assert.That(actions.Active,Is.False);Assert.That(actions.Context,Is.EqualTo(Context.Unarmed));
                Assert.That(Vector3.Distance(left.position,idleLeft),Is.LessThan(.005f),id+" left return");
                Assert.That(Vector3.Distance(right.position,idleRight),Is.LessThan(.005f),id+" right return");
                Assert.That(maxReturnStep,Is.LessThan(.035f),id+" return discontinuity");
            }
        }
        [Test]
        public void EveryContextPersistsWithMatchingPropAndRejectsIncompatibleActions()
        {
            foreach(Context context in Enum.GetValues(typeof(Context)))
            {
                actions.SetContext(context);for(int i=0;i<30;i++)Step(1f/60);
                Assert.That(actions.Context,Is.EqualTo(context));Assert.That(actions.Active,Is.False);
                var wrong=actions.Library.Entries.FirstOrDefault(e=>!actions.Compatible(e));
                if(wrong!=null){actions.Select(Array.IndexOf(actions.Library.Entries,wrong));Assert.That(actions.Active,Is.False,"incompatible "+wrong.Id);}
                Assert.That(actions.Weapon.activeSelf,Is.EqualTo(actions.UsesShotgunBase),context.ToString());
                foreach(var p in actions.Props)Assert.That(p.activeSelf,Is.EqualTo(actions.ContextEntry!=null&&actions.ContextEntry.Prop==p.name),context+" "+p.name);
                motor.ResetAt(new Vector3(1000,.03f,0));Step(1f/60);Assert.That(actions.Context,Is.EqualTo(context),"Reset changed context");
            }
        }
        [TestCase("Eat",Context.Food)][TestCase("RevolverFire",Context.Revolver)]
        [TestCase("RevolverReload",Context.Revolver)][TestCase("HarpoonFire",Context.Harpoon)]
        [TestCase("FishingCast",Context.Fishing)]
        public void OneShotReturnsToItsEquippedBaseline(string id,Context context)
        {
            actions.SetContext(context);Select(id);float duration=actions.Entry.Duration;
            for(int i=0;i<Mathf.CeilToInt((duration+.5f)*60);i++)
            {Step(1f/60);Assert.That(actions.Context,Is.EqualTo(context));Assert.That(actions.Weapon.activeSelf,Is.False);Assert.That(motor.Rig.Animator.GetLayerWeight(motor.Rig.Animator.GetLayerIndex("Hold")),Is.Zero);}
            Assert.That(actions.Active,Is.False);Assert.That(actions.Props.Single(p=>p.name==actions.ContextEntry.Prop).activeSelf,Is.True);
        }
        [TestCase("Repair",Context.Hammer)][TestCase("Carry",Context.Bundle)]
        [TestCase("FishingReel",Context.Fishing)][TestCase("SwimMove",Context.Unarmed)]
        public void StopLoopFadesToSameContext(string id,Context context)
        {
            actions.SetContext(context);Select(id);for(int i=0;i<50;i++)Step(1f/60);
            actions.ReturnToContext();float weight=actions.Weight;
            for(int i=0;i<30;i++){Step(1f/60);Assert.That(actions.Weight,Is.LessThanOrEqualTo(weight+.00001f));weight=actions.Weight;Assert.That(actions.Context,Is.EqualTo(context));Assert.That(actions.Weapon.activeSelf,Is.False);}
            Assert.That(actions.Active,Is.False);Assert.That(actions.Returning,Is.False);
        }
#endif
    }
}
