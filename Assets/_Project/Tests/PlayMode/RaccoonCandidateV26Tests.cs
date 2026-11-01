using System;
using System.Collections;
using System.Linq;
using Game.Gameplay.Debugging;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonCandidateV26Tests
    {
#if UNITY_EDITOR
        private const string Path="Assets/_Project/Art/Characters/Raccoon/PlayableCandidate_v26/PC_Raccoon_Candidate_v26.prefab";
        [Test]
        public void PreviewRecreatesTransientSolverAfterScriptReload()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            try
            {
                var lab=root.GetComponent<RaccoonSandboxV26>();lab.enabled=false;lab.Initialize();
                var field=typeof(RaccoonCandidateRigV26).GetField("walkGrounding",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
                field.SetValue(lab.Rig,null);
                Assert.DoesNotThrow(()=>lab.Rig.Tick(1f/60,RaccoonSandboxV26.RunSpeed,false));
                Assert.That(field.GetValue(lab.Rig),Is.Not.Null);
            }
            finally{Object.DestroyImmediate(root);}
        }
        [TestCase(false)] [TestCase(true)]
        public void RunningPreservesApprovedHybridCadenceAndLegTrajectory(bool grounded)
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position=new Vector3(1000,-.5f,20);floor.transform.localScale=new Vector3(20,1,100);
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            root.transform.position=new Vector3(1000,.03f,0);
            var reference=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/CH_Raccoon_Player.prefab"));
            try
            {
                foreach(var b in reference.GetComponents<MonoBehaviour>())b.enabled=false;
                var source=reference.GetComponent<Animator>();source.Rebind();source.Update(0);
                source.SetFloat("Speed",7);source.SetFloat("RaccoonShotgunRun",1);source.SetLayerWeight(source.GetLayerIndex("Hold"),0);
                var lab=root.GetComponent<RaccoonSandboxV26>();lab.enabled=false;lab.Initialize();Physics.SyncTransforms();
                var actual=lab.Rig.Animator;
                var legs=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes};
                float startPhase=0,maxHorizontal=0,maxHip=0;
                for(int frame=0;frame<=360;frame++)
                {
                    lab.Advance(1f/60,Vector2.up,true);
                    lab.Rig.Tick(1f/60,lab.ActualSpeed,grounded);
                    float phase=actual.GetCurrentAnimatorStateInfo(0).normalizedTime;
                    if(frame==60)startPhase=phase;
                    if(frame<60)continue;
                    Assert.That(actual.GetCurrentAnimatorClipInfo(0).Any(c=>c.weight>.999f&&c.clip.name=="Raccoon_ShotgunRun_LOOP_v27"),Is.True);
                    reference.transform.SetPositionAndRotation(actual.transform.position,actual.transform.rotation);
                    source.Play("Locomotion",0,phase);source.Update(0);
                    // Only the +/-0.1s seam window is deliberately changed in the new copy.
                    bool unchanged=Mathf.Repeat(phase,1)>.081f&&Mathf.Repeat(phase,1)<.919f;
                    foreach(var bone in legs)
                    {
                        if(!unchanged)continue;
                        Vector3 delta=actual.GetBoneTransform(bone).position-source.GetBoneTransform(bone).position;
                        if(bone==HumanBodyBones.Hips){maxHip=Mathf.Max(maxHip,delta.magnitude);Assert.That(delta.magnitude,Is.LessThan(.001f),"Do not add running pelvis crouch");}
                        if(!grounded)Assert.That(delta.magnitude,Is.LessThan(.001f),"Authored run trajectory: "+bone);
                        if(bone==HumanBodyBones.LeftFoot||bone==HumanBodyBones.RightFoot)
                        {
                            maxHorizontal=Mathf.Max(maxHorizontal,Vector3.ProjectOnPlane(delta,Vector3.up).magnitude);
                            Assert.That(Vector3.ProjectOnPlane(delta,Vector3.up).magnitude,Is.LessThan(.003f),"Do not replace running foot path");
                            Assert.That(Quaternion.Angle(actual.GetBoneTransform(bone).rotation,source.GetBoneTransform(bone).rotation),Is.LessThan(.1f),"Keep authored foot roll");
                        }
                    }
                    if(frame==360)Assert.That(phase-startPhase,Is.EqualTo(4).Within(.01f),"Five seconds must contain four original 1.25s cycles, not 2.2x playback");
                }
                Debug.Log($"RESTORED_RUN grounded={grounded} maxHorizontal={maxHorizontal} maxHip={maxHip}");
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(reference);Object.DestroyImmediate(floor);}
        }
        [Test]
        public void LoopBoundaryKeepsFootVelocityContinuous()
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            var source=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Raccoon/Animations/Raccoon_ShotgunRun_HYBRID_v8.anim");
            var improved=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/Art/Characters/Raccoon/PlayableCandidate_v26/Raccoon_ShotgunRun_LOOP_v27.anim");
            AnimatorOverrideController controller=null;
            try
            {
                foreach(var behaviour in root.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                var animator=root.GetComponentInChildren<Animator>();
                controller=Object.Instantiate((AnimatorOverrideController)animator.runtimeAnimatorController);
                animator.runtimeAnimatorController=controller;
                float Measure(AnimationClip clip)
                {
                    controller["Raccoon_ShotgunRun_AUTHORED_v7"]=clip;
                    animator.Rebind();animator.Update(0);animator.SetFloat("Speed",7);animator.SetFloat("RaccoonShotgunRun",1);animator.SetLayerWeight(animator.GetLayerIndex("Hold"),0);
                    const float dt=.001f;
                    var points=new Vector3[3,3];
                    var joints=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot};
                    for(int i=0;i<3;i++)
                    {
                        animator.Play("Locomotion",0,Mathf.Repeat((i-1)*dt/clip.length,1));animator.Update(0);
                        for(int j=0;j<3;j++)points[i,j]=animator.GetBoneTransform(joints[j]).position;
                    }
                    float peak=0;
                    for(int j=0;j<3;j++)peak=Mathf.Max(peak,((points[2,j]-points[1,j])-(points[1,j]-points[0,j])).magnitude/dt);
                    return peak;
                }
                float before=Measure(source),after=Measure(improved);
                Assert.That(after,Is.LessThan(before*.2f),"Match incoming and outgoing motion, not just boundary pose");
                Assert.That(after,Is.LessThan(.15f),"No abrupt foot velocity reversal at wrap");
                Assert.That(improved.length,Is.EqualTo(source.length).Within(.0001f));
                Debug.Log($"LOOP_SEAM velocityJumpBefore={before} after={after}");
            }
            finally{Object.DestroyImmediate(root);if(controller!=null)Object.DestroyImmediate(controller);}
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void WalkRunStopTransitionsKeepSolesAndGripStable(int fps)
        {
            Exercise(fps,false);
        }
        [Test]
        public void WalkingAcrossInclinedGroundUsesTheRuntimeCandidate()
        {
            Exercise(60,true);
        }
        private void Exercise(int fps,bool slope)
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,slope?0:-.5f,30);
            floor.transform.localScale=new Vector3(20,1,100);if(slope)floor.transform.rotation=Quaternion.Euler(-12,0,0);
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));root.transform.position=new Vector3(1000,slope?-5.8f:.1f,0);
            var lab=root.GetComponent<RaccoonSandboxV26>();lab.enabled=false;
            var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
            try
            {
                Physics.SyncTransforms();lab.Initialize();var animator=lab.Rig.Animator;
                var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                var soles=new[]{"Left","Right"}.Select(side=>{
                    var candidates=Enumerable.Range(0,vertices.Length).Where(i=>{
                        var w=weights[i];float W(int j,float value)=>skin.bones[j].name==side+"Foot"||skin.bones[j].name==side+"Toes"?value:0;
                        return W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3)>.8f;
                    }).ToArray();float min=candidates.Min(i=>vertices[i].y);return candidates.Where(i=>vertices[i].y<min+.025f).ToArray();
                }).ToArray();
                var equipment=root.GetComponentInChildren<RaccoonEquipmentPose>();equipment.Profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
                var stopped=new Vector3[2];var previous=new Vector3[2];int contacts=0;float minClear=999,maxGrip=0,maxStep=0;
                var clock=new System.Diagnostics.Stopwatch();int ticks=0;
                for(int frame=0;frame<fps*20;frame++)
                {
                    float t=frame/(float)fps;bool moving=t>=1&&t<11||t>=14&&t<18;
                    bool running=!slope&&t>=4&&t<7;
                    Vector2 input=moving?Vector2.up:Vector2.zero;
                    if(!slope&&t>=15&&t<17)input=Vector2.right;
                    lab.Advance(1f/fps,input,running);clock.Start();lab.Evaluate(1f/fps);clock.Stop();ticks++;
                    skin.BakeMesh(baked,true);var points=baked.vertices;
                    for(int side=0;side<2;side++)
                    {
                        var transformed=soles[side].Select(i=>skin.transform.TransformPoint(points[i])).ToArray();
                        float clear=transformed.Min(p=>Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,5,~(1<<2))?p.y-hit.point.y:999);
                        Vector3 centre=transformed.Aggregate(Vector3.zero,(a,b)=>a+b)/transformed.Length;
                        if(t>2)
                        {
                            minClear=Mathf.Min(minClear,clear);if(clear<.035f)contacts++;
                            Assert.That(clear,Is.GreaterThan(-.007f),$"Rendered sole below terrain, t={t:F3}, side={side}");
                            maxStep=Mathf.Max(maxStep,Vector3.Distance(centre,previous[side])*fps);
                        }
                        previous[side]=centre;
                        var foot=animator.GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                        if(frame==fps*13)stopped[side]=foot.position;
                        if(t>13&&t<14)Assert.That(Vector3.Distance(stopped[side],foot.position),Is.LessThan(.002f),"Settled feet must remain still");
                    }
                    var left=animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                    var right=animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                    float grip=(left-right-root.transform.rotation*(entry.Left.Position-entry.Right.Position)).magnitude;
                    if(t>1){maxGrip=Mathf.Max(maxGrip,grip);Assert.That(grip,Is.LessThan(.003f),"The support hand must stay on the weapon");}
                }
                Assert.That(contacts,Is.GreaterThan(fps*4),"Do not pass clearance by holding feet airborne");
                Assert.That(maxStep,Is.LessThan(30),"No sudden foot teleport in transitions");
                Debug.Log($"CANDIDATE_V26 fps={fps} slope={slope} minSole={minClear} maxGrip={maxGrip} maxFootSpeed={maxStep} meanPoseMs={clock.Elapsed.TotalMilliseconds/ticks:F3} contacts={contacts}");
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        }
#endif
    }
}
