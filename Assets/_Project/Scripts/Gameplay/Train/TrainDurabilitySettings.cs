using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 종류별 내구도 데이터 (개발 가이드 §M3 — 칸 단위 파괴). 수치 하드코딩 금지 원칙에 따라
    /// 밸런스 값은 SO로 분리한다. 기관차는 파괴 불가라 최대 체력을 노출하지 않는다(불변식은 로직이 강제).
    /// </summary>
    [CreateAssetMenu(fileName = "TrainDurabilitySettings", menuName = "Game/Train Durability Settings")]
    public sealed class TrainDurabilitySettings : ScriptableObject
    {
        [Header("칸 최대 체력 (§M3)")]
        [Tooltip("확장 칸 공통 최대 체력 — 칸의 개성은 종류가 아니라 칸 위 건축물이 만든다.")]
        [SerializeField, Min(1f)] private float _standardCarMaxHealth = 100f;

        [Header("연결부 (기획서 §9 — 밤 방어전 핵심 방어 목표)")]
        [Tooltip("칸보다 낮게 잡아 '연결부를 노리는' 공략이 성립하도록 한다.")]
        [SerializeField, Min(1f)] private float _couplingMaxHealth = 60f;

        [Header("이탈 이동·손잡이 저항 (손잡이-이탈저항 스펙 §4)")]
        [Tooltip("스크롤 속도에 더해 이탈 칸이 뒤로 밀려나는 기본 속도(m/s).")]
        [SerializeField, Min(0f)] private float _ejectExtraSpeed = 2f;

        [Tooltip("분리된 칸이 관성을 잃는 감속도(m/s²). 낮을수록 분리 직후 열차를 따라가다 천천히 뒤처진다.")]
        [SerializeField, Min(0.1f)] private float _ejectDeceleration = 4f;

        [Tooltip("손잡이 1인당 상쇄 속도(m/s). 후퇴 속도의 약 0.6~0.8배로 잡으면 '1인=지연, 2인=회수'가 성립.")]
        [SerializeField, Min(0f)] private float _pullPerGrabber = 6f;

        [Tooltip("아무도 안 잡은 채 이 거리(m) 넘게 멀어지면 칸 영구 소실.")]
        [SerializeField, Min(5f)] private float _lostDistance = 45f;

        [Tooltip("클라이언트 표시 — 재시뮬한 이탈 칸 표시 위치가 복제 값으로 수렴하는 드리프트 보정률(/s). " +
            "높을수록 복제에 즉각 붙지만 네트워크 틱 계단(탑승 시 월드 떨림)이 다시 드러난다.")]
        [SerializeField, Min(0f)] private float _ejectDisplayCorrectionRate = 3f;

        [Tooltip("저항 인원 변화 직후 이 시간(초) 동안만 보정률을 한시 상향한다 (M5 8차 — 7차 버그 5). " +
            "인원 복제 지연 동안 옛 저항으로 적분된 표시 드리프트를 빠르게 회수한다. 0 = 상향 없음.")]
        [SerializeField, Min(0f)] private float _ejectDisplayCorrectionBoostSeconds = 1.2f;

        [Tooltip("한시 상향 배율 — 보정률 × 이 값. 상시 상향은 틱 계단이 다시 보이므로 변화 직후만 쓴다.")]
        [SerializeField, Min(1f)] private float _ejectDisplayCorrectionBoostMultiplier = 5f;

        /// <summary>연결부 최대 체력.</summary>
        public float CouplingMaxHealth => _couplingMaxHealth;

        /// <summary>스크롤 위에 더해지는 기본 후퇴 속도(m/s).</summary>
        public float EjectExtraSpeed => _ejectExtraSpeed;

        /// <summary>분리된 칸이 관성을 잃는 감속도(m/s²) — 밀림 속도가 0에서 목표까지 오르는 기울기.</summary>
        public float EjectDeceleration => _ejectDeceleration;

        /// <summary>손잡이 1인당 상쇄 속도(m/s).</summary>
        public float PullPerGrabber => _pullPerGrabber;

        /// <summary>영구 소실 거리(m).</summary>
        public float LostDistance => _lostDistance;

        /// <summary>클라 이탈 칸 표시 재시뮬의 드리프트 보정률(/s).</summary>
        public float EjectDisplayCorrectionRate => _ejectDisplayCorrectionRate;

        /// <summary>저항 인원 변화 직후 보정률을 한시 상향하는 시간(초) — 0이면 상향 없음 (M5 8차).</summary>
        public float EjectDisplayCorrectionBoostSeconds => _ejectDisplayCorrectionBoostSeconds;

        /// <summary>한시 상향 배율 (M5 8차).</summary>
        public float EjectDisplayCorrectionBoostMultiplier => _ejectDisplayCorrectionBoostMultiplier;

        /// <summary>칸 종류의 최대 체력. 기관차는 파괴 불가이므로 양의 무한대를 돌려준다.</summary>
        public float MaxHealthFor(CarType type)
        {
            return type == CarType.Locomotive ? float.PositiveInfinity : _standardCarMaxHealth;
        }
    }
}
