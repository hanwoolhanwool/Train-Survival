using Game.Core.Services;
using Game.Systems.Networking;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Game.Systems
{
    /// <summary>
    /// Boot 씬 진입점. 전역 서비스를 일괄 등록한 뒤 Main 씬으로 진입한다.
    /// 씬 흐름: Boot(인프라 초기화) → Main(타이틀/메뉴) → Game(인게임).
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
            RegisterServices();
        }

        private void Start()
        {
            SceneManager.LoadScene(MainSceneName);
        }

        private static void RegisterServices()
        {
            if (!ServiceLocator.IsRegistered<IPlayerIdentityProvider>())
            {
                ServiceLocator.Register<IPlayerIdentityProvider>(new LocalGuidIdentityProvider());
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
        }
    }
}
