namespace Game.Gameplay.Region
{
    /// <summary>
    /// 지역 × 국면 fog의 조회 계약 (사막 지역 구현 계획 §4.2 — 소유권 3단).
    ///
    /// <para>날씨가 끝났을 때 <b>무엇으로 되돌릴지</b>를 이 계약이 정한다. 종전에는
    /// <c>WeatherVisualController</c>가 <c>OnEnable</c>에서 뜬 <b>씬 값</b>으로 되돌렸는데,
    /// 그 백업은 <b>지역이 바뀌면 낡는다</b> — 사막에서 걷힌 모래폭풍이 숲 안개로 돌아가고,
    /// 밤에 걷힌 폭풍이 낮 색으로 돌아간다.</para>
    ///
    /// <para><see cref="IRegionSkyProvider"/>와 같은 모양이다 — 하늘은 <b>슬롯</b>을,
    /// 이쪽은 <b>복원 대상</b>을 지역이 소유한다.</para>
    /// </summary>
    public interface IRegionFogProvider
    {
        /// <summary>지금 지역 × 국면이 fog를 소유하고 있는가. false면 씬 fog가 그대로 유효하다.</summary>
        bool OwnsFog { get; }

        /// <summary>
        /// 현재 지역 × 국면의 fog를 <b>즉시</b> 다시 적용한다 (크로스페이드 없이).
        /// 소유하고 있지 않으면 아무것도 하지 않고 false를 돌려준다 — 호출자는 종전 복원으로 간다.
        /// </summary>
        bool TryApplyCurrentFog();
    }
}
