using System;

namespace Game.Systems.Networking.Lobby
{
    /// <summary>
    /// 대기실 상태 계약 —
    /// [게임 준비 화면 구현 계획](docs/plans/features/게임-준비-화면-구현-계획.md) §6.1 · §7.
    ///
    /// <para><b>UI는 NGO를 직접 보지 않는다.</b> 준비 화면은 이 계약 하나만 알고, 그 뒤가
    /// NGO인지 Steam인지 모른다 — 기존 <see cref="INetworkSessionService"/>·
    /// <c>ISteamLobbyService</c>와 같은 격리 규약이다.</para>
    ///
    /// <para>여기서 <b>이름은 <see cref="string"/></b>이다. 안에서는 클라이언트 id만 나르고
    /// 표시 이름은 칸 번호에서 파생하지만(<see cref="RosterOrdering.DisplayName"/>),
    /// 그 사정은 UI가 알 필요가 없다.</para>
    /// </summary>
    public interface ILobbyRoomService
    {
        /// <summary>대기실 상태가 살아 있는가 — 스폰된 상태 객체가 있는지.</summary>
        bool IsActive { get; }

        /// <summary>지금 방에 있는 사람 수.</summary>
        int MemberCount { get; }

        /// <summary>
        /// 이번 여정의 난이도. 상태가 아직 서기 전이면 <see cref="GameDifficulty.Normal"/>이다.
        ///
        /// <para><b>여기가 M4가 배율을 꽂을 자리다.</b> 대기실 상태는 인게임 씬까지 따라가므로
        /// (§12 미결 6번) 인게임에서도 같은 값이 읽힌다 — 이번 계획은 값을 실어 나르는 데까지고,
        /// 웨이브 배율은 별도 마일스톤이다(§12 미결 1번).</para>
        /// </summary>
        GameDifficulty Difficulty { get; }

        /// <summary>대기실 상태가 바뀌었다 — <b>멤버가 들고 났거나 난이도가 바뀌었다.</b></summary>
        event Action Changed;

        /// <summary>칸에 앉은 사람을 읽는다. 빈 칸이면 <c>false</c>.</summary>
        bool TryGetSlot(int slot, out string displayName, out bool isHost);

        /// <summary>
        /// 호스트 전용 — 난이도를 바꾼다. 게스트가 부르면 아무 일도 없이 <c>false</c>다(§6.2).
        /// </summary>
        bool SetDifficulty(GameDifficulty value);

        /// <summary>호스트 전용 — 대기실 상태를 띄운다. 세션이 선 뒤에 부른다.</summary>
        bool Open();

        /// <summary>호스트 전용 — 대기실 상태를 내린다. 세션 종료와 함께 사라지므로 보통은 불필요하다.</summary>
        void Close();
    }
}
