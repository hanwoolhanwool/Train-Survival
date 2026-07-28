using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 연료 밸런스 데이터 (개발 가이드 M2 — 연료 소모→감속).
    /// 고갈 시 정지가 아니라 최저 속도 유지 — "멈추면 죽는다"는 감속 위기 연출이지 즉사가 아니다 (기획서 §2).
    /// </summary>
    [CreateAssetMenu(fileName = "FuelSettings", menuName = "Game/Fuel Settings")]
    public sealed class FuelSettings : ScriptableObject
    {
        [Header("저장량")]
        [SerializeField, Min(1f)] private float _capacity = 100f;
        [SerializeField, Min(0f)] private float _initialFuel = 60f;

        [Header("소모·충전")]
        [Tooltip("기관차 기본 소모율(초당) — 끌고 있는 칸 수와 무관한 고정분.")]
        [SerializeField, Min(0f)] private float _consumptionPerSecond = 0.5f;

        [Tooltip("연결된 화물칸 1칸당 추가 소모율(초당) — 칸 증설 트레이드오프 (기획서 §7.1).")]
        [SerializeField, Min(0f)] private float _consumptionPerCar = 0.15f;

        [SerializeField, Min(0f)] private float _fuelPerResource = 6f;

        [Header("감속")]
        [SerializeField, Range(0f, 1f)] private float _depletedSpeedRatio = 0.3f;
        [SerializeField, Min(0.01f)] private float _speedChangeRate = 1.5f;

        public float Capacity => _capacity;

        public float InitialFuel => _initialFuel;

        public float ConsumptionPerSecond => _consumptionPerSecond;

        /// <summary>연결된 화물칸 1칸당 추가 소모율(초당) — 칸 증설 트레이드오프 (기획서 §7.1).</summary>
        public float ConsumptionPerCar => _consumptionPerCar;

        /// <summary>자원 1개를 엔진에 투입할 때 충전되는 연료량 (기획서 §3.4).</summary>
        public float FuelPerResource => _fuelPerResource;

        /// <summary>연료 고갈 시 기본 스크롤 속도 대비 유지 비율.</summary>
        public float DepletedSpeedRatio => _depletedSpeedRatio;

        /// <summary>목표 속도로 수렴하는 가감속률 (m/s²) — 감속/재가속이 급변하지 않게 한다.</summary>
        public float SpeedChangeRate => _speedChangeRate;
    }
}
