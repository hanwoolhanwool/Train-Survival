using UnityEngine;

namespace Game.Gameplay.Player
{
    /// <summary>
    /// 애니메이션 표현·4인 색 구분 밸런스 데이터 (M8 1차 §2.6 · 플레이어 확장 계획 §2.1~2.2).
    /// 판정에 쓰이는 값이 아니라 전부 표현 층위다.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerAnimationSettings", menuName = "Game/Player Animation Settings")]
    public sealed class PlayerAnimationSettings : ScriptableObject
    {
        [Header("속도 추정·스무딩")]
        [SerializeField, Min(0f)] private float _speedSmoothingHalfLifeSeconds = 0.15f;
        [SerializeField, Min(0f)] private float _runHysteresisBand = 0.3f;
        [SerializeField, Min(0f)] private float _walkEnterSpeed = 0.5f;
        [SerializeField, Min(0f)] private float _idleEnterSpeed = 0.2f;
        [SerializeField, Min(1f)] private float _teleportSpeedThreshold = 15f;

        [Header("점프")]
        [SerializeField, Min(0.1f)] private float _jumpDetectSpeed = 3f;
        [SerializeField, Min(1)] private int _maxJumpRpcPerSecond = 4;

        [Header("시점 공유 (PlayerAimView)")]
        [SerializeField, Min(0f)] private float _pitchReplicationThresholdDegrees = 1f;
        [SerializeField, Min(0f)] private float _pitchSmoothingHalfLifeSeconds = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _chestPitchWeight = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _headPitchWeight = 0.45f;

        [Header("4인 색 구분 (MaterialPropertyBlock)")]
        [SerializeField] private Color[] _playerColors =
        {
            new Color(1f, 1f, 1f),
            new Color(1f, 0.62f, 0.62f),
            new Color(0.62f, 0.78f, 1f),
            new Color(0.68f, 1f, 0.66f),
        };

        /// <summary>Speed 파라미터 지수 스무딩 반감기 (초) — 원격 위치 떨림 흡수 (확장 계획 §2.2).</summary>
        public float SpeedSmoothingHalfLifeSeconds => _speedSmoothingHalfLifeSeconds;

        /// <summary>Walk↔Run 경계 히스테리시스 폭 (±m/s).</summary>
        public float RunHysteresisBand => _runHysteresisBand;

        /// <summary>Idle → Walk 진입 속력 (m/s) — 미세 떨림이 걸음으로 읽히지 않는 하한.</summary>
        public float WalkEnterSpeed => _walkEnterSpeed;

        /// <summary>Walk → Idle 복귀 속력 (m/s) — 진입보다 낮게 두어 경계 진동을 막는다.</summary>
        public float IdleEnterSpeed => _idleEnterSpeed;

        /// <summary>이 속력 초과 델타는 텔레포트(부활·복원)로 간주해 버린다 (m/s).</summary>
        public float TeleportSpeedThreshold => _teleportSpeedThreshold;

        /// <summary>소유자 점프 감지 상승 속도 문턱 (m/s) — 계단 오르기 잔진동보다 높게.</summary>
        public float JumpDetectSpeed => _jumpDetectSpeed;

        /// <summary>서버가 중계하는 점프 연출 RPC의 초당 상한 (홍수 방지).</summary>
        public int MaxJumpRpcPerSecond => _maxJumpRpcPerSecond;

        /// <summary>피치 복제 기록 임계 (도) — 이보다 작은 변화는 기록하지 않는다 (대역폭 절약).</summary>
        public float PitchReplicationThresholdDegrees => _pitchReplicationThresholdDegrees;

        /// <summary>원격 피치 스무딩 반감기 (초) — 임계 계단이 보이지 않게 한다.</summary>
        public float PitchSmoothingHalfLifeSeconds => _pitchSmoothingHalfLifeSeconds;

        /// <summary>가슴 본에 얹는 피치 비율 — 머리와 합쳐 시선 방향이 읽히는 정도로.</summary>
        public float ChestPitchWeight => _chestPitchWeight;

        /// <summary>머리 본에 얹는 피치 비율.</summary>
        public float HeadPitchWeight => _headPitchWeight;

        /// <summary>스폰 순번 → 몸체 틴트 색 (순번 % 길이). 텍스처 위에 곱해진다.</summary>
        public Color GetPlayerColor(int visualSlot)
        {
            if (_playerColors == null || _playerColors.Length == 0)
            {
                return Color.white;
            }

            int index = Mathf.Abs(visualSlot) % _playerColors.Length;
            return _playerColors[index];
        }
    }
}
