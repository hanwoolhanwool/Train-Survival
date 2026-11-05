using System.Diagnostics;
using System.Linq;
using Game.Gameplay.Debugging;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonSmoothV30Tests
    {
#if UNITY_EDITOR
        [TestCase(0,30)] [TestCase(1,30)]
        [TestCase(0,60)] [TestCase(1,60)]
        [TestCase(0,120)] [TestCase(1,120)]
        public void FlatStopFinishesInIdleStanceInsteadOfFirstGroundContact(int side,int rate)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/PC_Raccoon_Actions_v36.prefab");
            var actor=Object.Instantiate(prefab,new Vector3(1000,.03f,0),Quaternion.identity);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            var owner=new GameObject("walk-stop regression");
            try
            {
                foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                var review=owner.AddComponent<RaccoonContextActionsV50>();review.enabled=false;
                review.SourcePrefab=prefab;review.Display=actor.GetComponent<RaccoonActionReviewV36>();
                review.GroundLocomotion=true;review.LocomotionFloor=floor.transform;
                review.GroundLocomotionController=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/AC_ReviewContinuousLocomotion.overrideController");
                Physics.SyncTransforms();review.Initialize();
                float dt=1f/rate;
                review.Configure(RaccoonActionReviewV36.ReviewContext.Unarmed,RaccoonContextActionsV50.Situation.Ground,RaccoonContextActionsV50.Motion.Idle);
                for(int i=0;i<rate;i++)review.Step(dt);
                var feet=new[]{review.OutputAnimator.GetBoneTransform(HumanBodyBones.LeftFoot),review.OutputAnimator.GetBoneTransform(HumanBodyBones.RightFoot)};
                var idlePositions=feet.Select(f=>actor.transform.InverseTransformPoint(f.position)).ToArray();
                var idleRotations=feet.Select(f=>f.rotation).ToArray();
                review.Configure(RaccoonActionReviewV36.ReviewContext.Unarmed,RaccoonContextActionsV50.Situation.Ground,RaccoonContextActionsV50.Motion.Forward);
                for(int i=0;i<rate;i++)review.Step(dt);
                var source=review.BodySource.GetComponentInChildren<Animator>();
                // Advance continuously: seeking the clip alone leaves contact
                // anchors from an unrelated stride and can make this foot airborne.
                float startPhase=side==0?.25f:0;
                bool reachedPhase=false;
                for(int i=0;i<rate*3;i++)
                {
                    var state=source.GetCurrentAnimatorStateInfo(0);
                    float phase=Mathf.Repeat(state.normalizedTime,1);
                    if(phase>=startPhase&&phase<startPhase+dt/state.length+.002f){reachedPhase=true;break;}
                    review.Step(dt);
                }
                Assert.That(reachedPhase,Is.True);
                for(int i=0;i<Mathf.RoundToInt(.25f*rate);i++)review.Step(dt);
                var support=review.OutputAnimator.GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                var toes=review.OutputAnimator.GetBoneTransform(side==0?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
                var arriving=review.OutputAnimator.GetBoneTransform(side==0?HumanBodyBones.RightFoot:HumanBodyBones.LeftFoot);
                Vector3 position=support.position;Quaternion rotation=support.rotation,toeRotation=toes.localRotation;
                float arrivingHeight=arriving.position.y;
                review.Configure(RaccoonActionReviewV36.ReviewContext.Unarmed,RaccoonContextActionsV50.Situation.Ground,RaccoonContextActionsV50.Motion.Idle);
                for(int i=0;i<Mathf.CeilToInt(rate*1.2f);i++)
                {
                    review.Step(dt);
                    // The first foot provides support, then gets its own deliberate
                    // settling step. It must not remain frozen mid-stride forever.
                    if((i+1)*dt<=.4f)
                    {
                        Assert.That(Vector3.Distance(position,support.position),Is.LessThan(.001f),"Support foot shook while waiting for the arriving foot");
                        Assert.That(Quaternion.Angle(rotation,support.rotation),Is.LessThan(.1f));
                        Assert.That(Quaternion.Angle(toeRotation,toes.localRotation),Is.LessThan(.1f));
                    }
                }
                Assert.That(arriving.position.y,Is.LessThan(arrivingHeight-.015f),"The airborne foot must still finish landing");
                for(int s=0;s<2;s++)
                {
                    Assert.That(Vector3.Distance(idlePositions[s],actor.transform.InverseTransformPoint(feet[s].position)),Is.LessThan(.006f),"Flat-ground stop froze at an incidental contact instead of the idle stance");
                    Assert.That(Quaternion.Angle(idleRotations[s],feet[s].rotation),Is.LessThan(.1f),"Foot did not finish returning to its standing orientation");
                }
                var settled=feet.Select(f=>f.position).ToArray();
                for(int i=0;i<rate/2;i++)
                {
                    review.Step(dt);
                    for(int s=0;s<2;s++)Assert.That(Vector3.Distance(settled[s],feet[s].position),Is.LessThan(.001f),"Standing feet drifted after settling");
                }
                review.Configure(RaccoonActionReviewV36.ReviewContext.Unarmed,RaccoonContextActionsV50.Situation.Ground,RaccoonContextActionsV50.Motion.Forward);
                float resumedTravel=0;
                for(int i=0;i<rate;i++)
                {
                    review.Step(dt);
                    resumedTravel=Mathf.Max(resumedTravel,Vector3.Distance(settled[side],support.position));
                }
                Assert.That(resumedTravel,Is.GreaterThan(.1f),"Stop contact did not release when walking resumed");
            }
            finally{Object.DestroyImmediate(owner);Object.DestroyImmediate(actor);Object.DestroyImmediate(floor);}
        }

        [TestCase(0,30)] [TestCase(1,30)]
        [TestCase(0,60)] [TestCase(1,60)]
        [TestCase(0,120)] [TestCase(1,120)]
        public void ReturningFromRunWaitsForANewWalkPlant(int side,int rate)
        {
            const string prefab="Assets/_Project/Art/Characters/Raccoon/DirectionalCandidate_v29/PC_Raccoon_Directional_v29.prefab";
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab),new Vector3(1000,.03f,0),Quaternion.identity);
            try
            {
                foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                var a=actor.GetComponentInChildren<Animator>();a.Rebind();a.Update(0);
                a.SetFloat("Speed",4.5f);a.SetFloat("MoveX",0);a.SetFloat("MoveZ",1);
                Physics.SyncTransforms();
                var contacts=new RaccoonDirectionalContactsV30(a,actor.transform);
                contacts.ResetContacts(true);
                float dt=1f/rate, touchdown=side==0?.40f:.15f;
                // Re-enter during the support interval. This pose was blended from
                // running and must not become a fixed world-space walking anchor.
                a.Update(dt);contacts.Step(dt,.8f,touchdown+.15f,false);
                Assert.That(contacts.IsFootPlanted(side),Is.False,"Captured a running foot mid-stride");
                // A touchdown while the run blend is still present is also too early.
                a.Update(dt);contacts.Step(dt,.8f,touchdown+.49f,false);
                a.Update(dt);contacts.Step(dt,.8f,touchdown+.51f,false);
                a.Update(dt);contacts.Step(dt,.8f,touchdown+.65f,true);
                Assert.That(contacts.IsFootPlanted(side),Is.False,"Pinned before a fresh walk touchdown");
                // Once walking, the next real touchdown must restore support.
                a.Update(dt);contacts.Step(dt,.8f,touchdown+.99f,true);
                a.Update(dt);contacts.Step(dt,.8f,touchdown+1.01f,true);
                a.Update(dt);contacts.Step(dt,.8f,touchdown+1.08f,true);
                Assert.That(contacts.IsFootPlanted(side),Is.True,"Walking never reacquired support");
                Assert.That(contacts.FootTargetError(side),Is.LessThan(.01f));
            }
            finally{Object.DestroyImmediate(actor);Object.DestroyImmediate(floor);}
        }

        [Test]
        public void CachedContactEvaluationKeepsFeetAndTailAndReducesCpuCost()
        {
            const string prefab="Assets/_Project/Art/Characters/Raccoon/DirectionalCandidate_v29/PC_Raccoon_Directional_v29.prefab";
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            var roots=new[]{Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab)),Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab))};
            try
            {
                var animators=roots.Select(r=>r.GetComponentInChildren<Animator>()).ToArray();
                for(int side=0;side<2;side++)
                {
                    roots[side].transform.position=new Vector3(1000,.03f,0);
                    foreach(var b in roots[side].GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                    var a=animators[side];a.Rebind();a.Update(0);a.SetFloat("Speed",4.5f);a.SetFloat("MoveX",-1);a.SetFloat("MoveZ",0);a.SetFloat("RaccoonShotgunRun",1);a.SetLayerWeight(a.GetLayerIndex("Hold"),1);
                }
                Physics.SyncTransforms();
                var before=new RaccoonCandidateContactsV26(animators[0],roots[0].transform);
                var after=new RaccoonDirectionalContactsV30(animators[1],roots[1].transform);
                var bones=animators.Select(a=>a.GetComponentInChildren<SkinnedMeshRenderer>().bones).ToArray();
                var clocks=new[]{new Stopwatch(),new Stopwatch()};float maxPosition=0,maxAngle=0;
                for(int frame=0;frame<160;frame++)
                {
                    for(int side=0;side<2;side++)
                    {
                        roots[side].transform.position=new Vector3(1000-.45f*frame/60,.03f,0);
                        var a=animators[side];a.Update(1f/60);
                        float phase=Mathf.Repeat(a.GetCurrentAnimatorStateInfo(0).normalizedTime+RaccoonDirectionalRigV29.WalkCycleOffset,1);
                        var tail=a.GetComponent<RaccoonTailMotion>();tail.Step(.45f,0,1f/60);
                        if(frame>20)clocks[side].Start();
                        if(side==0){before.Step(1f/60,.45f,phase);before.ClearSoles();before.FitTail(1f/60);}
                        else{after.Step(1f/60,.45f,phase);after.ClearSoles();after.FitTail(1f/60);}
                        clocks[side].Stop();
                    }
                    for(int b=0;b<bones[0].Length;b++)
                    {
                        maxPosition=Mathf.Max(maxPosition,Vector3.Distance(bones[0][b].position,bones[1][b].position));
                        maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(bones[0][b].rotation,bones[1][b].rotation));
                    }
                }
                Assert.That(maxPosition,Is.LessThan(.0005f),"Optimizing must not simplify or move the solved mesh");
                Assert.That(maxAngle,Is.LessThan(.1f));
                Assert.That(clocks[1].Elapsed.TotalMilliseconds,Is.LessThan(clocks[0].Elapsed.TotalMilliseconds*.65),"Contact processing must be substantially faster, not just produce fewer visible frames");
                UnityEngine.Debug.Log($"SMOOTH_V30 beforeMs={clocks[0].Elapsed.TotalMilliseconds/139:F3} afterMs={clocks[1].Elapsed.TotalMilliseconds/139:F3} maxPosition={maxPosition} maxAngle={maxAngle}");
            }
            finally{foreach(var r in roots)Object.DestroyImmediate(r);Object.DestroyImmediate(floor);}
        }
#endif
    }
}
