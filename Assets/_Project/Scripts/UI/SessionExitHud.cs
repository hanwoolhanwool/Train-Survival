using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Player;
using Game.Systems.Networking;
using UnityEngine;
using UnityEngine.InputSystem;

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
        private bool _inventoryOpen;
        private bool _craftingOpen;
        private bool _storageOpen;
        private bool _bundleOpen;
        private bool _gameOver;

        /// <summary>이탈 절차가 진행 중인가 — 셧다운을 기다리는 동안 버튼이 다시 눌리는 것을 막는다.</summary>
        private bool _leaving;

        private void OnEnable()
        {
            EventBus<Gameplay.Inventory.InventoryPanelToggledLocalEvent>.Subscribe(OnInventoryToggled);
            EventBus<Gameplay.Crafting.CraftingPanelToggledLocalEvent>.Subscribe(OnCraftingToggled);
            EventBus<Gameplay.Train.StoragePanelToggledLocalEvent>.Subscribe(OnStorageToggled);
            EventBus<Gameplay.Train.BundlePanelToggledLocalEvent>.Subscribe(OnBundleToggled);
            EventBus<Gameplay.Session.GameOverEvent>.Subscribe(OnGameOver);
        }

        private void OnDisable()
        {
            EventBus<Gameplay.Inventory.InventoryPanelToggledLocalEvent>.Unsubscribe(OnInventoryToggled);
            EventBus<Gameplay.Crafting.CraftingPanelToggledLocalEvent>.Unsubscribe(OnCraftingToggled);
            EventBus<Gameplay.Train.StoragePanelToggledLocalEvent>.Unsubscribe(OnStorageToggled);
            EventBus<Gameplay.Train.BundlePanelToggledLocalEvent>.Unsubscribe(OnBundleToggled);
            EventBus<Gameplay.Session.GameOverEvent>.Unsubscribe(OnGameOver);
        }

        private void Update()
        {
            // 게임오버 중에는 Esc 메뉴를 억제한다 — 화면은 GameOverHud가 소유한다 (M6 3차).
            if (_gameOver)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || !IsSessionActive())
            {
                return;
            }

            // Esc 우선순위 (M5 4차): 열린 창 닫기 > 세션 메뉴. 메뉴가 열려 있으면 메뉴부터 닫는다.
            if (_menuOpen)
            {
                SetMenuOpen(false);
            }
            else if (_inventoryOpen || _craftingOpen || _storageOpen || _bundleOpen)
            {
                EventBus<UiCloseRequestedLocalEvent>.Publish(default);
            }
            else
            {
                SetMenuOpen(true);
            }
        }

        private void OnInventoryToggled(Gameplay.Inventory.InventoryPanelToggledLocalEvent evt)
        {
            _inventoryOpen = evt.IsOpen;
        }

        private void OnCraftingToggled(Gameplay.Crafting.CraftingPanelToggledLocalEvent evt)
        {
            _craftingOpen = evt.IsOpen;
        }

        private void OnStorageToggled(Gameplay.Train.StoragePanelToggledLocalEvent evt)
        {
            _storageOpen = evt.IsOpen;
        }

        private void OnBundleToggled(Gameplay.Train.BundlePanelToggledLocalEvent evt)
        {
            _bundleOpen = evt.IsOpen;
        }

        private void OnGameOver(Gameplay.Session.GameOverEvent evt)
        {
            // 복제 값(GameOverMonitor)이 아닌 로컬 플래그로 기억한다 — 호스트가 먼저 나가
            // 세션이 내려간 뒤에도 게임오버 화면이 세션 종료 오버레이로 바뀌지 않게.
            _gameOver = true;
            SetMenuOpen(false);
        }

        private void OnGUI()
        {
            if (_gameOver)
            {
                return;
            }

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
            // Steam 모드 + 로비 보유(호스트) — 세션 중 친구 초대 진입점 (M6 2차 결정 ③).
            bool canInvite = Game.Systems.Networking.ActiveTransportMode.IsSteam
                && ServiceLocator.TryGet(out Game.Systems.Networking.Steam.ISteamLobbyService lobby)
                && lobby.HasLobby;

            float height = canInvite ? 158f : 118f;
            var box = new Rect(Screen.width - 260f, 20f, 240f, height);
            GUI.Box(box, "메뉴 (Esc — 닫기)");

            float y = box.y + 32f;
            if (canInvite)
            {
                if (GUI.Button(new Rect(box.x + 16f, y, box.width - 32f, 34f), "친구 초대 (Steam 오버레이)"))
                {
                    ServiceLocator.Get<Game.Systems.Networking.Steam.ISteamLobbyService>().OpenInviteOverlay();
                }

                y += 40f;
            }

            if (GUI.Button(new Rect(box.x + 16f, y, box.width - 32f, 34f), "세션 나가기 — 메인 화면으로"))
            {
                LeaveToMain();
                return;
            }

            if (GUI.Button(new Rect(box.x + 16f, y + 40f, box.width - 32f, 34f), "계속하기"))
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

        /// <summary>
        /// 로컬 세션을 내리고 Main 씬으로 돌아간다 — NGO 씬 관리는 세션과 함께 죽으므로 일반 씬 로드를 쓴다.
        /// <b>셧다운 완료를 기다린 뒤</b> 로드한다 (잔여 문서 §5 ⑤-a) — 같은 프레임에 로드하면
        /// 이탈 통지가 전송 전에 잘려 호스트에 유령이 남는다. 절차는 <see cref="SessionExitFlow"/>.
        /// </summary>
        private void LeaveToMain()
        {
            if (_leaving)
            {
                return;
            }

            _leaving = true;
            SetMenuOpen(false);
            StartCoroutine(SessionExitFlow.ShutdownThenLoadMain(MainSceneName));
        }
    }
}
