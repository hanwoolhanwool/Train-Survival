using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Systems.Networking
{
    /// <summary>
    /// NGO 기반 <see cref="INetworkSessionService"/> 구현.
    /// Boot 씬의 NetworkManager(+UnityTransport)를 전제로 하며, 트랜스포트 전환(개발=UnityTransport
    /// 직결 / 릴리스=Steam 릴레이)은 이 구현 내부에 격리한다.
    /// </summary>
    public sealed class NgoNetworkSessionService : INetworkSessionService
    {
        public bool IsSessionActive
        {
            get
            {
                NetworkManager networkManager = NetworkManager.Singleton;
                return networkManager != null && networkManager.IsListening;
            }
        }

        public bool IsHost => IsSessionActive && NetworkManager.Singleton.IsHost;

        public bool StartHost()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[NgoNetworkSessionService] NetworkManager가 없습니다. Boot 씬 구성을 확인하세요.");
                return false;
            }

            if (networkManager.IsListening)
            {
                return false;
            }

            return networkManager.StartHost();
        }

        public bool StartClient(string address, ushort port)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
            {
                Debug.LogError("[NgoNetworkSessionService] NetworkManager가 없습니다. Boot 씬 구성을 확인하세요.");
                return false;
            }

            if (networkManager.IsListening)
            {
                return false;
            }

            var transport = networkManager.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[NgoNetworkSessionService] UnityTransport가 없습니다. NetworkManager 구성을 확인하세요.");
                return false;
            }

            transport.SetConnectionData(address, port);
            return networkManager.StartClient();
        }

        public void Shutdown()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }
    }
}
