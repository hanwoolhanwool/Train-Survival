using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.Gameplay.Player;
using Game.Gameplay.Inventory;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace Game.Editor
{
    public static class RaccoonWalkTerrainV25
    {
        public static string Output="output/raccoon-locomotion-v14";
        private const string Art=RaccoonEquipmentSetup.Art;
        [Serializable] public class Bone { public string name;public Vector3 position;public Quaternion rotation,restRotation; }
        [Serializable] public class Placement { public string name;public Vector3 position,scale;public Quaternion rotation;public bool visible=true; }
        [Serializable] public class Geometry { public string name,source;public Vector3[] vertices;public Vector2[] uv;public int[] triangles; }
        [Serializable] public class Frame { public int frame;public string label;public float speed,phase;public Vector3 root;public Bone[] bones;public Placement[] weapons;public float[] shapes;public bool[] planted;public float[] footError,soleClearance,soleSpeed; }
        [Serializable] public class Surface { public string name;public Vector3 position,scale;public Quaternion rotation; }
        [Serializable] public class Exchange { public int fps=60;public Geometry[] geometry;public Frame[] frames;public Surface[] surfaces; }
        [Serializable] public class Metric
        {
            public string phase;public int frames,contactSamples;public float maxGripError,maxHandStep,maxTailStep,minFootClearance=999,minTailClearance=999,meanContactSlide,maxContactSlide;
        }
        [Serializable] private class Report { public Metric[] phases;public string note="Physical CharacterController movement over box/ramp/step colliders; 60 Hz. Sole geometry is sampled after Animator and equipment/tail corrections. Contact slide uses sole-centroid world velocity near the surface; this is not network or moving-train QA."; }

        private const string Candidates=Art+"/WalkCandidates_v23";
        private static RuntimeAnimatorController previewController;
        public static void Build()
        {
            Output="output/raccoon-walk-terrain-v25";Directory.CreateDirectory(Output);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var original=AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>("Assets/_Project/Art/Animations/AC_Player.controller");
            var controller=new AnimatorOverrideController(original);
            var walk=original.animationClips.First(c=>c.name.EndsWith(":walk"));
            var idle=original.animationClips.First(c=>c.name.EndsWith(":idle"));
            controller[walk.name]=AssetDatabase.LoadAssetAtPath<AnimationClip>(Candidates+"/Raccoon_Walk_armed_calm.anim");
            controller[idle.name]=AssetDatabase.LoadAssetAtPath<AnimationClip>(RaccoonIdleSetup.ClipPath);
            previewController=controller;
            foreach(string test in new[]{"turns","slopes"}){Capture(test,"before",false);Capture(test,"after",false);}Capture("cross_slope","after",false);Capture("slope_turn","after",false);
            Object.DestroyImmediate(controller);
            Debug.Log("RACCOON_WALK_TERRAIN_V25_COMPLETE");
        }
        private static void Capture(string test,string revision,bool render)
        {
            bool walkComparison=false;
            bool originalWalk=true;
            string folder=Output+"/"+revision+"/"+test;Directory.CreateDirectory(folder);
            var environment=new GameObject("Review course");var surfaces=new List<Surface>();
            void Box(string name,Vector3 position,Vector3 scale,Quaternion rotation)
            {
                var o=GameObject.CreatePrimitive(PrimitiveType.Cube);o.name=name;o.transform.SetParent(environment.transform);o.transform.SetPositionAndRotation(position,rotation);o.transform.localScale=scale;
                surfaces.Add(new Surface{name=name,position=position,scale=scale,rotation=rotation});
            }
            Box("Ground",new Vector3(0,-.5f,40),new Vector3(200,1,160),Quaternion.identity);
            if(test=="slopes")
            {
                const float angle=12f,length=4f,thickness=.3f;
                float rise=Mathf.Tan(angle*Mathf.Deg2Rad)*length;
                foreach(bool ascending in new[]{true,false})
                {
                    var rotation=Quaternion.Euler(ascending?-angle:angle,0,0);
                    var top=new Vector3(0,rise*.5f,ascending?4:9.5f);
                    Box(ascending?"Uphill 12 degrees":"Downhill 12 degrees",top-rotation*Vector3.up*(thickness*.5f),new Vector3(6,thickness,length/Mathf.Cos(angle*Mathf.Deg2Rad)),rotation);
                }
                Box("Upper landing",new Vector3(0,rise*.5f,6.75f),new Vector3(6,rise,1.5f),Quaternion.identity);
            }
            if(test=="cross_slope"||test=="slope_turn")
            {
                var rotation=Quaternion.Euler(0,0,10);
                Box("Cross slope 10 degrees",new Vector3(0,1,8)-rotation*Vector3.up*.15f,new Vector3(6,.3f,30),rotation);
            }
            var mover=new GameObject("Review mover");mover.layer=2;var cc=mover.AddComponent<CharacterController>();cc.height=1.8f;cc.center=Vector3.up*.9f;cc.radius=.23f;cc.skinWidth=.025f;cc.stepOffset=.22f;cc.slopeLimit=45;
            if(test=="cross_slope"||test=="slope_turn")mover.transform.position=new Vector3(0,1.1f,0);
            var character=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Art+"/CH_Raccoon_Player.prefab"),mover.transform);character.transform.localPosition=Vector3.zero;character.transform.localRotation=Quaternion.identity;
            try
            {
                var animator=character.GetComponent<Animator>();
                if(previewController==null)throw new InvalidOperationException("Missing preview controller");animator.runtimeAnimatorController=previewController;
                var skin=character.GetComponentInChildren<SkinnedMeshRenderer>();var bones=skin.bones;var rest=bones.Select(b=>b.rotation).ToArray();
                var pose=character.GetComponent<RaccoonEquipmentPose>();var previewProfile=Object.Instantiate(pose.Profile);previewProfile.HasAuthoredShotgunRun=false;pose.Bind(previewProfile);var tail=character.GetComponent<RaccoonTailMotion>();tail.ResetMotion();pose.Profile.TryGetEntry(HotbarItemType.Shotgun,out var entry);
                var baseline=revision=="before"?new RaccoonWalkContactPreviewV24(animator,mover.transform):null;var grounding=revision=="after"?new RaccoonWalkContactPreviewV25(animator,mover.transform):null;
                var weapon=RaccoonEquipmentSetup.Attach(animator.GetBoneTransform(HumanBodyBones.RightHand),HotbarItemType.Shotgun,entry.Socket);var filters=weapon.GetComponentsInChildren<MeshFilter>();
                var geometry=filters.Select(m=>new Geometry{name=m.name,source=m.name.Replace("(Clone)",""),vertices=m.sharedMesh.vertices,uv=m.sharedMesh.uv,triangles=m.sharedMesh.triangles}).ToArray();
                var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;
                int[][] feet=new[]{"Left","Right"}.Select(side=>Enumerable.Range(0,vertices.Length).Where(i=>{
                    var w=weights[i];float Sum(int index,float weight)=>bones[index].name==side+"Foot"||bones[index].name==side+"Toes"?weight:0;
                    return Sum(w.boneIndex0,w.weight0)+Sum(w.boneIndex1,w.weight1)+Sum(w.boneIndex2,w.weight2)+Sum(w.boneIndex3,w.weight3)>.8f;
                }).ToArray()).ToArray();
                var soles=feet.Select(ids=>{float min=ids.Min(i=>skin.transform.TransformPoint(vertices[i]).y);return ids.Where(i=>skin.transform.TransformPoint(vertices[i]).y<min+.025f).ToArray();}).ToArray();
                var tailIndices=mesh.GetTriangles(mesh.subMeshCount-2).Distinct().Where((v,i)=>i%48==0).ToArray();
                var tailBones=bones.Where(b=>b.name.StartsWith("Tail")).ToArray();var lastTail=tailBones.Select(b=>b.localRotation).ToArray();
                var lastFeet=new Vector3[2];var lastClear=new float[]{999,999};Vector3 previousHand=Vector3.zero;
                var metrics=new Dictionary<string,Metric>();var frames=new List<Frame>();float speed=0,lastHeading=0;var tier=LocomotionTier.Idle;
                animator.Rebind();animator.Update(0);if(!originalWalk)animator.SetFloat("RaccoonShotgunRun",1);animator.SetInteger("HoldPose",2);
                Physics.SyncTransforms();
                for(int i=-120;i<1200;i++)
                {
                    const float dt=1f/60;float time=Mathf.Max(0,i)*dt;float desired=0,heading=0;string label;
                    desired=time<1?0:time<1.6f?.8f*Mathf.SmoothStep(0,1,(time-1)/.6f):time<17?.8f:time<18?.8f*(1-Mathf.SmoothStep(0,1,time-17)):0;
                    if(test=="turns")
                    {
                        heading=time<3?0:time<5.5f?90*Mathf.SmoothStep(0,1,(time-3)/2.5f):time<7?90:time<10?90-180*Mathf.SmoothStep(0,1,(time-7)/3):time<12?-90:time<14?-90+90*Mathf.SmoothStep(0,1,(time-12)/2):0;
                        label=time<1?"Idle":time<1.6f?"Start":time<3?"Straight":time<5.5f?"Turn right 90":time<7?"Straight":time<10?"Turn left 180":time<12?"Straight":time<14?"Turn right 90":time<17?"Straight":time<18?"Decelerate":"Stop";
                    }
                    else if(test=="slopes")label=mover.transform.position.z<2?"Flat":mover.transform.position.z<6?"Uphill 12":mover.transform.position.z<7.5f?"Landing":mover.transform.position.z<11.5f?"Downhill 12":"Flat after slope";
                                        else
                    {
                        label=time<1?"Cross slope idle":time<17?"Cross slope walk 10":time<18?"Cross slope decelerate":"Cross slope stop";
                        if(test=="slope_turn")
                        {
                            heading=time<3?0:time<6?30*Mathf.SmoothStep(0,1,(time-3)/3):time<10?30-60*Mathf.SmoothStep(0,1,(time-6)/4):time<14?-30+30*Mathf.SmoothStep(0,1,(time-10)/4):0;
                            if(time>=3 && time<14)label=time<6?"Slope turn right":time<10?"Slope turn left":"Slope return forward";
                        }
                    }
                    if(i<0 && !walkComparison){desired=0;heading=0;}
                    mover.transform.rotation=Quaternion.Euler(0,heading,0);var oldPosition=mover.transform.position;
                    cc.Move((mover.transform.forward*desired+Vector3.down*2)*dt);var delta=mover.transform.position-oldPosition;float actual=new Vector2(delta.x,delta.z).magnitude/dt;
                    speed=PlayerAnimationMath.SmoothTowards(speed,actual,.15f,dt);tier=PlayerAnimationMath.StepTier(tier,speed,.5f,.2f,5.75f,.3f);float display=PlayerAnimationMath.TierTargetSpeed(tier,speed,5.75f);
                    animator.SetFloat("Speed",Mathf.Clamp01(actual/.8f)*4.5f);animator.SetLayerWeight(animator.GetLayerIndex("Hold"),originalWalk?1:1-pose.Profile.AuthoredRunWeight(animator));animator.Update(dt);
                    if(grounding!=null)grounding.Step(dt,actual,Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1));else baseline.Step(dt,actual,Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1));
                    var head=animator.GetBoneTransform(HumanBodyBones.Head);head.rotation=Quaternion.AngleAxis(originalWalk?pose.Profile.HeadNeutralPitch:pose.Profile.HeadPitchFor(animator),mover.transform.right)*head.rotation;
                    pose.Apply(HotbarItemType.Shotgun,false,0,dt,mover.transform);tail.Step(actual,Mathf.DeltaAngle(lastHeading,heading)/dt,dt);lastHeading=heading;if(grounding!=null)grounding.FitTail(dt);character.GetComponent<RaccoonElbowCorrectives>().Apply();
                    if(!metrics.TryGetValue(label,out var m)){m=new Metric{phase=label};metrics[label]=m;}
                    var matrices=bones.Select((b,j)=>b.localToWorldMatrix*bind[j]).ToArray();
                    Vector3 Point(int index){var w=weights[index];var p=vertices[index];return matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;}
                    float Clearance(Vector3 p){return Physics.Raycast(p+Vector3.up*2,Vector3.down,out var hit,5,~(1<<2),QueryTriggerInteraction.Ignore)?p.y-hit.point.y:999;}
                    Vector3 hand=mover.transform.InverseTransformPoint(animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                    if(i>=0)
                    {
                        m.frames++;m.maxHandStep=Mathf.Max(m.maxHandStep,Vector3.Distance(hand,previousHand));
                        var separation=animator.GetBoneTransform(HumanBodyBones.LeftHand).position-animator.GetBoneTransform(HumanBodyBones.RightHand).position;
                        m.maxGripError=Mathf.Max(m.maxGripError,(separation-mover.transform.rotation*(entry.Left.Position-entry.Right.Position)).magnitude);
                        for(int j=0;j<tailBones.Length;j++)m.maxTailStep=Mathf.Max(m.maxTailStep,Quaternion.Angle(lastTail[j],tailBones[j].localRotation));
                    }
                    var soleClearance=new float[2];var soleSpeed=new float[2];
                    for(int side=0;side<2;side++)
                    {
                        var points=soles[side].Select(Point).ToArray();Vector3 centre=points.Aggregate(Vector3.zero,(a,b)=>a+b)/points.Length;
                        float clear=points.Min(Clearance);if(i>=0){m.minFootClearance=Mathf.Min(m.minFootClearance,clear);if(clear<.035f&&clear>-.1f&&lastClear[side]<.035f){float slip=new Vector2(centre.x-lastFeet[side].x,centre.z-lastFeet[side].z).magnitude/dt;m.meanContactSlide+=slip;m.contactSamples++;m.maxContactSlide=Mathf.Max(m.maxContactSlide,slip);}}
                        soleClearance[side]=clear;soleSpeed[side]=new Vector2(centre.x-lastFeet[side].x,centre.z-lastFeet[side].z).magnitude/dt;
                        lastFeet[side]=centre;lastClear[side]=clear;
                    }
                    if(i>=0&&i%6==0)foreach(int index in tailIndices)m.minTailClearance=Mathf.Min(m.minTailClearance,Clearance(Point(index)));
                    previousHand=hand;for(int j=0;j<tailBones.Length;j++)lastTail[j]=tailBones[j].localRotation;
                    if(i<0)continue; if(i==30 && animator.GetCurrentAnimatorStateInfo(0).normalizedTime<=0)throw new InvalidOperationException("Animation did not advance");
                    frames.Add(new Frame{frame=i+1,label=label,speed=actual,phase=Mathf.Repeat(animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1),root=mover.transform.position,bones=bones.Select((b,j)=>new Bone{name=b.name,position=b.position,rotation=b.rotation,restRotation=rest[j]}).ToArray(),shapes=Enumerable.Range(0,mesh.blendShapeCount).Select(skin.GetBlendShapeWeight).ToArray(),weapons=filters.Select(mf=>new Placement{name=mf.name,position=mf.transform.position,rotation=mf.transform.rotation,scale=mf.transform.lossyScale}).ToArray(),soleClearance=soleClearance,soleSpeed=soleSpeed,planted=grounding!=null?new[]{grounding.IsFootPlanted(0),grounding.IsFootPlanted(1)}:new[]{baseline.IsFootPlanted(0),baseline.IsFootPlanted(1)},footError=grounding!=null?new[]{grounding.FootTargetError(0),grounding.FootTargetError(1)}:new[]{baseline.FootTargetError(0),baseline.FootTargetError(1)}});
                    if(render&&i%6==0){Directory.CreateDirectory(folder+"/frames");RenderCourse(character,environment,mover.transform.position+new Vector3(0,.95f,0),folder+"/frames/"+(i/6).ToString("D4")+".png");}
                }
                foreach(var metric in metrics.Values)if(metric.contactSamples>0)metric.meanContactSlide/=metric.contactSamples;
                File.WriteAllText(folder+"/report.json",JsonUtility.ToJson(new Report{phases=metrics.Values.ToArray()},true));
                File.WriteAllText(folder+"/motion_exchange.json",JsonUtility.ToJson(new Exchange{geometry=geometry,frames=frames.ToArray(),surfaces=surfaces.ToArray()}));
                Debug.Log("LOCOMOTION_REVIEW "+test+" "+JsonUtility.ToJson(new Report{phases=metrics.Values.ToArray()}));
            }
            finally{Object.DestroyImmediate(mover);Object.DestroyImmediate(environment);}
        }
        private static void RenderCourse(GameObject character,GameObject environment,Vector3 aim,string path)
        {
            var preview=new PreviewRenderUtility();
            try
            {
                var copy=Object.Instantiate(character);preview.AddSingleGO(copy);copy.transform.SetPositionAndRotation(character.transform.position,character.transform.rotation);copy.transform.localScale=character.transform.lossyScale;var transforms=copy.GetComponentsInChildren<Transform>();var rotations=transforms.Select(t=>t.localRotation).ToArray();
                foreach(var behaviour in copy.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
                foreach(var animator in copy.GetComponentsInChildren<Animator>())animator.enabled=false;
                for(int i=0;i<transforms.Length;i++)transforms[i].localRotation=rotations[i];
                var course=Object.Instantiate(environment);preview.AddSingleGO(course);
                foreach(var renderer in course.GetComponentsInChildren<Renderer>())renderer.sharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Art+"/M_ReviewGround.mat");
                Vector3 direction=new Vector3(-3,.6f,-4);preview.camera.orthographic=true;preview.camera.orthographicSize=1.4f;preview.camera.transform.SetPositionAndRotation(aim+direction,Quaternion.LookRotation(-direction));preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=30;
                preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.18f,.20f,.22f);preview.ambientColor=new Color(.5f,.5f,.5f);
                preview.lights[0].intensity=1.2f;preview.lights[0].transform.rotation=Quaternion.Euler(35,210,0);preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(25,30,0);
                for(int pass=0;pass<2;pass++){preview.BeginStaticPreview(new Rect(0,0,600,600));preview.Render(true);var image=preview.EndStaticPreview();if(pass==1)File.WriteAllBytes(path,image.EncodeToPNG());Object.DestroyImmediate(image);}
            }
            finally{preview.Cleanup();}
        }
    }
}






