using System.Collections.Generic;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>Distance-driven foot contacts, sole orientation and leg reach. Never moves the player collider.</summary>
    [DefaultExecutionOrder(25)]
    [RequireComponent(typeof(Animator))]
    public sealed class RaccoonGrounding : MonoBehaviour
    {
        private sealed class Foot
        {
            public Transform upper,lower,ankle;
            public readonly List<int> samples=new List<int>();
            public float lift;
            public Vector3 soleNormal;
            public Vector3 target;
            public Quaternion rotation;
            public Quaternion restRotation,anchorRotation;
            public Transform toes,support;
            public Quaternion restToes;
            public Vector3 anchor,takeoff,lastTarget;
            public float soleHeight,side,swingStart;
            public bool swing,planted;
        }
        private readonly Foot[] _feet={new Foot(),new Foot()};
        private readonly RaycastHit[] _hits=new RaycastHit[16];
        private Animator _animator;
        private CharacterController _controller;
        private SkinnedMeshRenderer _skin;
        private Vector3[] _vertices;
        private BoneWeight[] _weights;
        private Matrix4x4[] _bind;
        private Transform[] _bones;
        private bool _bound;
        private const float MaximumLift=.22f;
        private bool _travelActive,_wasMoving,_hasTravelPosition;
        private Vector3 _lastRoot,_lastSurfaceLocal;
        private Transform _travelSurface;
        private float _phase,_pelvisDrop,_gaitBlend;
        public bool IsFootPlanted(int side)=>_feet[side].planted;
        public float FootTargetError(int side)=>Vector3.Distance(_feet[side].ankle.position,_feet[side].target);

        private void Awake()=>Bind();
        private void OnDisable(){foreach(var foot in _feet){foot.lift=0;foot.planted=false;}_travelActive=_hasTravelPosition=_wasMoving=false;}
        public void Bind()
        {
            _animator=GetComponent<Animator>();_controller=GetComponentInParent<CharacterController>();_skin=GetComponentInChildren<SkinnedMeshRenderer>();
            if(_animator==null || !_animator.isHuman || _skin==null)return;
            var mesh=_skin.sharedMesh;_vertices=mesh.vertices;_weights=mesh.boneWeights;_bind=mesh.bindposes;_bones=_skin.bones;
            for(int side=0;side<2;side++)
            {
                bool left=side==0;var foot=_feet[side];foot.samples.Clear();foot.lift=0;
                foot.upper=_animator.GetBoneTransform(left?HumanBodyBones.LeftUpperLeg:HumanBodyBones.RightUpperLeg);
                foot.lower=_animator.GetBoneTransform(left?HumanBodyBones.LeftLowerLeg:HumanBodyBones.RightLowerLeg);
                foot.ankle=_animator.GetBoneTransform(left?HumanBodyBones.LeftFoot:HumanBodyBones.RightFoot);
                int ankleIndex=System.Array.IndexOf(_bones,foot.ankle);
                if(ankleIndex>=0)
                {
                    var rest=_skin.transform.localToWorldMatrix*_bind[ankleIndex].inverse;
                    foot.soleNormal=Quaternion.Inverse(rest.rotation)*transform.up;
                    foot.restRotation=Quaternion.Inverse(transform.rotation)*rest.rotation;
                    foot.side=transform.InverseTransformPoint(rest.GetColumn(3)).x;
                    foot.toes=_animator.GetBoneTransform(left?HumanBodyBones.LeftToes:HumanBodyBones.RightToes);
                    int toeIndex=System.Array.IndexOf(_bones,foot.toes);
                    if(toeIndex>=0)foot.restToes=Quaternion.Inverse(rest.rotation)*(_skin.transform.localToWorldMatrix*_bind[toeIndex].inverse).rotation;
                }
                string prefix=left?"Left":"Right";float min=999;var candidates=new List<int>();
                for(int i=0;i<_vertices.Length;i++)
                {
                    var w=_weights[i];float Weight(int index,float value)=>_bones[index].name==prefix+"Foot"||_bones[index].name==prefix+"Toes"?value:0;
                    if(Weight(w.boneIndex0,w.weight0)+Weight(w.boneIndex1,w.weight1)+Weight(w.boneIndex2,w.weight2)+Weight(w.boneIndex3,w.weight3)<.8f)continue;
                    candidates.Add(i);min=Mathf.Min(min,_skin.transform.TransformPoint(_vertices[i]).y);
                }
                // Deduplicate seam vertices. Every distinct low sole point participates, including heel and toes.
                var seen=new HashSet<Vector3>();
                foreach(int i in candidates)if(_skin.transform.TransformPoint(_vertices[i]).y<min+.025f && seen.Add(_vertices[i]))foot.samples.Add(i);
                if(ankleIndex>=0)foot.soleHeight=(_skin.transform.localToWorldMatrix*_bind[ankleIndex].inverse).GetColumn(3).y-min;
            }
            _bound=true;
            _travelActive=_hasTravelPosition=_wasMoving=false;_pelvisDrop=_gaitBlend=0;
        }
        private void LateUpdate()
        {
            if(!_bound)Bind();
            Step(Time.deltaTime,_controller!=null && _controller.isGrounded);
        }
        public void Step(float dt,bool grounded)
        {
            if(!_bound)Bind();
            if(!_bound || dt<=0 || !float.IsFinite(dt))return;
            if(!grounded || !_animator.isActiveAndEnabled || !_animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion"))
            {OnDisable();return;}
            if(StepTravel(dt))return;
            foreach(var foot in _feet)
            {
                if(foot.ankle==null || foot.samples.Count==0)continue;
                // Only align near the supporting surface. Swing feet retain their authored rotation.
                float minimumClearance=999;Vector3 surfaceNormal=Vector3.zero;
                foreach(int index in foot.samples)
                {
                    Vector3 p=Point(index);
                    if(!Probe(p,out var hit))continue;
                    float clearance=p.y-hit.point.y;
                    if(clearance<-.24f || clearance>.18f)continue;
                    minimumClearance=Mathf.Min(minimumClearance,clearance);surfaceNormal+=hit.normal;
                }
                float contact=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.018f,.09f,minimumClearance));
                if(surfaceNormal.sqrMagnitude>.01f && foot.soleNormal.sqrMagnitude>.5f)
                {
                    Quaternion alignment=Quaternion.FromToRotation(foot.ankle.rotation*foot.soleNormal,surfaceNormal.normalized);
                    foot.ankle.rotation=Quaternion.Slerp(Quaternion.identity,alignment,contact)*foot.ankle.rotation;
                }
                float required=-999;bool supported=false;
                foreach(int index in foot.samples)
                {
                    Vector3 p=Point(index);if(!Probe(p,out var chosen))continue;
                    float clear=p.y-chosen.point.y;if(clear<-.24f || clear>.18f)continue;
                    supported=true;required=Mathf.Max(required,.006f-clear);
                }
                float target=supported?Mathf.Clamp(required,-.12f,MaximumLift):0;
                if(target<0)target*=contact;
                foot.lift=Mathf.Max(target,Mathf.Lerp(foot.lift,target,1-Mathf.Exp(-18*Mathf.Min(dt,.1f))));
                foot.target=foot.ankle.position+Vector3.up*foot.lift;
                foot.rotation=foot.ankle.rotation;
            }
            // Leveling a tilted foot can require more reach than the animated leg allows.
            // Lower the pelvis only by that missing reach; keep both ankle targets unchanged.
            float pelvisDrop=0;
            foreach(var foot in _feet)
            {
                if(foot.ankle==null || foot.samples.Count==0)continue;
                float reach=Vector3.Distance(foot.upper.position,foot.lower.position)+Vector3.Distance(foot.lower.position,foot.ankle.position)-.003f;
                Vector3 delta=foot.upper.position-foot.target;
                float horizontal=delta.x*delta.x+delta.z*delta.z;
                float available=Mathf.Sqrt(Mathf.Max(.001f,reach*reach-horizontal));
                pelvisDrop=Mathf.Max(pelvisDrop,delta.y-available);
            }
            pelvisDrop=Mathf.Clamp(pelvisDrop,0,.15f);
            if(pelvisDrop>0)_animator.GetBoneTransform(HumanBodyBones.Hips).position-=Vector3.up*pelvisDrop;
            foreach(var foot in _feet)
            {
                if(foot.ankle==null || foot.samples.Count==0)continue;
                if(Mathf.Abs(foot.lift)<.0001f && pelvisDrop<.0001f)continue;
                Vector3 hint=foot.lower.position+transform.forward*.12f;
                RaccoonEquipmentPose.SolveArm(foot.upper,foot.lower,foot.ankle,foot.target,hint);
                foot.ankle.rotation=foot.rotation;
                ClearSole(foot,hint);
            }
        }
        // Grounded travel is driven by distance, so contact does not depend on a retargeted clip's stride length.
        // The existing clip still supplies the pelvis, torso, arms and the knee bend plane.
        private bool StepTravel(float dt)
        {
            Vector3 root=transform.position;
            Transform surface=Probe(root,out var under)?under.collider.transform:null;
            Vector3 previous=_hasTravelPosition && surface!=null && surface==_travelSurface
                ?surface.TransformPoint(_lastSurfaceLocal):_lastRoot;
            Vector3 delta=_hasTravelPosition?Vector3.ProjectOnPlane(root-previous,Vector3.up):Vector3.zero;
            _lastRoot=root;_travelSurface=surface;_lastSurfaceLocal=surface!=null?surface.InverseTransformPoint(root):root;_hasTravelPosition=true;
            float speed=delta.magnitude/dt;
            if(speed>20){_travelActive=_wasMoving=false;return false;}
            bool moving=speed>.08f;
            if(!_travelActive && !moving)return false;
            _gaitBlend=Mathf.MoveTowards(_gaitBlend,Mathf.SmoothStep(0,1,Mathf.InverseLerp(4.5f,7f,speed)),dt*3);
            float run=_gaitBlend;
            float cycleDistance=Mathf.Lerp(1.5f,2f,run),stance=Mathf.Lerp(.48f,.32f,run);
            Vector3 direction=moving?delta.normalized:transform.forward;
            Vector3 strideCentre=root+Vector3.ProjectOnPlane(_animator.GetBoneTransform(HumanBodyBones.Hips).position-root,Vector3.up);
            float ahead=cycleDistance*stance*.5f;
            if(moving && !_wasMoving)
            {
                _phase=stance-.16f;
                foreach(var foot in _feet)
                {
                    foot.lastTarget=foot.ankle.position;
                    SetAnchor(foot,foot.lastTarget,foot.ankle.rotation);
                    foot.takeoff=foot.lastTarget;foot.swing=false;
                }
            }
            _travelActive=true;
            if(moving)_phase=Mathf.Repeat(_phase+delta.magnitude/cycleDistance,1);
            for(int side=0;side<2;side++)
            {
                var foot=_feet[side];
                if(foot.toes!=null)foot.toes.localRotation=foot.restToes;
                float phase=Mathf.Repeat(_phase+side*.5f,1);
                Quaternion flat=transform.rotation*foot.restRotation;
                if(!moving)
                {
                    if(_wasMoving)SetAnchor(foot,foot.lastTarget,flat);
                    Vector3 held=Anchor(foot);
                    Vector3 landing=SurfaceTarget(foot,held,flat,out var rotation);
                    foot.target=held;foot.target.y=Mathf.MoveTowards(foot.lastTarget.y,landing.y,dt*1.5f);
                    foot.rotation=rotation;
                    foot.planted=Mathf.Abs(foot.target.y-landing.y)<.003f;foot.swing=false;
                }
                else if(phase<stance)
                {
                    if(foot.swing)
                    {
                        Vector3 landing=strideCentre+transform.right*foot.side+direction*ahead;
                        landing.y=root.y+foot.soleHeight;
                        landing=SurfaceTarget(foot,landing,flat,out var rotation);
                        SetAnchor(foot,landing,rotation);
                    }
                    foot.target=Anchor(foot);foot.rotation=foot.support!=null?foot.support.rotation*foot.anchorRotation:flat;
                    foot.target=SurfaceTarget(foot,foot.target,foot.rotation,out foot.rotation);
                    foot.planted=true;foot.swing=false;
                }
                else
                {
                    if(!foot.swing){foot.takeoff=foot.lastTarget;foot.swingStart=phase;}
                    float u=(phase-foot.swingStart)/Mathf.Max(.01f,1-foot.swingStart);
                    float remaining=(1-phase)*cycleDistance/Mathf.Max(.1f,speed);
                    Vector3 landing=strideCentre+transform.right*foot.side+direction*(ahead+speed*remaining);
                    landing.y=root.y+foot.soleHeight;
                    landing=SurfaceTarget(foot,landing,flat,out var rotation);
                    float blend=u*u*(3-2*u);
                    foot.target=Vector3.Lerp(foot.takeoff,landing,blend);
                    foot.target.y+=Mathf.Sin(u*Mathf.PI)*Mathf.Sin(u*Mathf.PI)*Mathf.Lerp(.16f,.23f,run);
                    foot.rotation=rotation;foot.planted=false;foot.swing=true;
                }
                foot.lastTarget=foot.target;
            }
            float required=moving?Mathf.Lerp(.07f,.06f,run):0;
            foreach(var foot in _feet)
            {
                float reach=Vector3.Distance(foot.upper.position,foot.lower.position)+Vector3.Distance(foot.lower.position,foot.ankle.position)-.003f;
                Vector3 d=foot.upper.position-foot.target;
                required=Mathf.Max(required,d.y-Mathf.Sqrt(Mathf.Max(.001f,reach*reach-d.x*d.x-d.z*d.z)));
            }
            required=Mathf.Clamp(required,0,.22f);
            _pelvisDrop=Mathf.Max(required,Mathf.Lerp(_pelvisDrop,required,1-Mathf.Exp(-12*dt)));
            _animator.GetBoneTransform(HumanBodyBones.Hips).position-=Vector3.up*_pelvisDrop;
            foreach(var foot in _feet)
            {
                Vector3 hint=foot.lower.position+transform.forward*.25f;
                RaccoonEquipmentPose.SolveArm(foot.upper,foot.lower,foot.ankle,foot.target,hint);
                foot.ankle.rotation=foot.rotation;
                ClearSole(foot,hint);
            }
            _wasMoving=moving;
            return true;
        }
        private void ClearSole(Foot foot,Vector3 hint)
        {
            // Mixed ankle/lower-leg weights are not a perfectly rigid sole. Verify the deformed surface too.
            for(int pass=0;pass<3;pass++)
            {
                float correction=0;
                foreach(int i in foot.samples)
                {
                    Vector3 p=Point(i);
                    if(Probe(p,out var h) && p.y-h.point.y>-.25f)correction=Mathf.Max(correction,.006f-(p.y-h.point.y));
                }
                if(correction<.0002f)break;
                foot.target+=Vector3.up*Mathf.Min(correction,.12f);
                RaccoonEquipmentPose.SolveArm(foot.upper,foot.lower,foot.ankle,foot.target,hint);
                foot.ankle.rotation=foot.rotation;
            }
            foot.lastTarget=foot.target;
        }
        private Vector3 Anchor(Foot foot)=>foot.support!=null?foot.support.TransformPoint(foot.anchor):foot.anchor;
        private void SetAnchor(Foot foot,Vector3 position,Quaternion rotation)
        {
            foot.support=Probe(position,out var hit)?hit.collider.transform:null;
            foot.anchor=foot.support!=null?foot.support.InverseTransformPoint(position):position;
            foot.anchorRotation=foot.support!=null?Quaternion.Inverse(foot.support.rotation)*rotation:rotation;
        }
        private Vector3 SurfaceTarget(Foot foot,Vector3 position,Quaternion rotation,out Quaternion aligned)
        {
            aligned=rotation;
            if(Probe(position,out var hit))
            {
                aligned=Quaternion.FromToRotation(rotation*foot.soleNormal,hit.normal)*rotation;
                position.y=hit.point.y+foot.soleHeight+.006f;
            }
            // Test the whole sole footprint, not just the ankle; a stair may rise under the toes.
            Quaternion change=aligned*Quaternion.Inverse(foot.ankle.rotation);
            float lift=-999;
            foreach(int i in foot.samples)
            {
                Vector3 p=position+change*(Point(i)-foot.ankle.position);
                if(Probe(p,out var h))lift=Mathf.Max(lift,.006f-(p.y-h.point.y));
            }
            if(lift>-999)position.y+=Mathf.Clamp(lift,-.10f,.22f);
            return position;
        }
        private bool Probe(Vector3 point,out RaycastHit chosen)
        {
            int count=Physics.RaycastNonAlloc(point+Vector3.up*.35f,Vector3.down,_hits,.70f,~0,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity;chosen=default;
            for(int j=0;j<count;j++)
            {
                var hit=_hits[j];
                if(hit.normal.y<.5f || (_controller!=null && hit.collider.transform.IsChildOf(_controller.transform)))continue;
                if(hit.distance<nearest){nearest=hit.distance;chosen=hit;}
            }
            return !float.IsPositiveInfinity(nearest);
        }
        private Vector3 Point(int i)
        {
            var w=_weights[i];Vector3 p=_vertices[i];
            Vector3 Skin(int bone)=>(_bones[bone].localToWorldMatrix*_bind[bone]).MultiplyPoint3x4(p);
            return Skin(w.boneIndex0)*w.weight0+Skin(w.boneIndex1)*w.weight1+Skin(w.boneIndex2)*w.weight2+Skin(w.boneIndex3)*w.weight3;
        }
    }
}
