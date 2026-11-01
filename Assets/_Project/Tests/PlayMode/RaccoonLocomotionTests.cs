using System.Linq;
using Game.Gameplay.Inventory;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonLocomotionTests
    {
#if UNITY_EDITOR
        private const string Character="Assets/_Project/Art/Characters/Raccoon/CH_Raccoon_Player.prefab";
        [Test]
        public void AuthoredIdleKeepsFeetPlantedWhileChestBreathes()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Character));
            try
            {
                var a=go.GetComponent<Animator>();a.Rebind();a.Update(0);a.SetFloat("Speed",0);
                a.SetLayerWeight(a.GetLayerIndex("Hold"),0);a.Play("Locomotion",0,0);a.Update(0);
                var feet=new[]{a.GetBoneTransform(HumanBodyBones.LeftFoot),a.GetBoneTransform(HumanBodyBones.RightFoot)};
                var starts=feet.Select(f=>f.position).ToArray();var chest=a.GetBoneTransform(HumanBodyBones.Chest);
                var chestStart=chest.rotation;float displacement=0,breath=0;
                for(int frame=0;frame<480;frame++)
                {
                    a.Update(1f/60);
                    for(int side=0;side<2;side++)displacement=Mathf.Max(displacement,Vector3.Distance(starts[side],feet[side].position));
                    breath=Mathf.Max(breath,Quaternion.Angle(chestStart,chest.rotation));
                }
                Assert.That(displacement,Is.LessThan(.001f),"Stationary idle feet must not drift over two loops");
                Assert.That(breath,Is.GreaterThan(.1f),"Upper body must retain a visible subtle breath");
                Assert.That(breath,Is.LessThan(2f));
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test]
        public void SupportingFootAlignsToFloorAndSwingFootIsReleased()
        {
            var root=new GameObject("Sole alignment root");root.AddComponent<CharacterController>();
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Character),root.transform);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(4,1,4);
            try
            {
                var a=go.GetComponent<Animator>();var foot=a.GetBoneTransform(HumanBodyBones.LeftFoot);
                var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();int index=System.Array.IndexOf(skin.bones,foot);
                var rest=skin.transform.localToWorldMatrix*skin.sharedMesh.bindposes[index].inverse;
                var normal=Quaternion.Inverse(rest.rotation)*Vector3.up;
                a.Rebind();a.Update(0);a.SetFloat("Speed",0);a.Play("Locomotion",0,0);a.Update(0);
                var grounding=go.GetComponent<RaccoonGrounding>();grounding.Bind();Physics.SyncTransforms();
                foot.rotation=Quaternion.AngleAxis(12,Vector3.forward)*foot.rotation;
                grounding.Step(1f/60,true);
                Assert.That(Vector3.Angle(foot.rotation*normal,Vector3.up),Is.LessThan(2f),"Planted sole should be level");
                var rotation=foot.rotation;grounding.Step(1f/60,false);
                Assert.That(Quaternion.Angle(rotation,foot.rotation),Is.LessThan(.01f),"Airborne foot must be released");
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        }
        [Test]
        public void ShotgunGripRemainsClosedAcrossWalkingRunningAndAbruptStop()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Character));
            try
            {
                var a=go.GetComponent<Animator>();a.Rebind();a.Update(0);a.SetFloat("RaccoonShotgunRun",1);a.SetInteger("HoldPose",2);
                var pose=go.GetComponent<RaccoonEquipmentPose>();pose.Profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
                float speed=0,gap=0,step=0;Vector3 previous=Vector3.zero;
                for(int i=-120;i<600;i++)
                {
                    float target=i<0?0:i<180?4.5f:i<360?7:0;
                    speed=PlayerAnimationMath.SmoothTowards(speed,target,.15f,1f/60);a.SetFloat("Speed",speed);a.SetLayerWeight(a.GetLayerIndex("Hold"),1-pose.Profile.AuthoredRunWeight(a));a.Update(1f/60);
                    pose.Apply(HotbarItemType.Shotgun,false,0,1f/60,go.transform);
                    var right=a.GetBoneTransform(HumanBodyBones.RightHand).position;var left=a.GetBoneTransform(HumanBodyBones.LeftHand).position;
                    if(i>=0){gap=Mathf.Max(gap,(left-right-(entry.Left.Position-entry.Right.Position)).magnitude);step=Mathf.Max(step,Vector3.Distance(previous,right));}previous=right;
                }
                Assert.That(gap,Is.LessThan(.005f),"Two-hand grip separation");Assert.That(step,Is.LessThan(.025f),"Wrist transition jump");
            }
            finally{Object.DestroyImmediate(go);}
        }
        [Test]
        public void WalkingHasLevelSupportingFeetWithoutPersistentHover()
        {
            var root=new GameObject("Walking contact root");root.AddComponent<CharacterController>();
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Character),root.transform);
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.5f,0);floor.transform.localScale=new Vector3(4,1,4);
            var baked=new Mesh();
            try
            {
                var a=go.GetComponent<Animator>();a.Rebind();a.Update(0);a.SetFloat("Speed",4.5f);a.SetLayerWeight(a.GetLayerIndex("Hold"),0);
                var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var weights=skin.sharedMesh.boneWeights;
                var feet=new[]{a.GetBoneTransform(HumanBodyBones.LeftFoot),a.GetBoneTransform(HumanBodyBones.RightFoot)};
                var normals=feet.Select(f=>Quaternion.Inverse((skin.transform.localToWorldMatrix*skin.sharedMesh.bindposes[System.Array.IndexOf(skin.bones,f)].inverse).rotation)*Vector3.up).ToArray();
                var ids=new[]{"Left","Right"}.Select(side=>Enumerable.Range(0,weights.Length).Where(i=>{
                    var w=weights[i];float W(int j,float v)=>skin.bones[j].name==side+"Foot"||skin.bones[j].name==side+"Toes"?v:0;
                    return W(w.boneIndex0,w.weight0)+W(w.boneIndex1,w.weight1)+W(w.boneIndex2,w.weight2)+W(w.boneIndex3,w.weight3)>.8f;
                }).ToArray()).ToArray();
                var grounding=go.GetComponent<RaccoonGrounding>();grounding.Bind();Physics.SyncTransforms();int supported=0,level=0,total=0;float minimum=999,maximum=-999;
                for(int frame=0;frame<240;frame++)
                {
                    // Include the FBX renderer's scale in the bake's inverse transform before converting to world space.
                    a.Update(1f/60);grounding.Step(1f/60,true);skin.BakeMesh(baked,true);var vertices=baked.vertices;
                    if(frame<60)continue;bool hasSupport=false;
                    for(int side=0;side<2;side++)
                    {
                        float clearance=ids[side].Min(i=>skin.transform.TransformPoint(vertices[i]).y);
                        minimum=Mathf.Min(minimum,clearance);maximum=Mathf.Max(maximum,clearance);
                        Assert.That(clearance,Is.GreaterThan(-.004f),"Walking foot must not penetrate the flat floor");
                        if(clearance<.025f){hasSupport=true;total++;if(Vector3.Angle(feet[side].rotation*normals[side],Vector3.up)<6)level++;}
                    }
                    if(hasSupport)supported++;
                }
                Assert.That(supported,Is.GreaterThan(100),"Walking needs support for most frames, not persistent hovering; clearance range "+minimum+".."+maximum);
                Assert.That(level/(float)Mathf.Max(1,total),Is.GreaterThan(.8f),"Supporting soles must mostly align with the floor");
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        }
        [Test]
        public void GroundingClearsRaisedSupportAndLeavesRootAndHandsUnchanged()
        {
            var root=new GameObject("Grounding test root");root.AddComponent<CharacterController>();
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Character),root.transform);var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.transform.position=new Vector3(0,-.4f,0);ground.transform.localScale=new Vector3(4,1,4); // top .1 m
            try
            {
                var a=go.GetComponent<Animator>();a.Rebind();a.Update(0);a.SetFloat("Speed",0);a.Play("Locomotion",0,.2f);a.Update(0);
                var grounding=go.GetComponent<RaccoonGrounding>();Assert.That(grounding,Is.Not.Null);grounding.Bind();Physics.SyncTransforms();
                var rootPosition=root.transform.position;var hand=a.GetBoneTransform(HumanBodyBones.RightHand).position;
                var foot=a.GetBoneTransform(HumanBodyBones.LeftFoot);
                grounding.Step(1f/60,true);
                var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var baked=new Mesh();
                try
                {
                    skin.BakeMesh(baked,true);var vertices=baked.vertices;var weights=skin.sharedMesh.boneWeights;
                    float minimum=Enumerable.Range(0,vertices.Length).Where(i=>{
                        var w=weights[i];float Weight(int index,float value)=>skin.bones[index].name=="LeftFoot"||skin.bones[index].name=="LeftToes"?value:0;
                        return Weight(w.boneIndex0,w.weight0)+Weight(w.boneIndex1,w.weight1)+Weight(w.boneIndex2,w.weight2)+Weight(w.boneIndex3,w.weight3)>.8f;
                    }).Min(i=>skin.transform.TransformPoint(vertices[i]).y);
                    Assert.That(minimum,Is.GreaterThan(.095f),"Raised platform top is at 0.10 m");
                }
                finally{Object.DestroyImmediate(baked);}
                Assert.That(Vector3.Distance(rootPosition,root.transform.position),Is.LessThan(1e-6f));Assert.That(Vector3.Distance(hand,a.GetBoneTransform(HumanBodyBones.RightHand).position),Is.LessThan(1e-6f));
                a.Update(1f/60);var before=foot.position;grounding.Step(1f/60,false);Assert.That(Vector3.Distance(before,foot.position),Is.LessThan(1e-6f),"Airborne animation must remain free");
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(ground);}
        }
#endif
    }
}
