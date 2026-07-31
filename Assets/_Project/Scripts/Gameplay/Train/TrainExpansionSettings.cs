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

        [Header("건설 비용 (자원 개수)")]
        [Tooltip("확장 칸 1칸 건설(재건·증설 공통)에 드는 자원 수 — 증설 포트에서 개인 인벤토리 총량으로 지불한다.")]
        [SerializeField, Min(0)] private int _carBuildCost = 5;

        [Tooltip("칸 위 건축물(온실 돔 등) 1개 설치에 드는 자원 수 — 수리 망치 우클릭으로 지불한다.")]
        [SerializeField, Min(0)] private int _structureBuildCost = 3;

        [Tooltip("이탈 칸 1칸 재결합에 드는 자원 수 — 신규 건설보다 싸게 잡아 힘들여 끌어오는 선택에 " +
            "경제적 유인을 준다(손잡이-이탈저항 스펙 §4.1).")]
        [SerializeField, Min(0)] private int _recoupleCost = 2;

        public int MaxCarCount => _maxCarCount;

        public int CarBuildCost => _carBuildCost;

        public int StructureBuildCost => _structureBuildCost;

        public int RecoupleCost => _recoupleCost;
    }
}
