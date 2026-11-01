using System.Linq;
using Game.Gameplay.Debugging;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonDirectionalV29Tests
    {
#if UNITY_EDITOR
        private const string Folder="Assets/_Project/Art/Characters/Raccoon/DirectionalCandidate_v29";
        private static GameObject Spawn()=>Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/PC_Raccoon_Directional_v29.prefab"));
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void DirectionChangesKeepAimFeetAndGripStable(int fps)
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(60,1,60);
            var root=Spawn();root.transform.position=new Vector3(1000,.03f,0);var lab=root.GetComponent<RaccoonDirectionalMotorV29>();lab.enabled=false;
            var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
            try
            {
                Physics.SyncTransforms();lab.Initialize();var animator=lab.Rig.Animator;var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;
                var soles=new[]{"Left","Right"}.Select(side=>{
                    var candidates=Enumerable.Range(0,vertices.Length).Where(i=>{
                        var w=weights[i];float W(int j,float v)=>skin.bones[j].name==side+"Foot"||skin.bones[j].name==side+"Toes"?v:0;
                        return W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3)>.8f;
                    }).ToArray();float min=candidates.Min(i=>vertices[i].y);return candidates.Where(i=>vertices[i].y<min+.025f).ToArray();
                }).ToArray();
                var equipment=root.GetComponentInChildren<RaccoonEquipmentPose>();equipment.Profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
                float minSole=999,maxGrip=0,minSeparation=999,maxFootSpeed=0,maxIdleDrift=0;int contacts=0;
                var previous=new Vector3[2];var stopped=new Vector3[2];
                for(int frame=0;frame<fps*23;frame++)
                {
                    float t=frame/(float)fps;
                    Vector2 input=t<1?Vector2.zero:t<4?Vector2.left:t<7?Vector2.right:t<10?Vector2.down:t<13?new Vector2(-1,-1).normalized:t<21?Vector2.up:Vector2.zero;
                    lab.Advance(1f/fps,input,t>=16&&t<19);lab.Evaluate(1f/fps);
                    Assert.That(Quaternion.Angle(root.transform.rotation,Quaternion.identity),Is.LessThan(.01f),"Strafing must not turn the aiming body");
                    if(t>1.5f&&t<3.5f)Assert.That(lab.ActualVelocity.x,Is.LessThan(-.4f));
                    if(t>7.5f&&t<9.5f)Assert.That(lab.ActualVelocity.y,Is.LessThan(-.5f));
                    skin.BakeMesh(baked,true);var points=baked.vertices;
                    for(int side=0;side<2;side++)
                    {
                        var transformed=soles[side].Select(i=>skin.transform.TransformPoint(points[i])).ToArray();
                        float clear=transformed.Min(p=>p.y);Vector3 centre=transformed.Aggregate(Vector3.zero,(a,b)=>a+b)/transformed.Length;
                        if(t>1)
                        {
                            minSole=Mathf.Min(minSole,clear);if(clear<.035f)contacts++;
                            Assert.That(clear,Is.GreaterThan(-.007f),$"Sole penetration at {t} side {side}");
                            maxFootSpeed=Mathf.Max(maxFootSpeed,Vector3.Distance(previous[side],centre)*fps);
                        }
                        if(frame==22*fps)stopped[side]=centre;
                        if(t>22)maxIdleDrift=Mathf.Max(maxIdleDrift,Vector3.Distance(stopped[side],centre));
                        previous[side]=centre;
                    }
                    var left=animator.GetBoneTransform(HumanBodyBones.LeftFoot);var right=animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    float separation=root.transform.InverseTransformPoint(right.position).x-root.transform.InverseTransformPoint(left.position).x;
                    minSeparation=Mathf.Min(minSeparation,separation);
                    Assert.That(separation,Is.GreaterThan(.12f),$"Feet must not cross when changing sideways direction: t={t}, velocity={lab.ActualVelocity}");
                    float grip=(animator.GetBoneTransform(HumanBodyBones.LeftHand).position-animator.GetBoneTransform(HumanBodyBones.RightHand).position-root.transform.rotation*(entry.Left.Position-entry.Right.Position)).magnitude;
                    if(t>1){maxGrip=Mathf.Max(maxGrip,grip);Assert.That(grip,Is.LessThan(.003f));}
                }
                Assert.That(contacts,Is.GreaterThan(fps*8),"Clearance alone must not pass a floating gait");
                Assert.That(maxFootSpeed,Is.LessThan(12),"No foot teleport across direction changes");
                Assert.That(maxIdleDrift,Is.LessThan(.003f),"Feet must settle after directional movement");
                Debug.Log($"DIRECTIONAL_V29 fps={fps} minSole={minSole} contacts={contacts} minSeparation={minSeparation} maxGrip={maxGrip} maxFootSpeed={maxFootSpeed} idleDrift={maxIdleDrift}");
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        }
        [Test]
        public void ForwardWalkAndRunPreserveApprovedPoses()
        {
            var root=Spawn();var old=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/PlayableCandidate_v26/PC_Raccoon_Candidate_v26.prefab"));
            try
            {
                root.GetComponent<RaccoonDirectionalMotorV29>().enabled=false;old.GetComponent<RaccoonSandboxV26>().enabled=false;
                var current=root.GetComponentInChildren<RaccoonDirectionalRigV29>();var previous=old.GetComponentInChildren<RaccoonCandidateRigV26>();current.Initialize(root.transform);previous.Initialize(old.transform);
                float peak=0;
                for(int i=0;i<600;i++)
                {
                    float speed=i<180?.8f:i<420?2.5f:.8f;
                    current.Tick(1f/60,Vector2.up*speed,speed>1,false);previous.Tick(1f/60,speed,false);
                    foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.Head,HumanBodyBones.LeftHand})
                        peak=Mathf.Max(peak,Vector3.Distance(current.Animator.GetBoneTransform(bone).position,previous.Animator.GetBoneTransform(bone).position));
                }
                Assert.That(peak,Is.LessThan(.001f),"Adding directional clips must not alter approved forward animation");Debug.Log("DIRECTIONAL_V29_FORWARD maxDifference="+peak);
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(old);}
        }
        [TestCase("StrafeLeft")] [TestCase("StrafeRight")] [TestCase("WalkBackward")]
        public void AuthoredDirectionalClipsHaveContinuousLoopCurves(string name)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/Raccoon_"+name+"_v29.anim");
            Assert.That(clip.humanMotion,Is.True);Assert.That(clip.length,Is.EqualTo(7f/3).Within(.0001f));
            foreach(var binding in AnimationUtility.GetCurveBindings(clip))
            {
                var keys=AnimationUtility.GetEditorCurve(clip,binding).keys;
                Assert.That(keys.Last().value,Is.EqualTo(keys[0].value).Within(.00001f),binding.propertyName);
                Assert.That(keys.Last().inTangent,Is.EqualTo(keys[0].outTangent).Within(.00001f),binding.propertyName);
            }
        }
#endif
    }
}
