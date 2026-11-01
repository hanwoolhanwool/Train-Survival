using Game.Gameplay.Inventory;
using UnityEngine;

namespace Game.Gameplay.Player
{
    // Body aim runs first; elbow corrective shapes run after these solved arm transforms.
    [DefaultExecutionOrder(50)]
    [RequireComponent(typeof(Animator))]
    public sealed class RaccoonEquipmentPose : MonoBehaviour
    {
        [SerializeField] private RaccoonEquipmentProfile _profile;
        private Animator _animator;
        private PlayerAimView _aim;
        private PlayerCharacterView _view;
        private RaccoonEquipmentProfile.HandPose _lastRight, _lastLeft;
        private readonly RaccoonEquipmentProfile.HandPose _smoothedRight = new RaccoonEquipmentProfile.HandPose();
        private readonly RaccoonEquipmentProfile.HandPose _smoothedLeft = new RaccoonEquipmentProfile.HandPose();
        private float _rightWeight, _leftWeight;
        private float _upwardClearance;
        private float _bodyYawWeight;
        private Vector3 _rightTarget, _leftTarget;
        private bool _hasRightTarget, _hasLeftTarget;
        private float _targetBlend;
        private float _stablePairSeconds;
        private HotbarItemType _previousItem;
        private RaccoonWristDeformation _wristDeformation;
        public RaccoonEquipmentProfile Profile => _profile;
        // Resolved grip anchors in the root space supplied to Apply; useful for pose inspection.
        public Vector3 RightGripTargetLocal => _rightTarget;
        public Vector3 LeftGripTargetLocal => _leftTarget;
        public void Bind(RaccoonEquipmentProfile profile) { _profile = profile; _animator = GetComponent<Animator>(); }

        public static readonly HumanBodyBones[] RightFingers = {
            HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal,
            HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal,
            HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal,
            HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal,
            HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal };
        public static readonly HumanBodyBones[] LeftFingers = {
            HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal,
            HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal,
            HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal,
            HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal,
            HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal };

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _aim = GetComponentInParent<PlayerAimView>();
            _view = GetComponentInParent<PlayerCharacterView>();
        }

        private void OnEnable() { _rightWeight = _leftWeight = _bodyYawWeight = _stablePairSeconds = 0f; _previousItem=HotbarItemType.None; _lastRight = _lastLeft = null; _hasRightTarget = _hasLeftTarget = false; }

        private void LateUpdate()
        {
            if (_profile == null || _aim == null || !_aim.IsSpawned || _view == null
                || _view.ActiveAnimator != _animator || !_animator.isActiveAndEnabled) return;
            Apply(_aim.HeldItem, _aim.LeftHarpoonHeld, _aim.DisplayPitchDegrees, Time.deltaTime, _aim.transform);
        }

        // The editor review and runtime use the same solver, target data and finger rotations.
        public void Apply(HotbarItemType item, bool leftHarpoon, float pitch, float deltaTime, Transform root)
        {
            if (_profile == null) return;
            if (_animator == null) _animator = GetComponent<Animator>();
            bool found = _profile.TryGetEntry(item, out RaccoonEquipmentProfile.Entry entry);
            _targetBlend = 1 - Mathf.Exp(-Mathf.Max(0, deltaTime) * 8);
            bool right = found && !(item == HotbarItemType.Harpoon && leftHarpoon);
            bool left = found && entry.TwoHanded || leftHarpoon;
            if (right)
            {
                SmoothPose(_smoothedRight, entry.Right, _lastRight == null ? 1 : 1 - Mathf.Exp(-deltaTime * 22));
                _lastRight = _smoothedRight;
            }
            if (left)
            {
                SmoothPose(_smoothedLeft, leftHarpoon ? _profile.LeftHarpoon : entry.Left, _lastLeft == null ? 1 : 1 - Mathf.Exp(-deltaTime * 22));
                _lastLeft = _smoothedLeft;
            }
            float step = deltaTime / Mathf.Max(.01f, _profile.BlendSeconds);
            _rightWeight = Mathf.MoveTowards(_rightWeight, right ? 1f : 0f, step);
            _leftWeight = Mathf.MoveTowards(_leftWeight, left ? 1f : 0f, step);
            float displayPitch=Mathf.Clamp(pitch,-89,89);
            _upwardClearance=Mathf.Clamp01((-displayPitch-45f)/40f)*.08f;
            Quaternion aimRotation = root.rotation * Quaternion.Euler(displayPitch, 0, 0);
            Quaternion positionRotation = root.rotation * Quaternion.Euler(displayPitch*_profile.HandPositionPitchWeight,0,0);
            bool twoHanded = found && entry.TwoHanded && !leftHarpoon;
            _stablePairSeconds=twoHanded && item==_previousItem?_stablePairSeconds+Mathf.Max(0,deltaTime):0;
            _previousItem=item;
            _bodyYawWeight=Mathf.MoveTowards(_bodyYawWeight,twoHanded?1:0,step);
            if(_bodyYawWeight>0)
            {
                Transform chest=_animator.GetBoneTransform(HumanBodyBones.Chest);
                Transform head=_animator.GetBoneTransform(HumanBodyBones.Head);
                float yaw=_profile.TwoHandBodyYaw*_bodyYawWeight;
                if(chest!=null)chest.rotation=Quaternion.AngleAxis(yaw,root.up)*chest.rotation;
                if(head!=null)head.rotation=Quaternion.AngleAxis(-yaw,root.up)*head.rotation;
            }
            // At steep aim angles allow some extension so the stock stays clear of the torso.
            float supportBend=twoHanded && entry.MinimumSupportElbowBend>0
                ? entry.MinimumSupportElbowBend*(1-Mathf.InverseLerp(0,85,Mathf.Abs(displayPitch))) : 0;
            Vector3 pairOffset=twoHanded?FindPairOffset(aimRotation,positionRotation,root,supportBend):Vector3.zero;
            ApplyHand(false, _lastRight, _rightWeight, aimRotation, positionRotation, root, twoHanded, pairOffset);
            ApplyHand(true, _lastLeft, _leftWeight, aimRotation, positionRotation, root, twoHanded, pairOffset);
            if(_wristDeformation==null)_wristDeformation=GetComponent<RaccoonWristDeformation>();
            if(_wristDeformation!=null)_wristDeformation.Apply();
        }

        private Vector3 FindPairOffset(Quaternion aim,Quaternion positionAim,Transform root,float supportBend)
        {
            Transform ru=_animator.GetBoneTransform(HumanBodyBones.RightUpperArm), lu=_animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform rl=_animator.GetBoneTransform(HumanBodyBones.RightLowerArm), ll=_animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
            Transform rh=_animator.GetBoneTransform(HumanBodyBones.RightHand), lh=_animator.GetBoneTransform(HumanBodyBones.LeftHand);
            Vector3 reference=(_profile.RightShoulder+_profile.LeftShoulder)*.5f, origin=(ru.position+lu.position)*.5f;
            Vector3 originalRight=origin+aim*Vector3.Scale(_lastRight.Position-reference,root.lossyScale);
            Vector3 right=KeepInFront(origin+positionAim*Vector3.Scale(_lastRight.Position-reference,root.lossyScale),root);
            Vector3 left=right+aim*Vector3.Scale(_lastLeft.Position-_lastRight.Position,root.lossyScale);
            float ra=Vector3.Distance(ru.position,rl.position),rb=Vector3.Distance(rl.position,rh.position);
            float la=Vector3.Distance(lu.position,ll.position),lb=Vector3.Distance(ll.position,lh.position);
            Vector3 offset=Vector3.zero;
            // Move the weapon and BOTH hands together when either arm reaches its limit.
            for(int i=0;i<4;i++)
            {
                offset+=ReachCorrection(right+offset-ru.position,ra,rb);
                offset+=ReachCorrection(left+offset-lu.position,la,lb,supportBend);

            }
            // Keep the shared weapon anchor continuous when changing between one and two hands.
            Vector3 smoothed=SmoothTarget(false,right+offset,root);
            // Smoothing can leave the anchor beyond a moving shoulder's reach during a run.
            // Project the shared anchor back into BOTH arms' reach after smoothing.
            Vector3 separation=aim*Vector3.Scale(_lastLeft.Position-_lastRight.Position,root.lossyScale);
            float projectionWeight=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.16f,.35f,_stablePairSeconds));
            for(int i=0;i<4 && projectionWeight>0 && _rightWeight>.99f && _leftWeight>.99f;i++)
            {
                smoothed+=ReachCorrection(smoothed-ru.position,ra,rb)*projectionWeight;
                smoothed+=ReachCorrection(smoothed+separation-lu.position,la,lb,supportBend)*projectionWeight;

            }
            _rightTarget=root.InverseTransformPoint(smoothed);
            return smoothed-originalRight;
        }

        private Vector3 SmoothTarget(bool left,Vector3 target,Transform root)
        {
            Vector3 local=root.InverseTransformPoint(target);
            if(left)
            {
                _leftTarget=_hasLeftTarget?Vector3.Lerp(_leftTarget,local,_targetBlend):local;
                _hasLeftTarget=true;return root.TransformPoint(_leftTarget);
            }
            _rightTarget=_hasRightTarget?Vector3.Lerp(_rightTarget,local,_targetBlend):local;
            _hasRightTarget=true;return root.TransformPoint(_rightTarget);
        }

        private Vector3 KeepInFront(Vector3 target,Transform root)
        {
            float depth=Vector3.Dot(target-root.position,root.forward);
            return target+root.forward*Mathf.Max(0,(_profile.MinimumGripForward+_upwardClearance)*root.lossyScale.z-depth);
        }

        private static Vector3 ReachCorrection(Vector3 delta,float a,float b,float minimumBend=0)
        {
            float distance=delta.magnitude;
            if(distance<.0001f)return Vector3.zero;
            float maximum=Mathf.Sqrt(a*a+b*b+2*a*b*Mathf.Cos(Mathf.Clamp(minimumBend,0,100)*Mathf.Deg2Rad))-.002f;
            float reachable=Mathf.Clamp(distance,Mathf.Abs(a-b)+.002f,maximum);
            return delta*(reachable/distance-1f);
        }

        private static void SmoothPose(RaccoonEquipmentProfile.HandPose output, RaccoonEquipmentProfile.HandPose target, float blend)
        {
            output.Position=Vector3.Lerp(output.Position,target.Position,blend);
            output.ElbowHint=Vector3.Lerp(output.ElbowHint,target.ElbowHint,blend);
            output.Rotation=Quaternion.Slerp(output.Rotation,target.Rotation,blend);
            for(int i=0;i<output.Fingers.Length;i++)output.Fingers[i]=blend>=1?target.Fingers[i]:Quaternion.Slerp(output.Fingers[i],target.Fingers[i],blend);
        }

        private void ApplyHand(bool left, RaccoonEquipmentProfile.HandPose pose, float weight, Quaternion aim, Quaternion positionAim,Transform root, bool twoHanded,Vector3 pairOffset)
        {
            if (pose == null || weight <= 0) return;
            Transform upper = _animator.GetBoneTransform(left ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Transform lower = _animator.GetBoneTransform(left ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform hand = _animator.GetBoneTransform(left ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);
            Quaternion u = upper.localRotation, l = lower.localRotation, h = hand.localRotation;
            Vector3 reference = left ? _profile.LeftShoulder : _profile.RightShoulder;
            Vector3 origin=upper.position;
            if(twoHanded)
            {
                reference=(_profile.LeftShoulder+_profile.RightShoulder)*.5f;
                origin=(_animator.GetBoneTransform(HumanBodyBones.LeftUpperArm).position+_animator.GetBoneTransform(HumanBodyBones.RightUpperArm).position)*.5f;
            }
            Vector3 target = twoHanded ? origin + aim * Vector3.Scale(pose.Position - reference, root.lossyScale)+pairOffset
                : KeepInFront(origin+positionAim*Vector3.Scale(pose.Position-reference,root.lossyScale),root);
            if(!twoHanded)target=SmoothTarget(left,target,root);
            else if(left){_leftTarget=root.InverseTransformPoint(target);_hasLeftTarget=true;}
            Vector3 hint = upper.position + aim * Vector3.Scale(pose.ElbowHint - reference, root.lossyScale);
            SolveArm(upper, lower, hand, target, hint);
            hand.rotation = aim * pose.Rotation;
            upper.localRotation = Quaternion.Slerp(u, upper.localRotation, weight);
            lower.localRotation = Quaternion.Slerp(l, lower.localRotation, weight);
            hand.localRotation = Quaternion.Slerp(h, hand.localRotation, weight);
            HumanBodyBones[] fingers = left ? LeftFingers : RightFingers;
            for (int i = 0; i < fingers.Length && i < pose.Fingers.Length; i++)
            {
                Transform bone = _animator.GetBoneTransform(fingers[i]);
                if (bone != null) bone.localRotation = Quaternion.Slerp(bone.localRotation, pose.Fingers[i], weight);
            }
        }

        public static float SolveArm(Transform upper, Transform lower, Transform hand, Vector3 target, Vector3 hint)
        {
            Vector3 start = upper.position;
            float a = Vector3.Distance(start, lower.position), b = Vector3.Distance(lower.position, hand.position);
            Vector3 delta = target - start;
            if (delta.sqrMagnitude < .000001f || a < .0001f || b < .0001f) return delta.magnitude;
            float distance = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            Vector3 direction = delta.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(hint - start, direction).normalized;
            if (bend.sqrMagnitude < .001f) bend = Vector3.ProjectOnPlane(Vector3.down, direction).normalized;
            if (bend.sqrMagnitude < .001f) bend = Vector3.ProjectOnPlane(Vector3.right, direction).normalized;
            float along = (a * a - b * b + distance * distance) / (2 * distance);
            Vector3 elbow = start + direction * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(lower.position - start, elbow - start) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(hand.position - lower.position, start + direction * distance - lower.position) * lower.rotation;
            return Vector3.Distance(hand.position, target);
        }
    }
}
