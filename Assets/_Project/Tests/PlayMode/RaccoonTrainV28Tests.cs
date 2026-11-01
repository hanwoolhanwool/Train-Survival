using System.Linq;
using Game.Gameplay.Debugging;
using Game.Gameplay.Player;
using Game.Gameplay.Inventory;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonTrainV28Tests
    {
#if UNITY_EDITOR
        [Test]
        public void StationaryDeckKeepsApprovedV27Motion()
        {
            var deck=new GameObject("Static deck").AddComponent<RaccoonMovingDeckV28>();deck.Origin=new Vector3(200,3,0);deck.Sample(0,false);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.SetParent(deck.transform,false);floor.transform.localPosition=Vector3.down*.5f;floor.transform.localScale=new Vector3(20,1,100);
            var referenceFloor=GameObject.CreatePrimitive(PrimitiveType.Cube);referenceFloor.transform.position=new Vector3(300,2.5f,0);referenceFloor.transform.localScale=floor.transform.localScale;
            var train=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/TrainCandidate_v28/PC_Raccoon_Train_v28.prefab"));
            var reference=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/PlayableCandidate_v26/PC_Raccoon_Candidate_v26.prefab"));
            try
            {
                var a=train.GetComponent<RaccoonTrainMotorV28>();a.ResetAt(new Vector3(200,3.03f,0),Quaternion.identity,deck.transform);
                var b=reference.GetComponent<RaccoonSandboxV26>();b.enabled=false;b.ResetAt(new Vector3(300,3.03f,0));Physics.SyncTransforms();
                var joints=new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand};
                float maximum=0;
                for(int i=0;i<600;i++)
                {
                    float t=i/60f;var input=t<1||t>8?Vector2.zero:Vector2.up;bool run=t>3&&t<6;
                    a.Advance(1f/60,input,run);a.Evaluate(1f/60);b.Advance(1f/60,input,run);b.Evaluate(1f/60);
                    foreach(var joint in joints)
                    {
                        var p=train.transform.InverseTransformPoint(a.Rig.Animator.GetBoneTransform(joint).position);
                        var q=reference.transform.InverseTransformPoint(b.Rig.Animator.GetBoneTransform(joint).position);
                        maximum=Mathf.Max(maximum,Vector3.Distance(p,q));
                    }
                }
                Assert.That(maximum,Is.LessThan(.002f),"Stationary-frame handling must preserve the accepted animation");
                Debug.Log($"TRAIN_V28 approvedPoseMaxDifference={maximum}");
            }
            finally{Object.DestroyImmediate(train);Object.DestroyImmediate(reference);Object.DestroyImmediate(deck.gameObject);Object.DestroyImmediate(referenceFloor);}
        }
        [TestCase(30,false)] [TestCase(60,false)] [TestCase(120,false)]
        [TestCase(30,true)] [TestCase(60,true)] [TestCase(120,true)]
        public void DeckMotionDoesNotBecomeLocomotionOrDetachFeet(int fps,bool locomotion)
        {
            var deck=new GameObject("Test moving deck").AddComponent<RaccoonMovingDeckV28>();deck.Origin=new Vector3(200,3,0);
            var surface=GameObject.CreatePrimitive(PrimitiveType.Cube);surface.transform.SetParent(deck.transform,false);surface.transform.localPosition=Vector3.down*.5f;surface.transform.localScale=new Vector3(20,1,100);
            deck.Sample(0,true);
            var player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/TrainCandidate_v28/PC_Raccoon_Train_v28.prefab"));
            var motor=player.GetComponent<RaccoonTrainMotorV28>();var baked=new Mesh();
            try
            {
                motor.ResetAt(deck.transform.TransformPoint(new Vector3(0,.03f,-9)),Quaternion.identity,deck.transform);Physics.SyncTransforms();
                var skin=player.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=skin.sharedMesh;var w=mesh.boneWeights;var vertices=mesh.vertices;
                var samples=Enumerable.Range(0,vertices.Length).Where(i=>{
                    float W(int b,float x)=>skin.bones[b].name.EndsWith("Foot")||skin.bones[b].name.EndsWith("Toes")?x:0;
                    return W(w[i].boneIndex0,w[i].weight0)+W(w[i].boneIndex1,w[i].weight1)+W(w[i].boneIndex2,w[i].weight2)+W(w[i].boneIndex3,w[i].weight3)>.8f;
                }).ToArray();float lowest=samples.Min(i=>vertices[i].y);samples=samples.Where(i=>vertices[i].y<lowest+.025f).ToArray();
                var a=motor.Rig.Animator;var feet=new[]{a.GetBoneTransform(HumanBodyBones.LeftFoot),a.GetBoneTransform(HumanBodyBones.RightFoot)};
                var fixedFeet=new Vector3[2];Vector3 fixedRoot=default;float maxFootDrift=0,maxRootDrift=0,minClear=999,maxGrip=0,maxIdleSpeed=0;int contactFrames=0,runFrames=0;
                var equipment=player.GetComponentInChildren<RaccoonEquipmentPose>();equipment.Profile.TryGetEntry(HotbarItemType.Shotgun,out var grip);
                for(int frame=0;frame<fps*24;frame++)
                {
                    float t=frame/(float)fps;deck.Sample(t,true);Physics.SyncTransforms();
                    bool moving=locomotion&&t>=5&&t<17;bool run=moving&&t>=9&&t<12;
                    motor.Advance(1f/fps,moving?Vector2.up:Vector2.zero,run);motor.Evaluate(1f/fps);
                    if(frame==fps*2){fixedRoot=deck.transform.InverseTransformPoint(player.transform.position);for(int j=0;j<2;j++)fixedFeet[j]=deck.transform.InverseTransformPoint(feet[j].position);}
                    if(t>2&&(!locomotion||t<4.5f))
                    {
                        maxIdleSpeed=Mathf.Max(maxIdleSpeed,motor.RelativeSpeed);
                        maxRootDrift=Mathf.Max(maxRootDrift,Vector3.ProjectOnPlane(deck.transform.InverseTransformPoint(player.transform.position)-fixedRoot,Vector3.up).magnitude);
                        for(int j=0;j<2;j++)maxFootDrift=Mathf.Max(maxFootDrift,Vector3.Distance(deck.transform.InverseTransformPoint(feet[j].position),fixedFeet[j]));
                        Assert.That(motor.Rig.RunBlend,Is.LessThan(.001f),"Riding the train is not running");
                    }
                    if(motor.Rig.RunBlend>.99f)runFrames++;
                    if(t<2)continue;
                    skin.BakeMesh(baked,true);var points=baked.vertices;
                    float clearance=samples.Min(i=>deck.transform.InverseTransformPoint(skin.transform.TransformPoint(points[i])).y);minClear=Mathf.Min(minClear,clearance);if(clearance<.035f)contactFrames++;
                    Vector3 pair=a.GetBoneTransform(HumanBodyBones.LeftHand).position-a.GetBoneTransform(HumanBodyBones.RightHand).position;
                    maxGrip=Mathf.Max(maxGrip,(pair-player.transform.rotation*(grip.Left.Position-grip.Right.Position)).magnitude);
                }
                Assert.That(maxIdleSpeed,Is.LessThan(.025f));Assert.That(maxRootDrift,Is.LessThan(.01f));Assert.That(maxFootDrift,Is.LessThan(.008f));
                Assert.That(minClear,Is.GreaterThan(-.007f));Assert.That(maxGrip,Is.LessThan(.003f));Assert.That(contactFrames,Is.GreaterThan(fps*8));
                Assert.That(runFrames,locomotion?Is.GreaterThan(fps):Is.EqualTo(0));
                Debug.Log($"TRAIN_V28 fps={fps} movement={locomotion} rootDrift={maxRootDrift} footDrift={maxFootDrift} idleSpeed={maxIdleSpeed} sole={minClear} grip={maxGrip} contacts={contactFrames}");
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(player);Object.DestroyImmediate(deck.gameObject);}
        }
#endif
    }
}
