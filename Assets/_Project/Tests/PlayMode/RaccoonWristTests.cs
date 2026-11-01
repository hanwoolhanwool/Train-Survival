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
    public sealed class RaccoonWristTests
    {
#if UNITY_EDITOR
        [Test]
        public void SupportThumbBaseDoesNotTearAwayFromPalm()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/CH_Raccoon_Player.prefab"));
            var baked=new Mesh();
            try
            {
                var animator=go.GetComponent<Animator>();var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();var pose=go.GetComponent<RaccoonEquipmentPose>();
                // Surface edges crossing the two previously misweighted thumb-base vertices.
                Vector3[] points={new Vector3(-.61601257f,.78612214f,.35534766f),new Vector3(-.61905533f,.79053620f,.36086243f),
                    new Vector3(-.66245574f,.81959951f,.35680756f),new Vector3(-.66518861f,.82043919f,.34940901f)};
                var rest=skin.sharedMesh.vertices.Select(skin.transform.TransformPoint).ToArray();
                var indices=points.Select(p=>Enumerable.Range(0,rest.Length).OrderBy(i=>(rest[i]-p).sqrMagnitude).First()).ToArray();
                for(int i=0;i<points.Length;i++)Assert.That(Vector3.Distance(rest[indices[i]],points[i]),Is.LessThan(.0001f));
                animator.Rebind();animator.Update(0);animator.SetInteger("HoldPose",2);animator.SetFloat("RaccoonShotgunRun",1);
                foreach(float speed in new[]{0f,4.5f,7f})foreach(float pitch in new[]{-60f,0f,60f})
                {
                    animator.SetFloat("Speed",speed);animator.SetLayerWeight(animator.GetLayerIndex("Hold"),1-pose.Profile.AuthoredRunWeight(animator));
                    for(int frame=0;frame<90;frame++)
                    {
                        animator.Update(1f/60);pose.Apply(HotbarItemType.Shotgun,false,pitch,1f/60,go.transform);
                        if(frame<30||frame%3!=0)continue;
                        skin.BakeMesh(baked,true);var vertices=baked.vertices;
                        for(int pair=0;pair<indices.Length;pair+=2)
                        {
                            float distance=Vector3.Distance(skin.transform.TransformPoint(vertices[indices[pair]]),skin.transform.TransformPoint(vertices[indices[pair+1]]));
                            Assert.That(distance/Vector3.Distance(rest[indices[pair]],rest[indices[pair+1]]),Is.InRange(.4f,2f),"Thumb-base surface edge stretched into a spike");
                        }
                    }
                }
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(go);}
        }

        [Test]
        public void SupportElbowRemainsBentWhileHoldingShotgun()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/CH_Raccoon_Player.prefab"));
            try
            {
                var a=go.GetComponent<Animator>();var pose=go.GetComponent<RaccoonEquipmentPose>();
                a.Rebind();a.Update(0);a.SetInteger("HoldPose",2);a.SetFloat("RaccoonShotgunRun",1);
                var upper=a.GetBoneTransform(HumanBodyBones.LeftUpperArm);var elbow=a.GetBoneTransform(HumanBodyBones.LeftLowerArm);var wrist=a.GetBoneTransform(HumanBodyBones.LeftHand);
                foreach(float speed in new[]{0f,4.5f,7f})foreach(float pitch in new[]{-60f,0f,60f})
                {
                    a.SetFloat("Speed",speed);a.SetLayerWeight(a.GetLayerIndex("Hold"),1-pose.Profile.AuthoredRunWeight(a));
                    for(int frame=0;frame<90;frame++)
                    {
                        a.Update(1f/60);pose.Apply(HotbarItemType.Shotgun,false,pitch,1f/60,go.transform);
                        if(frame<30)continue;
                        float bend=Vector3.Angle(elbow.position-upper.position,wrist.position-elbow.position);
                        Assert.That(bend,Is.InRange(Mathf.Lerp(35f,0f,Mathf.Abs(pitch)/85f),125f),"Support elbow must have visible flex, including while aiming");
                    }
                }
            }
            finally{Object.DestroyImmediate(go);}
        }

        [Test]
        public void SupportWristKeepsItsCrossSectionDuringLocomotionAndAiming()
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/CH_Raccoon_Player.prefab"));var baked=new Mesh();
            try
            {
                var a=go.GetComponent<Animator>();var skin=go.GetComponentInChildren<SkinnedMeshRenderer>();
                // Four opposing surface pairs around the approved wrist ring, in the neutral asset's metre space.
                Vector3[] points={new Vector3(-.60741335f,.80993636f,.34812978f),new Vector3(-.65367359f,.81978777f,.24933890f),new Vector3(-.60190535f,.79170026f,.32011542f),new Vector3(-.66541541f,.82968784f,.27984452f),new Vector3(-.60598928f,.78558907f,.28441453f),new Vector3(-.66139269f,.83567103f,.31537551f),new Vector3(-.62439889f,.79128703f,.25073209f),new Vector3(-.64279556f,.83068494f,.34949514f)};
                var rest=skin.sharedMesh.vertices.Select(skin.transform.TransformPoint).ToArray();
                var indices=points.Select(p=>Enumerable.Range(0,rest.Length).OrderBy(i=>(rest[i]-p).sqrMagnitude).First()).ToArray();
                for(int i=0;i<points.Length;i++)Assert.That(Vector3.Distance(rest[indices[i]],points[i]),Is.LessThan(.0001f),"Surface probe must map to the actual wrist");
                var pose=go.GetComponent<RaccoonEquipmentPose>();Assert.That(go.GetComponent<RaccoonWristDeformation>(),Is.Not.Null);
                a.Rebind();a.Update(0);a.SetInteger("HoldPose",2);a.SetFloat("RaccoonShotgunRun",1);
                float minimumRatio=999,maximumRatio=0;
                foreach(float speed in new[]{0f,4.5f,7f})foreach(float pitch in new[]{-60f,0f,60f})
                {
                    a.SetFloat("Speed",speed);a.SetLayerWeight(a.GetLayerIndex("Hold"),1-pose.Profile.AuthoredRunWeight(a));
                    for(int frame=0;frame<90;frame++)
                    {
                        a.Update(1f/60);pose.Apply(HotbarItemType.Shotgun,false,pitch,1f/60,go.transform);
                        if(frame<30 || frame%3!=0)continue;
                        skin.BakeMesh(baked,true);var vertices=baked.vertices;
                        for(int pair=0;pair<indices.Length;pair+=2)
                        {
                            float diameter=Vector3.Distance(skin.transform.TransformPoint(vertices[indices[pair]]),skin.transform.TransformPoint(vertices[indices[pair+1]]));
                            float ratio=diameter/Vector3.Distance(rest[indices[pair]],rest[indices[pair+1]]);
                            minimumRatio=Mathf.Min(minimumRatio,ratio);maximumRatio=Mathf.Max(maximumRatio,ratio);
                        }
                    }
                }
                Assert.That(minimumRatio,Is.GreaterThan(.75f),"Support wrist cross-section must not pinch");
                Assert.That(maximumRatio,Is.LessThan(1.25f),"Support wrist must not balloon");
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(go);}
        }
#endif
    }
}
