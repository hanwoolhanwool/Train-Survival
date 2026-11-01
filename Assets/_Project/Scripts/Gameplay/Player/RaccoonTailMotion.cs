using System;
using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>Local cosmetic motion for the raccoon's five extra tail bones. Does not drive locomotion.</summary>
    [DefaultExecutionOrder(90)]
    [RequireComponent(typeof(Animator))]
    public sealed class RaccoonTailMotion : MonoBehaviour
    {
        [SerializeField] private Transform[] _bones = Array.Empty<Transform>();
        [SerializeField] private Quaternion[] _rest = Array.Empty<Quaternion>();
        [SerializeField] private Vector3[] _pitchAxes = Array.Empty<Vector3>();
        [SerializeField] private Vector3[] _yawAxes = Array.Empty<Vector3>();
        private readonly float[] _pitch = new float[5], _pitchVelocity = new float[5];
        private readonly float[] _yaw = new float[5], _yawVelocity = new float[5];
        private Animator _animator;
        private RaccoonEquipmentProfile _profile;
        private float _phase, _speed, _previousHeading;
        private bool _hasHeading, _hasSpeedParameter, _hasDeadParameter;
        private static readonly int SpeedId = Animator.StringToHash("Speed");
        private static readonly int DeadId = Animator.StringToHash("Dead");

        public void Bind(Animator animator)
        {
            _animator = animator;
            _profile=GetComponent<RaccoonEquipmentPose>()?.Profile;
            _bones = new Transform[5]; _rest = new Quaternion[5];
            _pitchAxes = new Vector3[5]; _yawAxes = new Vector3[5];
            Transform[] children = animator.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < 5; i++)
            {
                string boneName = "Tail" + (i + 1).ToString("00");
                foreach (Transform child in children)
                    if (child.name == boneName) { _bones[i] = child; break; }
                if (_bones[i] == null) continue;
                _rest[i] = _bones[i].localRotation;
                _pitchAxes[i] = _bones[i].InverseTransformDirection(animator.transform.right).normalized;
                _yawAxes[i] = _bones[i].InverseTransformDirection(animator.transform.up).normalized;
            }
            CacheParameters(); ResetMotion();
        }

        private void Awake()
        {
            _animator = GetComponent<Animator>();
            _profile=GetComponent<RaccoonEquipmentPose>()?.Profile;
            if (_bones.Length != 5 || _rest.Length != 5) Bind(_animator);
            else CacheParameters();
        }

        private void CacheParameters()
        {
            _hasSpeedParameter = _hasDeadParameter = false;
            if (_animator == null || _animator.runtimeAnimatorController == null) return;
            foreach (AnimatorControllerParameter parameter in _animator.parameters)
            {
                _hasSpeedParameter |= parameter.nameHash == SpeedId && parameter.type == AnimatorControllerParameterType.Float;
                _hasDeadParameter |= parameter.nameHash == DeadId && parameter.type == AnimatorControllerParameterType.Bool;
            }
        }

        private void OnEnable() => ResetMotion();
        private void OnDisable() => ResetMotion();

        public void ResetMotion()
        {
            _phase = _speed = 0; _hasHeading = false;
            Array.Clear(_pitch, 0, 5); Array.Clear(_yaw, 0, 5);
            Array.Clear(_pitchVelocity, 0, 5); Array.Clear(_yawVelocity, 0, 5);
            for (int i = 0; i < Mathf.Min(_bones.Length, _rest.Length); i++)
                if (_bones[i] != null) _bones[i].localRotation = _rest[i];
        }

        private void LateUpdate()
        {
            if (_animator == null || !_animator.isActiveAndEnabled || Time.deltaTime <= 0) return;
            // Speed is already corrected for world scrolling by PlayerAnimationDriver.
            float heading = transform.eulerAngles.y;
            float delta = _hasHeading ? Mathf.DeltaAngle(_previousHeading, heading) : 0;
            _previousHeading = heading; _hasHeading = true;
            // Camera snaps, respawns and long stalls should not whip the tail.
            float turnRate = Mathf.Abs(delta) > 80 || Time.deltaTime > .2f ? 0 : delta / Time.deltaTime;
            Step(_hasSpeedParameter ? _animator.GetFloat(SpeedId) : 0, turnRate, Time.deltaTime,
                !_hasDeadParameter || !_animator.GetBool(DeadId));
        }

        /// <summary>Same bounded, time-based motion is used by gameplay, review and tests.</summary>
        public void Step(float metresPerSecond, float yawDegreesPerSecond, float deltaTime, bool alive = true)
        {
            if (_bones.Length != 5 || !float.IsFinite(deltaTime) || deltaTime <= 0) return;
            if (!alive) { ResetMotion(); return; }
            if (!float.IsFinite(metresPerSecond) || !float.IsFinite(yawDegreesPerSecond)) { ResetMotion(); return; }
            float dt = Mathf.Min(deltaTime, .1f);
            float targetSpeed = Mathf.Clamp01(metresPerSecond / 6f);
            _speed = Mathf.Lerp(_speed, targetSpeed, 1 - Mathf.Exp(-dt * 7));
            _phase = (_phase + dt * Mathf.Lerp(1.6f, 7.5f, _speed)) % (Mathf.PI * 2);
            float runWeight=_profile!=null?_profile.AuthoredRunWeight(_animator):0;
            if(runWeight>0)
                _phase=Mathf.LerpAngle(_phase*Mathf.Rad2Deg,Mathf.Repeat(_animator.GetCurrentAnimatorStateInfo(0).normalizedTime,1)*360,runWeight)*Mathf.Deg2Rad;
            float turn = Mathf.Clamp(-yawDegreesPerSecond * .035f, -6f, 6f);
            for (int i = 0; i < 5; i++)
            {
                if (_bones[i] == null) continue;
                float gain = 1f / (1 + i * .45f);
                // A supported root and a gently hanging tip give the thick tail weight.
                // Use one coherent sway with a short lag, rather than a wave curling each joint.
                float restingBend=i==0?10+3*_speed:i==1?0:-.5f-i*.75f;
                float lift = restingBend + Mathf.Sin(_phase*2-i*.35f)*(i==0?1.1f:.4f+i*.3f)*_speed;
                float amplitude=i==0?2.6f:1.0f+i*.4f;
                float wave = Mathf.Sin(_phase-i*.30f)*Mathf.Lerp(.25f,amplitude,_speed);
                float sway = turn * gain + wave;
                Spring(ref _pitch[i], ref _pitchVelocity[i], lift, 16 - i*2, dt);
                Spring(ref _yaw[i], ref _yawVelocity[i], sway, 15 - i*1.5f, dt);
                // These extra bones belong to this component; evaluate from bind pose, never accumulate rotations.
                _bones[i].localRotation = _rest[i]
                    * Quaternion.AngleAxis(_yaw[i], _yawAxes[i])
                    * Quaternion.AngleAxis(_pitch[i], _pitchAxes[i]);
            }
        }

        private static void Spring(ref float value, ref float velocity, float target, float frequency, float dt)
        {
            float offset = value - target;
            float term = velocity + frequency * offset;
            float decay = Mathf.Exp(-frequency * dt);
            value = target + (offset + term * dt) * decay;
            velocity = (velocity - frequency * term * dt) * decay;
        }
    }
}
