using System.Collections;
using System.Reflection;
using Game.Gameplay.Player;
using Game.Gameplay.Inventory;
using Game.Gameplay.Harpoon;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonEquipmentTests
    {
        private GameObject _managerObject;
        private NetworkManager _manager;
        private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        private static T Field<T>(object owner,string name)=>(T)owner.GetType().GetField(name,Private).GetValue(owner);
        private static void Set(object owner,string name,object value)=>owner.GetType().GetField(name,Private).SetValue(owner,value);

        [TestCase(true,HotbarItemType.Harpoon,1,false,false,false)]
        [TestCase(true,HotbarItemType.Harpoon,2,false,false,true)]
        [TestCase(true,HotbarItemType.Revolver,2,true,false,true)]
        [TestCase(true,HotbarItemType.Revolver,3,true,false,true)]
        [TestCase(true,HotbarItemType.Revolver,2,false,false,false)]
        [TestCase(true,HotbarItemType.Shotgun,3,true,true,false)]
        [TestCase(false,HotbarItemType.Harpoon,3,true,false,false)]
        [TestCase(true,HotbarItemType.None,2,true,false,true)]
        public void LeftHarpoonVisibilityMatchesTierAndHandOccupancy(bool visible,HotbarItemType item,int tier,bool grab,bool two,bool expected)
        {
            Assert.That(PlayerAimView.ShouldShowLeftHarpoon(visible,item,tier,grab,two),Is.EqualTo(expected));
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator PlayerSpawn_EquipmentSwitch_LeftHarpoonAndHide()
        {
            GameObject prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Prefabs/Player.prefab");
            Assert.That(prefab,Is.Not.Null);
            _managerObject=new GameObject("Raccoon Equipment Test Network");
            _manager=_managerObject.AddComponent<NetworkManager>();
            var transport=_managerObject.AddComponent<UnityTransport>();transport.SetConnectionData("127.0.0.1",17893);
            _manager.NetworkConfig=new NetworkConfig { NetworkTransport=transport, PlayerPrefab=prefab, EnableSceneManagement=false };
            Assert.That(_manager.StartHost(),Is.True);
            yield return null;yield return null;
            GameObject player=_manager.LocalClient.PlayerObject.gameObject;
            var view=player.GetComponent<PlayerCharacterView>();
            Assert.That(view.ActiveAnimator.GetComponent<RaccoonEquipmentPose>(),Is.Not.Null);
            var animator=view.ActiveAnimator;
            var aim=player.GetComponent<PlayerAimView>();var hotbar=player.GetComponent<HotbarController>();
            var harpoon=player.GetComponent<HarpoonController>();
            Assert.That(aim.HeldItem,Is.EqualTo(HotbarItemType.Harpoon));
            Field<NetworkVariable<byte>>(harpoon,"_tier").Value=2;
            yield return null;yield return null;
            Assert.That(aim.LeftHarpoonHeld,Is.True);
            Transform left=animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Assert.That(left.Find("LeftHeldWeaponSocket"),Is.Not.Null);
            Assert.That(left.GetComponentsInChildren<MeshRenderer>().Length,Is.GreaterThan(0));
            var machine=Field<HarpoonStateMachine>(harpoon,"_stateMachine");
            machine.TryFire();machine.NotifyLocalHit();machine.NotifyGrabApproved();machine.NotifyHeld();
            Set(hotbar,"_selectedIndex",1);
            yield return null;yield return null;
            Assert.That(aim.HeldItem,Is.EqualTo(HotbarItemType.Revolver));
            Assert.That(aim.LeftHarpoonHeld,Is.True);
            // Let the real Animator, aim, equipment solver and elbow correction update together.
            for(int frame=0;frame<24;frame++)yield return null;
            Transform right=animator.GetBoneTransform(HumanBodyBones.RightHand);
            Assert.That(right.Find("HeldWeaponSocket"),Is.Not.Null);
            bool fittedGrip=false;
            foreach(var filter in right.GetComponentsInChildren<MeshFilter>())
                fittedGrip|=filter.sharedMesh!=null&&filter.sharedMesh.name=="Raccoon_Revolver_Grip";
            Assert.That(fittedGrip,Is.True,"The player's enlarged revolver must use the fitted handle mesh.");
            Assert.That(Vector3.Distance(right.position,left.position),Is.InRange(.2f,1.2f));
            foreach(Transform bone in animator.GetComponentsInChildren<Transform>())
                Assert.That(float.IsFinite(bone.position.sqrMagnitude),Is.True,bone.name);
            foreach(MeshRenderer renderer in left.GetComponentsInChildren<MeshRenderer>())Assert.That(renderer.enabled,Is.True);
            var inventory=player.GetComponent<PlayerInventory>();
            var slots=inventory.ServerCopySlotViews();slots[3]=new HotbarSlotView(HotbarItemType.Shotgun,1);inventory.ServerApplySlotViews(slots);
            Set(hotbar,"_selectedIndex",3);machine.TryReleaseForWeaponSwitch();
            for(int frame=0;frame<24;frame++)yield return null;
            Assert.That(aim.HeldItem,Is.EqualTo(HotbarItemType.Shotgun));Assert.That(aim.LeftHarpoonHeld,Is.False);
            Assert.That(left.GetComponentsInChildren<MeshRenderer>().Length,Is.EqualTo(0));
            Assert.That(right.GetComponentsInChildren<MeshRenderer>().CountEnabled(),Is.EqualTo(1));
            Set(hotbar,"_panelOpen",true);
            yield return null;yield return null;
            Assert.That(aim.HeldItem,Is.EqualTo(HotbarItemType.None));Assert.That(aim.LeftHarpoonHeld,Is.False);
            foreach(MeshRenderer renderer in right.GetComponentsInChildren<MeshRenderer>())Assert.That(renderer.enabled,Is.False);
            Assert.That(animator.GetComponentInChildren<SkinnedMeshRenderer>().shadowCastingMode,Is.EqualTo(UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly));
        }

        [Test]
        public void WeaponGripClosesAllFourFingersIncludingIndex()
        {
            var character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/CH_Raccoon_Player.prefab"));
            try
            {
                var animator=character.GetComponent<Animator>();animator.runtimeAnimatorController=null;
                character.GetComponent<RaccoonEquipmentPose>().Apply(HotbarItemType.Revolver,true,0,10,character.transform);
                foreach(var fingers in new[]{RaccoonEquipmentPose.RightFingers,RaccoonEquipmentPose.LeftFingers})
                for(int i=3;i<15;i+=3)
                {
                    Vector3 a=animator.GetBoneTransform(fingers[i]).position,b=animator.GetBoneTransform(fingers[i+1]).position,c=animator.GetBoneTransform(fingers[i+2]).position;
                    Assert.That(Vector3.Angle(b-a,c-b),Is.InRange(60f,120f),fingers[i]+" has an invalid grip bend");
                    Vector3 inward=fingers==RaccoonEquipmentPose.RightFingers?Vector3.left:Vector3.right;
                    // A finger must wrap to the opposite side, rather than folding back into the handle.
                    float fingerReach=Vector3.Distance(a,b)+Vector3.Distance(b,c);
                    Assert.That(Vector3.Dot(c-a,inward),Is.GreaterThan(fingerReach*.6f),fingers[i]+" did not wrap across the grip");
                }
            }
            finally{Object.DestroyImmediate(character);}
        }

        [Test]
        public void CalibratedArmsReachTargetsAtAimExtremes()
        {
            GameObject instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/CH_Raccoon_Player.prefab"));
            try
            {
                Animator animator=instance.GetComponent<Animator>();animator.runtimeAnimatorController=null;
                var pose=instance.GetComponent<RaccoonEquipmentPose>();var profile=pose.Profile;
                Transform[] bones=instance.GetComponentsInChildren<Transform>();
                var rotations=new Quaternion[bones.Length];for(int i=0;i<bones.Length;i++)rotations[i]=bones[i].localRotation;
                foreach(var entry in profile.Entries)
                foreach(float pitch in new[]{-85f,-60f,-30f,0f,30f,60f,85f})
                {
                    for(int i=0;i<bones.Length;i++)bones[i].localRotation=rotations[i];
                    pose.Apply(entry.Item,false,pitch,10,instance.transform);
                    Quaternion aim=Quaternion.Euler(pitch,0,0);
                    Vector3 reference=entry.TwoHanded?(profile.LeftShoulder+profile.RightShoulder)*.5f:profile.RightShoulder;
                    Quaternion positionAim=Quaternion.Euler(pitch*profile.HandPositionPitchWeight,0,0);
                    Vector3 origin=entry.TwoHanded
                        ? (animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position+animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position)*.5f
                        : animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position;
                    Vector3 target=origin+positionAim*(entry.Right.Position-reference);
                    target.z=Mathf.Max(profile.MinimumGripForward+Mathf.Clamp01((-pitch-45f)/40f)*.08f,target.z);
                    Transform right=animator.GetBoneTransform(HumanBodyBones.RightHand);
                    Assert.That(right.position.z,Is.GreaterThan(.40f),entry.Item+" wrist behind torso pitch="+pitch);
                    // A two-hand weapon moves to a shared reachable anchor. Test the solved
                    // anchor here; actual palm-to-weapon contact has separate surface tests.
                    if(entry.TwoHanded)target=instance.transform.TransformPoint(pose.RightGripTargetLocal);
                    Assert.That(Vector3.Distance(target,right.position),Is.LessThan(.002f),entry.Item+" right pitch="+pitch);
                    if(entry.TwoHanded)
                    {
                        target=right.position+aim*(entry.Left.Position-entry.Right.Position);
                        Assert.That(Vector3.Distance(target,animator.GetBoneTransform(HumanBodyBones.LeftHand).position),Is.LessThan(.002f),entry.Item+" left pitch="+pitch);
                    }
                    foreach(Transform bone in bones)Assert.That(float.IsFinite(bone.position.sqrMagnitude),Is.True);
                }
            }
            finally {Object.DestroyImmediate(instance);}
        }
#endif

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if(_manager!=null)_manager.Shutdown();yield return null;
            if(_managerObject!=null)Object.Destroy(_managerObject);yield return null;
        }
    }

    internal static class RaccoonRendererTestExtensions
    {
        public static int CountEnabled(this MeshRenderer[] renderers)
        {
            int count=0;foreach(var renderer in renderers)if(renderer.enabled)count++;return count;
        }
    }
}
