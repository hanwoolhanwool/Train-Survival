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
        [SerializeField, Range(0f, 1f)] private float _airControlRatio = 0.5f;
        [SerializeField, Min(1f)] private float _airAcceleration = 20f;
        [SerializeField, Min(1f)] private float _gravity = 20f;
        [SerializeField, Min(0.01f)] private float _lookSensitivity = 0.12f;
        [SerializeField, Range(10f, 89f)] private float _maxPitch = 85f;

        public float WalkSpeed => _walkSpeed;

        public float RunSpeed => _runSpeed;

        public float JumpHeight => _jumpHeight;

        /// <summary>공중에서 이동 입력의 유효 비율 (§4.1 — 50 %).</summary>
        public float AirControlRatio => _airControlRatio;

        public float AirAcceleration => _airAcceleration;

        public float Gravity => _gravity;

        public float LookSensitivity => _lookSensitivity;

        public float MaxPitch => _maxPitch;
    }
}
