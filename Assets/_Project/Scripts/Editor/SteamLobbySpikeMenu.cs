using Game.Core.Logging;
using System.Threading;
using Steamworks;
using UnityEditor;
using UnityEngine;

namespace Game.Editor
{
    /// <summary>
    /// M6 2차 §3-1 스파이크 — 에디터에서 SteamAPI 초기화·친구 전용 로비 생성·데이터 왕복을
    /// 단발로 확인한다 (임시 AppID 480). 검증용 QA 도구 — 릴리스 코드 경로와 무관.
    /// </summary>
    public static class SteamLobbySpikeMenu
    {
        [MenuItem("Game/QA/Steam Lobby Spike")]
        public static void Run()
        {
            if (!SteamAPI.Init())
            {
                GameLog.Error(LogCategory.Steam, "SteamAPI.Init 실패 — Steam 클라이언트 로그인과 " +
                                      "프로젝트 루트 steam_appid.txt(480)를 확인하세요.");
                return;
            }

            try
            {
                string me = SteamUser.GetSteamID().m_SteamID.ToString();
                string persona = SteamFriends.GetPersonaName();

                SteamAPICall_t call = SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, 4);
                bool done = false;
                bool failed = false;
                CSteamID lobbyId = default;
                var callResult = CallResult<LobbyCreated_t>.Create((result, ioFailure) =>
                {
                    done = true;
                    failed = ioFailure || result.m_eResult != EResult.k_EResultOK;
                    lobbyId = new CSteamID(result.m_ulSteamIDLobby);
                });
                callResult.Set(call);

                for (int i = 0; i < 100 && !done; i++)
                {
                    SteamAPI.RunCallbacks();
                    Thread.Sleep(50);
                }

                if (!done || failed)
                {
                    GameLog.Error(LogCategory.Steam, $"로비 생성 실패 — done={done} failed={failed} " +
                                          $"(me={me}/{persona})");
                    return;
                }

                SteamMatchmaking.SetLobbyData(lobbyId, "host_steam_id", me);
                string readBack = SteamMatchmaking.GetLobbyData(lobbyId, "host_steam_id");
                int memberCount = SteamMatchmaking.GetNumLobbyMembers(lobbyId);
                SteamMatchmaking.LeaveLobby(lobbyId);

                GameLog.Info(LogCategory.Steam, $"OK — lobby={lobbyId.m_SteamID} members={memberCount} " +
                                          $"data왕복={(readBack == me ? "일치" : "불일치")} me={me}({persona})");
            }
            finally
            {
                SteamAPI.Shutdown();
            }
        }
    }
}
