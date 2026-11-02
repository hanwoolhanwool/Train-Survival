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
    public sealed class RaccoonActionsV36Tests
    {
#if UNITY_EDITOR
        const string Folder="Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36";
        [Test]
        public void ActionInventoryHasFiniteDistinctClipsAndClosedLoops()
        {
            var library=AssetDatabase.LoadAssetAtPath<RaccoonActionLibraryV36>(Folder+"/Raccoon_ActionLibrary_v36.asset");
            Assert.That(library.Entries.Length,Is.EqualTo(36));Assert.That(library.Entries.Select(x=>x.Id).Distinct().Count(),Is.EqualTo(36));
            foreach(var e in library.Entries)
            {
                Assert.That(e.Clip,Is.Not.Null,e.Id);Assert.That(e.Clip.length,Is.EqualTo(e.Duration).Within(.02f),e.Id);
                foreach(var binding in AnimationUtility.GetCurveBindings(e.Clip))
                {
                    var curve=AnimationUtility.GetEditorCurve(e.Clip,binding);foreach(var k in curve.keys)Assert.That(float.IsFinite(k.value),Is.True,e.Id+binding.propertyName);
                    if(e.Loop){Assert.That(curve[0].value,Is.EqualTo(curve[curve.length-1].value).Within(.0001f),e.Id+binding.propertyName);Assert.That(curve[0].outTangent,Is.EqualTo(curve[curve.length-1].inTangent).Within(.002f),e.Id+binding.propertyName);}
                }
            }
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void EveryActionKeepsFiniteBonesAndBoundedWrists(int fps)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PC_Raccoon_Actions_v36.prefab"));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            try
            {
                var m=go.GetComponent<RaccoonActionMotorV36>();m.enabled=false;var actions=go.GetComponent<RaccoonActionReviewV36>();float dt=1f/fps;
                var errors=new System.Collections.Generic.List<string>();foreach(var e in actions.Library.Entries)
                {
                    m.ResetAt(new Vector3(1000,.03f,0));for(int i=0;i<fps;i++){m.Advance(dt,Vector2.zero,false);m.Evaluate(dt);}
                    actions.SetContext(RaccoonActionReviewV36.ContextFor(e));actions.Select(System.Array.IndexOf(actions.Library.Entries,e));float maxAngle=0,maxError=0,maxStep=0;Vector3 last=Vector3.zero;
                    for(int i=0;i<Mathf.CeilToInt((e.Loop?e.Duration*2:e.Duration+.15f)*fps);i++)
                    {
                        m.Advance(dt,Vector2.zero,false);m.Evaluate(dt);
                        var a=m.Rig.Animator;var hand=a.GetBoneTransform(HumanBodyBones.LeftHand);var lower=a.GetBoneTransform(HumanBodyBones.LeftLowerArm);var middle=a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                        Assert.That(float.IsFinite(hand.position.x)&&float.IsFinite(hand.position.y),Is.True,e.Id);
                        if(i>fps/3&&actions.Active&&actions.Weight>.99f&&e.Id!="Death"&&e.Id!="ShotgunDeath"&&!e.UpperBody)
                        {maxAngle=Mathf.Max(maxAngle,Vector3.Angle(hand.position-lower.position,middle.position-hand.position));maxError=Mathf.Max(maxError,actions.ContactError);if(i>0)maxStep=Mathf.Max(maxStep,Vector3.Distance(last,hand.position));}
                        last=hand.position;
                    }
                    if(maxAngle>=62)errors.Add(e.Id+" wrist="+maxAngle);
                    if(maxError>=.06f)errors.Add(e.Id+" contact="+maxError);
                    if(maxStep>=.18f)errors.Add(e.Id+" step="+maxStep);
                    Debug.Log($"ACTION_V36 {e.Id} fps={fps} wrist={maxAngle} contact={maxError} step={maxStep}");
                }Assert.That(errors,Is.Empty,string.Join("; ",errors));
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(floor);}
        }
        [TestCase(30,1)] [TestCase(60,2)] [TestCase(120,2)]
        public void WeaponEventsMatchShotsAndActuallyInsertedShells(int fps,int shots)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PC_Raccoon_Actions_v36.prefab"));
            try
            {
                var p=go.GetComponent<RaccoonWeaponPoseV36>();p.ResetState();float maximum=0;
                for(int s=0;s<shots;s++){Assert.That(p.Fire(),Is.True);for(int i=0;i<fps;i++){p.Step(1f/fps);maximum=Mathf.Max(maximum,p.PumpTravel);}}
                Assert.That(maximum,Is.InRange(.052f,.056f));Assert.That(p.Mechanics.Ejections,Is.EqualTo(shots));
                Assert.That(p.Reload(),Is.True);for(int i=0;i<fps*4;i++)p.Step(1f/fps);
                Assert.That(p.Mechanics.Insertions,Is.EqualTo(shots));Assert.That(p.Rounds,Is.EqualTo(2));Assert.That(p.Mechanics.SoundEvents,Is.GreaterThanOrEqualTo(shots*4));
                Assert.That(p.Mechanics.Casing.gameObject.activeSelf,Is.False);
            }
            finally{Object.DestroyImmediate(go);}
        }
        [Test]
        public void PumpSplitPreservesAllSourceTrianglesAndAudioDoesNotClip()
        {
            var body=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Shotgun_Body_v36.asset");var pump=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Shotgun_Pump_v36.asset");
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Characters/Raccoon/Raccoon_Shotgun_Grip.asset");
            Assert.That(body.GetIndexCount(0)+pump.GetIndexCount(0),Is.EqualTo(source.GetIndexCount(0)));Assert.That(pump.GetIndexCount(0),Is.GreaterThan(300));
            foreach(string name in new[]{"Shot","PumpOpen","PumpClose","ShellInsert","CasingImpact"})
            {var clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Folder+"/SFX_"+name+"_v36.wav");clip.LoadAudioData();var data=new float[clip.samples*clip.channels];Assert.That(clip.GetData(data,0),Is.True);Assert.That(data.Max(x=>Mathf.Abs(x)),Is.InRange(.7f,.81f));Assert.That(data.Average(x=>Mathf.Abs(x)),Is.GreaterThan(.001f));}
        }
        [Test]
        public void DeathUpdatesExtraWristJointsInsteadOfLeavingStandingSpikes()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PC_Raccoon_Actions_v36.prefab"));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            try
            {
                var m=go.GetComponent<RaccoonActionMotorV36>();m.enabled=false;m.ResetAt(new Vector3(1000,.03f,0));var actions=go.GetComponent<RaccoonActionReviewV36>();actions.Select(4);
                for(int i=0;i<360;i++){m.Advance(1f/60,Vector2.zero,false);m.Evaluate(1f/60);}
                var hand=m.Rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand);
                foreach(var joint in go.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("LeftWristBlend")))Assert.That(Vector3.Distance(joint.position,hand.position),Is.LessThan(.28f),joint.name);
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(floor);}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void ReloadFeedsIntoOpenThroatThenReturnsPumpAndHands(int fps)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PC_Raccoon_Actions_v36.prefab"));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            try
            {
                var m=go.GetComponent<RaccoonActionMotorV36>();m.enabled=false;m.ResetAt(new Vector3(1000,.03f,0));go.GetComponent<RaccoonActionReviewV36>().SetContext(RaccoonActionReviewV36.ReviewContext.Shotgun);float dt=1f/fps;
                for(int i=0;i<fps;i++){m.Advance(dt,Vector2.zero,false);m.Evaluate(dt);}m.Combat.Fire();
                for(int i=0;i<fps;i++){m.Advance(dt,Vector2.zero,false);m.Evaluate(dt);}m.Combat.Reload();
                float bestFeed=0,radial=1,handSpeed=0;var hand=m.Rig.Animator.GetBoneTransform(HumanBodyBones.LeftHand);var last=hand.position;
                for(int i=0;i<fps*4;i++)
                {
                    m.Advance(dt,Vector2.zero,false);m.Evaluate(dt);handSpeed=Mathf.Max(handSpeed,Vector3.Distance(last,hand.position)/dt);last=hand.position;
                    if(m.Combat.ShellFeed>bestFeed&&m.Combat.Shell.gameObject.activeSelf){bestFeed=m.Combat.ShellFeed;var local=m.Combat.Mechanics.Gun.InverseTransformPoint(m.Combat.Shell.position);radial=new Vector2(local.x,local.z-.00160f).magnitude*85;}
                }
                Assert.That(bestFeed,Is.GreaterThan(.90f));Assert.That(radial,Is.LessThan(.004f),"Shell approaches bore axis before occlusion");Assert.That(handSpeed,Is.LessThan(3.5f));Assert.That(m.Combat.Mechanics.Pump.localPosition.magnitude,Is.LessThan(.00001f));Assert.That(m.Combat.Shell.gameObject.activeSelf,Is.False);
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(floor);}
        }
#endif
    }
}

