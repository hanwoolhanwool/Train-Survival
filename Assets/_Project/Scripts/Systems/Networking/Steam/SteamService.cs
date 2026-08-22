using Game.Core.Logging;
using Steamworks;
using UnityEngine;

namespace Game.Systems.Networking.Steam
{
    /// <summary>
    /// Steam API 수명 주기 — Steam 모드에서만 초기화·콜백 펌프·종료한다 (M6 2차 §2.3).
    /// 펌프는 GameBootstrapper.Update가 매 프레임 호출한다. 초기화 실패는 UnityTransport로
    /// 폴백하지 않고 실패로 드러낸다 — 모드는 실행 인자로 명시된 의도다.
    /// </summary>
    public static class SteamService
    {
        public static bool IsInitialized { get; private set; }

        public static bool Initialize()
        {
            if (IsInitialized)
            {
                return true;
            }

            if (!SteamAPI.Init())
            {
                GameLog.Error(LogCategory.Steam, "SteamAPI.Init 실패 — Steam 클라이언트 로그인 상태와 "
                                      + "steam_appid.txt(개발: 480)를 확인하세요. Steam 모드는 폴백하지 않습니다.");
                return false;
            }

            IsInitialized = true;
            GameLog.Info(LogCategory.Steam, $"초기화 완료 — {SteamFriends.GetPersonaName()} "
                                      + $"({SteamUser.GetSteamID().m_SteamID})");
            return true;
        }

        public static void RunCallbacks()
        {
            if (IsInitialized)
            {
                SteamAPI.RunCallbacks();
            }
        }

        public static void Shutdown()
        {
            if (IsInitialized)
            {
                SteamAPI.Shutdown();
                IsInitialized = false;
            }
        }
    }
}
