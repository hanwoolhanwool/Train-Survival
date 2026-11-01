using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Gameplay.Debugging;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonContinuityV27
    {
        public const string Source="Assets/_Project/Art/Characters/Raccoon/Animations/Raccoon_ShotgunRun_HYBRID_v8.anim";
        public const string ClipPath=RaccoonSandboxSetupV26.Folder+"/Raccoon_ShotgunRun_LOOP_v27.anim";
        public const float Window=.10f;
        public const float WalkOffset=28f/120;

        public static void Build()
        {
            var original=AssetDatabase.LoadAssetAtPath<AnimationClip>(Source);
            var clip=Object.Instantiate(original);clip.name="Raccoon_ShotgunRun_LOOP_v27";
            float length=original.length;
            foreach(var binding in AnimationUtility.GetCurveBindings(original))
            {
                var c=AnimationUtility.GetEditorCurve(original,binding);
                float a=length-Window,b=Window,d=.001f;
                float va=c.Evaluate(a),vb=c.Evaluate(b);
                float ma=(c.Evaluate(a+d)-c.Evaluate(a-d))/(2*d);
                float mb=(c.Evaluate(b+d)-c.Evaluate(b-d))/(2*d);
                float span=2*Window;
                Keyframe Bridge(float time,float u)
                {
                    float u2=u*u,u3=u2*u;
                    float value=(2*u3-3*u2+1)*va+(u3-2*u2+u)*span*ma+(-2*u3+3*u2)*vb+(u3-u2)*span*mb;
                    float tangent=((6*u2-6*u)*va+(3*u2-4*u+1)*span*ma+(-6*u2+6*u)*vb+(3*u2-2*u)*span*mb)/span;
                    return new Keyframe(time,value,tangent,tangent);
                }
                var keys=new List<Keyframe>();
                for(int i=0;i<=12;i++)keys.Add(Bridge(Window*i/12,.5f+.5f*i/12));
                keys.AddRange(c.keys.Where(k=>k.time>Window+.00001f&&k.time<a-.00001f));
                for(int i=0;i<=12;i++)keys.Add(Bridge(a+Window*i/12,.5f*i/12));
                var result=new AnimationCurve(keys.ToArray()){preWrapMode=c.preWrapMode,postWrapMode=c.postWrapMode};
                AnimationUtility.SetEditorCurve(clip,binding,result);
            }
            var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;settings.loopBlend=false;AnimationUtility.SetAnimationClipSettings(clip,settings);
            var asset=AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipPath);
            if(asset==null){AssetDatabase.CreateAsset(clip,ClipPath);asset=clip;}
            else{EditorUtility.CopySerialized(clip,asset);Object.DestroyImmediate(clip);EditorUtility.SetDirty(asset);}
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(RaccoonSandboxSetupV26.Folder+"/AC_Candidate_v26.overrideController");
            controller["Raccoon_ShotgunRun_AUTHORED_v7"]=asset;EditorUtility.SetDirty(controller);
            var tree=AssetDatabase.LoadAllAssetsAtPath(RaccoonSandboxSetupV26.Folder+"/AC_Candidate_Base_v26.controller").OfType<BlendTree>().First(t=>t.name=="Locomotion");
            var children=tree.children;
            for(int i=0;i<children.Length;i++)if(Mathf.Approximately(children[i].threshold,4.5f))children[i].cycleOffset=WalkOffset;
            tree.children=children;EditorUtility.SetDirty(tree);AssetDatabase.SaveAssets();
        }

        [Serializable] public class Report { public string label;public float maxHipAcceleration,maxFootAcceleration,maxFootSpeed;public float[] transitionHipAcceleration,transitionFootAcceleration; }
        public static Report Measure(string label)
        {
            var report=new Report{label=label,transitionHipAcceleration=new float[8],transitionFootAcceleration=new float[8]};
            var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(1000,-.5f,20);floor.transform.localScale=new Vector3(20,1,100);
            try
            {
                for(int trial=0;trial<8;trial++)
                {
                    var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RaccoonSandboxSetupV26.PrefabPath));root.transform.position=new Vector3(1000,.03f,0);
                    try
                    {
                        var lab=root.GetComponent<RaccoonSandboxV26>();lab.enabled=false;lab.Initialize();Physics.SyncTransforms();
                        const float dt=1f/120;var a=lab.Rig.Animator;
                        for(int i=0;i<240;i++){lab.Advance(dt,Vector2.up,true);lab.Evaluate(dt);}
                        a.Play("Locomotion",0,trial/8f);a.speed=1;a.Update(0);a.speed=0;
                        var joints=new[]{a.GetBoneTransform(HumanBodyBones.Hips),a.GetBoneTransform(HumanBodyBones.LeftFoot),a.GetBoneTransform(HumanBodyBones.RightFoot)};
                        var previous=joints.Select(j=>j.position).ToArray();var velocity=new Vector3[3];
                        for(int i=0;i<240;i++)
                        {
                            lab.Advance(dt,Vector2.up,i<30);lab.Evaluate(dt);
                            for(int j=0;j<3;j++)
                            {
                                var v=(joints[j].position-previous[j])/dt;float acceleration=(v-velocity[j]).magnitude/dt;
                                if(i>=30&&i<210)
                                {
                                    if(j==0)report.transitionHipAcceleration[trial]=Mathf.Max(report.transitionHipAcceleration[trial],acceleration);
                                    else{report.transitionFootAcceleration[trial]=Mathf.Max(report.transitionFootAcceleration[trial],acceleration);report.maxFootSpeed=Mathf.Max(report.maxFootSpeed,v.magnitude);}
                                }
                                velocity[j]=v;previous[j]=joints[j].position;
                            }
                        }
                    }
                    finally{Object.DestroyImmediate(root);}
                }
            }
            finally{Object.DestroyImmediate(floor);}
            report.maxHipAcceleration=report.transitionHipAcceleration.Max();report.maxFootAcceleration=report.transitionFootAcceleration.Max();
            Directory.CreateDirectory("output/raccoon-loop-transition-v27");File.WriteAllText("output/raccoon-loop-transition-v27/"+label+".json",JsonUtility.ToJson(report,true));return report;
        }
    }
}
