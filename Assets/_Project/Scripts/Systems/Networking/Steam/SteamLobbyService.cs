using Game.Core.Services;
using Steamworks;
using Unity.Netcode;
using UnityEngine;

namespace Game.Systems.Networking.Steam
{
    /// <summary>
    /// <see cref="ISteamLobbyService"/> 구현 (M6 2차 §2.4). 호스트: 세션 시작 직후 친구 전용
    /// 로비 생성 + 로비 데이터에 호스트 SteamID 기록. 게스트: 초대 수락(실행 중 콜백 또는
    /// `+connect_lobby` 부팅) → 로비 참가 → 호스트 SteamID 조회 → 릴레이 접속.
    /// SteamAPI 초기화 이후에만 생성해야 한다 (콜백 등록이 초기화를 전제).
    /// </summary>
    public sealed class SteamLobbyService : ISteamLobbyService
    {
        private const string HostSteamIdKey = "host_steam_id";
        private const int MaxLobbyMembers = 4;

        private readonly Callback<GameLobbyJoinRequested_t> _joinRequested;
        private readonly CallResult<LobbyCreated_t> _lobbyCreated;
        private readonly CallResult<LobbyEnter_t> _lobbyEntered;

        private CSteamID _currentLobby;
        private bool _sessionLifecycleAttached;

        public bool HasLobby => _currentLobby.IsValid() && _currentLobby.IsLobby();

        public SteamLobbyService()
        {
            _joinRequested = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            _lobbyCreated = CallResult<LobbyCreated_t>.Create(OnLobbyCreated);
            _lobbyEntered = CallResult<LobbyEnter_t>.Create(OnLobbyEntered);
        }

        /// <summary>세션 종료와 로비 이탈을 묶는다 — 호스트(OnServerStopped)·게스트(OnClientStopped) 공통.</summary>
        public void AttachSessionLifecycle(NetworkManager networkManager)
        {
            if (_sessionLifecycleAttached || networkManager == null)
            {
                return;
            }

            networkManager.OnServerStopped += _ => LeaveLobby();
            networkManager.OnClientStopped += _ => LeaveLobby();
            _sessionLifecycleAttached = true;
        }

        public void CreateLobby()
        {
            if (!SteamService.IsInitialized || HasLobby)
            {
                return;
            }

            _lobbyCreated.Set(SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypeFriendsOnly, MaxLobbyMembers));
        }

        public void OpenInviteOverlay()
        {
            if (HasLobby)
            {
                SteamFriends.ActivateGameOverlayInviteDialog(_currentLobby);
            }
        }

        public void JoinLobby(ulong lobbyId)
        {
            if (!SteamService.IsInitialized || lobbyId == 0)
            {
                return;
            }

            if (ServiceLocator.TryGet(out INetworkSessionService session) && session.IsSessionActive)
            {
                Debug.LogWarning("[SteamLobbyService] 세션 중 로비 참가 요청 무시 — 먼저 세션을 종료하세요.");
                return;
            }

            Debug.Log($"[SteamLobbyService] 로비 참가 시도: {lobbyId}");
            _lobbyEntered.Set(SteamMatchmaking.JoinLobby(new CSteamID(lobbyId)));
        }

        public void LeaveLobby()
        {
            if (HasLobby)
            {
                Debug.Log($"[SteamLobbyService] 로비 이탈: {_currentLobby.m_SteamID}");
                SteamMatchmaking.LeaveLobby(_currentLobby);
                _currentLobby = default;
            }
        }

        private void OnLobbyCreated(LobbyCreated_t result, bool ioFailure)
        {
            if (ioFailure || result.m_eResult != EResult.k_EResultOK)
            {
                Debug.LogError($"[SteamLobbyService] 로비 생성 실패 — ioFailure={ioFailure} "
                    + $"result={result.m_eResult}");
                return;
            }

            _currentLobby = new CSteamID(result.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(
                _currentLobby, HostSteamIdKey, SteamUser.GetSteamID().m_SteamID.ToString());
            Debug.Log($"[SteamLobbyService] 친구 전용 로비 생성: {_currentLobby.m_SteamID} — "
                + "오버레이(Shift+Tab) 또는 초대 버튼으로 친구를 부르세요.");
        }

        /// <summary>실행 중 초대 수락·친구 목록 "게임 참가" — Steam이 로비 id를 넘겨준다.</summary>
        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t evt)
        {
            JoinLobby(evt.m_steamIDLobby.m_SteamID);
        }

        private void OnLobbyEntered(LobbyEnter_t result, bool ioFailure)
        {
            if (ioFailure
                || (EChatRoomEnterResponse)result.m_EChatRoomEnterResponse
                    != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                Debug.LogError($"[SteamLobbyService] 로비 입장 실패 — ioFailure={ioFailure} "
                    + $"response={result.m_EChatRoomEnterResponse}");
                return;
            }

            _currentLobby = new CSteamID(result.m_ulSteamIDLobby);

            string hostId = SteamMatchmaking.GetLobbyData(_currentLobby, HostSteamIdKey);
            string me = SteamUser.GetSteamID().m_SteamID.ToString();
            if (string.IsNullOrEmpty(hostId) || hostId == me)
            {
                return;
            }

            if (!ServiceLocator.TryGet(out INetworkSessionService session) || session.IsSessionActive)
            {
                return;
            }

            Debug.Log($"[SteamLobbyService] 로비 입장 완료 — 호스트({hostId})로 릴레이 접속을 시작합니다.");
            if (!session.StartClient(hostId, 0))
            {
                Debug.LogError("[SteamLobbyService] 릴레이 접속 시작 실패.");
            }
        }
    }
}
