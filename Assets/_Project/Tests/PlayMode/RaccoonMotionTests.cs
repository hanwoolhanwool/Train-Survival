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
    public sealed class RaccoonMotionTests
    {
#if UNITY_EDITOR
        private const string Character="Assets/_Project/Art/Characters/Raccoon/CH_Raccoon_Player.prefab";

        private static Quaternion[] SampleTail(int fps)
        {
            GameObject instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Character));
            try
            {
                var animator=instance.GetComponent<Animator>();animator.runtimeAnimatorController=null;
                var tail=instance.GetComponent<RaccoonTailMotion>();Assert.That(tail,Is.Not.Null);tail.ResetMotion();
                for(int i=0;i<fps*5;i++)
                {
                    float time=i/(float)fps;
                    tail.Step(time<1?0:time<3?7:0,time>=2&&time<3?90:0,1f/fps);
                }
                return instance.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tail")).OrderBy(t=>t.name).Select(t=>t.localRotation).ToArray();
            }
            finally {Object.DestroyImmediate(instance);}
        }

        [Test]
        public void TailSettlesConsistentlyAtDifferentFrameRates()
        {
            Quaternion[] slow=SampleTail(30),fast=SampleTail(120);
            Assert.That(slow.Length,Is.EqualTo(5));
            for(int i=0;i<5;i++)Assert.That(Quaternion.Angle(slow[i],fast[i]),Is.LessThan(.35f),"Tail "+i);
        }

        [Test]
        public void TailIsBoundedAndRestoresOnDeathAndDisable()
        {
            GameObject instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Character));
            try
            {
                instance.GetComponent<Animator>().runtimeAnimatorController=null;
                var tail=instance.GetComponent<RaccoonTailMotion>();tail.ResetMotion();
                Transform[] bones=instance.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tail")).ToArray();
                Quaternion[] rest=bones.Select(t=>t.localRotation).ToArray();
                for(int i=0;i<1800;i++)tail.Step(7,i%120<60?720:-720,1f/60);
                tail.Step(7,10000,4);
                for(int i=0;i<bones.Length;i++)Assert.That(Quaternion.Angle(rest[i],bones[i].localRotation),Is.LessThan(28f));
                tail.Step(7,100,1f/60,false);
                for(int i=0;i<bones.Length;i++)Assert.That(Quaternion.Angle(rest[i],bones[i].localRotation),Is.LessThan(.01f));
                for(int i=0;i<60;i++)tail.Step(7,100,1f/60);
                tail.enabled=false;
                for(int i=0;i<bones.Length;i++)Assert.That(Quaternion.Angle(rest[i],bones[i].localRotation),Is.LessThan(.01f));
            }
            finally {Object.DestroyImmediate(instance);}
        }

        [Test]
        public void LivePlayerPrefabIncludesRaccoonTailMotion()
        {
            var player=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");
            Assert.That(player.GetComponentsInChildren<RaccoonTailMotion>(true).Length,Is.EqualTo(1));
        }

        [Test]
        public void LocomotionCyclesStayNearThePlayerRoot()
        {
            GameObject instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Character));
            try
            {
                var animator=instance.GetComponent<Animator>();animator.Rebind();animator.Update(0);
                foreach(float speed in new[]{0f,4.5f,7f,0f})
                {
                    animator.SetFloat("Speed",speed);
                    for(int i=0;i<90;i++)
                    {
                        animator.Update(1f/30);
                        Vector3 hips=instance.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.Hips).position);
                        Assert.That(new Vector2(hips.x,hips.z).magnitude,Is.LessThan(.15f),"Locomotion offset at speed "+speed);
                    }
                }
            }
            finally {Object.DestroyImmediate(instance);}
        }

        [Test]
        public void WalkingWeaponTransitionsKeepHandsContinuous()
        {
            GameObject instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Character));
            try
            {
                var animator=instance.GetComponent<Animator>();animator.Rebind();animator.Update(0);
                var pose=instance.GetComponent<RaccoonEquipmentPose>();
                animator.SetLayerWeight(animator.GetLayerIndex("Hold"),1);
                Vector3 previous=Vector3.zero;float maximum=0;
                for(int i=0;i<150;i++)
                {
                    HotbarItemType item=i<45?HotbarItemType.Shotgun:i<90?HotbarItemType.Revolver:HotbarItemType.Shotgun;
                    animator.SetFloat("Speed",4.5f);animator.SetInteger("HoldPose",item==HotbarItemType.Shotgun?2:1);animator.Update(1f/30);
                    pose.Apply(item,false,0,1f/30,instance.transform);
                    Vector3 current=animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                    if(i>10)maximum=Mathf.Max(maximum,Vector3.Distance(previous,current));previous=current;
                    Assert.That(float.IsFinite(current.sqrMagnitude),Is.True);
                }
                Assert.That(maximum,Is.LessThan(.10f),"One-frame wrist jump while walking and changing weapons");
            }
            finally {Object.DestroyImmediate(instance);}
        }
#endif
    }
}
