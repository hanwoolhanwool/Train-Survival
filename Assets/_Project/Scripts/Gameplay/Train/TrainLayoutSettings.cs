using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 규모 데이터 (슬라이스 스펙 §5) — 레벨 디자인 기준 단위.
    /// 열차는 원점 고정: 중심 z=0, 전방 +Z. 칸 크기는 이후 건축 그리드·증설 프리팹의 기준이 된다.
    /// </summary>
    [CreateAssetMenu(fileName = "TrainLayoutSettings", menuName = "Game/Train Layout Settings")]
    public sealed class TrainLayoutSettings : ScriptableObject
    {
        [Header("칸 규격 (§5)")]
        [SerializeField, Min(1)] private int _carCount = 3;
        [SerializeField, Min(1f)] private float _carLength = 12f;
        [SerializeField, Min(1f)] private float _carWidth = 3f;
        [SerializeField, Min(1f)] private float _deckHeight = 3f;
        [SerializeField, Min(0f)] private float _couplingGap = 1.5f;

        [Header("열차 하부 즉사 존 (M5 6차)")]
        [Tooltip("이 높이(y) 이하 + 열차 발자국 안이면 견인·파지·기절 몬스터가 즉사한다. 0 = 존 비활성.")]
        [SerializeField, Min(0f)] private float _wheelKillHeight = 1.2f;

        [Header("낙하·이탈 규칙 (§4.2)")]
        [SerializeField, Min(1f)] private float _fallBehindWarningMeters = 30f;
        [SerializeField, Min(1f)] private float _fallBehindDeathMeters = 40f;
        [SerializeField, Min(0f)] private float _respawnDelaySeconds = 5f;

        public int CarCount => _carCount;

        public float CarLength => _carLength;

        public float CarWidth => _carWidth;

        public float DeckHeight => _deckHeight;

        public float CouplingGap => _couplingGap;

        /// <summary>열차 하부 즉사 존의 높이 상한 (M5 6차). 0 = 비활성.</summary>
        public float WheelKillHeight => _wheelKillHeight;

        /// <summary>연결부 포함 총 길이 (기관차 + 2칸 기본 구성 ≈ 39 m). 증설 칸은 여기 안 들어간다 — 초기 편성 기준.</summary>
        public float TotalLength => _carCount * _carLength + (_carCount - 1) * _couplingGap;

        public float FrontZ => TotalLength * 0.5f;

        /// <summary>
        /// 편성 인덱스 칸의 중심 Z — 선두(FrontZ)는 고정이고 열차는 후미로만 자라므로 증설 칸(초기 편성 밖 인덱스)에도 유효하다.
        /// </summary>
        public float CarCenterZ(int index)
        {
            return TrainLayoutMath.GetCarCenterZ(index, FrontZ, _carLength, _couplingGap);
        }

        /// <summary>이탈 오프셋만큼 뒤로 밀린 칸의 실제 중심 Z — 오프셋 0이면 슬롯 중심과 같다.</summary>
        public float CarCenterZ(int index, float ejectOffset)
        {
            return TrainLayoutMath.GetCarCenterZ(index, FrontZ, _carLength, _couplingGap, ejectOffset);
        }

        /// <summary>Z가 해당 칸의 갑판 범위 안인가 — 이탈 오프셋을 반영한다.</summary>
        public bool IsZOnCar(float z, int index, float ejectOffset)
        {
            return TrainLayoutMath.IsZOnCar(z, index, FrontZ, _carLength, _couplingGap, ejectOffset);
        }

        public float RearZ => -TotalLength * 0.5f;

        /// <summary>후미 기준 경고 시작 Z (§4.2 — 30 m부터 화면 경고).</summary>
        public float WarningZ => RearZ - _fallBehindWarningMeters;

        /// <summary>후미 기준 사망 확정 Z (§4.2 — 40 m 이상 뒤처지면 사망).</summary>
        public float DeathZ => RearZ - _fallBehindDeathMeters;

        public float RespawnDelaySeconds => _respawnDelaySeconds;

        /// <summary>부활 지점 — 후미 칸 지붕 (§4.2).</summary>
        public Vector3 RespawnPosition => new Vector3(0f, _deckHeight + 1f, RearZ + _carLength * 0.5f);

        /// <summary>접속 순서별 초기 스폰 지점 — 기관차 지붕에서 뒤로 나열.</summary>
        public Vector3 GetSpawnPosition(int playerIndex)
        {
            return new Vector3(0f, _deckHeight + 1f, FrontZ - _carLength * 0.5f - playerIndex * 2f);
        }
    }
}
