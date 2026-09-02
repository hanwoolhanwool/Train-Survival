namespace Game.Core.Diagnostics
{
    /// <summary>
    /// 측정 중 장면이 실제로 얼마나 무거웠는지 — 결과 JSON 이 스스로 밝히기 위한 조회 계약.
    ///
    /// <para><b>이 인터페이스가 <c>Game.Core</c>에 있는 이유</b>는 구현이 <c>Game.Gameplay</c>에
    /// 있고 소비자(<c>PerfRunner</c>)는 <c>Game.Systems</c>에 있기 때문이다. 단방향 의존에서
    /// 둘이 만나는 유일한 자리가 Core 다.</para>
    ///
    /// <para>몬스터 0마리인 낮 주행과 24마리인 밤 주행은 <b>같은 시나리오 이름을 달고도 전혀 다른 것</b>을
    /// 잰다. 그 차이가 파일에 남지 않으면 두 결과를 나중에 구분할 수 없다.</para>
    /// </summary>
    public interface IPerfSceneStats
    {
        /// <summary>측정 시점에 살아 있는 몬스터 수. 알 수 없으면 -1.</summary>
        int MonsterCount { get; }
    }

    /// <summary>벤치 시나리오가 강제하는 시간대.</summary>
    public enum PerfTimeOfDay
    {
        /// <summary>건드리지 않는다 — 게임이 정하는 대로 둔다.</summary>
        Unchanged = 0,

        Day = 1,

        Night = 2,
    }

    /// <summary>
    /// 벤치 주행이 시작됐고, 시나리오가 이런 상태를 원한다는 통지.
    ///
    /// <para><b>이 이벤트가 존재하는 이유는 어셈블리 방향이다.</b> 주행기(<c>PerfRunner</c>)는
    /// <c>Game.Systems</c>에 있고 낮/밤·웨이브는 <c>Game.Gameplay</c>에 있다. 의존은 단방향
    /// (<c>Systems ← Gameplay</c>)이므로 주행기가 게임플레이를 직접 부를 수 없다. 주행기는
    /// <b>무엇을 원하는지만 알리고</b>, 적용은 게임플레이 쪽이 한다
    /// (성능 프로파일링 자동화 계획 §4.7).</para>
    ///
    /// <para>강제한 내용은 결과 JSON 에 그대로 남는다 — <b>무엇을 바꾸고 잰 값인지</b>를
    /// 파일이 스스로 밝혀야 한다(§7).</para>
    /// </summary>
    public readonly struct PerfRunStartedEvent
    {
        public PerfRunStartedEvent(string scenarioId, PerfTimeOfDay timeOfDay, int dayNumber, bool forceWaveSpawn)
        {
            ScenarioId = scenarioId;
            TimeOfDay = timeOfDay;
            DayNumber = dayNumber;
            ForceWaveSpawn = forceWaveSpawn;
        }

        public string ScenarioId { get; }

        public PerfTimeOfDay TimeOfDay { get; }

        /// <summary>점프할 Day 번호 (1부터). <b>0이면 지금 Day 를 유지한다.</b></summary>
        public int DayNumber { get; }

        /// <summary>웨이브 스폰을 켜 둘 것인가. 밤 시나리오에서 꺼져 있으면 몬스터가 안 나온다.</summary>
        public bool ForceWaveSpawn { get; }
    }
}
