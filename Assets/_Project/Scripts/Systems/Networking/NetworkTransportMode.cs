using System;

namespace Game.Systems.Networking
{
    /// <summary>세션 트랜스포트 모드 (M6 2차 결정 ②) — 기본 = UnityTransport 직결 (개발·CI·MPPM).</summary>
    public enum NetworkTransportMode
    {
        UnityDirect = 0,

        /// <summary>Steam 릴레이 (SteamNetworkingSockets) — 빌드 실행 인자 `-steam` 또는 에디터 토글.</summary>
        SteamRelay = 1,
    }

    /// <summary>
    /// 실행 인자 → 트랜스포트 모드 결정 순수 로직 (EditMode 대상).
    /// `-steam` 명시 외에, Steam이 초대 수락으로 게임을 실행할 때 붙이는 `+connect_lobby &lt;id&gt;`도
    /// Steam 모드로 간주한다 — 초대로 켜진 게임이 UnityTransport로 부팅하면 참가가 불가능하다.
    /// </summary>
    public static class NetworkTransportModeResolver
    {
        public const string SteamArgument = "-steam";
        public const string ConnectLobbyArgument = "+connect_lobby";

        public static NetworkTransportMode Resolve(string[] args)
        {
            return HasSteamArgument(args) ? NetworkTransportMode.SteamRelay : NetworkTransportMode.UnityDirect;
        }

        private static bool HasSteamArgument(string[] args)
        {
            if (args == null)
            {
                return false;
            }

            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], SteamArgument, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(args[i], ConnectLobbyArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>`+connect_lobby &lt;로비 id&gt;` 인자 해석 — 게임 미실행 상태의 초대 수락 부팅 (§2.4).</summary>
        public static bool TryGetConnectLobbyId(string[] args, out ulong lobbyId)
        {
            lobbyId = 0;
            if (args == null)
            {
                return false;
            }

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], ConnectLobbyArgument, StringComparison.OrdinalIgnoreCase))
                {
                    return ulong.TryParse(args[i + 1], out lobbyId) && lobbyId != 0;
                }
            }

            return false;
        }
    }
}
