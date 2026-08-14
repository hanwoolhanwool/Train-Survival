using Unity.Netcode;

namespace Game.Gameplay.Session
{
    /// <summary>
    /// 게스트 이탈 통지의 진입점 (잔여 문서 §5 결정 ⑤-b) — <b>UI가 네트워크를 직접 알지 않게 하는
    /// 경계</b>다. UI 계층은 Netcode를 참조하지 않으므로(어셈블리 단방향 규칙) 로컬 플레이어
    /// 조회와 RPC 발신을 여기서 닫는다.
    /// </summary>
    public static class SessionLeaveNotifier
    {
        /// <summary>
        /// 게스트면 서버에 이탈을 알리고 true. 호스트·비접속·플레이어 오브젝트 부재면 false —
        /// 그 경우 호출부가 로컬 셧다운 경로로 넘어간다.
        /// </summary>
        public static bool TryNotifyServer()
        {
            NetworkManager manager = NetworkManager.Singleton;

            // 호스트는 자신을 끊을 수 없다 — 호스트 이탈 = 세션 종료라 셧다운 경로가 담당한다.
            if (manager == null || !manager.IsClient || manager.IsServer)
            {
                return false;
            }

            NetworkObject player = Player.LocalInteraction.GetLocalPlayerObject();
            PlayerSessionAgent agent = player != null ? player.GetComponent<PlayerSessionAgent>() : null;
            if (agent == null)
            {
                return false;
            }

            agent.RequestLeaveSession();
            return true;
        }
    }
}
