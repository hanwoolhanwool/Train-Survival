using Game.Gameplay.Debugging;
using NUnit.Framework;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using Object=UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public sealed class RaccoonHeadV31Tests
    {
#if UNITY_EDITOR
        [TestCase(30)] [TestCase(60)] [TestCase(120)] [TestCase(240)]
        public void StrafeReversalAndStopKeepHeadContinuousAtHighRefreshRates(int fps)
        {
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(40,1,40);
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Characters/Raccoon/DirectionalCandidate_v29/PC_Raccoon_Directional_v29.prefab"));
            try
            {
                var lab=root.GetComponent<RaccoonDirectionalMotorV29>();lab.enabled=false;lab.ResetAt(new Vector3(1000,.03f,0));Physics.SyncTransforms();
                var head=lab.Rig.Animator.GetBoneTransform(HumanBodyBones.Head);Quaternion previous=head.rotation;
                float maxStep=0,maxRate=0,at=0;
                for(int frame=0;frame<fps*12;frame++)
                {
                    float t=frame/(float)fps;
                    Vector2 input=t<1?Vector2.zero:t<4?Vector2.left:t<7?Vector2.right:t<9?Vector2.zero:t<11?new Vector2(-1,-1).normalized:Vector2.up;
                    lab.Advance(1f/fps,input,false);lab.Evaluate(1f/fps);
                    Quaternion current=Quaternion.Inverse(root.transform.rotation)*head.rotation;
                    float step=Quaternion.Angle(previous,current);
                    if(t>1.4f)
                    {
                        if(step>maxStep){maxStep=step;at=t;}maxRate=Mathf.Max(maxRate,step*fps);
                        Assert.That(step*fps,Is.LessThan(180),$"Head snapped at {t:F4}s; fps={fps}");
                        Assert.That(float.IsFinite(current.x)&&float.IsFinite(current.y)&&float.IsFinite(current.z)&&float.IsFinite(current.w),Is.True);
                    }
                    previous=current;
                }
                // A slow approach must not be classified as idle simply because each
                // horizontal increment is less than the old 1 mm movement threshold.
                lab.ResetAt(new Vector3(1000,.03f,0));float start=root.transform.position.x;
                for(int frame=0;frame<fps*2;frame++)lab.Advance(1f/fps,Vector2.left*.1f,false);
                float distance=start-root.transform.position.x;
                Assert.That(distance,Is.InRange(.075f,.105f),"Small intentional movement must survive high render rates");
                Debug.Log($"HEAD_V31 fps={fps} maxStepDegrees={maxStep} maxDegreesPerSecond={maxRate} at={at} slowTravel={distance}");
            }
            finally{Object.DestroyImmediate(root);Object.DestroyImmediate(floor);}
        }
#endif
    }
}
