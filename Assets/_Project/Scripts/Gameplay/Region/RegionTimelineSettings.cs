using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 지역 순환 순서와 전환 규칙의 밸런스 데이터 (기획서 §4 — 숲 → 사막 → 대초원 → 북극, §4.5 재순환).
    /// M4 범위는 숲·사막 2종이며, 지역을 배열에 추가하는 것만으로 확장된다.
    /// </summary>
    [CreateAssetMenu(fileName = "RegionTimelineSettings", menuName = "Game/Region Timeline Settings")]
    public sealed class RegionTimelineSettings : ScriptableObject
    {
        [Tooltip("진행 순서대로 나열한 지역 정의. 배열 순서가 곧 전환 순서다.")]
        [SerializeField] private RegionDefinition[] _regions;

        [Tooltip("지역 마지막 며칠부터 다음 지역을 예고할지 (기획서 §2 — '마지막 1~2일'로 앞당김).")]
        [SerializeField, Min(0)] private int _forecastLeadDays = 2;

        [Tooltip("마지막 지역 뒤 첫 지역으로 순환할지 (기획서 §4.5 — 챌린지 순환).")]
        [SerializeField] private bool _loopAfterLastRegion = true;

        [Tooltip("순환 1바퀴마다 난이도 배율에 가산되는 값 (기획서 §4.5 — 재순환 시 난이도 배율 상승).")]
        [SerializeField, Min(0f)] private float _cycleDifficultyBonus = 0.5f;

        private int[] _dayCountsCache;

        public int RegionCount => _regions == null ? 0 : _regions.Length;

        public int ForecastLeadDays => _forecastLeadDays;

        public bool LoopAfterLastRegion => _loopAfterLastRegion;

        public float CycleDifficultyBonus => _cycleDifficultyBonus;

        /// <summary>인덱스의 지역 정의. 범위를 벗어나거나 비어 있으면 null.</summary>
        public RegionDefinition GetRegion(int index)
        {
            if (_regions == null || index < 0 || index >= _regions.Length)
            {
                return null;
            }

            return _regions[index];
        }

        /// <summary>
        /// 지역별 일수 배열 (<see cref="RegionTimelineMath.Evaluate"/> 입력).
        /// 매 프레임 평가에서 할당이 생기지 않도록 캐시하며, 에디터에서 값이 바뀌면 무효화된다.
        /// </summary>
        public int[] GetDayCounts()
        {
            if (_regions == null || _regions.Length == 0)
            {
                return System.Array.Empty<int>();
            }

            if (_dayCountsCache == null || _dayCountsCache.Length != _regions.Length)
            {
                _dayCountsCache = new int[_regions.Length];
            }

            for (int i = 0; i < _regions.Length; i++)
            {
                _dayCountsCache[i] = _regions[i] == null ? 1 : _regions[i].DayCount;
            }

            return _dayCountsCache;
        }

        private void OnValidate()
        {
            _dayCountsCache = null;
        }
    }
}
