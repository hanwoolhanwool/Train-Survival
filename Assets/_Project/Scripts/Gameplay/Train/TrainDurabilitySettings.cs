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

        /// <summary>연결부 최대 체력.</summary>
        public float CouplingMaxHealth => _couplingMaxHealth;

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
