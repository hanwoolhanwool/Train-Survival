namespace Game.Systems.Networking.Lobby
{
    /// <summary>
    /// 이번 여정의 난이도 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §12 미결 7번.
    ///
    /// <para><b>3단계 · 일반 어휘로 확정됐다.</b> 세계관 어휘(완행·급행·특급)는 뜻이 즉시 읽히지
    /// 않아 폐기했고, 시안의 "보통"이 그대로 중앙값이라 그림과 어긋나지 않는다.</para>
    ///
    /// <para><b>표시 이름은 여기 없다.</b> "쉬움·보통·어려움"은 화면에 쓰는 말이라
    /// <c>Game.UI</c>의 <c>DifficultyStepper</c>가 갖는다 — 이 열거는 <b>실려 가는 값</b>일 뿐이다.</para>
    ///
    /// <para><b>이 값이 게임플레이를 바꾸지는 않는다</b>(§12 미결 1번 결정 — 표시·전달만).
    /// 웨이브 배율은 M4 시스템(<c>WaveMath</c>·<c>MonsterVariantCatalog</c>)을 건드려야 하는
    /// 별도 마일스톤이고, 그때 꽂을 자리가 <see cref="ILobbyRoomService.Difficulty"/>다 —
    /// 대기실 상태가 인게임 씬까지 따라가므로(§12 미결 6번) 거기서 그대로 읽힌다.</para>
    /// </summary>
    public enum GameDifficulty
    {
        /// <summary>쉬움.</summary>
        Easy = 0,

        /// <summary>보통 — 기본값이자 시안에 그려진 값이다.</summary>
        Normal = 1,

        /// <summary>어려움.</summary>
        Hard = 2,
    }
}
