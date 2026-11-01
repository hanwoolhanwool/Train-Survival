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
    public sealed class RaccoonJumpV32Tests
    {
#if UNITY_EDITOR
        private const string Path="Assets/_Project/Art/Characters/Raccoon/JumpCandidate_v32/PC_Raccoon_Jump_v32.prefab";
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void JumpLandsOnCollisionAndPreservesGrip(int fps,bool running)
        {
            var floor=Box(new Vector3(1000,-.5f,15),new Vector3(40,1,60));var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            var skin=root.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
            try
            {
                var lab=root.GetComponent<RaccoonJumpMotorV32>();lab.enabled=false;lab.ResetAt(new Vector3(1000,.03f,0));Physics.SyncTransforms();
                var animator=lab.Rig.Animator;var equipment=root.GetComponentInChildren<RaccoonEquipmentPose>();equipment.Profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
                var weights=skin.sharedMesh.boneWeights;var vertices=skin.sharedMesh.vertices;
                var footIds=Enumerable.Range(0,vertices.Length).Where(i=>{
                    var w=weights[i];float W(int j,float value)=>skin.bones[j].name.EndsWith("Foot")||skin.bones[j].name.EndsWith("Toes")?value:0;
                    return W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3)>.8f;
                }).ToArray();
                float ground=0,peak=0,minSole=999,maxGrip=0,airSeconds=0,maxHeadRate=0;bool takeoff=false,air=false,landing=false;int framesOnFloor=0;
                var head=animator.GetBoneTransform(HumanBodyBones.Head);var lastHead=head.rotation;
                for(int frame=0;frame<fps*5;frame++)
                {
                    float t=frame/(float)fps;Vector2 input=running&&t<3?Vector2.up:Vector2.zero;
                    // Extra presses well before landing must not cause a midair second jump.
                    bool pressed=frame==fps||frame==fps+fps/4;
                    lab.Advance(1f/fps,input,running,pressed);lab.Evaluate(1f/fps);
                    if(t>.9f&&t<2.3f)maxHeadRate=Mathf.Max(maxHeadRate,Quaternion.Angle(lastHead,head.rotation)*fps);
                    lastHead=head.rotation;
                    if(frame==fps-1)ground=root.transform.position.y;
                    if(t>=1)peak=Mathf.Max(peak,root.transform.position.y-ground);
                    takeoff|=lab.State==RaccoonJumpMotorV32.JumpState.Takeoff;
                    air|=lab.State==RaccoonJumpMotorV32.JumpState.Airborne;
                    landing|=lab.State==RaccoonJumpMotorV32.JumpState.Landing;
                    if(t>1&&lab.State==RaccoonJumpMotorV32.JumpState.Airborne)airSeconds+=1f/fps;
                    float grip=(animator.GetBoneTransform(HumanBodyBones.LeftHand).position-animator.GetBoneTransform(HumanBodyBones.RightHand).position-root.transform.rotation*(entry.Left.Position-entry.Right.Position)).magnitude;
                    if(t>.5f){maxGrip=Mathf.Max(maxGrip,grip);Assert.That(grip,Is.LessThan(.003f),$"Grip lost at t={t}");}
                    if(t>.5f&&lab.Grounded)
                    {
                        skin.BakeMesh(baked,true);var points=baked.vertices;
                        float clearance=footIds.Min(i=>skin.transform.TransformPoint(points[i]).y);minSole=Mathf.Min(minSole,clearance);
                        Assert.That(clearance,Is.GreaterThan(-.007f),$"Landing foot penetration at {t}");
                        if(clearance<.04f)framesOnFloor++;
                    }
                }
                Assert.That(takeoff&&air&&landing,Is.True);Assert.That(lab.LaunchCount,Is.EqualTo(1));Assert.That(lab.LandingCount,Is.EqualTo(1));
                Assert.That(peak,Is.InRange(1.17f,1.23f));Assert.That(airSeconds,Is.InRange(.60f,.77f));
                Assert.That(lab.State,Is.EqualTo(RaccoonJumpMotorV32.JumpState.Grounded));Assert.That(lab.JumpWeight,Is.EqualTo(0));
                Assert.That(framesOnFloor,Is.GreaterThan(fps*2),"Landing must not leave the feet hovering");
                Assert.That(maxHeadRate,Is.LessThan(300),"Jump/landing must not reintroduce a one-frame head snap");
                Debug.Log($"JUMP_V32 fps={fps} running={running} peak={peak} airtime={airSeconds} minSole={minSole} maxGrip={maxGrip} maxHeadRate={maxHeadRate}");
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        }
        [Test]
        public void CeilingHitStopsAscentAndLedgeFallUsesLandingWithoutLaunch()
        {
            var floor=Box(new Vector3(1000,-.5f,0),new Vector3(30,1,30));var ceiling=Box(new Vector3(1000,2.3f,0),new Vector3(4,.2f,4));
            var ledge=Box(new Vector3(1005,.5f,0),new Vector3(2,1,2));var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
            try
            {
                var lab=root.GetComponent<RaccoonJumpMotorV32>();lab.enabled=false;lab.ResetAt(new Vector3(1000,.03f,0));Physics.SyncTransforms();float peak=0;
                for(int i=0;i<240;i++){lab.Advance(1f/60,Vector2.zero,false,i==60);lab.Evaluate(1f/60);peak=Mathf.Max(peak,root.transform.position.y);}
                Assert.That(peak,Is.LessThan(.5f));Assert.That(lab.LandingCount,Is.EqualTo(1));Assert.That(lab.Grounded,Is.True);
                lab.ResetAt(new Vector3(1005,1.03f,0));
                for(int i=0;i<420;i++){lab.Advance(1f/60,i>60?Vector2.up:Vector2.zero,false);lab.Evaluate(1f/60);}
                Assert.That(lab.LaunchCount,Is.Zero);Assert.That(lab.LandingCount,Is.EqualTo(1));Assert.That(lab.Grounded,Is.True);Assert.That(root.transform.position.y,Is.LessThan(.05f));
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(floor);Object.DestroyImmediate(ceiling);Object.DestroyImmediate(ledge);}
        }
        [Test]
        public void ZeroJumpWeightKeepsApprovedGroundMotion()
        {
            var current=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));var previous=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/DirectionalCandidate_v29/PC_Raccoon_Directional_v29.prefab"));
            try
            {
                current.GetComponent<RaccoonJumpMotorV32>().enabled=false;previous.GetComponent<RaccoonDirectionalMotorV29>().enabled=false;
                var a=current.GetComponentInChildren<RaccoonJumpRigV32>();var b=previous.GetComponentInChildren<RaccoonDirectionalRigV29>();a.Initialize(current.transform);b.Initialize(previous.transform);
                float peak=0;
                for(int i=0;i<600;i++)
                {
                    Vector2 v=i<120?Vector2.zero:i<240?Vector2.left*.45f:i<360?Vector2.down*.55f:i<480?Vector2.up*2.5f:Vector2.up*.8f;
                    a.Tick(1f/60,v,v.y>1,false);b.Tick(1f/60,v,v.y>1,false);
                    foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.Head,HumanBodyBones.LeftHand})peak=Mathf.Max(peak,Vector3.Distance(a.Animator.GetBoneTransform(bone).position,b.Animator.GetBoneTransform(bone).position));
                }
                Assert.That(peak,Is.LessThan(.001f));Debug.Log("JUMP_V32_GROUND maxDifference="+peak);
            }
            finally{Object.DestroyImmediate(current);Object.DestroyImmediate(previous);}
        }
        private static GameObject Box(Vector3 p,Vector3 size){var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.transform.position=p;b.transform.localScale=size;return b;}
#endif
    }
}
