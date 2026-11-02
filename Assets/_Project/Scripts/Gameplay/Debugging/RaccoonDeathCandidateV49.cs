using System.Collections.Generic;
using UnityEngine;
using Game.Gameplay.Player;

namespace Game.Gameplay.Debugging
{
    // Review candidate 1: an unpowered articulated body, shared by every equipment state.
    // The proxies own simulation; the character's original bones, mesh and clips are retained.
    public sealed class RaccoonDeathCandidateV49
    {
        public const string Id="DeathSoftV49";
        public const float ReleaseTime=.16f;
        readonly Animator animator;readonly Transform root;
        readonly List<Part> parts=new List<Part>();
        readonly List<Collider> colliders=new List<Collider>();
        readonly List<Mesh> contactMeshes=new List<Mesh>();
        readonly List<Transform> fingers=new List<Transform>();
        readonly List<Quaternion> grip=new List<Quaternion>(),loose=new List<Quaternion>();
        GameObject container,dropped,held;Vector3 releasePosition;Quaternion releaseRotation;
        Quaternion floorRotation;
        readonly List<DropSurface> dropSurfaces=new List<DropSurface>();
        class DropSurface{public Transform transform;public Vector3[] vertices;}
        bool released,settled;float floor;
        Transform[] restoreBones;Vector3[] restorePositions;Quaternion[] restoreRotations;
        class Part{public Transform bone;public Rigidbody body;}
        public bool Released=>released;
        public bool Settled=>settled;
        public GameObject Dropped=>dropped;
        public RaccoonDeathCandidateV49(Animator a,Transform r){animator=a;root=r;}
        Transform Bone(HumanBodyBones b)=>animator.GetBoneTransform(b);
        public void Begin(GameObject item)
        {
            Dispose();held=item;floor=root.position.y;
            RaycastHit hit;
            if(Physics.Raycast(root.position+Vector3.up*.12f,Vector3.down,out hit,.4f,~0,QueryTriggerInteraction.Ignore))floor=hit.point.y;
            container=new GameObject("Death candidate 1 · physics");
            // Extract relaxed finger rotations without changing the captured starting pose.
            var transforms=animator.GetComponentsInChildren<Transform>();
            var positions=new Vector3[transforms.Length];var rotations=new Quaternion[transforms.Length];
            for(int i=0;i<transforms.Length;i++){positions[i]=transforms[i].localPosition;rotations[i]=transforms[i].localRotation;}
            restoreBones=transforms;restorePositions=positions;restoreRotations=rotations;
            foreach(var id in RaccoonEquipmentPose.LeftFingers)fingers.Add(Bone(id));
            foreach(var id in RaccoonEquipmentPose.RightFingers)fingers.Add(Bone(id));
            foreach(var f in fingers)grip.Add(f.localRotation);
            using(var handler=new HumanPoseHandler(animator.avatar,animator.transform))
            {var pose=new HumanPose();handler.GetHumanPose(ref pose);for(int i=55;i<HumanTrait.MuscleCount;i++)pose.muscles[i]=RaccoonNeutralPoseV39.Finger(HumanTrait.MuscleName[i]);handler.SetHumanPose(ref pose);foreach(var f in fingers)loose.Add(f.localRotation);}
            for(int i=0;i<transforms.Length;i++){transforms[i].localPosition=positions[i];transforms[i].localRotation=rotations[i];}
            var hips=Add(Bone(HumanBodyBones.Hips),3.5f);
            Box(hips,hips.position+root.up*.025f,new Vector3(.34f,.24f,.28f));
            var chest=Add(Bone(HumanBodyBones.Chest),3f);
            Capsule(chest,Bone(HumanBodyBones.Spine).position+root.up*.03f,Bone(HumanBodyBones.Neck).position-root.up*.05f,.17f);
            Joint(chest,hips,Bone(HumanBodyBones.Spine).position,root.right,-12,40,25);
            var head=Add(Bone(HumanBodyBones.Head),2.5f);
            Sphere(head,head.position+root.up*.045f+root.forward*.025f,.255f);
            Joint(head,chest,Bone(HumanBodyBones.Neck).position,root.right,-35,40,35);
            for(int side=0;side<2;side++)
            {
                bool left=side==0;
                var thigh=Add(Bone(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg),1.2f);
                var shin=Add(Bone(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg),.75f);
                var foot=Add(Bone(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot),.35f);
                Capsule(thigh,thigh.position,shin.position,.080f);Capsule(shin,shin.position,foot.position,.060f);
                Box(foot,Vector3.Lerp(foot.position,Bone(left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes).position,.55f),new Vector3(.14f,.075f,.23f));
                Joint(thigh,hips,thigh.position,root.right,-95,45,35);
                Joint(shin,thigh,shin.position,root.right,-5,135,7);
                Joint(foot,shin,foot.position,root.right,-25,30,15);
                var upper=Add(Bone(left?HumanBodyBones.LeftUpperArm:HumanBodyBones.RightUpperArm),.65f);
                var lower=Add(Bone(left?HumanBodyBones.LeftLowerArm:HumanBodyBones.RightLowerArm),.4f);
                var hand=Add(Bone(left?HumanBodyBones.LeftHand:HumanBodyBones.RightHand),.2f);
                Capsule(upper,upper.position,lower.position,.068f);Capsule(lower,lower.position,hand.position,.052f);
                Sphere(hand,Vector3.Lerp(hand.position,Bone(left?HumanBodyBones.LeftMiddleProximal:HumanBodyBones.RightMiddleProximal).position,.6f),.065f);
                Joint(upper,chest,upper.position,root.right,-100,80,75);
                Joint(lower,upper,lower.position,root.right,-130,10,20);
                Joint(hand,lower,hand.position,root.right,-30,30,25);
            }
            var skin=animator.GetComponentInChildren<SkinnedMeshRenderer>();var parent=hips;
            float[] tailRadii={.06f,.12f,.16f,.14f,.07f};
            for(int i=0;i<5;i++)
            {
                var bone=System.Array.Find(skin.bones,b=>b.name=="Tail0"+(i+1));if(bone==null)break;
                var next=System.Array.Find(skin.bones,b=>b.name=="Tail0"+(i+2));
                var body=Add(bone,.13f);
                Vector3 end=next!=null?next.position:bone.position+(bone.position-parent.position).normalized*.1f;
                Capsule(body,bone.position,end,tailRadii[i]);Joint(body,parent,bone.position,root.right,-65,65,i==0?75:40);parent=body;
            }
            FitContactSurfaces(skin);
            // Keep connected/initially overlapping surfaces from exploding at release,
            // while distant body parts collide instead of folding through each other.
            for(int i=0;i<colliders.Count;i++)
            {
                for(int j=0;j<i;j++)
                {
                    var x=colliders[i];var y=colliders[j];var xj=x.attachedRigidbody.GetComponent<ConfigurableJoint>();var yj=y.attachedRigidbody.GetComponent<ConfigurableJoint>();Vector3 direction;float distance;
                    bool adjacent=xj!=null&&xj.connectedBody==y.attachedRigidbody||yj!=null&&yj.connectedBody==x.attachedRigidbody;
                    bool overlap=Physics.ComputePenetration(x,x.transform.position,x.transform.rotation,y,y.transform.position,y.transform.rotation,out direction,out distance);
                    if(adjacent||overlap)Physics.IgnoreCollision(x,y);
                }
                foreach(var c in root.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(colliders[i],c);
            }
            foreach(var p in parts)
            {p.body.linearVelocity=root.right*.12f+root.forward*.25f-Vector3.up*.22f;p.body.angularVelocity=root.right*.8f-root.forward*.18f;}
            Physics.SyncTransforms();
        }
        Rigidbody Add(Transform bone,float mass)
        {
            var go=new GameObject(bone.name+" · limp");go.transform.SetParent(container.transform);go.transform.SetPositionAndRotation(bone.position,bone.rotation);
            var b=go.AddComponent<Rigidbody>();b.mass=mass;b.linearDamping=.45f;b.angularDamping=1.2f;b.maxAngularVelocity=6;b.solverIterations=18;b.solverVelocityIterations=8;b.interpolation=RigidbodyInterpolation.Interpolate;b.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
            parts.Add(new Part{bone=bone,body=b});return b;
        }
        void FitContactSurfaces(SkinnedMeshRenderer skin)
        {
            if(skin.sharedMesh==null||!skin.sharedMesh.isReadable)return;
            // Use the visible silhouette, including the oversized head and overalls,
            // rather than human-sized primitive bounds that let those features sink.
            var groups=new Dictionary<Transform,List<Vector3>>();var byBone=new Dictionary<Transform,Part>();
            foreach(var p in parts){groups[p.bone]=new List<Vector3>();byBone[p.bone]=p;}
            var mesh=skin.sharedMesh;var vertices=mesh.vertices;var weights=mesh.boneWeights;var bind=mesh.bindposes;var bones=skin.bones;var matrices=new Matrix4x4[bones.Length];
            for(int i=0;i<bones.Length;i++)matrices[i]=bones[i].localToWorldMatrix*bind[i];
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];int index=w.boneIndex0;float max=w.weight0;if(w.weight1>max){index=w.boneIndex1;max=w.weight1;}if(w.weight2>max){index=w.boneIndex2;max=w.weight2;}if(w.weight3>max)index=w.boneIndex3;
                Transform owner=bones[index];if(owner.name.StartsWith("LeftWristBlend"))owner=Bone(HumanBodyBones.LeftLowerArm);
                while(owner!=null&&!groups.ContainsKey(owner))owner=owner.parent;if(owner==null)continue;
                var v=vertices[i];var point=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                groups[owner].Add(byBone[owner].body.transform.InverseTransformPoint(point));
            }
            // The fingers open after release. Include that silhouette in each hand's
            // contact hull, so a previously curled thumb cannot unfold into the floor.
            for(int i=0;i<fingers.Count;i++)fingers[i].localRotation=loose[i];
            for(int i=0;i<bones.Length;i++)matrices[i]=bones[i].localToWorldMatrix*bind[i];
            for(int i=0;i<vertices.Length;i++)
            {
                var w=weights[i];int index=w.boneIndex0;float max=w.weight0;if(w.weight1>max){index=w.boneIndex1;max=w.weight1;}if(w.weight2>max){index=w.boneIndex2;max=w.weight2;}if(w.weight3>max)index=w.boneIndex3;
                var owner=bones[index];while(owner!=null&&!groups.ContainsKey(owner))owner=owner.parent;
                if(owner!=Bone(HumanBodyBones.LeftHand)&&owner!=Bone(HumanBodyBones.RightHand))continue;
                var v=vertices[i];var point=matrices[w.boneIndex0].MultiplyPoint3x4(v)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(v)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(v)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(v)*w.weight3;
                groups[owner].Add(byBone[owner].body.transform.InverseTransformPoint(point));
            }
            for(int i=0;i<fingers.Count;i++)fingers[i].localRotation=grip[i];
            foreach(var p in parts)
            {
                var cloud=groups[p.bone];if(cloud.Count<8)continue;
                var extremes=new List<Vector3>();
                for(int i=0;i<96;i++)
                {float y=1-2*(i+.5f)/96,r=Mathf.Sqrt(1-y*y),angle=i*2.3999632f;var direction=new Vector3(r*Mathf.Cos(angle),y,r*Mathf.Sin(angle));float best=float.NegativeInfinity;var point=Vector3.zero;foreach(var v in cloud){float score=Vector3.Dot(v,direction);if(score>best){best=score;point=v;}}if(!extremes.Contains(point))extremes.Add(point);}
                if(extremes.Count<4)continue;
                var centre=Vector3.zero;foreach(var v in extremes)centre+=v;centre/=extremes.Count;
                float padding=p.bone==Bone(HumanBodyBones.Head)?.03f:.006f;
                for(int i=0;i<extremes.Count;i++)extremes[i]+=(extremes[i]-centre).normalized*padding;
                foreach(var c in p.body.GetComponentsInChildren<Collider>()){colliders.Remove(c);c.enabled=false;Object.Destroy(c);}
                var hull=new Mesh{name="Death contact · "+p.bone.name};hull.SetVertices(extremes);var triangles=new List<int>();for(int i=1;i<extremes.Count-1;i++){triangles.Add(0);triangles.Add(i);triangles.Add(i+1);}hull.SetTriangles(triangles,0);hull.RecalculateBounds();contactMeshes.Add(hull);
                var collider=p.body.gameObject.AddComponent<MeshCollider>();collider.convex=true;collider.sharedMesh=hull;collider.contactOffset=.008f;colliders.Add(collider);
            }
        }
        Transform Shape(Rigidbody b,Vector3 position)
        {var t=new GameObject("contact").transform;t.SetParent(b.transform);t.position=position;t.rotation=root.rotation;return t;}
        void Sphere(Rigidbody b,Vector3 center,float radius){var c=Shape(b,center).gameObject.AddComponent<SphereCollider>();c.radius=radius;colliders.Add(c);}
        void Box(Rigidbody b,Vector3 center,Vector3 size){var c=Shape(b,center).gameObject.AddComponent<BoxCollider>();c.size=size;colliders.Add(c);}
        void Capsule(Rigidbody b,Vector3 from,Vector3 to,float radius)
        {var t=Shape(b,(from+to)*.5f);t.rotation=Quaternion.FromToRotation(Vector3.up,to-from);var c=t.gameObject.AddComponent<CapsuleCollider>();c.direction=1;c.radius=radius;c.height=Mathf.Max(radius*2,Vector3.Distance(from,to)+radius*.65f);colliders.Add(c);}
        void Joint(Rigidbody child,Rigidbody parent,Vector3 pivot,Vector3 axis,float low,float high,float swing)
        {
            var j=child.gameObject.AddComponent<ConfigurableJoint>();j.connectedBody=parent;j.autoConfigureConnectedAnchor=false;j.anchor=child.transform.InverseTransformPoint(pivot);j.connectedAnchor=parent.transform.InverseTransformPoint(pivot);
            j.axis=child.transform.InverseTransformDirection(axis);j.secondaryAxis=child.transform.InverseTransformDirection(root.up);
            j.xMotion=j.yMotion=j.zMotion=ConfigurableJointMotion.Locked;j.angularXMotion=j.angularYMotion=j.angularZMotion=ConfigurableJointMotion.Limited;
            j.lowAngularXLimit=new SoftJointLimit{limit=low};j.highAngularXLimit=new SoftJointLimit{limit=high};j.angularYLimit=j.angularZLimit=new SoftJointLimit{limit=swing};j.projectionMode=JointProjectionMode.PositionAndRotation;j.projectionDistance=.02f;j.projectionAngle=8;j.enablePreprocessing=false;
        }
        public void Apply(float time)
        {
            if(container==null)return;
            foreach(var p in parts)p.bone.SetPositionAndRotation(p.body.transform.position,p.body.transform.rotation);
            float release=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.12f,.55f,time));
            for(int i=0;i<fingers.Count;i++)fingers[i].localRotation=Quaternion.Slerp(grip[i],loose[i],release);
            if(held!=null&&!released&&time>=ReleaseTime)
            {
                releasePosition=held.transform.position;releaseRotation=held.transform.rotation;
                dropped=Object.Instantiate(held);dropped.name="Released · "+held.name;dropped.transform.SetParent(null);dropped.transform.localScale=held.transform.lossyScale;
                dropped.transform.SetPositionAndRotation(releasePosition,releaseRotation);
                foreach(var b in dropped.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
                foreach(var c in dropped.GetComponentsInChildren<Collider>())c.enabled=false;
                foreach(var b in dropped.GetComponentsInChildren<Rigidbody>())b.isKinematic=true;
                MeshFilter largest=null;float size=0;
                foreach(var mf in dropped.GetComponentsInChildren<MeshFilter>())if(mf.sharedMesh!=null)
                {var surface=RaccoonDropSurfaceV49.ReadSurface(mf);dropSurfaces.Add(new DropSurface{transform=mf.transform,vertices=surface});float s=Vector3.Scale(mf.sharedMesh.bounds.size,mf.transform.lossyScale).sqrMagnitude;if(s>size){size=s;largest=mf;}}
                floorRotation=releaseRotation;
                if(largest!=null)
                {var s=Vector3.Scale(largest.sharedMesh.bounds.size,largest.transform.lossyScale);s=new Vector3(Mathf.Abs(s.x),Mathf.Abs(s.y),Mathf.Abs(s.z));var axis=s.x<s.y&&s.x<s.z?Vector3.right:s.y<s.z?Vector3.up:Vector3.forward;var normal=largest.transform.TransformDirection(axis);if(Vector3.Dot(normal,Vector3.up)<0)normal=-normal;floorRotation=Quaternion.FromToRotation(normal,Vector3.up)*releaseRotation;}
                dropped.SetActive(true);released=true;held.SetActive(false);
            }
            if(dropped!=null&&!settled)
            {
                float t=Mathf.Clamp(time-ReleaseTime,0,.65f);
                dropped.transform.position=releasePosition+root.TransformVector(new Vector3(-1.25f,.10f,.14f))*t-Vector3.up*(4.9f*t*t);
                dropped.transform.rotation=Quaternion.Slerp(releaseRotation,floorRotation,Mathf.SmoothStep(0,1,t/.65f));
                float bottom=float.PositiveInfinity;foreach(var s in dropSurfaces)foreach(var v in s.vertices)bottom=Mathf.Min(bottom,s.transform.TransformPoint(v).y);
                if(bottom<floor+.01f)dropped.transform.position+=Vector3.up*(floor+.01f-bottom);
            }
            if(!settled&&time>4f)
            {foreach(var p in parts){p.body.linearVelocity=Vector3.zero;p.body.angularVelocity=Vector3.zero;p.body.isKinematic=true;}settled=true;}
        }
        public void Dispose()
        {
            if(restoreBones!=null)for(int i=0;i<restoreBones.Length;i++)if(restoreBones[i]!=null){restoreBones[i].localPosition=restorePositions[i];restoreBones[i].localRotation=restoreRotations[i];}
            restoreBones=null;
            foreach(var mesh in contactMeshes)Object.Destroy(mesh);contactMeshes.Clear();
            if(container!=null){container.SetActive(false);Object.Destroy(container);}if(dropped!=null){dropped.SetActive(false);Object.Destroy(dropped);}container=dropped=null;held=null;parts.Clear();colliders.Clear();fingers.Clear();grip.Clear();loose.Clear();dropSurfaces.Clear();released=settled=false;
        }
    }
}
