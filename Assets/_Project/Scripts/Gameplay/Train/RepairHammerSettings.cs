using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 수리 망치 밸런스 데이터 (기획서 §9 — 수리 망치로 수리, 좌클릭 홀드/연타의 간단한 조작).
    /// 자원 소모 여부는 미결(기획서 §9.1)이라 M3에서는 무소모다.
    /// </summary>
    [CreateAssetMenu(fileName = "RepairHammerSettings", menuName = "Game/Repair Hammer Settings")]
    public sealed class RepairHammerSettings : ScriptableObject
    {
        [Header("수리")]
        [Tooltip("타격 1회당 회복량.")]
        [SerializeField, Min(1f)] private float _repairPerHit = 10f;

        [Tooltip("타격 간격(초) — 좌클릭 홀드 시 이 간격으로 반복 타격한다.")]
        [SerializeField, Min(0.05f)] private float _hitInterval = 0.5f;

        [Header("철거 (건축 개편 2차 — 결정 ④)")]
        [Tooltip("X 홀드 철거 성립 시간(초) — 짧은 홀드 + 게이지로 전투 중 오철거를 막는다.")]
        [SerializeField, Min(0.1f)] private float _demolishHoldSeconds = 0.5f;

        [Header("설치 프리뷰 (건축 개편 1·3차)")]
        [Tooltip("건축물·판자 프리뷰 상자의 높이(m). 표시용이자 '자리에 사람·몬스터가 있는가' 판정 상자의 " +
            "높이이기도 하므로 설치 성립을 좌우한다 — 소유자 프리뷰와 호스트 재검증이 같은 값을 써야 해서 " +
            "프리팹이 아니라 공유 에셋이 든다.")]
        [SerializeField, Min(0.2f)] private float _ghostHeight = 1.2f;

        [Header("사거리")]
        [Tooltip("수리 가능 거리(m) — 근접 도구이므로 짧게 잡는다.")]
        [SerializeField, Min(0.5f)] private float _maxRange = 4f;

        [Tooltip("호스트 거리 재검증 여유(m) — 이동·지연으로 어긋난 정상 보고를 기각하지 않기 위한 오차 허용.")]
        [SerializeField, Min(0f)] private float _rangeTolerance = 1.5f;

        public float RepairPerHit => _repairPerHit;

        public float HitInterval => _hitInterval;

        /// <summary>X 홀드 철거 성립 시간(초) — 건축 개편 2차 결정 ④.</summary>
        public float DemolishHoldSeconds => _demolishHoldSeconds;

        /// <summary>설치 프리뷰·자리 점유 판정 상자의 높이(m) — 건축 개편 1·3차.</summary>
        public float GhostHeight => _ghostHeight;

        public float MaxRange => _maxRange;

        public float RangeTolerance => _rangeTolerance;
    }
}
