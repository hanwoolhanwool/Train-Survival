using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 날씨 이벤트 1종의 정의 (기획서 §7.4 — 모래폭풍·폭설·뇌우·혹한파).
    /// M4 범위는 모래폭풍 1종이며, 지역 데이터의 날씨 목록에 추가하는 것만으로 확장된다.
    /// 날씨는 지역 정체성을 강화하는 요소이므로 지역 도메인에 함께 둔다.
    /// </summary>
    [CreateAssetMenu(fileName = "WeatherDefinition", menuName = "Game/Weather Definition")]
    public sealed class WeatherDefinition : ScriptableObject
    {
        [Header("표시")]
        [SerializeField] private string _displayName = "모래폭풍";

        [Header("지속 시간 (초) — 발생 시 이 범위에서 무작위")]
        [SerializeField, Min(5f)] private float _minDurationSeconds = 45f;
        [SerializeField, Min(5f)] private float _maxDurationSeconds = 90f;

        [Header("영향")]
        [Tooltip("월드 스크롤 속도(= 체감 열차 속도)에 곱하는 배율. 1보다 작으면 감속한다.")]
        [SerializeField, Range(0.1f, 2f)] private float _scrollSpeedMultiplier = 0.65f;

        [Header("시야 차단 연출 (로컬 표현 — 각 피어가 스스로 적용)")]
        [SerializeField] private Color _fogColor = new Color(0.76f, 0.65f, 0.42f, 1f);
        [SerializeField, Min(0f)] private float _fogDensity = 0.035f;

        [Header("체온·집게 개입 (M7 3차 — 혹한파·폭설)")]
        [Tooltip("지역 환경 온도에 가산할 오프셋(℃). 음수 = 더 춥다(혹한파). 기본 0 = 개입 없음 — " +
            "기존 날씨 에셋이 무수정으로 통과한다.")]
        [SerializeField] private float _ambientTemperatureOffset;

        [Tooltip("집게 사거리에 곱하는 배율 (기획서 §7.4 — 폭설은 시야와 함께 사거리를 줄인다). " +
            "기본 1 = 개입 없음.")]
        [SerializeField, Range(0.1f, 1f)] private float _harpoonRangeMultiplier = 1f;

        public string DisplayName => _displayName;

        public float ScrollSpeedMultiplier => _scrollSpeedMultiplier;

        public Color FogColor => _fogColor;

        public float FogDensity => _fogDensity;

        /// <summary>환경 온도 가산 오프셋 (M7 3차 혹한파). 0 = 개입 없음.</summary>
        public float AmbientTemperatureOffset => _ambientTemperatureOffset;

        /// <summary>집게 사거리 배율 (M7 3차 폭설). 1 = 개입 없음.</summary>
        public float HarpoonRangeMultiplier => _harpoonRangeMultiplier;

        /// <summary>발생 시 지속 시간을 무작위로 뽑는다 (호스트 전용 — 결과는 복제로 전파된다).</summary>
        public float RollDurationSeconds()
        {
            float min = Mathf.Min(_minDurationSeconds, _maxDurationSeconds);
            float max = Mathf.Max(_minDurationSeconds, _maxDurationSeconds);

            return Random.Range(min, max);
        }
    }
}
