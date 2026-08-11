namespace Game.Systems.Networking.Steam
{
    /// <summary>
    /// Steam 로비·초대 계약 (M6 2차 §2.4, 결정 ③ — 친구 전용 4인).
    /// 초대 수락 → 로비 참가 → 호스트 SteamID 조회 → 릴레이 접속의 참가 흐름은 구현이
    /// 내부에서 처리한다 — UI는 로비 생성·초대 창 열기만 안다.
    /// </summary>
    public interface ISteamLobbyService
    {
        /// <summary>현재 로비에 들어가 있는가 (호스트·게스트 공통).</summary>
        bool HasLobby { get; }

        /// <summary>호스트 세션 시작 직후 친구 전용 로비를 만든다 (비동기 — 완료는 로그).</summary>
        void CreateLobby();

        /// <summary>Steam 오버레이 초대 창을 연다 — 로비가 있어야 한다.</summary>
        void OpenInviteOverlay();

        /// <summary>로비에 참가한다 (초대 수락 콜백·`+connect_lobby` 부팅이 쓴다). 입장이 완료되면
        /// 로비 데이터의 호스트 SteamID로 세션 접속까지 이어진다.</summary>
        void JoinLobby(ulong lobbyId);

        /// <summary>로비에서 나온다. 세션 종료 시 자동 호출된다.</summary>
        void LeaveLobby();
    }
}
