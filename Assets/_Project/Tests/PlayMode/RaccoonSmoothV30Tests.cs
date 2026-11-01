using System.Diagnostics;
using System.Linq;
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
    public sealed class RaccoonSmoothV30Tests
    {
#if UNITY_EDITOR
        [Test]
        public void CachedContactEvaluationKeepsFeetAndTailAndReducesCpuCost()
        {
            const string prefab="Assets/_Project/Art/Characters/Raccoon/DirectionalCandidate_v29/PC_Raccoon_Directional_v29.prefab";
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,0);floor.transform.localScale=new Vector3(20,1,20);
            var roots=new[]{Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab)),Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(prefab))};
            try
            {
                var animators=roots.Select(r=>r.GetComponentInChildren<Animator>()).ToArray();
                for(int side=0;side<2;side++)
                {
                    roots[side].transform.position=new Vector3(1000,.03f,0);
                    foreach(var b in roots[side].GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                    var a=animators[side];a.Rebind();a.Update(0);a.SetFloat("Speed",4.5f);a.SetFloat("MoveX",-1);a.SetFloat("MoveZ",0);a.SetFloat("RaccoonShotgunRun",1);a.SetLayerWeight(a.GetLayerIndex("Hold"),1);
                }
                Physics.SyncTransforms();
                var before=new RaccoonCandidateContactsV26(animators[0],roots[0].transform);
                var after=new RaccoonDirectionalContactsV30(animators[1],roots[1].transform);
                var bones=animators.Select(a=>a.GetComponentInChildren<SkinnedMeshRenderer>().bones).ToArray();
                var clocks=new[]{new Stopwatch(),new Stopwatch()};float maxPosition=0,maxAngle=0;
                for(int frame=0;frame<160;frame++)
                {
                    for(int side=0;side<2;side++)
                    {
                        roots[side].transform.position=new Vector3(1000-.45f*frame/60,.03f,0);
                        var a=animators[side];a.Update(1f/60);
                        float phase=Mathf.Repeat(a.GetCurrentAnimatorStateInfo(0).normalizedTime+RaccoonDirectionalRigV29.WalkCycleOffset,1);
                        var tail=a.GetComponent<RaccoonTailMotion>();tail.Step(.45f,0,1f/60);
                        if(frame>20)clocks[side].Start();
                        if(side==0){before.Step(1f/60,.45f,phase);before.ClearSoles();before.FitTail(1f/60);}
                        else{after.Step(1f/60,.45f,phase);after.ClearSoles();after.FitTail(1f/60);}
                        clocks[side].Stop();
                    }
                    for(int b=0;b<bones[0].Length;b++)
                    {
                        maxPosition=Mathf.Max(maxPosition,Vector3.Distance(bones[0][b].position,bones[1][b].position));
                        maxAngle=Mathf.Max(maxAngle,Quaternion.Angle(bones[0][b].rotation,bones[1][b].rotation));
                    }
                }
                Assert.That(maxPosition,Is.LessThan(.0005f),"Optimizing must not simplify or move the solved mesh");
                Assert.That(maxAngle,Is.LessThan(.1f));
                Assert.That(clocks[1].Elapsed.TotalMilliseconds,Is.LessThan(clocks[0].Elapsed.TotalMilliseconds*.65),"Contact processing must be substantially faster, not just produce fewer visible frames");
                UnityEngine.Debug.Log($"SMOOTH_V30 beforeMs={clocks[0].Elapsed.TotalMilliseconds/139:F3} afterMs={clocks[1].Elapsed.TotalMilliseconds/139:F3} maxPosition={maxPosition} maxAngle={maxAngle}");
            }
            finally{foreach(var r in roots)Object.DestroyImmediate(r);Object.DestroyImmediate(floor);}
        }
#endif
    }
}
