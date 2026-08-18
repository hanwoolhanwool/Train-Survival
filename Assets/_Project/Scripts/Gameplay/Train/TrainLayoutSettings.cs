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

        [Header("건축 그리드 (건축 개편 1차 — 결정 ①)")]
        [Tooltip("건축 그리드 정사각 셀 한 변(m) — 1.0이면 폭 4.6 m 칸에서 4열이 나온다.")]
        [SerializeField, Min(0.25f)] private float _structureCellSize = 1f;

        [Tooltip("칸 앞뒤 끝에서 갑판·건축 그리드가 빠지는 셀 행 수 (건축 개편 §7.2) — 1이면 "
            + "첫 행·마지막 행이 빠져 길이 15 m 칸이 13행이 된다. 칸 콜라이더도 같은 범위로 맞춘다.")]
        [SerializeField, Min(0)] private int _deckEdgeRows = 1;

        [Header("열차 하부 즉사 존 (M5 6차)")]
        [Tooltip("이 높이(y) 이하 + 열차 발자국 안이면 견인·파지·기절 몬스터가 즉사한다. 0 = 존 비활성.")]
        [SerializeField, Min(0f)] private float _wheelKillHeight = 1.2f;

        [Header("낙하·이탈 규칙 (§4.2)")]
        [SerializeField, Min(1f)] private float _fallBehindWarningMeters = 30f;
        [SerializeField, Min(1f)] private float _fallBehindDeathMeters = 40f;

        public int CarCount => _carCount;

        public float CarLength => _carLength;

        public float CarWidth => _carWidth;

        public float DeckHeight => _deckHeight;

        public float CouplingGap => _couplingGap;

        /// <summary>건축 그리드 정사각 셀 한 변(m) — 건축 개편 1차 결정 ①.</summary>
        public float StructureCellSize => _structureCellSize;

        /// <summary>
        /// 밟을 수 있는 갑판의 Z 길이 (m) — 칸 길이에서 앞뒤 제외 행을 뺀 값 (건축 개편 §7.2).
        /// <b>칸 콜라이더·건축 그리드·갑판 판정이 전부 이 길이를 쓴다.</b> 칸 간격·이탈 계산은
        /// <see cref="CarLength"/> 그대로다 — 편성 좌표는 바뀌지 않고 밟는 면만 좁아진다.
        /// </summary>
        public float DeckLength =>
            Mathf.Max(_structureCellSize, _carLength - 2f * _deckEdgeRows * _structureCellSize);

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

        /// <summary>
        /// Z가 해당 칸의 <b>귀속</b> 범위 안인가 (칸 길이 기준) — 이탈 오프셋을 반영한다.
        /// "어느 칸 위인가"를 묻는 판정용이다. 밟을 수 있는 면은 <see cref="IsZOnDeck"/>가 더 좁게 본다.
        /// </summary>
        public bool IsZOnCar(float z, int index, float ejectOffset)
        {
            return TrainLayoutMath.IsZOnCar(z, index, FrontZ, _carLength, _couplingGap, ejectOffset);
        }

        /// <summary>
        /// Z가 그 칸의 <b>밟을 수 있는 갑판</b> 범위 안인가 (건축 개편 §7.2) — 앞뒤 끝 행은 콜라이더가
        /// 없어 제외된다. 칸 귀속(<see cref="IsZOnCar"/>)과 달리 물건 안착·낙하 판정이 쓴다.
        /// </summary>
        public bool IsZOnDeck(float z, float carCenterZ)
        {
            return TrainLayoutMath.IsWithinDeckSpan(z, carCenterZ, DeckLength);
        }

        public float RearZ => -TotalLength * 0.5f;

        /// <summary>후미 기준 경고 시작 Z (§4.2 — 30 m부터 화면 경고).</summary>
        public float WarningZ => RearZ - _fallBehindWarningMeters;

        /// <summary>후미 기준 사망 확정 Z (§4.2 — 40 m 이상 뒤처지면 사망).</summary>
        public float DeathZ => RearZ - _fallBehindDeathMeters;

        /// <summary>부활 지점 — 후미 칸 지붕 (§4.2).</summary>
        public Vector3 RespawnPosition => new Vector3(0f, _deckHeight + 1f, RearZ + _carLength * 0.5f);

        /// <summary>
        /// 스폰이 올라서는 편성 인덱스 — 0은 기관차다. 기관차는 걸어 다닐 지붕이 아니라 차체가
        /// 통째로 선 형상이라 그 위에 세우면 플레이어가 모델 안에 묻힌다 (M8 아트 패스).
        /// 그래서 첫 화차 갑판에 세운다.
        /// </summary>
        private const int SpawnCarIndex = 1;

        /// <summary>접속 순서별 초기 스폰 지점 — 첫 화차 갑판에서 뒤로 2 m 간격 나열.</summary>
        public Vector3 GetSpawnPosition(int playerIndex)
        {
            return new Vector3(0f, _deckHeight + 1f, CarCenterZ(SpawnCarIndex) - playerIndex * 2f);
        }
    }
}
