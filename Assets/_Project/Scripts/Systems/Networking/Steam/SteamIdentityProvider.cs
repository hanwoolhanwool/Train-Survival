using Steamworks;
using UnityEngine;

namespace Game.Systems.Networking.Steam
{
    /// <summary>
    /// 릴리스용 <see cref="IPlayerIdentityProvider"/> — 토큰 = SteamID64 (M6 2차 §2.5).
    /// Steam 계정이 곧 플레이어이므로 "세션 간 영속·기기(플레이어) 단위" 계약을 자동 충족한다.
    /// M6 1차가 격리해 둔 유일 교체 지점 — 재접속 축(캡처·복원·중복 킥)은 무수정으로 탄다.
    /// </summary>
    public sealed class SteamIdentityProvider : IPlayerIdentityProvider
    {
        private string _cachedToken;

        public string LocalPlayerToken
        {
            get
            {
                if (string.IsNullOrEmpty(_cachedToken))
                {
                    if (!SteamService.IsInitialized)
                    {
                        Debug.LogWarning("[SteamIdentityProvider] SteamAPI 미초기화 — 식별 토큰 없음. "
                            + "재접속 복귀가 동작하지 않습니다.");
                        return null;
                    }

                    _cachedToken = SteamUser.GetSteamID().m_SteamID.ToString();
                }

                return _cachedToken;
            }
        }
    }
}
