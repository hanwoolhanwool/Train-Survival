using System;
using Game.Core.Services;
using Game.Systems.Networking;
using Game.Systems.Networking.Steam;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Systems
{
    /// <summary>
    /// Boot 씬 진입점. 전역 서비스를 일괄 등록한 뒤 Main 씬으로 진입한다.
    /// 씬 흐름: Boot(인프라 초기화) → Main(타이틀/메뉴) → Game(인게임).
    /// Steam 모드(M6 2차 결정 ②)에서는 SteamAPI 초기화·콜백 펌프·로비 서비스 배선과
    /// 초대 부팅(`+connect_lobby`) 자동 참가까지 여기서 처리한다.
    /// </summary>
    public sealed class GameBootstrapper : MonoBehaviour
    {
        private const string MainSceneName = "Main";

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
        }

        // 등록을 OnEnable에서 하는 이유: 에디터에서 플레이 중 재컴파일(도메인 리로드)이 일어나면
        // ServiceLocator의 정적 상태가 초기화되는데, 이 오브젝트가 살아 있으면 OnEnable이 다시 불려 재등록된다.
        private void OnEnable()
        {
            if (ActiveTransportMode.IsSteam)
            {
                SteamService.Initialize();
            }

            RegisterServices();
        }

        private void Start()
        {
            if (ActiveTransportMode.IsSteam && SteamService.IsInitialized
                && ServiceLocator.TryGet(out ISteamLobbyService lobbyService))
            {
                ((SteamLobbyService)lobbyService).AttachSessionLifecycle(NetworkManager.Singleton);

                // 게임 미실행 상태의 초대 수락 — Steam이 붙여준 로비로 부팅 직후 자동 참가한다.
                if (NetworkTransportModeResolver.TryGetConnectLobbyId(
                        Environment.GetCommandLineArgs(), out ulong lobbyId))
                {
                    lobbyService.JoinLobby(lobbyId);
                }
            }

            SceneManager.LoadScene(MainSceneName);
        }

        private void Update()
        {
            SteamService.RunCallbacks();
        }

        private void OnApplicationQuit()
        {
            SteamService.Shutdown();
        }

        private static void RegisterServices()
        {
            if (!ServiceLocator.IsRegistered<IPlayerIdentityProvider>())
            {
                // M6 1차가 격리해 둔 유일 교체 지점 — Steam 모드면 SteamID64, 아니면 로컬 GUID.
                ServiceLocator.Register<IPlayerIdentityProvider>(ActiveTransportMode.IsSteam
                    ? new SteamIdentityProvider()
                    : (IPlayerIdentityProvider)new LocalGuidIdentityProvider());
            }

            if (!ServiceLocator.IsRegistered<IConnectionIdentityRegistry>())
            {
                ServiceLocator.Register<IConnectionIdentityRegistry>(new ConnectionIdentityRegistry());
            }

            if (!ServiceLocator.IsRegistered<INetworkSessionService>())
            {
                ServiceLocator.Register<INetworkSessionService>(new NgoNetworkSessionService(
                    ServiceLocator.Get<IPlayerIdentityProvider>(),
                    (ConnectionIdentityRegistry)ServiceLocator.Get<IConnectionIdentityRegistry>()));
            }

            if (ActiveTransportMode.IsSteam && SteamService.IsInitialized
                && !ServiceLocator.IsRegistered<ISteamLobbyService>())
            {
                ServiceLocator.Register<ISteamLobbyService>(new SteamLobbyService());
            }
        }
    }
}
