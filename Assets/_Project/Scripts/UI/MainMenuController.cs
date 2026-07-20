using Game.Core.Services;
using Game.Systems.Networking;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// Main 씬 임시 메뉴 (슬라이스용) — 호스트 시작 / 로컬 클라이언트 접속.
    /// 게임 시작은 항상 세션 서비스 경유: 1인 플레이 = 혼자 호스트인 세션 (개발 원칙 2).
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

            GUILayout.EndArea();
        }
    }
}
