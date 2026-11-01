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
    public sealed class RaccoonAuthoredRunTests
    {
#if UNITY_EDITOR
        private const string Art="Assets/_Project/Art/Characters/Raccoon/";
        private static GameObject Spawn(out Animator animator)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"CH_Raccoon_Player.prefab"));
            animator=go.GetComponent<Animator>();animator.Rebind();animator.Update(0);
            animator.SetFloat("Speed",7);animator.SetFloat("RaccoonShotgunRun",1);
            animator.SetLayerWeight(animator.GetLayerIndex("Hold"),0);return go;
        }
        private static void Sample(Animator animator,float phase)
        {animator.Play("Locomotion",0,phase);animator.Update(0);}

        [Test]
        public void NewRunIsSelectedOnlyByShotgunStyle()
        {
            var go=Spawn(out var animator);
            try
            {
                Sample(animator,.2f);
                Assert.That(animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.weight>.99f&&c.clip.name=="Raccoon_ShotgunRun_HYBRID_v8"),Is.True);
                var profile=go.GetComponent<RaccoonEquipmentPose>().Profile;
                Assert.That(profile.HeadPitchFor(animator),Is.EqualTo(0).Within(.001f));
                animator.SetFloat("RaccoonShotgunRun",0);Sample(animator,.2f);
                Assert.That(animator.GetCurrentAnimatorClipInfo(0).Where(c=>c.weight>.001f).All(c=>c.clip.name!="Raccoon_ShotgunRun_HYBRID_v8"),Is.True);
                Assert.That(profile.HeadPitchFor(animator),Is.EqualTo(profile.HeadNeutralPitch));
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test]
        public void ArchivedV7SourceCycleLoopsAndKeepsSolesAboveFloor()
        {
            var go=Spawn(out var animator);
            var original=(AnimatorOverrideController)animator.runtimeAnimatorController;
            var controller=Object.Instantiate(original);
            controller["Raccoon_ShotgunRun_AUTHORED_v7"]=AssetDatabase.LoadAssetAtPath<AnimationClip>(Art+"Animations/Raccoon_ShotgunRun_AUTHORED_v7.anim");
            animator.runtimeAnimatorController=controller;
            try
            {
                var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=skin.sharedMesh;
                var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;var bones=skin.bones;
                var ids=new[]{"Left","Right"}.Select(side=>Enumerable.Range(0,vertices.Length).Where(j=>{
                    var w=weights[j];return Weight(w.boneIndex0,w.weight0,side)+Weight(w.boneIndex1,w.weight1,side)+Weight(w.boneIndex2,w.weight2,side)+Weight(w.boneIndex3,w.weight3,side)>.65f;
                    float Weight(int index,float weight,string s)=>bones[index].name==s+"Foot"||bones[index].name==s+"Toes"?weight:0;
                }).ToArray()).ToArray();
                float min=999,maxContact=-999,minContact=999;
                Sample(animator,0);var start=bones.Select(b=>b.position).ToArray();
                for(int frame=0;frame<=60;frame++)
                {
                    float phase=frame/60f;Sample(animator,phase);
                    var matrices=bones.Select((b,j)=>b.localToWorldMatrix*bind[j]).ToArray();
                    for(int side=0;side<2;side++)
                    {
                        float sole=ids[side].Min(j=>{var w=weights[j];var v=vertices[j];return (matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3).y;});
                        min=Mathf.Min(min,sole);
                        if(Mathf.Repeat(phase+side*.5f,1)<.199f){maxContact=Mathf.Max(maxContact,sole);minContact=Mathf.Min(minContact,sole);}
                    }
                }
                Assert.That(min,Is.GreaterThan(-.008f),"Sole penetration");
                Assert.That(maxContact,Is.LessThan(.015f),"Floating support foot");
                Assert.That(minContact,Is.GreaterThan(-.008f));
                for(int i=0;i<bones.Length;i++)Assert.That(Vector3.Distance(start[i],bones[i].position),Is.LessThan(.001f),"Loop seam: "+bones[i].name);
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(controller);}
        }

        [Test]
        public void HybridPreservesLegacyLegMotionAndClosesTheLoop()
        {
            var a=Spawn(out var legacy);var b=Spawn(out var hybrid);
            try
            {
                legacy.SetFloat("RaccoonShotgunRun",0);
                var legs=new[]{HumanBodyBones.LeftUpperLeg,HumanBodyBones.LeftLowerLeg,HumanBodyBones.LeftFoot,HumanBodyBones.LeftToes,HumanBodyBones.RightUpperLeg,HumanBodyBones.RightLowerLeg,HumanBodyBones.RightFoot,HumanBodyBones.RightToes};
                // The final 0.12 s is intentionally smoothed to remove the legacy clip's end-pose jump.
                for(int i=0;i<=68;i++)
                {
                    Sample(legacy,i/75f);Sample(hybrid,i/75f);
                    var ah=legacy.GetBoneTransform(HumanBodyBones.Hips).position;var bh=hybrid.GetBoneTransform(HumanBodyBones.Hips).position;
                    foreach(var bone in legs)
                    {
                        Assert.That(Vector3.Distance(legacy.GetBoneTransform(bone).position-ah,hybrid.GetBoneTransform(bone).position-bh),Is.LessThan(.001f),bone+" trajectory");
                        // Sub-degree resampling differences are permitted; joint trajectories remain within 1 mm.
                        Assert.That(Quaternion.Angle(legacy.GetBoneTransform(bone).rotation,hybrid.GetBoneTransform(bone).rotation),Is.LessThan(.5f),bone+" orientation");
                    }
                }
                Sample(hybrid,0);var start=legs.Select(n=>hybrid.GetBoneTransform(n).position).ToArray();Sample(hybrid,.999999f);
                for(int i=0;i<legs.Length;i++)Assert.That(Vector3.Distance(start[i],hybrid.GetBoneTransform(legs[i]).position),Is.LessThan(.002f));
                Assert.That(hybrid.GetCurrentAnimatorClipInfo(0).Single(c=>c.weight>.99f).clip.length,Is.EqualTo(1.25f).Within(.001f));
            }
            finally{Object.DestroyImmediate(a);Object.DestroyImmediate(b);}
        }

        [Test]
        public void RunningTailHasVisibleTravelWithoutScalingItsBones()
        {
            var go=Spawn(out var animator);
            try
            {
                var tail=go.GetComponent<RaccoonTailMotion>();var hips=animator.GetBoneTransform(HumanBodyBones.Hips);
                var bones=go.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tail")).OrderBy(t=>t.name).ToArray();
                var scales=bones.Select(t=>t.localScale).ToArray();Vector3 minimum=Vector3.one*999,maximum=Vector3.one*-999;
                for(int i=-180;i<300;i++)
                {
                    animator.Update(1f/60);tail.Step(7,0,1f/60);
                    if(i<0)continue;
                    var tip=hips.InverseTransformDirection(bones[4].position-bones[0].position);
                    minimum=Vector3.Min(minimum,tip);maximum=Vector3.Max(maximum,tip);
                    for(int j=0;j<bones.Length;j++)Assert.That(Vector3.Distance(scales[j],bones[j].localScale),Is.LessThan(.00001f));
                }
                Assert.That((maximum-minimum).magnitude,Is.GreaterThan(.05f),"Tail movement must be visible at character scale");
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test]
        public void ShotgunGripRemainsStableThroughAuthoredRun()
        {
            var go=Spawn(out var animator);
            try
            {
                var pose=go.GetComponent<RaccoonEquipmentPose>();pose.Profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
                float error=0,step=0;Vector3 previous=Vector3.zero;
                for(int i=-120;i<240;i++)
                {
                    animator.Update(1f/120);pose.Apply(HotbarItemType.Shotgun,false,0,1f/120,go.transform);
                    var right=animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                    var left=animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                    if(i>=0){error=Mathf.Max(error,Vector3.Distance(left-right,entry.Left.Position-entry.Right.Position));step=Mathf.Max(step,Vector3.Distance(right,previous));}
                    previous=right;
                }
                Assert.That(error,Is.LessThan(.01f));Assert.That(step,Is.LessThan(.02f));
            }
            finally{Object.DestroyImmediate(go);}
        }
#endif
    }
}
