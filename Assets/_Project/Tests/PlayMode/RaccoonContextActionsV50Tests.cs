using System;
using System.Linq;
using Game.Gameplay.Debugging;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Context=Game.Gameplay.Debugging.RaccoonActionReviewV36.ReviewContext;
using Situation=Game.Gameplay.Debugging.RaccoonContextActionsV50.Situation;
using Motion=Game.Gameplay.Debugging.RaccoonContextActionsV50.Motion;
using Hands=Game.Gameplay.Debugging.RaccoonContextActionsV50.Hands;
using HandTask=Game.Gameplay.Debugging.RaccoonContextActionsV50.HandTask;

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonContextActionsV50Tests
    {
#if UNITY_EDITOR
        GameObject actor,owner;
        RaccoonContextActionsV50 d;
        [SetUp] public void Setup()
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/ActionsCandidate_v36/PC_Raccoon_Actions_v36.prefab");
            actor=UnityEngine.Object.Instantiate(prefab,new Vector3(1000,0,0),Quaternion.identity);
            foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            owner=new GameObject("v50 test");d=owner.AddComponent<RaccoonContextActionsV50>();d.enabled=false;
            d.SourcePrefab=prefab;d.Display=actor.GetComponent<RaccoonActionReviewV36>();d.Initialize();
        }
        [TearDown] public void Cleanup(){UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(actor);}
        void Advance(float duration,float rate=60){for(int i=0;i<Mathf.CeilToInt(duration*rate);i++)d.Step(1/rate);}
        const string SkinV59="Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_AnatomicalSkin_v59.asset";
        void SetupMacheteV74()
        {
            d.MachetePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Weapon_CartoonMachete_v74.prefab");
            Assert.That(d.MachetePrefab,Is.Not.Null);
            d.RebuiltSkin=d.PreviousRebuiltSkin=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_ShoulderSkin_v71.asset");
            d.ShoulderAimV71=d.ContextAimV72=d.ArmpitVolumeV73=true;
            d.Configure(Context.Machete,Situation.Ground,Motion.Idle);Advance(.7f);
        }
        [Test] public void MacheteV75MatchesHammerArmAndKeepsWristDuringAttack()
        {
            SetupMacheteV74();d.MacheteHammerGripV75=true;
            var bones=new[]{HumanBodyBones.RightShoulder,HumanBodyBones.RightUpperArm,HumanBodyBones.RightLowerArm,HumanBodyBones.RightHand};
            var transforms=bones.Select(d.OutputAnimator.GetBoneTransform).ToArray();
            var chest=d.OutputAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            foreach(var state in new[]{Situation.Ground,Situation.Surface,Situation.Underwater,Situation.Ladder,Situation.Grabbed,Situation.Carried})
            {
                d.Configure(Context.Hammer,state,Motion.Idle);d.AimYaw=30;Advance(.7f);
                var positions=transforms.Select(t=>t.position-chest.position).ToArray();var rotations=transforms.Select(t=>t.rotation).ToArray();
                d.Configure(Context.Machete,state,Motion.Idle);Advance(.7f);
                for(int i=0;i<transforms.Length;i++)
                {
                    Assert.That(Vector3.Distance(positions[i],transforms[i].position-chest.position),Is.LessThan(.0004f),state+" "+bones[i]);
                    Assert.That(Quaternion.Angle(rotations[i],transforms[i].rotation),Is.LessThan(.2f),state+" "+bones[i]);
                }
            }
            d.Configure(Context.Machete,Situation.Ground,Motion.Idle);d.AimYaw=0;Advance(.7f);
            var hand=transforms[3];var wrist=hand.localRotation;var lower=transforms[2].localRotation;var start=hand.position-chest.position;
            Assert.That(d.StartMacheteAttack(),Is.True);
            for(int i=0;i<80;i++)
            {
                d.Step(1f/60);
                Assert.That(Quaternion.Angle(wrist,hand.localRotation),Is.LessThan(.2f),"Independent wrist twist");
                Assert.That(Quaternion.Angle(lower,transforms[2].localRotation),Is.LessThan(.2f),"Elbow grip changed");
            }
            Assert.That(d.MacheteAttacking,Is.False);
            Assert.That(Vector3.Distance(start,hand.position-chest.position),Is.LessThan(.001f));
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void MacheteV74UsesCartoonAssetAndFollowsGripAcrossStates()
        {
            SetupMacheteV74();var model=d.MacheteModel;
            Assert.That(model.GetComponent<MeshFilter>().sharedMesh.bounds.size.y,Is.InRange(.68f,.69f));
            Assert.That(model.GetComponent<MeshRenderer>().sharedMaterial.GetTexture("_BaseMap").name,Is.EqualTo("T_Weapon_CartoonMachete_BaseColor"));
            var hand=d.OutputAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            Vector3 grip=hand.InverseTransformPoint(model.transform.position);
            foreach(var state in new[]{Situation.Ground,Situation.Surface,Situation.Underwater,Situation.Ladder,Situation.Grabbed,Situation.Carried})
            foreach(float yaw in new[]{-60f,0f,60f})
            {
                d.Configure(Context.Machete,state,Motion.Forward);d.AimYaw=yaw;Advance(.7f);
                Assert.That(model.activeSelf,Is.True);Assert.That(d.Display.Weapon.activeSelf,Is.False);
                Assert.That(d.Display.Props.All(p=>!p.activeSelf),Is.True,"Other equipment remains visible");
                Assert.That(Vector3.Distance(hand.TransformPoint(grip),model.transform.position),Is.LessThan(.0003f));
                Assert.That(d.HeadLookError,Is.LessThan(.2f));Assert.That(d.CanFire||d.CanReload,Is.False);
            }
            d.Configure(Context.Machete,Situation.Grabbed,Motion.Idle,Hands.Restrained);Advance(.5f);
            Assert.That(d.CanMacheteAttack,Is.False);Assert.That(d.StartMacheteAttack(),Is.False);
            d.Configure(Context.Shotgun,Situation.Ground,Motion.Idle);Advance(.5f);
            Assert.That(model.activeSelf,Is.False);Assert.That(d.Display.Weapon.activeSelf,Is.True);
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(30)] [TestCase(60)] public void MacheteV74AttackRecoversAndCanRepeat(int rate)
        {
            SetupMacheteV74();var hand=d.OutputAnimator.GetBoneTransform(HumanBodyBones.RightHand);
            var chest=d.OutputAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            var origin=hand.position-chest.position;var rotation=hand.rotation;
            Vector3 grip=hand.InverseTransformPoint(d.MacheteModel.transform.position);
            for(int repeat=0;repeat<2;repeat++)
            {
                Assert.That(d.StartMacheteAttack(),Is.True);Assert.That(d.StartMacheteAttack(),Is.False);
                Assert.That(d.CanStartTask,Is.False);var previous=hand.position;var previousRotation=hand.rotation;
                float largestMotion=0;
                for(int frame=0;frame<Mathf.CeilToInt(1.4f*rate);frame++)
                {
                    d.Step(1f/rate);
                    largestMotion=Mathf.Max(largestMotion,Vector3.Distance(origin,hand.position-chest.position));
                    Assert.That(Vector3.Distance(previous,hand.position),Is.LessThan(.18f),"Hand jumps during slash");
                    Assert.That(Quaternion.Angle(previousRotation,hand.rotation),Is.LessThan(65),"Wrist rotation jumps");
                    Assert.That(Vector3.Distance(hand.TransformPoint(grip),d.MacheteModel.transform.position),Is.LessThan(.0003f));
                    previous=hand.position;previousRotation=hand.rotation;
                }
                Assert.That(largestMotion,Is.GreaterThan(.25f),"Attack has no visible reach");
                Assert.That(d.MacheteAttacking,Is.False);Assert.That(d.CanMacheteAttack,Is.True);
                Assert.That(Vector3.Distance(origin,hand.position-chest.position),Is.LessThan(.006f));
                Assert.That(Quaternion.Angle(rotation,hand.rotation),Is.LessThan(.2f));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void ArmpitV73KeepsSeamsCuffsAndGripAcrossContexts(Context equipment)
        {
            var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_ShoulderSkin_v71.asset");
            d.RebuiltSkin=d.PreviousRebuiltSkin=source;d.ShoulderAimV71=d.ContextAimV72=d.ArmpitVolumeV73=true;
            var original=source.vertices;var uv=source.uv;var weights=source.boneWeights;
            var sourceSkin=d.SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>();
            var map=d.SourcePrefab.transform.worldToLocalMatrix*sourceSkin.transform.localToWorldMatrix;
            var cuff=source.GetTriangles(2).Distinct().ToArray();
            var groups=Enumerable.Range(0,original.Length).GroupBy(i=>Vector3Int.RoundToInt(map.MultiplyPoint3x4(original[i])*100000)).Where(g=>g.Count()>1).Select(g=>g.ToArray()).ToArray();
            var skin=d.OutputAnimator.GetComponentInChildren<SkinnedMeshRenderer>();
            float largestCorrection=0;
            foreach(var state in new[]{Situation.Ground,Situation.Surface,Situation.Underwater,Situation.Ladder,Situation.Grabbed,Situation.Carried})
            foreach(var aim in new[]{new Vector2(-60,-30),new Vector2(60,-30),new Vector2(60,0),new Vector2(-60,30),Vector2.zero})
            {
                d.Configure(equipment,state,Motion.Idle);d.AimYaw=aim.x;d.AimPitch=aim.y;Advance(.65f);
                var runtime=skin.sharedMesh;Assert.That(runtime,Is.Not.SameAs(source));
                var v=runtime.vertices;var n=runtime.normals;
                CollectionAssert.AreEqual(uv,runtime.uv);CollectionAssert.AreEqual(weights,runtime.boneWeights);
                for(int s=0;s<source.subMeshCount;s++)CollectionAssert.AreEqual(source.GetTriangles(s),runtime.GetTriangles(s));
                foreach(int i in cuff)Assert.That(v[i],Is.EqualTo(original[i]),"Cuff ring moved");
                for(int i=0;i<v.Length;i++)
                {
                    Assert.That(float.IsNaN(v[i].sqrMagnitude)||float.IsInfinity(v[i].sqrMagnitude),Is.False);
                    Assert.That(n[i].sqrMagnitude,Is.InRange(.98f,1.02f));
                    float correction=map.MultiplyVector(v[i]-original[i]).magnitude;
                    largestCorrection=Mathf.Max(largestCorrection,correction);
                    Assert.That(correction,Is.LessThan(.1601f));
                    if(correction>.000001f)
                    {
                        var p=map.MultiplyPoint3x4(original[i]);
                        Assert.That(p.y,Is.InRange(.88f,1.31f));Assert.That(Mathf.Abs(p.x),Is.InRange(.14f,.49f));
                    }
                }
                foreach(var group in groups)
                {
                    var delta=v[group[0]]-original[group[0]];
                    foreach(int i in group)Assert.That(map.MultiplyVector((v[i]-original[i])-delta).magnitude,Is.LessThan(.0004f),"New gap at UV/cuff seam "+i);
                }
                float clock=d.Clock;var chest=d.OutputAnimator.GetBoneTransform(HumanBodyBones.UpperChest).rotation;
                var left=d.OutputAnimator.GetBoneTransform(HumanBodyBones.LeftHand).position;
                var right=d.OutputAnimator.GetBoneTransform(HumanBodyBones.RightHand).position;
                d.SetSkinRevision(false);Assert.That(skin.sharedMesh,Is.SameAs(source));
                Assert.That(Quaternion.Angle(chest,d.OutputAnimator.GetBoneTransform(HumanBodyBones.UpperChest).rotation),Is.LessThan(.15f));
                Assert.That(Vector3.Distance(left,d.OutputAnimator.GetBoneTransform(HumanBodyBones.LeftHand).position),Is.LessThan(.0002f));
                Assert.That(Vector3.Distance(right,d.OutputAnimator.GetBoneTransform(HumanBodyBones.RightHand).position),Is.LessThan(.0002f));
                d.SetSkinRevision(true);Assert.That(skin.sharedMesh,Is.SameAs(runtime));Assert.That(d.Clock,Is.EqualTo(clock));
                Assert.That(d.HeadLookError,Is.LessThan(.2f));Assert.That(d.SupportError,Is.LessThan(.006f));
            }
            Assert.That(largestCorrection,Is.GreaterThan(.01f),"Correction never activated");
            CollectionAssert.AreEqual(original,source.vertices);LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Situation.Surface)] [TestCase(Situation.Underwater)]
        [TestCase(Situation.Grabbed)] [TestCase(Situation.Carried)]
        public void ContextAimV72TurnsTorsoAndPreservesBodyGripAndGaze(Situation state)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_ShoulderSkin_v71.asset");
            d.PreviousRebuiltSkin=d.RebuiltSkin=mesh;d.ShoulderAimV71=d.ContextAimV72=true;
            var hips=d.OutputAnimator.GetBoneTransform(HumanBodyBones.Hips);
            var chest=d.OutputAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            var feet=new[]{HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot}.Select(d.OutputAnimator.GetBoneTransform).ToArray();
            foreach(var equipment in new[]{Context.Unarmed,Context.Shotgun,Context.Revolver,Context.Harpoon,Context.Food})
            foreach(var freedom in state==Situation.Grabbed||state==Situation.Carried?new[]{Hands.BothFree,Hands.RightFree,Hands.Restrained}:new[]{Hands.BothFree})
            {
                d.Configure(equipment,state,Motion.Forward,freedom);Advance(.8f);
                foreach(float yaw in new[]{-60f,0f,60f})foreach(float pitch in new[]{-30f,30f})
                {
                    d.AimYaw=yaw;d.AimPitch=pitch;Advance(.6f);
                    float clock=d.Clock;var hp=hips.position;var hr=hips.rotation;var cr=chest.rotation;
                    var fp=feet.Select(x=>x.position).ToArray();var fr=feet.Select(x=>x.rotation).ToArray();
                    Assert.That(d.HeadLookError,Is.LessThan(.2f),state+" head");
                    Assert.That(d.SupportError,Is.LessThan(.006f),state+" grip");
                    bool fire=d.CanFire,reload=d.CanReload;
                    d.SetSkinRevision(false);
                    var delta=cr*Quaternion.Inverse(chest.rotation);
                    Assert.That(Quaternion.Angle(delta,Quaternion.AngleAxis(yaw*.85f,d.Display.transform.up)),Is.LessThan(.2f),"Torso must follow signed yaw even when hands are unavailable");
                    Assert.That(d.Clock,Is.EqualTo(clock));
                    Assert.That(Vector3.Distance(hp,hips.position),Is.LessThan(.0002f));
                    Assert.That(Quaternion.Angle(hr,hips.rotation),Is.LessThan(.1f));
                    for(int i=0;i<feet.Length;i++)
                    {
                        Assert.That(Vector3.Distance(fp[i],feet[i].position),Is.LessThan(.0002f));
                        Assert.That(Quaternion.Angle(fr[i],feet[i].rotation),Is.LessThan(.1f));
                    }
                    d.SetSkinRevision(true);
                    Assert.That(Quaternion.Angle(cr,chest.rotation),Is.LessThan(.2f));
                    Assert.That(d.HeadLookError,Is.LessThan(.2f));Assert.That(d.SupportError,Is.LessThan(.006f));
                    Assert.That(d.CanFire,Is.EqualTo(fire));Assert.That(d.CanReload,Is.EqualTo(reload));
                }
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Shotgun)] [TestCase(Context.Revolver)]
        [TestCase(Context.Harpoon)] [TestCase(Context.RiflePreview)]
        public void ShoulderAimV71PreservesGripGazeAndSameTimeComparison(Context equipment)
        {
            var before=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_SleeveSkin_v70.asset");
            var after=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_ShoulderSkin_v71.asset");
            CollectionAssert.AreEqual(before.vertices,after.vertices);
            CollectionAssert.AreEqual(before.uv,after.uv);
            CollectionAssert.AreEqual(before.normals,after.normals);
            CollectionAssert.AreEqual(before.tangents,after.tangents);
            for(int s=0;s<before.subMeshCount;s++)CollectionAssert.AreEqual(before.GetTriangles(s),after.GetTriangles(s));
            var aw=before.boneWeights;var bw=after.boneWeights;
            foreach(int i in before.GetTriangles(2).Distinct())Assert.That(bw[i],Is.EqualTo(aw[i]),"v70 cuff binding changed");
            d.PreviousRebuiltSkin=before;d.RebuiltSkin=after;d.ShoulderAimV71=true;
            d.Configure(equipment,Situation.Ground,Motion.Idle);Advance(.8f);
            var skin=d.OutputAnimator.GetComponentInChildren<SkinnedMeshRenderer>();
            var hips=d.OutputAnimator.GetBoneTransform(HumanBodyBones.Hips);
            var chest=d.OutputAnimator.GetBoneTransform(HumanBodyBones.UpperChest);
            foreach(float yaw in new[]{-60f,0f,60f})foreach(float pitch in new[]{-30f,0f,30f})
            {
                d.AimYaw=yaw;d.AimPitch=pitch;Advance(.6f);
                Assert.That(d.HeadLookError,Is.LessThan(.2f));Assert.That(d.SupportError,Is.LessThan(.006f));
                float clock=d.Clock;var hipPosition=hips.position;var hipRotation=hips.rotation;
                var revisedChest=chest.rotation;
                d.SetSkinRevision(false);
                Assert.That(skin.sharedMesh,Is.SameAs(before));Assert.That(d.Clock,Is.EqualTo(clock));
                Assert.That(Vector3.Distance(hipPosition,hips.position),Is.LessThan(.0002f));
                Assert.That(Quaternion.Angle(hipRotation,hips.rotation),Is.LessThan(.1f));
                if(Mathf.Abs(yaw)>50)Assert.That(Quaternion.Angle(revisedChest,chest.rotation),Is.GreaterThan(25),"Torso remains fixed while arms turn");
                d.SetSkinRevision(true);
                Assert.That(skin.sharedMesh,Is.SameAs(after));Assert.That(d.Clock,Is.EqualTo(clock));
                Assert.That(Quaternion.Angle(chest.rotation,revisedChest),Is.LessThan(.15f));
                Assert.That(d.HeadLookError,Is.LessThan(.2f));Assert.That(d.SupportError,Is.LessThan(.006f));
            }
            LogAssert.NoUnexpectedReceived();
        }
        const string SkinV60="Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_ChinSkin_v60.asset";
        const string SkinV61="Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_ClosedNeck_v61.asset";
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void ClosedNeckHasNoOpenEdgesAndSeamsStayJoinedAcrossPoses(Context equipment)
        { CheckClosedNeck(equipment,SkinV61,19371,1.22f); }
        const string SkinV62="Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_CollarRepair_v62.asset";
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void CollarRepairKeepsV60NeckAndClosesSurroundingHoles(Context equipment)
        {
            var before=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV60);var after=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV62);
            var a=before.vertices;var b=after.vertices;var aw=before.boneWeights;var bw=after.boneWeights;
            var an=before.normals;var bn=after.normals;var au=before.uv;var bu=after.uv;
            for(int i=0;i<a.Length;i++){Assert.That(b[i],Is.EqualTo(a[i]),"v60 surface moved "+i);Assert.That(bw[i],Is.EqualTo(aw[i]),"v60 binding changed "+i);Assert.That(bn[i],Is.EqualTo(an[i]));Assert.That(bu[i],Is.EqualTo(au[i]));}
            CheckClosedNeck(equipment,SkinV62,before.vertexCount,1.19f);
        }
        const string SkinV63="Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_ScarfFinish_v63.asset";
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void ScarfFinishUsesClothColourAndPreservesNeckAcrossPoses(Context equipment)
        {
            var before=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV62);var after=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV63);
            var a=before.vertices;var b=after.vertices;var aw=before.boneWeights;var bw=after.boneWeights;
            var prefabSkin=d.SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>();
            var map=d.SourcePrefab.transform.worldToLocalMatrix*prefabSkin.transform.localToWorldMatrix;
            int moved=0;for(int i=0;i<a.Length;i++)
            {
                Assert.That(bw[i],Is.EqualTo(aw[i]));var p=map.MultiplyPoint3x4(a[i]);float distance=Vector3.Distance(p,map.MultiplyPoint3x4(b[i]));
                if(distance>.000001f){moved++;Assert.That(p.y,Is.InRange(1.23f,1.32f));Assert.That(Mathf.Abs(p.x),Is.LessThan(.23f));Assert.That(distance,Is.LessThan(.035f));}
                if(i>=19454&&i<19518)Assert.That(b[i],Is.EqualTo(a[i]),"Central neck changed");
            }
            Assert.That(moved,Is.GreaterThan(20));
            var texture=new Texture2D(2,2);
            try
            {
                texture.LoadImage(System.IO.File.ReadAllBytes("Assets/_Project/Art/Characters/Raccoon/Textures/T_Raccoon_Body_BaseColor.png"));
                var uv=after.uv;var used=new System.Collections.Generic.HashSet<int>(after.GetTriangles(1));int cloth=0;
                foreach(int i in used)
                {
                    if(i<a.Length||uv[i].x<.2099f||uv[i].x>.2201f||uv[i].y<.0449f||uv[i].y>.0501f)continue;
                    var color=texture.GetPixelBilinear(uv[i].x,uv[i].y);cloth++;
                    Assert.That(color.r,Is.GreaterThan(color.g*1.6f),"Non-scarf colour on repaired cloth");
                    Assert.That(color.g,Is.GreaterThan(color.b*1.25f));
                }
                Assert.That(cloth,Is.GreaterThan(150));
            }
            finally{UnityEngine.Object.DestroyImmediate(texture);}
            CheckClosedNeck(equipment,SkinV63,before.vertexCount,1.19f,SkinV62);
        }
        const string SkinV64="Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_ScarfRestore_v64.asset";
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void RestoredScarfPreservesOriginalUpperClothAndStaysClosed(Context equipment)
        {
            var before=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV59);var after=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV64);
            var v=after.vertices;var w=after.boneWeights;var used=new System.Collections.Generic.HashSet<int>(after.GetTriangles(1));
            // The upper scarf was previously cut at 1.32 m and made to follow the head.
            foreach(int i in new[]{6421,6422,6428,6434})
            {
                Assert.That(used.Contains(i),Is.True,"Original scarf corner missing");
                Assert.That(Vector3.Distance(v[i],before.vertices[i]),Is.LessThan(.00000001f));Assert.That(after.uv[i],Is.EqualTo(before.uv[i]));
                Assert.That(w[i].boneIndex0,Is.EqualTo(4));Assert.That(w[i].boneIndex1,Is.EqualTo(5));
                Assert.That(w[i].weight0+w[i].weight1,Is.EqualTo(1).Within(.00001f));
            }
            var map=d.SourcePrefab.transform.worldToLocalMatrix*d.SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>().transform.localToWorldMatrix;
            var old=before.vertices;
            for(int i=0;i<old.Length;i++)if((v[i]-old[i]).sqrMagnitude>1e-16f)
            {
                var p=map.MultiplyPoint3x4(old[i]);Assert.That(p.y,Is.InRange(1.24f,1.34f));
                Assert.That(p.z,Is.GreaterThan(.30f));Assert.That(Mathf.Abs(p.x),Is.LessThan(.20f));
                Assert.That(Vector3.Distance(map.MultiplyPoint3x4(v[i]),p),Is.LessThan(.0281f));
            }
            var chin=new[]{8717,8714,8716,5162}.Select(i=>used.First(j=>(v[j]-old[i]).sqrMagnitude<1e-16f&&w[j].boneIndex0==6&&w[j].weight0>.9999f)).ToArray();
            CheckClosedNeck(equipment,SkinV64,before.vertexCount,1.19f,SkinV63,chin,1.40f);
        }
        const string SkinV65="Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_ScarfClean_v65.asset";
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void ScarfCleanupRemovesLoosePiecesWithoutChangingMainSurface(Context equipment)
        {
            var before=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV64);var after=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV65);
            CollectionAssert.AreEqual(before.vertices,after.vertices);CollectionAssert.AreEqual(before.boneWeights,after.boneWeights);
            CollectionAssert.AreEqual(before.uv,after.uv);CollectionAssert.AreEqual(before.normals,after.normals);CollectionAssert.AreEqual(before.tangents,after.tangents);
            var v=after.vertices;var w=after.boneWeights;var tr=after.GetTriangles(1);
            var map=d.SourcePrefab.transform.worldToLocalMatrix*d.SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>().transform.localToWorldMatrix;
            var canonical=new int[v.Length];var keys=new System.Collections.Generic.Dictionary<string,int>();
            for(int i=0;i<v.Length;i++)
            {
                var b=w[i];var p=map.MultiplyPoint3x4(v[i]);
                string key=Vector3Int.RoundToInt(p*100000).ToString()+"/"+b.boneIndex0+":"+Mathf.RoundToInt(b.weight0*100000)+"/"+b.boneIndex1+":"+Mathf.RoundToInt(b.weight1*100000)+"/"+b.boneIndex2+":"+Mathf.RoundToInt(b.weight2*100000)+"/"+b.boneIndex3+":"+Mathf.RoundToInt(b.weight3*100000);
                int id;if(!keys.TryGetValue(key,out id)){id=i;keys[key]=i;}canonical[i]=id;
            }
            // Sharing only a single vertex must not hide a loose closed fragment.
            var parent=Enumerable.Range(0,tr.Length/3).ToArray();Func<int,int> root=null;
            root=i=>{while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;};
            var edges=new System.Collections.Generic.Dictionary<Vector2Int,int>();
            for(int k=0;k<tr.Length;k+=3)for(int j=0;j<3;j++)
            {
                int a=canonical[tr[k+j]],b=canonical[tr[k+(j+1)%3]];if(a==b)continue;
                var edge=new Vector2Int(Mathf.Min(a,b),Mathf.Max(a,b));int other;
                if(edges.TryGetValue(edge,out other))parent[root(k/3)]=root(other);else edges[edge]=k/3;
            }
            var pieces=Enumerable.Range(0,parent.Length).GroupBy(root).Select(g=>g.Count()).ToArray();
            Assert.That(pieces.Length,Is.EqualTo(2),"Only the designed head and body shells should remain");
            Assert.That(pieces.Min(),Is.GreaterThan(1000),"Loose scarf fragment remains");
            var kept=new System.Collections.Generic.HashSet<string>(Enumerable.Range(0,tr.Length/3).Select(i=>tr[i*3]+","+tr[i*3+1]+","+tr[i*3+2]));
            var oldTri=before.GetTriangles(1);int removed=0;
            for(int k=0;k<oldTri.Length;k+=3)if(!kept.Contains(oldTri[k]+","+oldTri[k+1]+","+oldTri[k+2]))
            {
                removed++;for(int j=0;j<3;j++){var p=map.MultiplyPoint3x4(v[oldTri[k+j]]);Assert.That(p.y,Is.InRange(1.30f,1.34f));Assert.That(Mathf.Abs(p.x),Is.LessThan(.24f));}
            }
            Assert.That(removed,Is.GreaterThan(0));Assert.That(removed,Is.LessThan(30));
            var used=new System.Collections.Generic.HashSet<int>(tr);
            var chin=new[]{8717,8714,8716,5162}.Select(i=>used.First(j=>(v[j]-v[i]).sqrMagnitude<1e-16f&&w[j].boneIndex0==6&&w[j].weight0>.9999f)).ToArray();
            CheckClosedNeck(equipment,SkinV65,before.vertexCount,1.19f,SkinV64,chin,1.40f);
        }
        const string SkinV66="Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_ScarfSeam_v66.asset";
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void ScarfRimStaysSmoothWhenLookingUp(Context equipment)
        {
            var before=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV65);var after=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV66);
            CollectionAssert.AreEqual(before.vertices,after.vertices);CollectionAssert.AreEqual(before.uv,after.uv);
            CollectionAssert.AreEqual(before.normals,after.normals);CollectionAssert.AreEqual(before.tangents,after.tangents);
            for(int s=0;s<before.subMeshCount;s++)CollectionAssert.AreEqual(before.GetTriangles(s),after.GetTriangles(s));
            foreach(var w in after.boneWeights)Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.00001f));
            d.RebuiltSkin=after;d.Configure(equipment,Situation.Ground,Motion.Idle);d.AimPitch=-30;d.AimYaw=0;Advance(1);
            var skin=d.Display.GetComponentInChildren<SkinnedMeshRenderer>();
            int[] rim={5061,5083,8787,8785,8963,8789,8783,8790,9130,9293,9700,9702,9706,10264,10265,10267,10269,10270,10688};
            Func<Mesh,float> cornerAngle=mesh=>
            {
                var v=mesh.vertices;var w=mesh.boneWeights;var bind=mesh.bindposes;float max=0;
                for(int j=1;j<rim.Length-1;j++)
                {
                    var a=SkinPoint(v[rim[j-1]],w[rim[j-1]],skin.bones,bind);
                    var b=SkinPoint(v[rim[j]],w[rim[j]],skin.bones,bind);
                    var c=SkinPoint(v[rim[j+1]],w[rim[j+1]],skin.bones,bind);
                    max=Mathf.Max(max,Vector3.Angle(b-a,c-b));
                }
                return max;
            };
            Assert.That(cornerAngle(before),Is.GreaterThan(60),"Raised-head sawtooth regression not reproduced");
            Assert.That(cornerAngle(after),Is.LessThan(25),"Abrupt fold remains on the visible inner rim");
            var av=after.vertices;var aw=after.boneWeights;var used=new System.Collections.Generic.HashSet<int>(after.GetTriangles(1));
            var chin=new[]{8717,8714,8716,5162}.Select(i=>used.First(j=>(av[j]-av[i]).sqrMagnitude<1e-16f&&aw[j].boneIndex0==6&&aw[j].weight0>.9999f)).ToArray();
            CheckClosedNeck(equipment,SkinV66,before.vertexCount,1.19f,SkinV65,chin,1.40f);
        }
        const string SkinV67="Assets/_Project/Art/Characters/Raccoon/MESH_Raccoon_NeckBlend_v67.asset";
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void NeckAndHeadFormOneContinuousSurfaceAcrossPoses(Context equipment)
        {
            var before=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV66);var after=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV67);
            var old=before.vertices;var v=after.vertices;var oldWeights=before.boneWeights;var w=after.boneWeights;var oldUV=before.uv;var uv=after.uv;
            for(int i=0;i<old.Length;i++){Assert.That(v[i],Is.EqualTo(old[i]),"Existing face/scarf moved");Assert.That(w[i],Is.EqualTo(oldWeights[i]),"Existing face/scarf weights changed");Assert.That(uv[i],Is.EqualTo(oldUV[i]));}
            var map=d.SourcePrefab.transform.worldToLocalMatrix*d.SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>().transform.localToWorldMatrix;
            var keys=new System.Collections.Generic.Dictionary<string,int>();var canonical=new int[v.Length];
            for(int i=0;i<v.Length;i++)
            {
                var b=w[i];var p=map.MultiplyPoint3x4(v[i]);string key=Vector3Int.RoundToInt(p*100000).ToString()+"/"+b.boneIndex0+":"+Mathf.RoundToInt(b.weight0*100000)+"/"+b.boneIndex1+":"+Mathf.RoundToInt(b.weight1*100000)+"/"+b.boneIndex2+":"+Mathf.RoundToInt(b.weight2*100000)+"/"+b.boneIndex3+":"+Mathf.RoundToInt(b.weight3*100000);
                int id;if(!keys.TryGetValue(key,out id)){id=i;keys[key]=i;}canonical[i]=id;
                if(i>=old.Length){Assert.That(p.y,Is.InRange(1.244f,1.34f));Assert.That(b.weight0+b.weight1+b.weight2+b.weight3,Is.EqualTo(1).Within(.00001f));}
            }
            var tr=after.GetTriangles(1);var parent=Enumerable.Range(0,tr.Length/3).ToArray();Func<int,int> root=null;root=i=>{while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;};
            var edges=new System.Collections.Generic.Dictionary<Vector2Int,int>();
            for(int k=0;k<tr.Length;k+=3)for(int j=0;j<3;j++)
            {
                int a=canonical[tr[k+j]],b=canonical[tr[k+(j+1)%3]];if(a==b)continue;var edge=new Vector2Int(Mathf.Min(a,b),Mathf.Max(a,b));int other;
                if(edges.TryGetValue(edge,out other))parent[root(k/3)]=root(other);else edges[edge]=k/3;
            }
            Assert.That(Enumerable.Range(0,parent.Length).Select(root).Distinct().Count(),Is.EqualTo(1),"Head and neck still form separate shells");
            var used=new System.Collections.Generic.HashSet<int>(tr);
            var chin=new[]{8717,8714,8716,5162}.Select(i=>used.First(j=>(v[j]-v[i]).sqrMagnitude<1e-16f&&w[j].boneIndex0==6&&w[j].weight0>.9999f)).ToArray();
            CheckClosedNeck(equipment,SkinV67,before.vertexCount,1.19f,SkinV66,chin,1.40f);
        }
        void CheckClosedNeck(Context equipment,string assetPath,int newVertexStart,float minHeight,string previousPath=SkinV60,int[] chinIndices=null,float maxHeight=1.38f)
        {
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
            var previous=AssetDatabase.LoadAssetAtPath<Mesh>(previousPath);
            d.RebuiltSkin=mesh;d.PreviousRebuiltSkin=previous;
            var skin=d.Display.GetComponentInChildren<SkinnedMeshRenderer>();
            var prefabSkin=d.SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>();
            var map=d.SourcePrefab.transform.worldToLocalMatrix*prefabSkin.transform.localToWorldMatrix;
            var v=mesh.vertices;var w=mesh.boneWeights;var bind=mesh.bindposes;
            var normals=mesh.normals;
            for(int i=newVertexStart;i<mesh.vertexCount;i++)Assert.That(normals[i].magnitude,Is.EqualTo(1).Within(.001f),"Invalid neck normal "+i);
            var canonical=new int[v.Length];
            var groups=new System.Collections.Generic.Dictionary<string,int>();
            var seams=new System.Collections.Generic.List<Vector2Int>();
            for(int i=0;i<v.Length;i++)
            {
                var p=map.MultiplyPoint3x4(v[i]);if(p.y<minHeight||p.y>maxHeight){canonical[i]=i;continue;}
                var key=Vector3Int.RoundToInt(p*100000).ToString()+"/"+w[i].boneIndex0+":"+Mathf.RoundToInt(w[i].weight0*100000)+"/"+w[i].boneIndex1+":"+Mathf.RoundToInt(w[i].weight1*100000)+"/"+w[i].boneIndex2+":"+Mathf.RoundToInt(w[i].weight2*100000)+"/"+w[i].boneIndex3+":"+Mathf.RoundToInt(w[i].weight3*100000);
                int other;if(groups.TryGetValue(key,out other)){canonical[i]=other;seams.Add(new Vector2Int(i,other));}else{groups.Add(key,i);canonical[i]=i;}
            }
            var counts=new System.Collections.Generic.Dictionary<Vector2Int,int>();
            var winding=new System.Collections.Generic.Dictionary<Vector2Int,int>();
            var tr=mesh.GetTriangles(1);
            for(int k=0;k<tr.Length;k+=3)for(int j=0;j<3;j++)
            {
                int a=canonical[tr[k+j]],b=canonical[tr[k+(j+1)%3]];if(a==b)continue;
                var pa=map.MultiplyPoint3x4(v[a]);var pb=map.MultiplyPoint3x4(v[b]);
                if(pa.y<minHeight||pa.y>maxHeight||pb.y<minHeight||pb.y>maxHeight)continue;
                var edge=new Vector2Int(Mathf.Min(a,b),Mathf.Max(a,b));
                if(!counts.ContainsKey(edge)){counts[edge]=0;winding[edge]=0;}counts[edge]++;winding[edge]+=a<b?1:-1;
            }
            Assert.That(counts.Count,Is.GreaterThan(500));
            foreach(var edge in counts){Assert.That(edge.Value,Is.EqualTo(2),"Open/non-manifold neck edge "+edge.Key);Assert.That(winding[edge.Key],Is.Zero,"Reversed neck face "+edge.Key);}
            foreach(int s in new[]{0,2,3,4})CollectionAssert.AreEqual(previous.GetTriangles(s),mesh.GetTriangles(s));
            Assert.That(mesh.blendShapeCount,Is.EqualTo(previous.blendShapeCount));
            foreach(var state in new[]{Situation.Ground,Situation.Surface,Situation.Underwater,Situation.Ladder,Situation.Grabbed,Situation.Carried,Situation.Melee})
            foreach(var aim in new[]{new Vector2(0,0),new Vector2(-60,-30),new Vector2(-60,30),new Vector2(60,-30),new Vector2(60,30)})
            {
                d.Configure(equipment,state,Motion.Forward);d.AimYaw=aim.x;d.AimPitch=aim.y;Advance(1);
                foreach(var pair in seams)Assert.That(Vector3.Distance(SkinPoint(v[pair.x],w[pair.x],skin.bones,bind),SkinPoint(v[pair.y],w[pair.y],skin.bones,bind)),Is.LessThan(.0003f),state+" seam opened "+pair);
                foreach(int i in chinIndices??new[]{8717,8714,8716,5162})Assert.That(Vector3.Distance(SkinPoint(v[i],w[i],skin.bones,bind),skin.bones[6].TransformPoint(bind[6].MultiplyPoint3x4(v[i]))),Is.LessThan(.0003f),state+" chin stretched");
                float clock=d.Clock;d.SetSkinRevision(false);Assert.That(skin.sharedMesh,Is.SameAs(previous));d.SetSkinRevision(true);Assert.That(skin.sharedMesh,Is.SameAs(mesh));Assert.That(d.Clock,Is.EqualTo(clock));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void ChinSurfacePreservesBodyHandsTailAndCorrectiveShapes()
        {
            var old=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV59);var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV60);
            Assert.That(mesh.vertexCount,Is.GreaterThan(old.vertexCount));Assert.That(mesh.vertexCount-old.vertexCount,Is.LessThan(1000));
            var a=old.vertices;var b=mesh.vertices;var ow=old.boneWeights;var nw=mesh.boneWeights;
            var prefabSkin=d.SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>();
            var map=d.SourcePrefab.transform.worldToLocalMatrix*prefabSkin.transform.localToWorldMatrix;
            for(int i=0;i<a.Length;i++)
            {
                Assert.That(b[i],Is.EqualTo(a[i]),"Original surface moved");
                if(map.MultiplyPoint3x4(a[i]).y<1.20f)Assert.That(nw[i],Is.EqualTo(ow[i]),"Body/hand/tail binding changed");
            }
            foreach(int s in new[]{0,2,3,4})CollectionAssert.AreEqual(old.GetTriangles(s),mesh.GetTriangles(s));
            Assert.That(mesh.blendShapeCount,Is.EqualTo(old.blendShapeCount));
            for(int s=0;s<old.blendShapeCount;s++)Assert.That(mesh.GetBlendShapeName(s),Is.EqualTo(old.GetBlendShapeName(s)));
            foreach(var w in nw)Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.00001f));
            foreach(int i in mesh.triangles)Assert.That(i,Is.InRange(0,mesh.vertexCount-1));
        }
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void ChinLengthStaysConstantAcrossEnvironmentAndLookDirection(Context equipment)
        {
            d.PreviousRebuiltSkin=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV59);
            d.RebuiltSkin=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV60);
            var skin=d.Display.GetComponentInChildren<SkinnedMeshRenderer>();var mesh=d.RebuiltSkin;
            var v=mesh.vertices;var w=mesh.boneWeights;var bind=mesh.bindposes;
            var old=d.PreviousRebuiltSkin.boneWeights;float worstBefore=0;
            foreach(var state in new[]{Situation.Ground,Situation.Surface,Situation.Underwater,Situation.Ladder,Situation.Grabbed,Situation.Carried,Situation.Melee})
            foreach(float yaw in new[]{-60f,0f,60f})
            {
                d.Configure(equipment,state,Motion.Forward);d.AimYaw=yaw;d.AimPitch=yaw==0?-30:30;Advance(1);
                int head=Array.IndexOf(skin.bones,d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head));
                foreach(int i in new[]{8717,8714,8716,5162})
                {
                    var expected=skin.bones[head].TransformPoint(bind[head].MultiplyPoint3x4(v[i]));
                    Assert.That(Vector3.Distance(SkinPoint(v[i],w[i],skin.bones,bind),expected),Is.LessThan(.0005f),state+" chin stretched");
                    worstBefore=Mathf.Max(worstBefore,Vector3.Distance(SkinPoint(v[i],old[i],skin.bones,bind),expected));
                }
                float clock=d.Clock;var rotation=d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head).rotation;
                d.SetSkinRevision(false);Assert.That(skin.sharedMesh,Is.SameAs(d.PreviousRebuiltSkin));
                d.SetSkinRevision(true);Assert.That(skin.sharedMesh,Is.SameAs(mesh));
                Assert.That(d.Clock,Is.EqualTo(clock));Assert.That(d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head).rotation,Is.EqualTo(rotation));
                Assert.That(d.HeadLookError,Is.LessThan(.2f));Assert.That(d.SupportError,Is.LessThan(.006f));
            }
            Assert.That(worstBefore,Is.GreaterThan(.03f),"Regression pose should reproduce the old jaw distortion");
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void StateSelectionResumesPausedPreviewAndKeepsMeshChoiceInEveryEnvironment()
        {
            d.RebuiltSkin=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV59);
            var skin=d.Display.GetComponentInChildren<SkinnedMeshRenderer>();
            foreach(bool rebuilt in new[]{false,true})
            foreach(var state in new[]{Situation.Ground,Situation.Surface,Situation.Underwater,Situation.Ladder,Situation.Grabbed,Situation.Carried,Situation.Melee})
            {
                d.SetSkinRevision(rebuilt);d.SetComparison(false);d.Paused=true;
                d.PreviewSelection(Context.Shotgun,state,Motion.Forward);
                Assert.That(d.Paused,Is.False,state+" stays frozen");Assert.That(d.Candidate,Is.True);
                Assert.That(d.State,Is.EqualTo(state));Assert.That(d.UseRebuiltSkin,Is.EqualTo(rebuilt));
                float clock=d.Clock;Advance(.5f);Assert.That(d.Clock,Is.GreaterThan(clock));
                Assert.That(skin.sharedMesh==d.RebuiltSkin,Is.EqualTo(rebuilt));
                Assert.That(d.Display.Ladder.activeSelf,Is.EqualTo(state==Situation.Ladder));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void RebuiltSkinPreservesGeometryAndRepairsSharedSeams()
        {
            var source=d.SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh;
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV59);Assert.That(mesh,Is.Not.Null);
            CollectionAssert.AreEqual(source.vertices,mesh.vertices);
            CollectionAssert.AreEqual(source.normals,mesh.normals);
            CollectionAssert.AreEqual(source.uv,mesh.uv);
            CollectionAssert.AreEqual(source.bindposes,mesh.bindposes);
            Assert.That(mesh.blendShapeCount,Is.EqualTo(source.blendShapeCount));
            for(int s=0;s<source.subMeshCount;s++)CollectionAssert.AreEqual(source.GetTriangles(s),mesh.GetTriangles(s));
            var weights=mesh.boneWeights;var vertices=mesh.vertices;
            var skin=d.Display.GetComponentInChildren<SkinnedMeshRenderer>();
            var prefabSkin=d.SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>();
            var map=d.SourcePrefab.transform.worldToLocalMatrix*prefabSkin.transform.localToWorldMatrix;
            var bodyVertices=new System.Collections.Generic.HashSet<int>(source.GetTriangles(1));
            bodyVertices.UnionWith(source.GetTriangles(4));
            var groups=new System.Collections.Generic.Dictionary<Vector3Int,int>();
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.00001f));
                var p=map.MultiplyPoint3x4(vertices[i]);var key=Vector3Int.RoundToInt(p*100000);
                if(groups.TryGetValue(key,out int other))
                {
                    // Every shared body/garment boundary must deform as a single point.
                    if((bodyVertices.Contains(i)||bodyVertices.Contains(other))&&p.y>.82f&&p.y<1.37f)
                        Assert.That(Vector3.Distance(SkinPoint(vertices[i],w,skin.bones,mesh.bindposes),SkinPoint(vertices[other],weights[other],skin.bones,mesh.bindposes)),Is.LessThan(.0002f),"Body seam "+i+" / "+other);
                }
                else groups.Add(key,i);
            }
        }
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)] [TestCase(Context.Revolver)]
        public void RebuiltSkinKeepsFaceRigidAcrossContextsAndSupportsSamePoseComparison(Context equipment)
        {
            d.RebuiltSkin=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV59);
            var skin=d.Display.GetComponentInChildren<SkinnedMeshRenderer>();
            var vertices=d.RebuiltSkin.vertices;var bind=d.RebuiltSkin.bindposes;
            var weights=d.RebuiltSkin.boneWeights;
            var map=d.Display.transform.worldToLocalMatrix*skin.transform.localToWorldMatrix;
            int head=Array.IndexOf(skin.bones,d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head));
            foreach(var state in new[]{Situation.Ladder,Situation.Ground,Situation.Surface,Situation.Underwater})
            foreach(float yaw in new[]{-60f,0f,60f})
            {
                d.Configure(equipment,state,state==Situation.Ladder?Motion.Up:Motion.Forward);
                d.AimYaw=yaw;d.AimPitch=yaw*.5f;Advance(1);
                Assert.That(skin.sharedMesh,Is.SameAs(d.RebuiltSkin));int count=0;
                for(int i=0;i<vertices.Length;i++)
                {
                    var p=map.MultiplyPoint3x4(vertices[i]);if(p.y<1.38f||p.y>1.66f)continue;
                    var expected=skin.bones[head].TransformPoint(bind[head].MultiplyPoint3x4(vertices[i]));
                    Assert.That(Vector3.Distance(SkinPoint(vertices[i],weights[i],skin.bones,bind),expected),Is.LessThan(.0005f),state+" face dragged by torso");count++;
                }
                Assert.That(count,Is.GreaterThan(200));
                Assert.That(d.HeadLookError,Is.LessThan(.2f));Assert.That(d.SupportError,Is.LessThan(.006f));
                var before=d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head).localRotation;float clock=d.Clock;
                d.SetSkinRevision(false);Assert.That(skin.sharedMesh,Is.Not.SameAs(d.RebuiltSkin));
                d.SetSkinRevision(true);Assert.That(skin.sharedMesh,Is.SameAs(d.RebuiltSkin));
                Assert.That(d.Clock,Is.EqualTo(clock));Assert.That(d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head).localRotation,Is.EqualTo(before));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void RebuiltBackPanelDoesNotFollowArmAndScarfMotion()
        {
            d.RebuiltSkin=AssetDatabase.LoadAssetAtPath<Mesh>(SkinV59);
            d.Configure(Context.Shotgun,Situation.Ladder,Motion.Up);Advance(1);
            var skin=d.Display.GetComponentInChildren<SkinnedMeshRenderer>();
            var prefabSkin=d.SourcePrefab.GetComponentInChildren<SkinnedMeshRenderer>();
            var map=d.SourcePrefab.transform.worldToLocalMatrix*prefabSkin.transform.localToWorldMatrix;
            var vertices=d.RebuiltSkin.vertices;var weights=d.RebuiltSkin.boneWeights;var bind=d.RebuiltSkin.bindposes;
            var selected=new System.Collections.Generic.HashSet<int>();
            foreach(int i in d.RebuiltSkin.GetTriangles(1))
            {
                var p=map.MultiplyPoint3x4(vertices[i]);
                if(p.y>.90f&&p.y<1.29f&&Mathf.Abs(p.x)<.17f&&p.z<.23f)selected.Add(i);
            }
            Assert.That(selected.Count,Is.GreaterThan(80));
            var before=new System.Collections.Generic.Dictionary<int,Vector3>();
            foreach(int i in selected)before[i]=SkinPoint(vertices[i],weights[i],skin.bones,bind);
            foreach(var b in skin.bones)
                if(b.name.Contains("Shoulder")||b.name.Contains("UpperArm")||b.name.StartsWith("Scarf"))
                    b.localRotation*=Quaternion.Euler(45,35,-30);
            foreach(int i in selected)
                Assert.That(Vector3.Distance(before[i],SkinPoint(vertices[i],weights[i],skin.bones,bind)),Is.LessThan(.0002f),"Back panel dragged by arm/scarf at "+i);
        }
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)] [TestCase(Context.Revolver)] [TestCase(Context.Harpoon)]
        public void LadderDefaultGazeMatchesCharacterForward(Context equipment)
        {
            d.AimYaw=0;d.AimPitch=0;
            d.Configure(equipment,Situation.Ground,Motion.Idle);Advance(1);
            var head=d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head);
            var forwardHead=Quaternion.Inverse(d.Display.transform.rotation)*head.rotation;
            foreach(var motion in new[]{Motion.Idle,Motion.Up,Motion.Down,Motion.Left,Motion.Right})
            {
                d.Configure(equipment,Situation.Ladder,motion);Advance(1);
                Assert.That(Quaternion.Angle(Quaternion.Inverse(d.Display.transform.rotation)*head.rotation,forwardHead),Is.LessThan(.2f),"Ladder introduces an unsolicited side-facing offset");
                Assert.That(d.HeadLookError,Is.LessThan(.2f));
                Assert.That(d.SupportError,Is.LessThan(.006f));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Shotgun)] [TestCase(Context.Revolver)] [TestCase(Context.Harpoon)]
        public void LadderSideAimSharesTorsoRotationAndPreservesLadderContacts(Context equipment)
        {
            foreach(var motion in new[]{Motion.Idle,Motion.Up,Motion.Down})
            {
                d.Configure(equipment,Situation.Ladder,motion);
                foreach(float yaw in new[]{0f,20f,60f,-60f})
                {
                    d.AimYaw=yaw;d.AimPitch=yaw==20?25:0;Advance(1);
                    var a=d.OutputAnimator;var source=d.BodySource.GetComponentInChildren<Animator>();
                    foreach(var bone in new[]{HumanBodyBones.Hips,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot})
                        Assert.That(Vector3.Distance(a.GetBoneTransform(bone).position,source.GetBoneTransform(bone).position),Is.LessThan(.003f),bone+" loses ladder contact");
                    foreach(var bone in new[]{HumanBodyBones.Spine,HumanBodyBones.Chest,HumanBodyBones.UpperChest})
                        Assert.That(Quaternion.Angle(a.GetBoneTransform(bone).localRotation,source.GetBoneTransform(bone).localRotation),Is.LessThan(23),bone+" takes too much twist");
                    Assert.That(Quaternion.Angle(a.GetBoneTransform(HumanBodyBones.Head).localRotation,source.GetBoneTransform(HumanBodyBones.Head).localRotation),Is.LessThan(30),"Face still relies on a large head-only twist");
                    if(!d.LongGun)Assert.That(Vector3.Distance(a.GetBoneTransform(HumanBodyBones.LeftHand).position,source.GetBoneTransform(HumanBodyBones.LeftHand).position),Is.LessThan(.012f),"Free hand leaves ladder");
                    Assert.That(d.HeadLookError,Is.LessThan(.2f));Assert.That(d.SupportError,Is.LessThan(.006f));
                }
            }
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void LadderSkinCorrectionPreservesSourceAndFaceVolumeAndRestoresOtherStates()
        {
            d.Configure(Context.Shotgun,Situation.Ground,Motion.Idle);Advance(1);
            var skin=d.Display.GetComponentInChildren<SkinnedMeshRenderer>();var source=skin.sharedMesh;
            var original=source.boneWeights;var vertices=source.vertices;var bind=source.bindposes;var bones=skin.bones;
            int head=Array.IndexOf(bones,d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head));
            d.Configure(Context.Shotgun,Situation.Ladder,Motion.Up);d.AimYaw=60;Advance(1);
            Assert.That(skin.sharedMesh,Is.Not.SameAs(source));var corrected=skin.sharedMesh.boneWeights;
            CollectionAssert.AreEqual(original,source.boneWeights,"Imported weights changed");
            int changedFace=0;float oldError=0,newError=0;
            var map=d.Display.transform.worldToLocalMatrix*skin.transform.localToWorldMatrix;
            float headY=map.MultiplyPoint3x4(bind[head].inverse.MultiplyPoint3x4(Vector3.zero)).y;
            var headGroup=Array.ConvertAll(bones,b=>b==bones[head]||b.name.StartsWith("Ear"));
            for(int i=0;i<corrected.Length;i++)
            {
                var w=corrected[i];Assert.That(w.weight0+w.weight1+w.weight2+w.weight3,Is.EqualTo(1).Within(.0001f));
                float hw=(headGroup[w.boneIndex0]?w.weight0:0)+(headGroup[w.boneIndex1]?w.weight1:0)+(headGroup[w.boneIndex2]?w.weight2:0)+(headGroup[w.boneIndex3]?w.weight3:0);
                var ow=original[i];float oh=(headGroup[ow.boneIndex0]?ow.weight0:0)+(headGroup[ow.boneIndex1]?ow.weight1:0)+(headGroup[ow.boneIndex2]?ow.weight2:0)+(headGroup[ow.boneIndex3]?ow.weight3:0);
                var point=map.MultiplyPoint3x4(vertices[i]);
                // Ear bones legitimately contribute to the face, and follow the head
                // rigidly in this pose. Do not require an artificial Head-only binding.
                if(hw<.9999f||oh>.99f||point.y<headY+.02f||point.y>headY+.25f||Mathf.Abs(point.x)>.22f)continue;
                Vector3 expected=bones[head].TransformPoint(bind[head].MultiplyPoint3x4(vertices[i]));
                oldError+=Vector3.Distance(SkinPoint(vertices[i],ow,bones,bind),expected);
                newError+=Vector3.Distance(SkinPoint(vertices[i],w,bones,bind),expected);changedFace++;
            }
            Assert.That(changedFace,Is.GreaterThan(10));Assert.That(oldError/changedFace,Is.GreaterThan(.003f));
            Assert.That(newError,Is.LessThan(oldError*.1f),"Face still dragged away from the head by arms/scarf");
            d.SetComparison(false);Advance(.4f);Assert.That(skin.sharedMesh,Is.SameAs(source));
            d.SetComparison(true);Advance(.4f);Assert.That(skin.sharedMesh,Is.Not.SameAs(source));
            d.Configure(Context.Shotgun,Situation.Underwater,Motion.Back);Advance(.5f);Assert.That(skin.sharedMesh,Is.SameAs(source));
            CollectionAssert.AreEqual(original,source.boneWeights);LogAssert.NoUnexpectedReceived();
        }
        static Vector3 SkinPoint(Vector3 vertex,BoneWeight w,Transform[] bones,Matrix4x4[] bind)
        {
            return bones[w.boneIndex0].TransformPoint(bind[w.boneIndex0].MultiplyPoint3x4(vertex))*w.weight0
                +bones[w.boneIndex1].TransformPoint(bind[w.boneIndex1].MultiplyPoint3x4(vertex))*w.weight1
                +bones[w.boneIndex2].TransformPoint(bind[w.boneIndex2].MultiplyPoint3x4(vertex))*w.weight2
                +bones[w.boneIndex3].TransformPoint(bind[w.boneIndex3].MultiplyPoint3x4(vertex))*w.weight3;
        }
        [TestCase(Context.Unarmed,30)] [TestCase(Context.Unarmed,60)] [TestCase(Context.Unarmed,120)]
        [TestCase(Context.Shotgun,60)] [TestCase(Context.Revolver,60)]
        public void UnderwaterBackwardLeansBackAndKicksForwardWithoutLosingGazeOrGrip(Context equipment,int rate)
        {
            d.Configure(equipment,Situation.Underwater,Motion.Forward);Advance(1,rate);
            var a=d.OutputAnimator;var hips=a.GetBoneTransform(HumanBodyBones.Hips);var chest=a.GetBoneTransform(HumanBodyBones.UpperChest);
            float forwardLean=d.Display.transform.InverseTransformDirection((chest.position-hips.position).normalized).z;
            d.Configure(equipment,Situation.Underwater,Motion.Back);Advance(1,rate);
            float backwardLean=d.Display.transform.InverseTransformDirection((chest.position-hips.position).normalized).z;
            Assert.That(forwardLean-backwardLean,Is.GreaterThan(.4f),"Backward silhouette still resembles forward diving");
            Assert.That(backwardLean,Is.LessThan(-.05f),"Torso should lean behind the hips during reverse swimming");
            var left=a.GetBoneTransform(HumanBodyBones.LeftFoot);var right=a.GetBoneTransform(HumanBodyBones.RightFoot);
            float minZ=100,maxZ=-100,minHand=100,maxHand=-100;var hand=a.GetBoneTransform(HumanBodyBones.LeftHand);
            Vector3 previousLeft=left.position,previousRight=right.position,previousHand=hand.position;
            for(int i=0;i<rate*6;i++)
            {
                d.Step(1f/rate);
                foreach(var foot in new[]{left,right})
                {
                    float z=d.Display.transform.InverseTransformVector(foot.position-hips.position).z;
                    Assert.That(z,Is.GreaterThan(.02f),"Reverse kick drifts behind the hips");minZ=Mathf.Min(minZ,z);maxZ=Mathf.Max(maxZ,z);
                }
                float hz=d.Display.transform.InverseTransformVector(hand.position-chest.position).z;minHand=Mathf.Min(minHand,hz);maxHand=Mathf.Max(maxHand,hz);
                Assert.That(Vector3.Distance(previousLeft,left.position),Is.LessThan(4f/rate));
                Assert.That(Vector3.Distance(previousRight,right.position),Is.LessThan(4f/rate));
                Assert.That(Vector3.Distance(previousHand,hand.position),Is.LessThan(4f/rate));
                previousLeft=left.position;previousRight=right.position;previousHand=hand.position;
                Assert.That(d.HeadLookError,Is.LessThan(.2f));Assert.That(d.SupportError,Is.LessThan(.006f));
            }
            Assert.That(maxZ-minZ,Is.GreaterThan(.07f),"Reverse kick lacks a visible forward stroke");
            if(!d.LongGun)Assert.That(maxHand-minHand,Is.GreaterThan(.10f),"Free arm must push forward visibly");
            foreach(var direction in new[]{Motion.Forward,Motion.Back,Motion.Left,Motion.Back})
            {d.Configure(equipment,Situation.Underwater,direction);Advance(.6f,rate);Assert.That(d.HeadLookError,Is.LessThan(.2f));Assert.That(d.SupportError,Is.LessThan(.006f));}
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Situation.Ground,Context.Unarmed)] [TestCase(Situation.Surface,Context.Unarmed)] [TestCase(Situation.Underwater,Context.Unarmed)]
        [TestCase(Situation.Ground,Context.Shotgun)] [TestCase(Situation.Surface,Context.Shotgun)] [TestCase(Situation.Underwater,Context.Shotgun)]
        public void CommonTaskButtonsKeepSelectedEnvironmentAndReplayAfterCompletion(Situation state,Context equipment)
        {
            d.Configure(equipment,state,Motion.Forward);Advance(1);
            foreach(var task in new[]{HandTask.Interact,HandTask.Pickup,HandTask.Interact})
            {
                d.Paused=true;float phase=d.BodyPhase;
                Assert.That(d.CanStartTask,Is.True);Assert.That(d.StartTask(task),Is.True);
                Assert.That(d.Paused,Is.False,"Action button must resume its preview");
                Assert.That(d.State,Is.EqualTo(state));Assert.That(d.ActiveTask,Is.EqualTo(task));
                if(state!=Situation.Ground)Assert.That(d.BodyPhase,Is.EqualTo(phase));
                Assert.That(d.CanStartTask,Is.False);Assert.That(d.StartTask(task),Is.False);
                Advance(2.8f);Assert.That(d.State,Is.EqualTo(state));Assert.That(d.ActiveTask,Is.EqualTo(HandTask.None));
                Assert.That(d.CanStartTask,Is.True);Assert.That(d.HeadLookError,Is.LessThan(.2f));
            }
            d.Configure(equipment,Situation.Ladder,Motion.Up);Assert.That(d.CanStartTask,Is.False);
            Assert.That(d.StartTask(HandTask.Interact),Is.False);
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)] [TestCase(Context.Revolver)]
        public void PickupClosesFingersAndVisiblyLiftsBeforeReturning(Context equipment)
        {
            foreach(var state in new[]{Situation.Ground,Situation.Surface,Situation.Underwater})
            {
                d.Configure(equipment,state,Motion.Forward);Advance(1);
                bool left=equipment!=Context.Unarmed;
                var a=d.OutputAnimator;
                var finger=a.GetBoneTransform(left?HumanBodyBones.LeftMiddleIntermediate:HumanBodyBones.RightMiddleIntermediate);
                var hand=a.GetBoneTransform(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                var anatomy=new RaccoonActionHandsV37(a);
                Assert.That(d.StartTask(HandTask.Pickup),Is.True);
                Advance(.65f);Quaternion open=finger.localRotation;Vector3 touch=anatomy.PalmCenter(hand,left);
                Advance(.6f);Vector3 lifted=anatomy.PalmCenter(hand,left);
                Assert.That(Quaternion.Angle(open,finger.localRotation),Is.GreaterThan(15),"Pickup never grasps");
                Assert.That(lifted.y-touch.y,Is.GreaterThan(.08f),state+" pickup has no readable lift");
                Assert.That(d.TaskWristAngle,Is.LessThan(67));
                Assert.That(d.InteractionBusy,Is.True,"Lift should be visible before returning");
                Advance(1.1f);Assert.That(d.InteractionBusy,Is.False);
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void LostNonSerializedTaskHelpersAreRebuiltBeforeActionEvaluation(Context equipment)
        {
            d.Configure(equipment,Situation.Ground,Motion.Idle);Advance(1);
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            foreach(var state in new[]{Situation.Ground,Situation.Surface,Situation.Underwater})
            {
                d.Configure(equipment,state,Motion.Forward);Advance(1);
                foreach(var task in new[]{HandTask.Interact,HandTask.Pickup})
                {
                    d.GetType().GetField("armedInteraction",flags).SetValue(d,null);
                    d.GetType().GetField("unarmedInteraction",flags).SetValue(d,null);
                    Assert.DoesNotThrow(()=>d.Step(1f/60));
                    Assert.That(d.StartTask(task),Is.True);Advance(2.8f);
                    Assert.That(d.InteractionBusy,Is.False);
                }
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Unarmed,30)] [TestCase(Context.Unarmed,60)] [TestCase(Context.Unarmed,120)]
        [TestCase(Context.Shotgun,60)] [TestCase(Context.Revolver,60)] [TestCase(Context.Harpoon,60)]
        [TestCase(Context.Hammer,60)] [TestCase(Context.Food,60)] [TestCase(Context.Bundle,60)] [TestCase(Context.Fishing,60)] [TestCase(Context.RiflePreview,60)]
        public void SwimmingHandTasksPreserveBodyPhaseAndReturnToTheLiveSwimPose(Context equipment,int rate)
        {
            var referenceActor=UnityEngine.Object.Instantiate(d.SourcePrefab,new Vector3(1003,0,0),Quaternion.identity);
            foreach(var b in referenceActor.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;
            var referenceOwner=new GameObject("v54 uninterrupted swimming reference");
            var reference=referenceOwner.AddComponent<RaccoonContextActionsV50>();reference.enabled=false;
            reference.SourcePrefab=d.SourcePrefab;reference.Display=referenceActor.GetComponent<RaccoonActionReviewV36>();reference.Initialize();
            try
            {
                foreach(var state in new[]{Situation.Surface,Situation.Underwater})
                {
                    d.Configure(equipment,state,Motion.Forward);reference.Configure(equipment,state,Motion.Forward);
                    for(int i=0;i<rate;i++){d.Step(1f/rate);reference.Step(1f/rate);}
                    bool usesLeft=equipment!=Context.Unarmed&&equipment!=Context.Food&&equipment!=Context.Bundle&&equipment!=Context.Fishing;
                    var working=d.OutputAnimator.GetBoneTransform(usesLeft?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                    var sameHand=reference.OutputAnimator.GetBoneTransform(usesLeft?HumanBodyBones.LeftHand:HumanBodyBones.RightHand);
                    foreach(var task in new[]{HandTask.Interact,HandTask.Pickup})
                    {
                        float phase=d.BodyPhase;int ammo=d.Rounds;
                        Assert.That(d.StartSwimmingTask(task),Is.True);Assert.That(d.BodyPhase,Is.EqualTo(phase));
                        Assert.That(d.StartSwimmingTask(task),Is.False,"Repeated input restarts an active reach");
                        Assert.That(d.CanFire||d.CanReload,Is.False);
                        var invariant=new[]{HumanBodyBones.Hips,HumanBodyBones.Chest,HumanBodyBones.Head,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot,usesLeft?HumanBodyBones.RightHand:HumanBodyBones.LeftHand};
                        Vector3 last=working.position,firstFoot=d.OutputAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position;
                        Quaternion lastRotation=working.rotation;
                        float departure=0,footMotion=0;bool touched=false;
                        for(int i=0;i<rate*3;i++)
                        {
                            d.Step(1f/rate);reference.Step(1f/rate);float t=(i+1f)/rate;
                            Assert.That(d.State,Is.EqualTo(state));Assert.That(d.BodyPhase,Is.EqualTo(reference.BodyPhase).Within(.00001f));
                            foreach(var bone in invariant)
                            {
                                var actual=d.OutputAnimator.GetBoneTransform(bone);var expected=reference.OutputAnimator.GetBoneTransform(bone);
                                Assert.That(Vector3.Distance(d.Display.transform.InverseTransformPoint(actual.position),reference.Display.transform.InverseTransformPoint(expected.position)),Is.LessThan(.004f),bone+" changed outside working arm");
                                Assert.That(Quaternion.Angle(actual.rotation,expected.rotation),Is.LessThan(.3f),bone+" changed outside working arm");
                            }
                            Assert.That(Vector3.Distance(last,working.position),Is.LessThan(5f/rate),"Working hand snapped");last=working.position;
                            if(t>.45f&&d.InteractionBusy)Assert.That(Quaternion.Angle(lastRotation,working.rotation),Is.LessThan(1200f/rate),"Working wrist flips during lift or recovery");
                            lastRotation=working.rotation;
                            footMotion=Mathf.Max(footMotion,Vector3.Distance(firstFoot,d.OutputAnimator.GetBoneTransform(HumanBodyBones.LeftFoot).position));
                            float deviation=Vector3.Distance(d.Display.transform.InverseTransformPoint(working.position),reference.Display.transform.InverseTransformPoint(sameHand.position));
                            departure=Mathf.Max(departure,deviation);
                            if(t>(task==HandTask.Pickup?.63f:.48f)&&t<(task==HandTask.Pickup?.77f:.62f))
                            {touched=true;Assert.That(d.SwimmingContactError,Is.LessThan(.025f),"Palm did not reach swimming target");Assert.That(d.TaskWristAngle,Is.LessThan(67),"Wrist folds back during contact");}
                            if(t>2.3f){Assert.That(deviation,Is.LessThan(.004f));Assert.That(Quaternion.Angle(working.rotation,sameHand.rotation),Is.LessThan(.3f));}
                        }
                        Assert.That(touched,Is.True);Assert.That(departure,Is.GreaterThan(.08f));Assert.That(footMotion,Is.GreaterThan(.02f));
                        Assert.That(d.SwimmingTask,Is.EqualTo(HandTask.None));Assert.That(d.CanStartSwimmingTask,Is.True);Assert.That(d.Rounds,Is.EqualTo(ammo));
                        if(d.IsGun)Assert.That(d.CanFire&&d.CanReload,Is.True);
                    }
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(referenceOwner);UnityEngine.Object.DestroyImmediate(referenceActor);}
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)]
        public void SwimmingTargetsStayReachableInEveryDirectionWithRotatedGaze(Context equipment)
        {
            foreach(var state in new[]{Situation.Surface,Situation.Underwater})
            foreach(Motion direction in Enum.GetValues(typeof(Motion)))
            foreach(var task in new[]{HandTask.Interact,HandTask.Pickup})
            {
                d.Configure(equipment,state,direction);d.AimYaw=35;d.AimPitch=-15;Advance(1);
                Assert.That(d.StartSwimmingTask(task),Is.True);
                for(int i=0;i<150;i++)
                {
                    d.Step(1f/60);float t=(i+1f)/60;
                    if(t>(task==HandTask.Pickup?.63f:.48f)&&t<(task==HandTask.Pickup?.77f:.62f))
                    {
                        Assert.That(d.SwimmingContactError,Is.LessThan(.025f),state+" "+direction+" "+task+" target is outside arm reach");
                        Assert.That(d.TaskWristAngle,Is.LessThan(67),state+" "+direction+" wrist bends backwards");
                    }
                    Assert.That(d.HeadLookError,Is.LessThan(.2f));
                }
                Assert.That(d.SwimmingTask,Is.EqualTo(HandTask.None));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void SwimmingTaskCancelsReloadAndSurvivesSwimDirectionChangesButNotEquipmentChanges()
        {
            d.Configure(Context.Shotgun,Situation.Surface,Motion.Forward);Advance(1);Assert.That(d.Fire(),Is.True);Advance(.8f);
            Assert.That(d.Reload(),Is.True);Advance(.2f);int ammo=d.Rounds;
            Assert.That(d.StartSwimmingTask(HandTask.Pickup),Is.True);Assert.That(d.Reloading,Is.False);Assert.That(d.Rounds,Is.EqualTo(ammo));
            Advance(.2f);d.Configure(Context.Shotgun,Situation.Underwater,Motion.Down);Assert.That(d.SwimmingTask,Is.EqualTo(HandTask.Pickup));
            Advance(2.3f);Assert.That(d.InteractionBusy,Is.False);Assert.That(d.CanFire,Is.True);
            Assert.That(d.StartSwimmingTask(HandTask.Interact),Is.True);Advance(.2f);
            d.Configure(Context.Revolver,Situation.Underwater,Motion.Left);Assert.That(d.SwimmingTask,Is.EqualTo(HandTask.None));Advance(.5f);
            Assert.That(d.StartSwimmingTask(HandTask.Interact),Is.True);d.SetComparison(false);Assert.That(d.SwimmingTask,Is.EqualTo(HandTask.None));Assert.That(d.CanStartSwimmingTask,Is.False);
            d.SetComparison(true);d.Configure(Context.Revolver,Situation.Ground,Motion.Idle);Assert.That(d.StartSwimmingTask(HandTask.Pickup),Is.False);
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Shotgun)] [TestCase(Context.Revolver)] [TestCase(Context.Harpoon)] [TestCase(Context.RiflePreview)]
        public void WaterSupportsFireReloadAndRepeatedFireWithoutHolstering(Context equipment)
        {
            foreach(var state in new[]{Situation.Surface,Situation.Underwater})
            {
                d.Configure(Context.Unarmed,state,Motion.Forward);d.Configure(equipment,state,Motion.Forward);Advance(.8f);
                Assert.That(d.Fire(),Is.True);Assert.That(d.Rounds,Is.EqualTo(d.Capacity-1));Advance(.8f);
                Assert.That(d.Reload(),Is.True);Advance(4.5f);
                Assert.That(d.Reloading,Is.False);Assert.That(d.Rounds,Is.EqualTo(d.Capacity));Assert.That(d.Fire(),Is.True);
                Advance(.8f);Assert.That(d.SupportError,Is.LessThan(.006f));
                if(d.LongGun)Assert.That(d.Display.Weapon.activeSelf,Is.True);
                else Assert.That(Array.Exists(d.Display.Props,p=>p.activeSelf&&p.name==(equipment==Context.Revolver?"Revolver":"Harpoon")),Is.True);
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Motion.Up)] [TestCase(Motion.Down)]
        public void ShotgunClimbContinuesDuringPumpAndAmmoReload(Motion direction)
        {
            d.Configure(Context.Shotgun,Situation.Ladder,direction);Advance(.8f);
            Assert.That(d.Fire(),Is.True);Advance(.8f);Assert.That(d.Reload(),Is.True);
            var foot=d.OutputAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            Vector3 start=foot.position;float excursion=0;bool sawReload=false;
            for(int i=0;i<240;i++){d.Step(1f/60);if(d.Reloading){sawReload=true;excursion=Mathf.Max(excursion,Vector3.Distance(start,foot.position));}}
            Assert.That(sawReload,Is.True);Assert.That(excursion,Is.GreaterThan(.04f),"Feet must keep climbing during reload");
            Assert.That(d.Rounds,Is.EqualTo(d.Capacity));Assert.That(d.Fire(),Is.True);LogAssert.NoUnexpectedReceived();
        }
        [Test] public void RestraintCancelsReloadWithoutGrantingAmmoAndCanReleaseIntoSwimming()
        {
            d.Configure(Context.Revolver,Situation.Grabbed,Motion.Idle);Advance(.8f);Assert.That(d.Fire(),Is.True);Advance(.8f);
            int ammo=d.Rounds;Assert.That(d.Reload(),Is.True);Advance(.3f);
            d.Configure(Context.Revolver,Situation.Carried,Motion.Idle,Hands.RightFree);
            Assert.That(d.Reloading,Is.False);Assert.That(d.Rounds,Is.EqualTo(ammo));Assert.That(d.CanReload,Is.False);
            d.Configure(Context.Revolver,Situation.Carried,Motion.Idle,Hands.Restrained);Assert.That(d.Fire(),Is.False);
            d.Configure(Context.Revolver,Situation.Underwater,Motion.Forward);Advance(.5f);
            Assert.That(d.Reload(),Is.True);Advance(3);Assert.That(d.Rounds,Is.EqualTo(d.Capacity));
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void RepeatedSwimAndLadderLoopsKeepFiniteContinuousBonesAndGrip(int rate)
        {
            foreach(var state in new[]{Situation.Surface,Situation.Underwater,Situation.Ladder,Situation.Grabbed,Situation.Carried})
            {
                d.Configure(Context.Shotgun,state,state==Situation.Ladder?Motion.Up:Motion.Forward);Advance(.8f,rate);
                var bones=new[]{HumanBodyBones.Head,HumanBodyBones.LeftHand,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot};
                var last=Array.ConvertAll(bones,b=>d.OutputAnimator.GetBoneTransform(b).position);
                for(int i=0;i<rate*5;i++)
                {
                    d.Step(1f/rate);
                    for(int n=0;n<bones.Length;n++)
                    {
                        var p=d.OutputAnimator.GetBoneTransform(bones[n]).position;
                        Assert.That(float.IsFinite(p.x)&&float.IsFinite(p.y)&&float.IsFinite(p.z),Is.True);
                        Assert.That(Vector3.Distance(last[n],p),Is.LessThan(6f/rate),state+" "+bones[n]+" loop discontinuity");last[n]=p;
                    }
                    Assert.That(d.SupportError,Is.LessThan(.006f));
                }
            }
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void OriginalComparisonMatchesPreservedEquipmentPose()
        {
            d.Configure(Context.Revolver,Situation.Underwater,Motion.Forward);Advance(1);d.SetComparison(false);Advance(.5f);
            var a=d.OutputAnimator;var source=d.WeaponSource.GetComponentInChildren<Animator>();
            foreach(var b in new[]{HumanBodyBones.Head,HumanBodyBones.Hips,HumanBodyBones.RightHand,HumanBodyBones.LeftFoot})
            {
                Assert.That(Quaternion.Angle(a.GetBoneTransform(b).localRotation,source.GetBoneTransform(b).localRotation),Is.LessThan(.1f));
                Assert.That(Vector3.Distance(a.GetBoneTransform(b).localPosition,source.GetBoneTransform(b).localPosition),Is.LessThan(.0001f));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Shotgun,30)] [TestCase(Context.Shotgun,60)] [TestCase(Context.Shotgun,120)]
        [TestCase(Context.Revolver,60)] [TestCase(Context.Harpoon,60)] [TestCase(Context.RiflePreview,60)]
        public void ArmedInteractionLeavesGripDirectlyTouchesAndRestoresItWithoutMovement(Context equipment,int rate)
        {
            d.Configure(equipment,Situation.Ground,Motion.Idle);Advance(1,rate);
            var a=d.OutputAnimator;var left=a.GetBoneTransform(HumanBodyBones.LeftHand);var right=a.GetBoneTransform(HumanBodyBones.RightHand);
            float startY=left.position.y;
            Vector3 relative=right.InverseTransformPoint(left.position);
            Quaternion relativeRotation=Quaternion.Inverse(right.rotation)*left.rotation;
            var fingers=Array.ConvertAll(Game.Gameplay.Player.RaccoonEquipmentPose.LeftFingers,b=>a.GetBoneTransform(b));
            var rotations=Array.ConvertAll(fingers,b=>b.localRotation);
            int ammo=d.Rounds;
            for(int repeat=0;repeat<2;repeat++)
            {
                if(repeat==0)d.Configure(equipment,Situation.Interact,Motion.Idle);else d.RestartBody();
                Assert.That(d.CanFire,Is.False);Vector3 previous=left.position;bool touched=false;
                for(int i=0;i<rate*2;i++)
                {
                    d.Step(1f/rate);float t=(i+1f)/rate;
                    if(t<=.15f)Assert.That(left.position.y,Is.GreaterThan(startY-.06f),"Hand first drops to neutral at hip");
                    Assert.That(Vector3.Distance(previous,left.position),Is.LessThan(3f/rate),"Interaction boundary jumps");previous=left.position;
                    if(t>=.48f&&t<=.62f)
                    {
                        touched=true;Assert.That(d.InteractionContactError,Is.LessThan(.012f));
                        Assert.That(Vector3.Distance(right.TransformPoint(relative),left.position),Is.GreaterThan(.05f),"Hand never leaves the grip");
                    }
                    if(t>=RaccoonArmedInteractionV52.Duration+.15f)
                    {
                        Assert.That(Vector3.Distance(relative,right.InverseTransformPoint(left.position)),Is.LessThan(.006f),"Hand does not return to its original equipment pose");
                        Assert.That(Quaternion.Angle(relativeRotation,Quaternion.Inverse(right.rotation)*left.rotation),Is.LessThan(.2f));
                        for(int n=0;n<fingers.Length;n++)Assert.That(Quaternion.Angle(rotations[n],fingers[n].localRotation),Is.LessThan(.2f));
                    }
                }
                Assert.That(touched,Is.True);Assert.That(d.CanFire,Is.True);Assert.That(d.Rounds,Is.EqualTo(ammo));
            }
            Assert.That(d.Fire(),Is.True);Advance(.8f);Assert.That(d.Reload(),Is.True);Advance(4.5f);Assert.That(d.Rounds,Is.EqualTo(d.Capacity));
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void UnarmedInteractionOnlyMovesDominantArmAndKeepsBodyPlanted(int rate)
        {
            d.Configure(Context.Unarmed,Situation.Ground,Motion.Idle);Advance(1,rate);
            var a=d.OutputAnimator;
            var fixedBones=new[]{HumanBodyBones.LeftHand,HumanBodyBones.LeftLowerArm,HumanBodyBones.LeftUpperArm,HumanBodyBones.Hips,HumanBodyBones.Chest,HumanBodyBones.LeftFoot,HumanBodyBones.RightFoot};
            var positions=Array.ConvertAll(fixedBones,b=>a.GetBoneTransform(b).position);
            var rotations=Array.ConvertAll(fixedBones,b=>a.GetBoneTransform(b).rotation);
            var right=a.GetBoneTransform(HumanBodyBones.RightHand);var initial=right.position;var initialRotation=right.rotation;
            for(int repeat=0;repeat<2;repeat++)
            {
                if(repeat==0)d.Configure(Context.Unarmed,Situation.Interact,Motion.Idle);else d.RestartBody();
                float excursion=0;
                for(int frame=0;frame<rate*2;frame++)
                {
                    d.Step(1f/rate);excursion=Mathf.Max(excursion,Vector3.Distance(initial,right.position));
                    for(int n=0;n<fixedBones.Length;n++)
                    {
                        Assert.That(Vector3.Distance(positions[n],a.GetBoneTransform(fixedBones[n]).position),Is.LessThan(.003f),fixedBones[n]+" moves during a one-hand reach");
                        Assert.That(Quaternion.Angle(rotations[n],a.GetBoneTransform(fixedBones[n]).rotation),Is.LessThan(.3f));
                    }
                }
                Assert.That(excursion,Is.GreaterThan(.15f));
                Assert.That(Vector3.Distance(initial,right.position),Is.LessThan(.003f));
                Assert.That(Quaternion.Angle(initialRotation,right.rotation),Is.LessThan(.3f));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)] [TestCase(Context.Revolver)]
        [TestCase(Context.Harpoon)] [TestCase(Context.Hammer)] [TestCase(Context.Food)]
        [TestCase(Context.Bundle)] [TestCase(Context.Fishing)] [TestCase(Context.RiflePreview)]
        public void PickupRestoresBothHandsAndFingersWithoutMovement(Context equipment)
        {
            d.Configure(equipment,Situation.Ground,Motion.Idle);Advance(1);
            var a=d.OutputAnimator;
            var bones=new System.Collections.Generic.List<HumanBodyBones>{HumanBodyBones.LeftHand,HumanBodyBones.RightHand};
            bones.AddRange(Game.Gameplay.Player.RaccoonEquipmentPose.LeftFingers);bones.AddRange(Game.Gameplay.Player.RaccoonEquipmentPose.RightFingers);
            var positions=bones.ConvertAll(b=>a.GetBoneTransform(b).position);
            var rotations=bones.ConvertAll(b=>a.GetBoneTransform(b).rotation);
            int ammo=d.Rounds;
            for(int repeat=0;repeat<2;repeat++)
            {
                if(repeat==0)d.Configure(equipment,Situation.Pickup,Motion.Idle);else d.RestartBody();
                var active=a.GetBoneTransform(equipment==Context.Unarmed?HumanBodyBones.RightHand:HumanBodyBones.LeftHand);
                Vector3 initial=active.position;float excursion=0;
                for(int frame=0;frame<180;frame++)
                {
                    d.Step(1f/60);excursion=Mathf.Max(excursion,Vector3.Distance(initial,active.position));
                }
                Assert.That(excursion,Is.GreaterThan(.10f),"Pickup never reaches");
                for(int n=0;n<bones.Count;n++)
                {
                    Assert.That(Vector3.Distance(positions[n],a.GetBoneTransform(bones[n]).position),Is.LessThan(.006f),equipment+" "+bones[n]+" fails to return");
                    Assert.That(Quaternion.Angle(rotations[n],a.GetBoneTransform(bones[n]).rotation),Is.LessThan(.3f),equipment+" "+bones[n]+" rotation fails to return");
                }
                Assert.That(d.Rounds,Is.EqualTo(ammo));
                if(d.IsGun)Assert.That(d.CanFire&&d.CanReload,Is.True);
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Unarmed)] [TestCase(Context.Shotgun)] [TestCase(Context.Revolver)]
        public void AllSituationsShareGazeIndependentOfBodyLeanOrHeldItem(Context equipment)
        {
            d.Configure(equipment,Situation.Ground,Motion.Idle);d.AimYaw=20;d.AimPitch=-10;Advance(1);
            var head=d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head);
            Quaternion expected=head.rotation;
            foreach(Situation situation in Enum.GetValues(typeof(Situation)))
            {
                d.Configure(equipment,situation,Motion.Forward);Advance(1);
                Quaternion direction=expected;
                Assert.That(Quaternion.Angle(direction,head.rotation),Is.LessThan(.15f),equipment+" "+situation+" gaze follows body instead of look direction");
                Assert.That(d.SupportError,Is.LessThan(.006f));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(30)] [TestCase(60)] [TestCase(120)]
        public void LookReversalIsBoundedAndUnderwaterDirectionsKeepTheSameGaze(int rate)
        {
            d.Configure(Context.Shotgun,Situation.Underwater,Motion.Forward);d.AimYaw=60;d.AimPitch=-25;Advance(1,rate);
            var head=d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head);
            Quaternion last=head.rotation;d.AimYaw=-60;
            for(int i=0;i<rate;i++)
            {
                d.Step(1f/rate);
                Assert.That(Quaternion.Angle(last,head.rotation),Is.LessThan(245f/rate));last=head.rotation;
                Assert.That(d.SupportError,Is.LessThan(.006f));
            }
            Quaternion expected=head.rotation;
            foreach(Motion direction in Enum.GetValues(typeof(Motion)))
            {
                d.Configure(Context.Shotgun,Situation.Underwater,direction);Advance(1,rate);
                Assert.That(Quaternion.Angle(expected,head.rotation),Is.LessThan(.15f),"Swimming direction drags gaze");
                Assert.That(d.HeadLookError,Is.LessThan(.15f));
            }
            LogAssert.NoUnexpectedReceived();
        }
        [Test] public void FireAndReloadDoNotDragHeadWithWeaponRecoilOrReloadTilt()
        {
            foreach(var state in new[]{Situation.Surface,Situation.Underwater,Situation.Ladder,Situation.Grabbed,Situation.Carried})
            {
                d.Configure(Context.Unarmed,state,Motion.Forward);d.Configure(Context.Shotgun,state,Motion.Forward);Advance(1);
                var head=d.OutputAnimator.GetBoneTransform(HumanBodyBones.Head);Quaternion expected=head.rotation;
                Assert.That(d.Fire(),Is.True);
                for(int i=0;i<60;i++){d.Step(1f/60);Assert.That(Quaternion.Angle(expected,head.rotation),Is.LessThan(.15f));}
                Assert.That(d.Reload(),Is.True);
                for(int i=0;i<210;i++){d.Step(1f/60);Assert.That(Quaternion.Angle(expected,head.rotation),Is.LessThan(.15f));}
            }
            LogAssert.NoUnexpectedReceived();
        }
        [TestCase(Context.Harpoon)] [TestCase(Context.Shotgun)]
        public void WristDeformationJointsFollowElevatedReloadInsteadOfLeavingStretchedMesh(Context equipment)
        {
            d.Configure(equipment,Situation.Carried,Motion.Idle);Advance(.8f);Assert.That(d.Fire(),Is.True);Advance(.8f);Assert.That(d.Reload(),Is.True);
            var skin=d.Display.GetComponentInChildren<SkinnedMeshRenderer>();
            var hand=d.OutputAnimator.GetBoneTransform(HumanBodyBones.LeftHand);
            for(int i=0;i<180;i++)
            {
                d.Step(1f/60);
                for(int n=1;n<=3;n++)
                {
                    var bone=Array.Find(skin.bones,b=>b.name=="LeftWristBlend"+n);
                    Assert.That(Vector3.Distance(bone.position,hand.position),Is.LessThan(.18f),"Wrist helper left behind by composed arm pose");
                }
            }
            LogAssert.NoUnexpectedReceived();
        }
#endif
    }
}
