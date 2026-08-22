using Game.Core.Logging;
using System.Collections.Generic;
using Game.Systems.Meta;
using Steamworks;
using UnityEngine;

namespace Game.Systems.Networking.Steam
{
    /// <summary>
    /// Steam 업적 미러 데코레이터 (M6 3차 결정 ③) — 로컬(<see cref="MetaProgressService"/>)이
    /// 원천이고, Steam 모드에서 해금이 확정되면 매핑된 Steam 업적으로 미러한다.
    /// AppID 480(Spacewar)에는 자체 업적을 정의할 수 없어 매핑은 스모크용 테스트 업적뿐이다 —
    /// 실 매핑 테이블은 자체 AppID 발급 후 채운다 (§0 비범위).
    /// </summary>
    public sealed class SteamAchievementsMirror : IAchievementService
    {
        // 임시 매핑 (AppID 480 스모크 전용): 첫 게임오버 → Spacewar 테스트 업적.
        // Set→Store 왕복 경로 검증이 목적이다 — 자체 AppID 발급 시 실제 업적 id로 교체한다.
        private static readonly Dictionary<string, string> SteamIdByAchievementId =
            new Dictionary<string, string>
            {
                { AchievementIds.FirstGameOver, "ACH_WIN_ONE_GAME" },
            };

        private readonly IAchievementService _inner;

        public SteamAchievementsMirror(IAchievementService inner)
        {
            _inner = inner;
        }

        public bool IsUnlocked(string achievementId)
        {
            return _inner.IsUnlocked(achievementId);
        }

        public void Unlock(string achievementId)
        {
            _inner.Unlock(achievementId);
            TryMirror(achievementId);
        }

        private static void TryMirror(string achievementId)
        {
            if (!SteamService.IsInitialized
                || !SteamIdByAchievementId.TryGetValue(achievementId, out string steamId))
            {
                return;
            }

            // 미러 실패는 게임을 막지 않는다 — 로컬이 원천이라 다음 해금 때 다시 시도된다.
            if (!SteamUserStats.SetAchievement(steamId) || !SteamUserStats.StoreStats())
            {
                GameLog.Warn(LogCategory.Steam, $"Steam 업적 미러 실패: {achievementId} → {steamId}");
                return;
            }

            GameLog.Info(LogCategory.Steam, $"Steam 업적 미러: {achievementId} → {steamId}");
        }
    }
}
