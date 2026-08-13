namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 밤 웨이브가 현재 유지 중인 개체 수의 조회 계약 (M7 2차).
    /// 보스 소속 개체(소환·무리)가 <b>합산 cap</b>을 지키려면 웨이브 쪽 인원을 알아야 한다 —
    /// 토글 계약(<see cref="IWaveSpawnToggle"/>)과 분리해 소비자가 필요한 것만 보게 한다.
    /// <see cref="Game.Core.Services.ServiceLocator"/>에 등록된다.
    /// </summary>
    public interface IMonsterPopulation
    {
        /// <summary>현재 살아 있는 웨이브 개체 수.</summary>
        int ActiveMonsterCount { get; }
    }
}
