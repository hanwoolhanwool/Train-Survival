using System.Linq;
using Game.Gameplay.Player;
using Game.Gameplay.Inventory;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonContactTests
    {
#if UNITY_EDITOR
        [TestCase(false)]
        [TestCase(true)]
        public void SupportPalmContactIsMeasuredAgainstTheWeaponSurface(bool formerSideOffset)
        {
            var character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/CH_Raccoon_Player.prefab"));
            var baked=new Mesh();RaccoonEquipmentProfile profile=null;
            try
            {
                var animator=character.GetComponent<Animator>();var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();var pose=character.GetComponent<RaccoonEquipmentPose>();
                profile=Object.Instantiate(pose.Profile);pose.Bind(profile);profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
                // Keep the former hand-to-weapon offset even if the paired stance moves.
                if(formerSideOffset)entry.Left.Position=entry.Right.Position+new Vector3(-.16f,-.065f,.39f);
                var hand=animator.GetBoneTransform(HumanBodyBones.RightHand);
                var socket=new GameObject("Contact test socket").transform;socket.SetParent(hand,false);
                var s=hand.lossyScale;socket.localScale=new Vector3(1/s.x,1/s.y,1/s.z);
                var weapon=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/Weapon_Shotgun.fbx"));
                Vector3 originalScale=weapon.transform.localScale;weapon.transform.SetParent(socket,false);
                weapon.transform.localPosition=entry.Socket.Position;weapon.transform.localRotation=entry.Socket.Rotation;weapon.transform.localScale=Vector3.Scale(originalScale,entry.Socket.Scale);
                var filter=weapon.GetComponentInChildren<MeshFilter>();if(entry.MeshOverride!=null)filter.sharedMesh=entry.MeshOverride;
                var collider=filter.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=filter.sharedMesh;
                Vector3[] points={new Vector3(-.67736143f,.71899243f,.37856841f),new Vector3(-.67090786f,.72500354f,.36930209f),
                    new Vector3(-.67200011f,.72199883f,.35605383f),new Vector3(-.66709787f,.72142947f,.31177935f),new Vector3(-.64423990f,.74242064f,.29967871f)};
                var rest=skin.sharedMesh.vertices.Select(skin.transform.TransformPoint).ToArray();
                var indices=points.Select(p=>Enumerable.Range(0,rest.Length).OrderBy(i=>(rest[i]-p).sqrMagnitude).First()).ToArray();
                for(int i=0;i<points.Length;i++)Assert.That(Vector3.Distance(rest[indices[i]],points[i]),Is.LessThan(.0001f));
                animator.Rebind();animator.Update(0);animator.SetInteger("HoldPose",2);animator.SetFloat("RaccoonShotgunRun",1);
                foreach(float speed in new[]{0f,4.5f,7f})foreach(float pitch in new[]{-60f,0f,60f})
                {
                    animator.SetFloat("Speed",speed);animator.SetLayerWeight(animator.GetLayerIndex("Hold"),1-profile.AuthoredRunWeight(animator));
                    for(int frame=0;frame<90;frame++)
                    {
                        animator.Update(1f/60);pose.Apply(HotbarItemType.Shotgun,false,pitch,1f/60,character.transform);
                        if(frame<45||frame%15!=0)continue;
                        Physics.SyncTransforms();skin.BakeMesh(baked,true);var vertices=baked.vertices;
                        Vector3 up=Quaternion.Euler(pitch,0,0)*Vector3.up;int contacts=0;
                        foreach(int index in indices)
                        {
                            Vector3 point=skin.transform.TransformPoint(vertices[index]);
                            if(collider.Raycast(new Ray(point-up*.025f,up),out var hit,.06f))
                            {
                                float gap=Vector3.Dot(hit.point-point,up);
                                if(gap>=-.004f && gap<=.004f && Vector3.Dot(hit.normal,up)<-.2f)contacts++;
                            }
                        }
                        if(formerSideOffset)Assert.That(contacts,Is.LessThan(3),"The former floating palm must fail the contact check");
                        else Assert.That(contacts,Is.GreaterThanOrEqualTo(3),$"Palm must contact the foregrip underside: speed={speed}, pitch={pitch}, samples={contacts}/5");
                    }
                }
            }
            finally{Object.DestroyImmediate(baked);Object.DestroyImmediate(character);if(profile!=null)Object.DestroyImmediate(profile);}
        }
#endif
    }
}
