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

        /// <summary>이 밤 몬스터의 최대 체력에 곱할 배율 (기획서 §5 — Day 비례 "수 → 체력" 2단계).</summary>
        public readonly float HealthMultiplier;

        /// <summary>지역 마지막 밤(대형 웨이브)인가 — 기획서 §5 "지역 졸업 시험".</summary>
        public readonly bool IsFinalNight;

        /// <summary>지역 중간 강화 밤인가 (M7 2차 결정 ⑥) — 마지막 밤보다 낮은 가중.</summary>
        public readonly bool IsReinforcedNight;

        public WavePlan(
            int totalCount, float spawnInterval, int maxAlive, float healthMultiplier,
            bool isFinalNight, bool isReinforcedNight)
        {
            TotalCount = totalCount;
            SpawnInterval = spawnInterval;
            MaxAlive = maxAlive;
            HealthMultiplier = healthMultiplier;
            IsFinalNight = isFinalNight;
            IsReinforcedNight = isReinforcedNight;
        }
    }

    /// <summary>
    /// 밤 웨이브 밸런스 곡선의 순수 수치 묶음 — <see cref="WaveSettings"/>에서 뽑아 <see cref="WaveMath"/>에 넘긴다.
    /// 순수 로직이 ScriptableObject를 모르게 유지하기 위한 경계다.
    /// </summary>
    public readonly struct WaveCurve
    {
        public readonly int BaseCount;
        public readonly int CountGrowthPerDay;
        public readonly int TotalCountCap;

        public readonly float BaseInterval;
        public readonly float IntervalReductionPerDay;
        public readonly float MinInterval;

        public readonly int BaseMaxAlive;
        public readonly int MaxAliveGrowthPerDay;
        public readonly int MaxAliveCap;

        public readonly float HealthGrowthPerDay;
        public readonly float HealthMultiplierCap;

        public readonly float FinalNightCountMultiplier;
        public readonly float FinalNightHealthMultiplier;

        public readonly float ReinforcedNightCountMultiplier;
        public readonly float ReinforcedNightHealthMultiplier;

        public WaveCurve(
            int baseCount, int countGrowthPerDay, int totalCountCap,
            float baseInterval, float intervalReductionPerDay, float minInterval,
            int baseMaxAlive, int maxAliveGrowthPerDay, int maxAliveCap,
            float healthGrowthPerDay, float healthMultiplierCap,
            float finalNightCountMultiplier, float finalNightHealthMultiplier,
            float reinforcedNightCountMultiplier, float reinforcedNightHealthMultiplier)
        {
            BaseCount = baseCount;
            CountGrowthPerDay = countGrowthPerDay;
            TotalCountCap = totalCountCap;
            BaseInterval = baseInterval;
            IntervalReductionPerDay = intervalReductionPerDay;
            MinInterval = minInterval;
            BaseMaxAlive = baseMaxAlive;
            MaxAliveGrowthPerDay = maxAliveGrowthPerDay;
            MaxAliveCap = maxAliveCap;
            HealthGrowthPerDay = healthGrowthPerDay;
            HealthMultiplierCap = healthMultiplierCap;
            FinalNightCountMultiplier = finalNightCountMultiplier;
            FinalNightHealthMultiplier = finalNightHealthMultiplier;
            ReinforcedNightCountMultiplier = reinforcedNightCountMultiplier;
            ReinforcedNightHealthMultiplier = reinforcedNightHealthMultiplier;
        }
    }

    /// <summary>
    /// 밤 웨이브 규모의 순수 계산 로직 — Day 비례 증가(기획서 §5)에 지역 난이도 배율과
    /// 지역 마지막 밤 대형 웨이브(M4)를 곱해 최종 계획을 낸다.
    /// </summary>
    public static class WaveMath
    {
        /// <summary>
        /// 하룻밤의 웨이브 계획을 계산한다.
        /// </summary>
        /// <param name="dayNumber">1부터 시작하는 Day 번호.</param>
        /// <param name="curve">밸런스 곡선 수치.</param>
        /// <param name="regionCountMultiplier">지역 난이도의 물량 배율 (기획서 §4 — 숲 1 / 사막 4).</param>
        /// <param name="regionHealthMultiplier">지역 난이도의 체력 배율.</param>
        /// <param name="isFinalNight">지역 마지막 밤인가 (대형 웨이브).</param>
        /// <param name="isReinforcedNight">지역 중간 강화 밤인가 (M7 2차 결정 ⑥).</param>
        public static WavePlan Plan(
            int dayNumber, in WaveCurve curve,
            float regionCountMultiplier, float regionHealthMultiplier,
            bool isFinalNight, bool isReinforcedNight)
        {
            int daysElapsed = Mathf.Max(0, dayNumber - 1);

            // 마지막 밤이 우선한다 — 두 가중이 곱해지면 졸업 시험의 위상이 흐려진다.
            bool reinforced = isReinforcedNight && !isFinalNight;

            float nightCountMultiplier = isFinalNight
                ? curve.FinalNightCountMultiplier
                : (reinforced ? curve.ReinforcedNightCountMultiplier : 1f);

            // 0 이하 배율이 들어와도 물량이 사라지거나 간격이 무한대가 되지 않도록 하한을 둔다.
            float countScale = Mathf.Max(0.01f, regionCountMultiplier * nightCountMultiplier);

            // 총량: Day 성장분에 밸런스 상한을 먼저 걸고, 지역·마지막 밤 배율은 그 위에 곱한다
            // (대형 웨이브가 상한에 눌려 의미를 잃지 않게 — 대역폭 방어선은 MaxAlive 쪽이 맡는다).
            int dayCount = Mathf.Min(curve.TotalCountCap, curve.BaseCount + curve.CountGrowthPerDay * daysElapsed);
            int totalCount = Mathf.Max(1, Mathf.RoundToInt(dayCount * countScale));

            // 간격: 물량이 늘어난 만큼 짧아져야 같은 밤 길이 안에 다 유입된다.
            float dayInterval = Mathf.Max(curve.MinInterval, curve.BaseInterval - curve.IntervalReductionPerDay * daysElapsed);
            float interval = Mathf.Max(curve.MinInterval, dayInterval / countScale);

            // 동시 존재 상한은 대역폭 방어선이므로 배율을 곱한 뒤에도 MaxAliveCap을 절대 넘지 않는다.
            int dayMaxAlive = Mathf.Min(curve.MaxAliveCap, curve.BaseMaxAlive + curve.MaxAliveGrowthPerDay * daysElapsed);
            int maxAlive = Mathf.Clamp(Mathf.RoundToInt(dayMaxAlive * countScale), 1, curve.MaxAliveCap);

            // 체력: Day 성장분에만 상한을 걸고, 지역·마지막 밤 배율은 별개 축으로 곱한다.
            float dayHealth = Mathf.Min(curve.HealthMultiplierCap, 1f + curve.HealthGrowthPerDay * daysElapsed);
            float nightHealthMultiplier = isFinalNight
                ? curve.FinalNightHealthMultiplier
                : (reinforced ? curve.ReinforcedNightHealthMultiplier : 1f);
            float healthMultiplier = Mathf.Max(0.01f, dayHealth * regionHealthMultiplier * nightHealthMultiplier);

            return new WavePlan(totalCount, interval, maxAlive, healthMultiplier, isFinalNight, reinforced);
        }
    }
}
