using Game.Core.Services;
using Game.Systems.Networking;
using Game.Systems.Networking.Steam;
using UnityEngine;

namespace Game.UI.MainMenu
{
    /// <summary>
    /// 메뉴가 실제로 무엇을 하는가 — 호스트 시작·친구 참가·인게임 씬 선택.
    /// [로비·메인 메뉴 구현 계획](docs/plans/features/로비-메인메뉴-구현-계획.md) §5.2에서 옮겨온 로직이다.
    ///
    /// <para><b>게임 시작은 언제나 세션 서비스를 거친다.</b> 1인 플레이도 "혼자 호스트인 세션"이라
    /// (개발 원칙 2) 여기에 예외 경로를 두지 않는다 — 두면 혼자 할 때만 재현되는 버그가 생긴다.</para>
    ///
    /// <para><b>Steam 분기는 그대로 유지한다.</b> Steam 모드에서 호스트 시작은 친구 전용 로비 생성을
    /// 겸하고, 참가는 오버레이 초대로만 이뤄진다(M6 2차). 직결 모드에서는 주소로 접속한다.</para>
    ///
    /// <para>화면을 그리지 않는다 — 무엇을 보여줄지는 <see cref="MainMenuRoot"/>가 정하고,
    /// 여기서는 "지금 할 수 있는가"(<see cref="IsReady"/>)와 결과만 돌려준다.</para>
    /// </summary>
    public sealed class MenuSessionActions : MonoBehaviour
    {
        /// <summary>직결 모드 기본 접속 주소 — 같은 PC에서 두 벌 띄워 보는 개발 흐름의 기본값.</summary>
        public const string DefaultAddress = "127.0.0.1";

        /// <summary>직결 모드 기본 포트.</summary>
        public const ushort DefaultPort = 7777;

        /// <summary>세션 서비스가 등록됐고 아직 세션이 열리지 않았는가 — 메뉴를 누를 수 있는 조건.</summary>
        public bool IsReady
        {
            get
            {
                INetworkSessionService session = Session;
                return session != null && !session.IsSessionActive;
            }
        }

        /// <summary>이번 실행이 Steam 릴레이 모드인가.</summary>
        public bool IsSteamMode => ActiveTransportMode.IsSteam;

        /// <summary>Steam 로비 서비스가 살아 있는가 — 초기화에 실패하면 초대를 열 수 없다.</summary>
        public bool IsSteamReady => ServiceLocator.TryGet(out ISteamLobbyService _);

        /// <summary>이번에 들어갈 인게임 씬 이름.</summary>
        public string GameplayScene => GameplaySceneRoute.Current;

        /// <summary>토글하면 갈 반대편 씬 이름.</summary>
        public string OtherGameplayScene => GameplaySceneRoute.Other;

        private static INetworkSessionService Session
        {
            get { return ServiceLocator.TryGet(out INetworkSessionService session) ? session : null; }
        }

        /// <summary>
        /// 새 여정 — 호스트 세션을 열고 인게임 씬을 로드한다.
        /// Steam 모드에서는 호스트 시작이 친구 전용 로비 생성을 겸한다.
        /// </summary>
        public bool StartNewJourney()
        {
            INetworkSessionService session = Session;
            if (session == null || session.IsSessionActive)
            {
                return false;
            }

            if (!session.StartHost())
            {
                return false;
            }

            if (ActiveTransportMode.IsSteam && ServiceLocator.TryGet(out ISteamLobbyService lobby))
            {
                lobby.CreateLobby();
            }

            return session.LoadGameplayScene(GameplaySceneRoute.Current);
        }

        /// <summary>
        /// 친구 참가 — Steam 모드에서는 오버레이 초대 창을 연다.
        /// 참가 자체는 초대 수락 쪽에서 이뤄지므로 여기서는 창만 연다.
        /// </summary>
        public bool InviteFriends()
        {
            if (!ActiveTransportMode.IsSteam)
            {
                return false;
            }

            if (!ServiceLocator.TryGet(out ISteamLobbyService lobby))
            {
                return false;
            }

            lobby.OpenInviteOverlay();
            return true;
        }

        /// <summary>직결 모드 참가 — 주소가 비면 기본값을 쓴다.</summary>
        public bool JoinDirect(string address, ushort port)
        {
            INetworkSessionService session = Session;
            if (session == null || session.IsSessionActive)
            {
                return false;
            }

            string target = string.IsNullOrWhiteSpace(address) ? DefaultAddress : address.Trim();
            return session.StartClient(target, port == 0 ? DefaultPort : port);
        }

        /// <summary>시작할 인게임 씬을 고른다 — 호스트만 고르고 클라이언트는 씬 동기화로 따라온다.</summary>
        public void SelectGameplayScene(string sceneName)
        {
            GameplaySceneRoute.Select(sceneName);
        }

        /// <summary>기본 편성과 아트 검증 편성을 오간다.</summary>
        public void ToggleGameplayScene()
        {
            GameplaySceneRoute.Select(GameplaySceneRoute.Other);
        }
    }
}
