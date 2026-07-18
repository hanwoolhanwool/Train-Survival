namespace Game.Systems.Networking
{
    /// <summary>
    /// 네트워크 세션(리슨 서버) 수명주기 계약. 게임플레이·UI는 NGO API 대신 이 추상에 의존한다 (DIP).
    /// 1인 플레이도 "혼자 호스트인 세션"으로 취급하므로 게임 시작 = <see cref="StartHost"/>다.
    /// </summary>
    public interface INetworkSessionService
    {
        /// <summary>세션이 열려 있는지 (호스트 또는 클라이언트로 참여 중).</summary>
        bool IsSessionActive { get; }

        /// <summary>현재 세션의 호스트인지.</summary>
        bool IsHost { get; }

        /// <summary>호스트로 세션을 시작한다. 이미 세션 중이면 false.</summary>
        bool StartHost();

        /// <summary>지정 주소의 호스트에 클라이언트로 접속한다. 이미 세션 중이면 false.</summary>
        bool StartClient(string address, ushort port);

        /// <summary>
        /// 세션 종료를 요청한다. 세션 중이 아니면 아무것도 하지 않는다.
        /// 종료는 비동기로 완료된다 — 호출 직후에는 <see cref="IsSessionActive"/>가 아직 true일 수 있다.
        /// </summary>
        void Shutdown();
    }
}
