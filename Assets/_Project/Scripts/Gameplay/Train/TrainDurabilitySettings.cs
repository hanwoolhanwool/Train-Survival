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
        [Header("칸 종류별 최대 체력 (§M3)")]
        [SerializeField, Min(1f)] private float _standardCarMaxHealth = 100f;
        [SerializeField, Min(1f)] private float _greenhouseCarMaxHealth = 80f;

        [Header("연결부 (기획서 §9 — 밤 방어전 핵심 방어 목표)")]
        [Tooltip("칸보다 낮게 잡아 '연결부를 노리는' 공략이 성립하도록 한다.")]
        [SerializeField, Min(1f)] private float _couplingMaxHealth = 60f;

        [Header("칸 위 건축물 (기획서 §9 — 개별 파괴 가능)")]
        [Tooltip("붙박이 건축물(온실 돔 등)의 최대 체력 — 칸보다 낮게 잡아 건축물부터 노출되는 위험을 만든다.")]
        [SerializeField, Min(1f)] private float _structureMaxHealth = 50f;

        [Header("이탈 이동·손잡이 저항 (손잡이-이탈저항 스펙 §4)")]
        [Tooltip("스크롤 속도에 더해 이탈 칸이 뒤로 밀려나는 기본 속도(m/s).")]
        [SerializeField, Min(0f)] private float _ejectExtraSpeed = 2f;

        [Tooltip("분리된 칸이 관성을 잃는 감속도(m/s²). 낮을수록 분리 직후 열차를 따라가다 천천히 뒤처진다.")]
        [SerializeField, Min(0.1f)] private float _ejectDeceleration = 4f;

        [Tooltip("손잡이 1인당 상쇄 속도(m/s). 후퇴 속도의 약 0.6~0.8배로 잡으면 '1인=지연, 2인=회수'가 성립.")]
        [SerializeField, Min(0f)] private float _pullPerGrabber = 6f;

        [Tooltip("아무도 안 잡은 채 이 거리(m) 넘게 멀어지면 칸 영구 소실.")]
        [SerializeField, Min(5f)] private float _lostDistance = 45f;

        /// <summary>연결부 최대 체력.</summary>
        public float CouplingMaxHealth => _couplingMaxHealth;

        /// <summary>칸 위 붙박이 건축물의 최대 체력.</summary>
        public float StructureMaxHealth => _structureMaxHealth;

        /// <summary>스크롤 위에 더해지는 기본 후퇴 속도(m/s).</summary>
        public float EjectExtraSpeed => _ejectExtraSpeed;

        /// <summary>분리된 칸이 관성을 잃는 감속도(m/s²) — 밀림 속도가 0에서 목표까지 오르는 기울기.</summary>
        public float EjectDeceleration => _ejectDeceleration;

        /// <summary>손잡이 1인당 상쇄 속도(m/s).</summary>
        public float PullPerGrabber => _pullPerGrabber;

        /// <summary>영구 소실 거리(m).</summary>
        public float LostDistance => _lostDistance;

        /// <summary>칸 종류의 최대 체력. 기관차는 파괴 불가이므로 양의 무한대를 돌려준다.</summary>
        public float MaxHealthFor(CarType type)
        {
            switch (type)
            {
                case CarType.Locomotive:
                    return float.PositiveInfinity;
                case CarType.Greenhouse:
                    return _greenhouseCarMaxHealth;
                default:
                    return _standardCarMaxHealth;
            }
        }
    }
}
