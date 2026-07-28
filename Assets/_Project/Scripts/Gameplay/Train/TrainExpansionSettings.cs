using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 증설 밸런스 데이터 (개발 가이드 §M3 — 칸 증설/연결, 기획서 §7.1).
    /// 편성 상한은 씬에 미리 배치된 예비 칸 슬롯 수와 일치해야 한다(슬롯 사전 확보 방식).
    /// </summary>
    [CreateAssetMenu(fileName = "TrainExpansionSettings", menuName = "Game/Train Expansion Settings")]
    public sealed class TrainExpansionSettings : ScriptableObject
    {
        [Header("편성 상한")]
        [Tooltip("기관차 포함 최대 칸 수 — 씬의 예비 칸 슬롯(Car_N·Connector_N·손잡이 앵커) 수와 맞춘다.")]
        [SerializeField, Min(1)] private int _maxCarCount = 5;

        [Header("증설 비용 (자원 개수)")]
        [Tooltip("온실칸 1칸 증설에 드는 자원 수 — 증설 포트에서 개인 인벤토리 총량으로 지불한다.")]
        [SerializeField, Min(0)] private int _greenhouseCarCost = 5;

        public int MaxCarCount => _maxCarCount;

        public int GreenhouseCarCost => _greenhouseCarCost;
    }
}
