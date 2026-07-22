using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>Day 번호가 결정하는 하룻밤 웨이브 계획.</summary>
    public readonly struct WavePlan
    {
        /// <summary>이 밤에 유입될 총 마릿수.</summary>
        public readonly int TotalCount;

        /// <summary>유입 간격 (초).</summary>
        public readonly float SpawnInterval;

        /// <summary>동시 존재 상한.</summary>
        public readonly int MaxAlive;

        public WavePlan(int totalCount, float spawnInterval, int maxAlive)
        {
            TotalCount = totalCount;
            SpawnInterval = spawnInterval;
            MaxAlive = maxAlive;
        }
    }

    /// <summary>
    /// 밤 웨이브 규모의 순수 계산 로직 — Day 비례 증가(기획서 §5)를 상한과 함께 평가한다.
    /// </summary>
    public static class WaveMath
    {
        public static WavePlan Plan(
            int dayNumber,
            int baseCount, int countGrowthPerDay, int totalCountCap,
            float baseInterval, float intervalReductionPerDay, float minInterval,
            int baseMaxAlive, int maxAliveGrowthPerDay, int maxAliveCap)
        {
            int daysElapsed = Mathf.Max(0, dayNumber - 1);

            int totalCount = Mathf.Min(totalCountCap, baseCount + countGrowthPerDay * daysElapsed);
            float interval = Mathf.Max(minInterval, baseInterval - intervalReductionPerDay * daysElapsed);
            int maxAlive = Mathf.Min(maxAliveCap, baseMaxAlive + maxAliveGrowthPerDay * daysElapsed);

            return new WavePlan(totalCount, interval, maxAlive);
        }
    }
}
