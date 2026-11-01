using Game.Gameplay.Debugging;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonReloadWristV34Tests
    {
#if UNITY_EDITOR
        [TestCase(30,0)] [TestCase(60,0)] [TestCase(120,0)]
        [TestCase(30,1)] [TestCase(60,1)] [TestCase(120,1)]
        [TestCase(30,2)] [TestCase(60,2)] [TestCase(120,2)]
        public void ReloadWristAndElbowRemainAnatomicallyContinuous(int fps,int movement)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/CombatCandidate_v33/PC_Raccoon_Combat_v33.prefab"));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,15);floor.transform.localScale=new Vector3(40,1,60);
            try
            {
                var lab=go.GetComponent<RaccoonCombatMotorV33>();lab.enabled=false;lab.ResetAt(new Vector3(1000,.03f,0));
                float dt=1f/fps;Vector2 input=movement==0?Vector2.zero:movement==1?Vector2.left:Vector2.up;
                for(int i=0;i<3*fps;i++)
                {
                    if(i==fps||i==2*fps)Assert.That(lab.Combat.Fire(),Is.True);
                    lab.Advance(dt,input,movement==2);lab.Evaluate(dt);
                }
                Assert.That(lab.Combat.Reload(),Is.True);
                var a=lab.Rig.Animator;var upper=a.GetBoneTransform(HumanBodyBones.LeftUpperArm);var arm=a.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                var hand=a.GetBoneTransform(HumanBodyBones.LeftHand);var middle=a.GetBoneTransform(HumanBodyBones.LeftMiddleProximal);
                Quaternion lastHand=hand.rotation,lastArm=arm.rotation;Vector3 shellLocal=Vector3.zero;bool haveShell=false;
                float maxBend=0,maxHandRate=0,maxArmRate=0,maxShellSlip=0,elbowFront=0;
                for(int i=0;i<4*fps;i++)
                {
                    lab.Advance(dt,input,movement==2);lab.Evaluate(dt);
                    float angle=Vector3.Angle(hand.position-arm.position,middle.position-hand.position);
                    if(lab.Combat.Reloading)maxBend=Mathf.Max(maxBend,angle);
                    maxHandRate=Mathf.Max(maxHandRate,Quaternion.Angle(lastHand,hand.rotation)/dt);
                    maxArmRate=Mathf.Max(maxArmRate,Quaternion.Angle(lastArm,arm.rotation)/dt);
                    lastHand=hand.rotation;lastArm=arm.rotation;
                    float phase=lab.Combat.ReloadPhase;
                    if(phase>.2f&&phase<.8f)elbowFront=Mathf.Max(elbowFront,Vector3.Dot(arm.position-hand.position,go.transform.forward));
                    if(lab.Combat.Shell.gameObject.activeSelf)
                    {
                        Vector3 local=Quaternion.Inverse(hand.rotation)*(lab.Combat.Shell.position-hand.position);
                        if(haveShell)maxShellSlip=Mathf.Max(maxShellSlip,Vector3.Distance(local,shellLocal));
                        shellLocal=local;haveShell=true;
                    }
                }
                Assert.That(maxBend,Is.LessThan(36),"Palm must not fold back over the forearm");
                Assert.That(maxHandRate,Is.LessThan(600),"Wrist orientation must not snap on release or regrip");
                Assert.That(maxArmRate,Is.LessThan(600),"Elbow pole must not flip");
                Assert.That(elbowFront,Is.LessThan(.04f),"Loading elbow stays behind the working hand");
                Assert.That(maxShellSlip,Is.LessThan(.001f),"Shell must travel in the palm frame");
                Assert.That(haveShell,Is.True);
                Debug.Log($"WRIST_V34 fps={fps} movement={movement} bend={maxBend} handRate={maxHandRate} armRate={maxArmRate} elbowFront={elbowFront} shellSlip={maxShellSlip}");
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(floor);}
        }
#endif
    }
}
