using Game.Gameplay.Debugging;
using Game.Gameplay.Player;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonReloadContactV35Tests
    {
#if UNITY_EDITOR
        [TestCase(30,0)] [TestCase(60,1)] [TestCase(120,2)]
        public void ActualFingertipsCarryShellAndReturnWithoutSnapping(int fps,int movement)
        {
            var go=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/CombatCandidate_v33/PC_Raccoon_Combat_v33.prefab"));
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,15);floor.transform.localScale=new Vector3(40,1,60);
            try
            {
                var lab=go.GetComponent<RaccoonCombatMotorV33>();lab.enabled=false;lab.ResetAt(new Vector3(1000,.03f,0));
                float dt=1f/fps;Vector2 input=movement==0?Vector2.zero:movement==1?Vector2.left:Vector2.up;
                for(int i=0;i<3*fps;i++){if(i==fps||i==2*fps)lab.Combat.Fire();lab.Advance(dt,input,movement==2);lab.Evaluate(dt);}
                Assert.That(lab.Combat.Reload(),Is.True);
                var a=lab.Rig.Animator;var fingers=new Transform[6];var previous=new Quaternion[6];
                for(int i=0;i<6;i++){fingers[i]=a.GetBoneTransform(RaccoonEquipmentPose.LeftFingers[i]);previous[i]=fingers[i].localRotation;}
                float maxError=0,maxRate=0,maxSurfaceGap=0;int contactFrames=0;
                for(int i=0;i<4*fps;i++)
                {
                    lab.Advance(dt,input,movement==2);lab.Evaluate(dt);
                    for(int j=0;j<6;j++){maxRate=Mathf.Max(maxRate,Quaternion.Angle(previous[j],fingers[j].localRotation)/dt);previous[j]=fingers[j].localRotation;}
                    var combat=lab.Combat;var shell=combat.Shell;
                    if(!shell.gameObject.activeSelf)continue;
                    contactFrames++;maxError=Mathf.Max(maxError,combat.PinchError);
                    foreach(var tip in new[]{combat.ThumbContact,combat.IndexContact})
                    {
                        Vector3 delta=tip-shell.position;float axial=Vector3.Dot(delta,shell.up);
                        float radial=Vector3.ProjectOnPlane(delta,shell.up).magnitude;
                        maxSurfaceGap=Mathf.Max(maxSurfaceGap,Mathf.Abs(radial-.0125f));
                        Assert.That(Mathf.Abs(axial),Is.LessThan(.028f),"Contact stays on the shell body");
                    }
                }
                Assert.That(contactFrames,Is.GreaterThan(fps/2));Assert.That(maxError,Is.LessThan(.002f));
                Assert.That(maxSurfaceGap,Is.LessThan(.003f),"Both fingertip surfaces must reach the shell");
                Assert.That(maxRate,Is.LessThan(500),"Finger joints must blend in and out of the pinch");
                var equipment=a.GetComponent<RaccoonEquipmentPose>();equipment.Profile.TryGetEntry(Game.Gameplay.Inventory.HotbarItemType.Shotgun,out var entry);
                for(int i=0;i<6;i++)Assert.That(Quaternion.Angle(fingers[i].localRotation,entry.Left.Fingers[i]),Is.LessThan(.15f),"Return to approved grip");
                Debug.Log($"CONTACT_V35 fps={fps} movement={movement} tipError={maxError} surfaceGap={maxSurfaceGap} fingerRate={maxRate} samples={contactFrames}");
            }
            finally{Object.DestroyImmediate(go);Object.DestroyImmediate(floor);}
        }
#endif
    }
}
