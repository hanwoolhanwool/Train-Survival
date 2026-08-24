using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 거치 무기(거치 기관총·자동 터렛) 한 종류의 정의 (M7 4차 §2.1).
    /// <b>이 에셋이 물려 있는 종류가 곧 거치 무기다</b> — <see cref="StructureCatalog.Entry.ProvidesHeat"/>·
    /// 저장 블록 플래그가 그랬듯 참조 유무가 곧 판정이고, 코드는 종류 이름을 하드코딩하지 않는다(OCP).
    /// 세 번째 거치 무기가 생기면 에셋만 추가하면 된다.
    /// 사격 데이터는 <see cref="GunSettings"/>를 그대로 재사용한다 — 피해·사거리·연사·탄창·탄종·연출이
    /// 개인 화기 3종과 같은 축을 쓴다 (§2.4 — 파이프라인 이식이지 코드 복사가 아니다).
    /// </summary>
    [CreateAssetMenu(fileName = "MountedWeaponSettings", menuName = "Game/Mounted Weapon Settings")]
    public sealed class MountedWeaponSettings : ScriptableObject
    {
        [Header("사격 (GunSettings 재사용)")]
        [Tooltip("피해·사거리·연사·탄창 용량·탄종·연출 — 거치 무기 전용 에셋을 물린다. " +
            "비어 있으면 사격이 성립하지 않는다(설치는 되지만 쏘지 못한다).")]
        [SerializeField] private GunSettings _gun;

        [Tooltip("HUD 탄약 줄 표시명 — 비면 GunSettings의 표시명을 쓴다.")]
        [SerializeField] private string _displayName;

        [Header("점유 (A단계 — 사람이 붙어서 쓴다)")]
        [Tooltip("사람이 붙는 무기인가 — 끄면 자동 터렛(B단계, 서버 구동)이라 점유 자체가 없다.")]
        [SerializeField] private bool _manned = true;

        [Tooltip("붙을 수 있는 좌석 반경(m) — 서버가 점유 승인 시 건축물 점유 영역 중심 기준으로 재검증한다.")]
        [SerializeField, Min(0.5f)] private float _seatRadius = 2.5f;

        [Tooltip("붙기 전 '쳐다봤다'고 볼 시선 정렬 하한 — 제작대·화구와 같은 상호작용 규약.")]
        [SerializeField, Range(0f, 1f)] private float _lookDotThreshold = 0.6f;

        [Tooltip("좌석 위치 오프셋(m, 건축물 로컬) — 프리팹에 좌석 앵커가 없을 때의 폴백. " +
            "권위 판정은 이 값을 쓰지 않는다(중심 기준 반경) — 표현 전용이다.")]
        [SerializeField] private Vector3 _seatLocalOffset = new Vector3(0f, 1.35f, -0.7f);

        [Header("사각 — 아군 오사와 '포신이 칸을 뚫는' 그림을 데이터로 막는다 (§2.3)")]
        [Tooltip("건축물 정면 기준 좌우 회전 한계(도). 열차 안쪽으로는 돌아가지 않는다.")]
        [SerializeField, Range(1f, 180f)] private float _yawLimit = 110f;

        [Tooltip("내려다보기 한계(도, 음수).")]
        [SerializeField, Range(-89f, 0f)] private float _pitchMin = -15f;

        [Tooltip("올려다보기 한계(도).")]
        [SerializeField, Range(0f, 89f)] private float _pitchMax = 40f;

        [Header("자동 터렛 (B단계 — 조작자만 AI로 바뀐다)")]
        [Tooltip("대상 탐색 반경(m) — 사거리(GunSettings.MaxRange)와 별개다. 사각·시야 제한은 사람과 같다.")]
        [SerializeField, Min(1f)] private float _searchRadius = 35f;

        [Tooltip("동시에 사격하는 자동 터렛 수 상한 — 밤 웨이브와 겹칠 때의 RPC·프레임 방어선.")]
        [SerializeField, Min(1)] private int _maxActiveTurrets = 4;

        /// <summary>사격 데이터 — 없으면 발사·재장전이 전부 기각된다.</summary>
        public GunSettings Gun => _gun;

        /// <summary>HUD 표시명 — 자체 값이 비면 사격 데이터의 표시명으로 물러선다.</summary>
        public string DisplayName => !string.IsNullOrEmpty(_displayName)
            ? _displayName
            : (_gun != null ? _gun.DisplayName : "거치 무기");

        /// <summary>사람이 붙는 무기인가 — false면 자동 터렛(점유 없음, 서버 구동).</summary>
        public bool Manned => _manned;

        public float SeatRadius => _seatRadius;

        /// <summary>좌석 반경의 제곱 — 승인 판정이 제곱 거리로 비교한다(√ 없음).</summary>
        public float SeatRadiusSqr => _seatRadius * _seatRadius;

        public float LookDotThreshold => _lookDotThreshold;

        /// <summary>좌석 오프셋 (건축물 로컬, 표현 전용).</summary>
        public Vector3 SeatLocalOffset => _seatLocalOffset;

        public float YawLimit => _yawLimit;

        public float PitchMin => _pitchMin;

        public float PitchMax => _pitchMax;

        /// <summary>자동 대상 탐색 반경(m) — B단계.</summary>
        public float SearchRadius => _searchRadius;

        /// <summary>동시 활성 터렛 상한 — B단계.</summary>
        public int MaxActiveTurrets => _maxActiveTurrets;

        /// <summary>탄창 용량 — 사격 데이터가 없으면 0(= 장전 불가).</summary>
        public int MagazineCapacity => _gun != null ? _gun.MagazineCapacity : 0;
    }
}
