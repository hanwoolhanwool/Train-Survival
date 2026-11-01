using System.Linq;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonFootworkTests
    {
#if UNITY_EDITOR
        [TestCase(4.5f)]
        [TestCase(7f)]
        public void SoleContactsStayStableWhileTheCharacterActuallyTranslates(float speed)
        {
            var root=new GameObject("Footwork moving root");root.AddComponent<CharacterController>();
            var character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/CH_Raccoon_Player.prefab"),root.transform);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,30);floor.transform.localScale=new Vector3(20,1,100);
            var baked=new Mesh();
            try
            {
                var animator=character.GetComponent<Animator>();var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();
                var grounding=character.GetComponent<RaccoonGrounding>();grounding.Bind();
                var rest=skin.sharedMesh.vertices;var weights=skin.sharedMesh.boneWeights;
                var soles=new[]{"Left","Right"}.Select(side=>
                {
                    var candidates=Enumerable.Range(0,rest.Length).Where(i=>
                    {
                        var w=weights[i];float W(int j,float v)=>skin.bones[j].name==side+"Foot"||skin.bones[j].name==side+"Toes"?v:0;
                        return W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3)>.8f;
                    }).ToArray();
                    float minimum=candidates.Min(i=>skin.transform.TransformPoint(rest[i]).y);
                    return candidates.Where(i=>skin.transform.TransformPoint(rest[i]).y<minimum+.025f).ToArray();
                }).ToArray();
                animator.Rebind();animator.Update(0);animator.SetFloat("RaccoonShotgunRun",1);animator.SetLayerWeight(animator.GetLayerIndex("Hold"),0);
                Physics.SyncTransforms();
                var previous=new Vector3[2];var previousClear=new[]{999f,999f};float totalSlip=0;int contacts=0;
                for(int frame=-60;frame<300;frame++)
                {
                    float moving=frame<0?0:speed;root.transform.position+=Vector3.forward*(moving/60);
                    animator.SetFloat("Speed",moving);animator.Update(1f/60);grounding.Step(1f/60,true);
                    skin.BakeMesh(baked,true);var points=baked.vertices;
                    for(int side=0;side<2;side++)
                    {
                        var vertices=soles[side].Select(i=>skin.transform.TransformPoint(points[i])).ToArray();
                        float clear=vertices.Min(v=>v.y);Vector3 centre=vertices.Aggregate(Vector3.zero,(a,b)=>a+b)/vertices.Length;
                        if(frame>=60)
                        {
                            Assert.That(clear,Is.GreaterThan(-.006f),"Rendered sole must not penetrate the flat floor");
                            if(clear<.035f && previousClear[side]<.035f)
                            {totalSlip+=Vector3.ProjectOnPlane(centre-previous[side],Vector3.up).magnitude*60;contacts++;}
                        }
                        previous[side]=centre;previousClear[side]=clear;
                    }
                }
                Assert.That(contacts,Is.GreaterThan(speed<5?120:60),"A lower slip score must not be achieved by keeping feet airborne");
                Assert.That(totalSlip/Mathf.Max(1,contacts),Is.LessThan(1.5f),"Mean visible sole slide during real translation, m/s");
                // Changing the stance fraction must never snap a swinging foot onto the ground.
                float otherSpeed=speed>5?4.5f:7f;
                for(int frame=0;frame<120;frame++)
                {
                    root.transform.position+=Vector3.forward*(otherSpeed/60);
                    animator.SetFloat("Speed",otherSpeed);animator.Update(1f/60);grounding.Step(1f/60,true);
                    skin.BakeMesh(baked,true);var points=baked.vertices;
                    for(int side=0;side<2;side++)
                    {
                        var vertices=soles[side].Select(i=>skin.transform.TransformPoint(points[i])).ToArray();
                        float clear=vertices.Min(v=>v.y);Vector3 centre=vertices.Aggregate(Vector3.zero,(a,b)=>a+b)/vertices.Length;
                        Assert.That(clear,Is.GreaterThan(-.006f));
                        if(clear<.035f && previousClear[side]<.035f)
                            Assert.That(Vector3.ProjectOnPlane(centre-previous[side],Vector3.up).magnitude*60,Is.LessThan(12f),"No contact teleport during walk/run transition");
                        previous[side]=centre;previousClear[side]=clear;
                    }
                }
                var stopped=new Vector3[2];
                for(int frame=0;frame<180;frame++)
                {
                    animator.SetFloat("Speed",0);animator.Update(1f/60);grounding.Step(1f/60,true);
                    for(int side=0;side<2;side++)
                    {
                        var foot=animator.GetBoneTransform(side==0?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                        if(frame==90)stopped[side]=foot.position;
                        if(frame>90)Assert.That(Vector3.Distance(stopped[side],foot.position),Is.LessThan(.001f),"Settled idle must not shuffle");
                    }
                }
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        }
#endif
    }
}
