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
        [Tooltip("갑판 상면의 월드 y — 건축물·몬스터 착지·조준 평면이 전부 이 높이를 밟는 면으로 본다. "
            + "씬에서 열차를 올리거나 내리면 반드시 함께 맞춘다. QA 높이 토글(F2)은 "
            + "이 값을 고치지 않고 런타임 오프셋만 얹는다.")]
        [SerializeField, Min(1f)] private float _deckHeight = 3f;

        [Tooltip("칸 몸통의 세로 크기(m) — 바닥에서 갑판 상면까지. 증설·재결합 프리뷰 상자의 높이다. "
            + "열차가 지면에 붙어 있지 않을 수 있으므로 갑판 높이와 별개 값이다.")]
        [SerializeField, Min(1f)] private float _carBodyHeight = 3f;

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

        // ── QA 높이 토글 (열차 높이 스펙 — docs/specs/world/train-elevation.md) ──────────
        // 직렬화하지 않는다: 플레이 세션에서만 살고 에셋 파일을 더럽히지 않는다.
        // 씬 표현은 TrainElevationFollower가, 규칙은 이 오프셋이 얹힌 아래 프로퍼티들이 따라간다.
        [System.NonSerialized] private float _elevationOffset;

        public int CarCount => _carCount;

        public float CarLength => _carLength;

        public float CarWidth => _carWidth;

        /// <summary>
        /// 갑판 상면의 월드 y — 밟는 면·설치 면의 단일 기준.
        /// QA 높이 오프셋(<see cref="ElevationOffset"/>)이 반영된 <b>지금</b>의 값이다.
        /// </summary>
        public float DeckHeight => _deckHeight + _elevationOffset;

        /// <summary>씬에 굳어 있는 기준 갑판 높이 — 오프셋 0단계의 값(에셋에 적힌 그대로).</summary>
        public float BaseDeckHeight => _deckHeight;

        /// <summary>지금 적용 중인 QA 높이 오프셋(m) — 0이 기준, 음수가 내려간 상태다.</summary>
        public float ElevationOffset => _elevationOffset;

        /// <summary>
        /// QA 높이 오프셋을 갈아 끼운다 — <see cref="TrainElevationController"/> 전용이다.
        /// 열차 표현과 <b>같은 오프셋</b>이 들어와야 갑판 판정이 실제 갑판면과 맞는다.
        /// </summary>
        public void SetElevationOffset(float offset)
        {
            _elevationOffset = offset;
        }

        /// <summary>칸 몸통의 세로 크기 (m) — 프리뷰 상자 높이. 갑판 높이와 달리 열차를 올려도 변하지 않는다.</summary>
        public float CarBodyHeight => _carBodyHeight;

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

        /// <summary>
        /// 열차 하부 즉사 존의 높이 상한 (M5 6차). 0 = 비활성.
        /// 존은 <b>바퀴 밑 공간</b>을 뜻하므로 열차가 내려가면 같이 내려온다 — 안 내리면
        /// 낮아진 갑판 밑이 필요 이상으로 넓게 즉사 판정을 받는다. 오프셋이 커도 0 밑으로는 안 간다.
        /// </summary>
        public float WheelKillHeight =>
            _wheelKillHeight <= 0f ? 0f : Mathf.Max(0f, _wheelKillHeight + _elevationOffset);

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
        public Vector3 RespawnPosition => new Vector3(0f, DeckHeight + 1f, RearZ + _carLength * 0.5f);

        /// <summary>
        /// 스폰이 올라서는 편성 인덱스 — 0은 기관차다. 기관차는 걸어 다닐 지붕이 아니라 차체가
        /// 통째로 선 형상이라 그 위에 세우면 플레이어가 모델 안에 묻힌다 (M8 아트 패스).
        /// 그래서 첫 화차 갑판에 세운다.
        /// </summary>
        private const int SpawnCarIndex = 1;

        /// <summary>접속 순서별 초기 스폰 지점 — 첫 화차 갑판에서 뒤로 2 m 간격 나열.</summary>
        public Vector3 GetSpawnPosition(int playerIndex)
        {
            return new Vector3(0f, DeckHeight + 1f, CarCenterZ(SpawnCarIndex) - playerIndex * 2f);
        }
    }
}
