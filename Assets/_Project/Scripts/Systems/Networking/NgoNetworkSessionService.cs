using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Game.Systems.Networking
{
    /// <summary>
    /// NGO 기반 <see cref="INetworkSessionService"/> 구현.
    /// Boot 씬의 NetworkManager(+UnityTransport)를 전제로 하며, 트랜스포트 전환(개발=UnityTransport
    /// 직결 / 릴리스=Steam 릴레이)은 이 구현 내부에 격리한다.
    /// 재접속 식별(M6 1차): 시작 전에 <see cref="IPlayerIdentityProvider"/> 토큰을 승인 페이로드
    /// (NetworkConfig.ConnectionData)에 탑재하고, 호스트는 승인 콜백에서 토큰 ↔ clientId 매핑을
    /// <see cref="ConnectionIdentityRegistry"/>에 기록한다.
    /// </summary>
    public sealed class NgoNetworkSessionService : INetworkSessionService
    {
        // 승인 페이로드 방어 상한 — 토큰(GUID "N" 32자)보다 충분히 크고, 비정상 페이로드는 무시한다.
        private const int MaxTokenPayloadBytes = 256;

        private readonly IPlayerIdentityProvider _identityProvider;
        private readonly ConnectionIdentityRegistry _identityRegistry;

        public NgoNetworkSessionService(
            IPlayerIdentityProvider identityProvider, ConnectionIdentityRegistry identityRegistry)
        {
            _identityProvider = identityProvider;
            _identityRegistry = identityRegistry;
        }

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

            if (!networkManager.NetworkConfig.ConnectionApproval)
            {
                Debug.LogWarning(
                    "[NgoNetworkSessionService] ConnectionApproval이 꺼져 있습니다 — 재접속 식별이 동작하지 않습니다. "
                    + "Boot 씬 NetworkManager 설정을 확인하세요.");
            }

            _identityRegistry.Clear();
            LoadTokenPayload(networkManager);
            networkManager.ConnectionApprovalCallback = HandleConnectionApproval;
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

            // 주소 설정 — 승인 페이로드(NetworkConfig.ConnectionData)와는 이름만 비슷한 별개다.
            transport.SetConnectionData(address, port);
            LoadTokenPayload(networkManager);
            return networkManager.StartClient();
        }

        public bool LoadGameplayScene(string sceneName)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsHost || networkManager.SceneManager == null)
            {
                Debug.LogError("[NgoNetworkSessionService] 호스트 세션이 아니어서 씬 전환을 요청할 수 없습니다.");
                return false;
            }

            return networkManager.SceneManager.LoadScene(sceneName, UnityEngine.SceneManagement.LoadSceneMode.Single)
                   == SceneEventProgressStatus.Started;
        }

        public void Shutdown()
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
            }
        }

        private void LoadTokenPayload(NetworkManager networkManager)
        {
            string token = _identityProvider.LocalPlayerToken;
            networkManager.NetworkConfig.ConnectionData =
                string.IsNullOrEmpty(token) ? null : Encoding.UTF8.GetBytes(token);
        }

        /// <summary>
        /// 승인 정책은 "전부 승인" — 콜백의 목적은 토큰 ↔ clientId 매핑 기록과 중복 접속 정리다.
        /// 승인을 켜면 자동 스폰이 콜백 응답에 위임되므로 CreatePlayerObject를 직접 켜야 한다.
        /// </summary>
        private void HandleConnectionApproval(
            NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            string token = DecodeToken(request.Payload);

            if (!string.IsNullOrEmpty(token))
            {
                // 결정 ⑥: 같은 토큰의 살아있는(유령) 연결은 새 접속 우선으로 정리한다.
                // DisconnectClient가 despawn → 스냅샷 캡처를 이 자리에서 동기로 완료시킨 뒤 승인되므로,
                // 캡처가 새 플레이어 스폰보다 항상 앞선다.
                if (networkManager != null
                    && _identityRegistry.TryGetClientId(token, out ulong existingClientId)
                    && existingClientId != request.ClientNetworkId
                    && networkManager.ConnectedClients.ContainsKey(existingClientId))
                {
                    Debug.Log(
                        $"[NgoNetworkSessionService] 중복 식별 토큰 접속 — 기존 연결(clientId={existingClientId})을 "
                        + $"킥하고 새 접속(clientId={request.ClientNetworkId})을 승인합니다.");
                    networkManager.DisconnectClient(existingClientId, "duplicate identity reconnect");
                }

                _identityRegistry.Record(request.ClientNetworkId, token);
            }

            response.Approved = true;
            response.CreatePlayerObject = true;
        }

        private static string DecodeToken(byte[] payload)
        {
            if (payload == null || payload.Length == 0 || payload.Length > MaxTokenPayloadBytes)
            {
                return null;
            }

            return Encoding.UTF8.GetString(payload);
        }
    }
}
