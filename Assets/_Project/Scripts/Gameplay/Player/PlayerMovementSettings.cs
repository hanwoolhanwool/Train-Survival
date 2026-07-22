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
    }
}
