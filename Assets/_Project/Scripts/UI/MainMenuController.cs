using Game.Core.Services;
using Game.Systems.Networking;
using Game.Systems.Networking.Steam;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Main 씬 임시 메뉴 (슬라이스용) — 호스트 시작 / 로컬 클라이언트 접속.
    /// 게임 시작은 항상 세션 서비스 경유: 1인 플레이 = 혼자 호스트인 세션 (개발 원칙 2).
    /// Steam 모드(M6 2차)에서는 호스트 시작이 친구 전용 로비 생성을 겸하고, 참가는
    /// Steam 오버레이(초대 수락)로만 이뤄진다 — 직접 주소 접속 버튼은 숨긴다.
    /// </summary>
    public sealed class MainMenuController : MonoBehaviour
    {
        private const string GameSceneName = "Game";
        private const string DefaultAddress = "127.0.0.1";
        private const ushort DefaultPort = 7777;

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(40f, 40f, 320f, 240f));
            GUILayout.Label("<b>Train Survival — 집게 수직 슬라이스</b>");

            if (!ServiceLocator.TryGet(out INetworkSessionService session))
            {
                GUILayout.Label("세션 서비스 초기화 대기 중...");
                GUILayout.EndArea();
                return;
            }

            if (session.IsSessionActive)
            {
                GUILayout.Label(session.IsHost ? "호스트 세션 진행 중" : "클라이언트 접속 중...");
                GUILayout.EndArea();
                return;
            }

            if (ActiveTransportMode.IsSteam)
            {
                DrawSteamMenu(session);
            }
            else
            {
                DrawDirectMenu(session);
            }

            GUILayout.EndArea();
        }

        private static void DrawDirectMenu(INetworkSessionService session)
        {
            if (GUILayout.Button("호스트 시작 (혼자여도 호스트 세션)", GUILayout.Height(40f)))
            {
                if (session.StartHost())
                {
                    session.LoadGameplayScene(GameSceneName);
                }
            }

            if (GUILayout.Button($"클라이언트 접속 ({DefaultAddress}:{DefaultPort})", GUILayout.Height(40f)))
            {
                session.StartClient(DefaultAddress, DefaultPort);
            }
        }

        private static void DrawSteamMenu(INetworkSessionService session)
        {
            bool steamReady = ServiceLocator.TryGet(out ISteamLobbyService lobby);
            GUILayout.Label(steamReady
                ? "<b>Steam 모드</b> — 릴레이 접속"
                : "<b>Steam 모드</b> — 초기화 실패 (콘솔 확인)");

            GUI.enabled = steamReady;
            if (GUILayout.Button("Steam 호스트 시작 (친구 전용 로비)", GUILayout.Height(40f)))
            {
                if (session.StartHost())
                {
                    lobby.CreateLobby();
                    session.LoadGameplayScene(GameSceneName);
                }
            }

            GUI.enabled = true;
            GUILayout.Label("참가: 친구의 초대를 Steam 오버레이에서 수락하세요.");
        }
    }
}
