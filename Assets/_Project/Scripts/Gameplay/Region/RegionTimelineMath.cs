using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>Day 번호 하나에서 유도한 지역 타임라인 상태 (순수 파생값).</summary>
    public readonly struct RegionTimelineState
    {
        /// <summary>현재 지역의 배열 인덱스. 순환 중에도 항상 배열 범위 안으로 정규화된다.</summary>
        public readonly int RegionIndex;

        /// <summary>지역 순환 횟수 (0 = 첫 바퀴). 기획서 §4.5 챌린지 순환의 난이도 배율 입력.</summary>
        public readonly int CycleNumber;

        /// <summary>현재 지역 안에서의 일차 (1부터).</summary>
        public readonly int DayInRegion;

        /// <summary>현재 지역의 총 일수.</summary>
        public readonly int RegionDayCount;

        /// <summary>다음에 진입할 지역의 인덱스. 순환하지 않는 마지막 지역에서는 자기 자신.</summary>
        public readonly int NextRegionIndex;

        /// <summary>오늘이 이 지역의 마지막 날인가 — 밤에 대형 웨이브("지역 졸업 시험", 기획서 §5)가 온다.</summary>
        public readonly bool IsFinalDayOfRegion;

        /// <summary>다음 지역 예고 구간인가 (기획서 §2 — "마지막 1~2일"에 예고 연출).</summary>
        public readonly bool IsForecastWindow;

        public RegionTimelineState(
            int regionIndex, int cycleNumber, int dayInRegion, int regionDayCount,
            int nextRegionIndex, bool isFinalDayOfRegion, bool isForecastWindow)
        {
            RegionIndex = regionIndex;
            CycleNumber = cycleNumber;
            DayInRegion = dayInRegion;
            RegionDayCount = regionDayCount;
            NextRegionIndex = nextRegionIndex;
            IsFinalDayOfRegion = isFinalDayOfRegion;
            IsForecastWindow = isForecastWindow;
        }

        /// <summary>유효한 지역 정의로 평가된 상태인가 (지역 목록이 비면 false).</summary>
        public bool IsValid => RegionDayCount > 0;
    }

    /// <summary>
    /// Day 번호 → 지역 타임라인의 순수 파생 (기획서 §4 — 지역당 3~5일 주기).
    /// 낮/밤 사이클과 같은 원칙: 지역은 별도 동기화 상태가 아니라 이미 호스트 권위로 복제된
    /// Day 번호의 함수이므로, 모든 피어가 같은 입력에서 같은 지역을 독립 유도한다.
    /// </summary>
    public static class RegionTimelineMath
    {
        /// <summary>
        /// 지역 타임라인을 평가한다.
        /// </summary>
        /// <param name="dayNumber">1부터 시작하는 Day 번호. 0 이하는 1로 고정된다.</param>
        /// <param name="regionDayCounts">지역별 일수 배열 (지역 순서 = 배열 순서).</param>
        /// <param name="forecastLeadDays">지역 마지막 며칠부터 다음 지역을 예고할지.</param>
        /// <param name="loopAfterLastRegion">마지막 지역 뒤 첫 지역으로 순환할지 (기획서 §4.5).</param>
        public static RegionTimelineState Evaluate(
            int dayNumber, int[] regionDayCounts, int forecastLeadDays, bool loopAfterLastRegion)
        {
            if (regionDayCounts == null || regionDayCounts.Length == 0)
            {
                return default;
            }

            int day = Mathf.Max(1, dayNumber);
            int dayIndex = day - 1;

            int totalDays = 0;
            for (int i = 0; i < regionDayCounts.Length; i++)
            {
                totalDays += Mathf.Max(1, regionDayCounts[i]);
            }

            int lastIndex = regionDayCounts.Length - 1;

            // 순환하지 않는데 전체 일수를 넘겼다 — 마지막 지역에 무기한 머문다.
            if (!loopAfterLastRegion && dayIndex >= totalDays)
            {
                int lastDayCount = Mathf.Max(1, regionDayCounts[lastIndex]);
                int dayInLastRegion = dayIndex - (totalDays - lastDayCount) + 1;

                // 마지막 날을 이미 지났으므로 졸업 웨이브·예고는 다시 트리거하지 않는다.
                return new RegionTimelineState(
                    lastIndex, 0, dayInLastRegion, lastDayCount, lastIndex, false, false);
            }

            int cycleNumber = loopAfterLastRegion ? dayIndex / totalDays : 0;
            int dayInCycle = loopAfterLastRegion ? dayIndex % totalDays : dayIndex;

            int regionIndex = 0;
            int consumed = 0;
            for (int i = 0; i < regionDayCounts.Length; i++)
            {
                int count = Mathf.Max(1, regionDayCounts[i]);
                if (dayInCycle < consumed + count)
                {
                    regionIndex = i;
                    break;
                }

                consumed += count;
            }

            int regionDayCount = Mathf.Max(1, regionDayCounts[regionIndex]);
            int dayInRegion = dayInCycle - consumed + 1;

            int nextRegionIndex = loopAfterLastRegion
                ? (regionIndex + 1) % regionDayCounts.Length
                : Mathf.Min(regionIndex + 1, lastIndex);

            bool isFinalDay = dayInRegion >= regionDayCount;
            bool isForecast = forecastLeadDays > 0 && dayInRegion > regionDayCount - forecastLeadDays;

            return new RegionTimelineState(
                regionIndex, cycleNumber, dayInRegion, regionDayCount,
                nextRegionIndex, isFinalDay, isForecast);
        }
    }
}
