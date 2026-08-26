using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>1인칭 플레이어 이동 밸런스 데이터 (슬라이스 스펙 §4.1 초기값).</summary>
    [CreateAssetMenu(fileName = "PlayerMovementSettings", menuName = "Game/Player Movement Settings")]
    public sealed class PlayerMovementSettings : ScriptableObject
    {
        [SerializeField, Min(0.1f)] private float _walkSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float _runSpeed = 7f;
        [SerializeField, Min(0.1f)] private float _jumpHeight = 1.2f;
        [SerializeField, Min(0f)] private float _groundGraceSeconds = 0.15f;
        [SerializeField, Range(0f, 1f)] private float _airControlRatio = 0.5f;
        [SerializeField, Min(1f)] private float _airAcceleration = 20f;
        [SerializeField, Min(1f)] private float _gravity = 20f;
        [SerializeField, Min(0.01f)] private float _lookSensitivity = 0.12f;
        [SerializeField, Range(10f, 89f)] private float _maxPitch = 85f;

        [Header("수영·잠수 (바다 지역)")]
        [Tooltip("물속 수평 속도 (m/s). 달리기보다 느려야 하므로 스크롤 6 m/s를 이기지 못한다 — " +
            "그래서 수면에서는 뒤로 밀리고, 잠수해야 앞으로 간다.")]
        [SerializeField, Min(0.1f)] private float _swimSpeed = 3.5f;

        [Tooltip("물속 상승·하강 속도 (m/s).")]
        [SerializeField, Min(0.1f)] private float _swimVerticalSpeed = 2f;

        [Tooltip("수직 입력이 없을 때 수면으로 떠오르는 속도 (m/s).")]
        [SerializeField, Min(0f)] private float _swimBuoyancySpeed = 0.6f;

        [Tooltip("발이 이만큼 잠기면 수영이 시작된다 (m). 대략 가슴 높이.")]
        [SerializeField, Min(0.1f)] private float _swimEnterDepth = 1f;

        [Tooltip("이보다 얕아지면 수영이 끝난다 (m). 진입값보다 작아야 경계에서 깜빡이지 않는다.")]
        [SerializeField, Min(0f)] private float _swimExitDepth = 0.2f;

        [Tooltip("이 깊이부터 물살이 약해지기 시작한다 (m). 대략 머리가 잠기는 지점.")]
        [SerializeField, Min(0f)] private float _swimDragStartDepth = 1.8f;

        [Tooltip("이 깊이에서 물살 감쇠가 최대가 된다 (m).")]
        [SerializeField, Min(0.1f)] private float _swimDragFullDepth = 3.5f;

        [Tooltip("완전히 잠겼을 때의 물살 배율. 0.4면 6 m/s 스크롤이 2.4 m/s가 되어 " +
            "수영 3.5 m/s가 순 +1.1 m/s로 앞선다 — 잠수가 성립하는 조건이다.")]
        [SerializeField, Range(0.05f, 1f)] private float _submergedScrollFactor = 0.4f;

        public float WalkSpeed => _walkSpeed;

        public float RunSpeed => _runSpeed;

        public float JumpHeight => _jumpHeight;

        /// <summary>
        /// 접지 유예 시간 (코요테 타임). 지형이 스트리밍 타일이라 이음새·회수 순간 isGrounded가 깜빡이는데,
        /// 이 유예 동안은 접지로 간주해 순간 공중 제어 전환(느려짐)·수직 튐을 막는다.
        /// </summary>
        public float GroundGraceSeconds => _groundGraceSeconds;

        /// <summary>공중에서 이동 입력의 유효 비율 (§4.1 — 50 %).</summary>
        public float AirControlRatio => _airControlRatio;

        public float AirAcceleration => _airAcceleration;

        public float Gravity => _gravity;

        public float LookSensitivity => _lookSensitivity;

        public float MaxPitch => _maxPitch;

        public float SwimSpeed => _swimSpeed;

        public float SwimVerticalSpeed => _swimVerticalSpeed;

        public float SwimBuoyancySpeed => _swimBuoyancySpeed;

        public float SwimEnterDepth => _swimEnterDepth;

        public float SwimExitDepth => _swimExitDepth;

        public float SwimDragStartDepth => _swimDragStartDepth;

        public float SwimDragFullDepth => _swimDragFullDepth;

        public float SubmergedScrollFactor => _submergedScrollFactor;
    }
}
