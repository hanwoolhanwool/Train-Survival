using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Player;
using Game.Systems.Networking;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Game.UI
{
    /// <summary>
    /// 세션 나가기 HUD — Esc = 세션 메뉴 토글(커서 해제·시점/무기 정지는 <see cref="SessionMenuToggledLocalEvent"/> 구독자가 처리).
    /// '세션 나가기'는 로컬 NetworkManager를 내리고 Main 씬으로 돌아간다 — 호스트가 누르면 세션 전체가 끝나고,
    /// 남은 클라이언트에게는 세션 종료 안내와 복귀 버튼이 자동으로 뜬다(호스트 이탈로 끊겼을 때 포함).
    /// Game 씬 HUD 오브젝트(SliceHud)에 부착한다.
    /// </summary>
    public sealed class SessionExitHud : MonoBehaviour
    {
        private const string MainSceneName = "Main";

        private bool _menuOpen;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame && IsSessionActive())
            {
                SetMenuOpen(!_menuOpen);
            }
        }

        private void OnGUI()
        {
            if (!IsSessionActive())
            {
                DrawSessionEndedOverlay();
                return;
            }

            if (_menuOpen)
            {
                DrawSessionMenu();
            }
        }

        /// <summary>세션이 끝났다(호스트 이탈·접속 끊김 등) — 게임 씬에 남은 피어에게 복귀 경로를 준다.</summary>
        private void DrawSessionEndedOverlay()
        {
            var box = new Rect(Screen.width * 0.5f - 160f, Screen.height * 0.4f, 320f, 96f);
            GUI.Box(box, "세션이 종료되었습니다");

            if (GUI.Button(new Rect(box.x + 20f, box.y + 40f, box.width - 40f, 36f), "메인 화면으로"))
            {
                LeaveToMain();
            }
        }

        private void DrawSessionMenu()
        {
            var box = new Rect(Screen.width - 260f, 20f, 240f, 118f);
            GUI.Box(box, "메뉴 (Esc — 닫기)");

            if (GUI.Button(new Rect(box.x + 16f, box.y + 32f, box.width - 32f, 34f), "세션 나가기 — 메인 화면으로"))
            {
                LeaveToMain();
                return;
            }

            if (GUI.Button(new Rect(box.x + 16f, box.y + 72f, box.width - 32f, 34f), "계속하기"))
            {
                SetMenuOpen(false);
            }
        }

        private static bool IsSessionActive()
        {
            return ServiceLocator.TryGet(out INetworkSessionService session) && session.IsSessionActive;
        }

        private void SetMenuOpen(bool open)
        {
            if (_menuOpen == open)
            {
                return;
            }

            _menuOpen = open;
            EventBus<SessionMenuToggledLocalEvent>.Publish(new SessionMenuToggledLocalEvent(open));
        }

        /// <summary>로컬 세션을 내리고 Main 씬으로 돌아간다 — NGO 씬 관리는 세션과 함께 죽으므로 일반 씬 로드를 쓴다.</summary>
        private void LeaveToMain()
        {
            SetMenuOpen(false);

            if (ServiceLocator.TryGet(out INetworkSessionService session))
            {
                session.Shutdown();
            }

            SceneManager.LoadScene(MainSceneName);
        }
    }
}
