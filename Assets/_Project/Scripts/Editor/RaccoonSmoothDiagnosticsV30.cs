using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Game.Gameplay.Debugging;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonSmoothDiagnosticsV30
    {
        [Serializable] public sealed class Result
        {
            public string direction;public bool grounded;public float meanPoseMs,maxFootSpeed,maxVelocityJump,peakPhase;
        }
        [Serializable] public sealed class Report { public List<Result> results=new List<Result>(); }
        public static string Measure(string label,bool grounded)
        {
            var report=new Report();var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(60,1,60);
            try
            {
                foreach(var direction in new[]{Vector2.left,Vector2.right,Vector2.down,Vector2.up})
                {
                    var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonDirectionalSetupV29.PrefabPath));root.transform.position=new Vector3(1000,.03f,0);
                    try
                    {
                        var lab=root.GetComponent<RaccoonDirectionalMotorV29>();lab.enabled=false;lab.Initialize();Physics.SyncTransforms();
                        var rig=lab.Rig;var feet=new[]{rig.Animator.GetBoneTransform(HumanBodyBones.LeftFoot),rig.Animator.GetBoneTransform(HumanBodyBones.RightFoot)};
                        var last=new Vector3[2];var velocity=new Vector3[2];var timer=new Stopwatch();
                        var result=new Result{direction=direction.ToString(),grounded=grounded};
                        for(int f=0;f<300;f++)
                        {
                            lab.Advance(1f/60,direction,false);timer.Start();rig.Tick(1f/60,lab.ActualVelocity,false,grounded);timer.Stop();
                            for(int side=0;side<2;side++)
                            {
                                Vector3 v=(feet[side].position-last[side])*60;
                                if(f>60)
                                {
                                    result.maxFootSpeed=Mathf.Max(result.maxFootSpeed,v.magnitude);
                                    float jump=(v-velocity[side]).magnitude;
                                    if(jump>result.maxVelocityJump){result.maxVelocityJump=jump;result.peakPhase=Mathf.Repeat(rig.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1);}
                                }
                                last[side]=feet[side].position;velocity[side]=v;
                            }
                        }
                        result.meanPoseMs=(float)timer.Elapsed.TotalMilliseconds/300;report.results.Add(result);
                    }
                    finally{Object.DestroyImmediate(root);}
                }
            }
            finally{Object.DestroyImmediate(floor);}
            string json=JsonUtility.ToJson(report,true);File.WriteAllText("output/raccoon-directional-smooth-v30/"+label+".json",json);return json;
        }
    }
}
