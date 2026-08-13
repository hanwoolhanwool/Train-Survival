namespace Game.Gameplay.Region
{
    /// <summary>지정 Day의 난이도 배율 묶음 — 지역 난이도 × 순환 보너스가 이미 반영된 값이다.</summary>
    public readonly struct RegionDifficulty
    {
        /// <summary>밤 웨이브 총량·동시 상한에 곱할 배율.</summary>
        public readonly float WaveCountMultiplier;

        /// <summary>몬스터 체력에 곱할 배율.</summary>
        public readonly float MonsterHealthMultiplier;

        /// <summary>그 Day의 밤이 지역 마지막 밤(대형 웨이브)인가 — 기획서 §5 "지역 졸업 시험".</summary>
        public readonly bool IsFinalNightOfRegion;

        /// <summary>
        /// 그 Day의 밤이 지역 중간 강화 밤인가 (M7 2차 결정 ⑥ — 기획서 §5 잔여 이행).
        /// 마지막 밤과 배타적이다 — 첫날·마지막 날은 판정에서 제외된다.
        /// </summary>
        public readonly bool IsReinforcedNightOfRegion;

        public RegionDifficulty(
            float waveCountMultiplier, float monsterHealthMultiplier,
            bool isFinalNightOfRegion, bool isReinforcedNightOfRegion)
        {
            WaveCountMultiplier = waveCountMultiplier;
            MonsterHealthMultiplier = monsterHealthMultiplier;
            IsFinalNightOfRegion = isFinalNightOfRegion;
            IsReinforcedNightOfRegion = isReinforcedNightOfRegion;
        }

        /// <summary>지역 데이터가 없을 때 쓰는 무보정 기본값.</summary>
        public static RegionDifficulty Neutral => new RegionDifficulty(1f, 1f, false, false);
    }

    /// <summary>
    /// 현재 지역·지역 내 일차·난이도 배율의 조회 계약 (개발 가이드 M4).
    /// 낮/밤 사이클과 같은 규약을 따른다 — 이산 전환은 <see cref="RegionChangedEvent"/>로 발행하고,
    /// 연속 조회(예고 여부·배율)는 이 서비스로만 노출한다. setter는 두지 않는다.
    /// </summary>
    public interface IRegionService
    {
        /// <summary>현재 지역 정의. 지역 목록이 비어 있으면 null.</summary>
        RegionDefinition CurrentRegion { get; }

        /// <summary>다음에 진입할 지역 정의 (예고 연출용). 없으면 null.</summary>
        RegionDefinition NextRegion { get; }

        /// <summary>현재 지역의 배열 인덱스.</summary>
        int RegionIndex { get; }

        /// <summary>지역 순환 횟수 (0 = 첫 바퀴, 기획서 §4.5).</summary>
        int CycleNumber { get; }

        /// <summary>현재 지역 안에서의 일차 (1부터).</summary>
        int DayInRegion { get; }

        /// <summary>현재 지역의 총 일수.</summary>
        int RegionDayCount { get; }

        /// <summary>오늘 밤이 지역 마지막 밤(대형 웨이브)인가.</summary>
        bool IsFinalDayOfRegion { get; }

        /// <summary>다음 지역 예고 구간인가.</summary>
        bool IsForecastWindow { get; }

        /// <summary>
        /// 지정 Day의 난이도 배율을 즉시 평가한다.
        /// 지역은 Day 번호의 순수 함수이므로, 소비자가 Day를 직접 넘겨
        /// <see cref="Cycle.DayPhaseChangedEvent"/> 구독자 간 처리 순서에 의존하지 않게 한다.
        /// </summary>
        RegionDifficulty GetDifficultyForDay(int dayNumber);

        /// <summary>
        /// 지정 Day의 지역 타임라인 상태를 즉시 평가한다 (지역 인덱스·지역 내 일차 등).
        /// <see cref="GetDifficultyForDay"/>와 같은 이유로 처리 순서에 의존하지 않는 조회 경로다 —
        /// 지역 전환 당일에 "현재 지역"을 읽으면 갱신 순서에 따라 이전/새 지역이 갈린다.
        /// </summary>
        RegionTimelineState EvaluateForDay(int dayNumber);

        /// <summary>배열 인덱스의 지역 정의 — 복제된 지역 경계 기록(M6 1차)을 프리팹으로 해석할 때
        /// 쓴다 (전 피어가 같은 타임라인 에셋을 참조하므로 인덱스만으로 충분하다). 범위 밖이면 null.</summary>
        RegionDefinition GetRegion(int regionIndex);
    }
}
