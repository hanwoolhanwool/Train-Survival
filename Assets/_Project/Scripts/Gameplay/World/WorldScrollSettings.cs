using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 월드 스크롤·지형 타일 스트리밍 밸런스 데이터.
    /// 기본 열차 속도 6 m/s는 슬라이스 스펙 §5 (달리기 7 m/s보다 낮게 — 복귀 규칙 성립 조건).
    /// </summary>
    [CreateAssetMenu(fileName = "WorldScrollSettings", menuName = "Game/World Scroll Settings")]
    public sealed class WorldScrollSettings : ScriptableObject
    {
        [Header("스크롤")]
        [SerializeField, Min(0f)] private float _baseScrollSpeed = 6f;
        [SerializeField, Min(0.1f)] private float _correctionRate = 5f;

        [Header("지형 타일 스트리밍")]
        [SerializeField, Min(1f)] private float _tileLength = 40f;
        [SerializeField, Min(1)] private int _tilesAhead = 5;
        [SerializeField, Min(1)] private int _tilesBehind = 3;

        public float BaseScrollSpeed => _baseScrollSpeed;

        /// <summary>클라이언트 표시 거리의 오차 수렴 속도 (지수 감쇠 계수).</summary>
        public float CorrectionRate => _correctionRate;

        public float TileLength => _tileLength;

        public int TilesAhead => _tilesAhead;

        public int TilesBehind => _tilesBehind;
    }
}
