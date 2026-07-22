using UnityEngine;

namespace Game.Gameplay.Cycle
{
    /// <summary>
    /// 낮/밤 타임라인 밸런스 데이터 (기획서 §2, 개발 가이드 M2).
    /// 지역별 일수 조정(기획서 §4)이 코드 수정 없이 가능하도록 길이를 데이터로 분리한다.
    /// </summary>
    [CreateAssetMenu(fileName = "DayTimelineSettings", menuName = "Game/Day Timeline Settings")]
    public sealed class DayTimelineSettings : ScriptableObject
    {
        [Header("국면 길이 (초)")]
        [SerializeField, Min(10f)] private float _dayDurationSeconds = 240f;
        [SerializeField, Min(10f)] private float _nightDurationSeconds = 150f;

        public float DayDurationSeconds => _dayDurationSeconds;

        public float NightDurationSeconds => _nightDurationSeconds;
    }
}
