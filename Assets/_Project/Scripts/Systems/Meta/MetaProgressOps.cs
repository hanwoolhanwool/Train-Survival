using System.Collections.Generic;
using UnityEngine;

namespace Game.Systems.Meta
{
    /// <summary>메타 진행 갱신 규칙 — 순수 로직 (EditMode 대상). 파일 I/O는 <see cref="MetaProgressStore"/>에 있다.</summary>
    public static class MetaProgressOps
    {
        public const int CurrentSchemaVersion = 1;

        /// <summary>
        /// 읽어 들인 데이터를 사용 가능한 형태로 보정한다 — null(파일 없음·손상)과
        /// 구버전 스키마(누락 필드 = 기본값)를 수용해 저장 파일이 게임을 막지 않게 한다.
        /// </summary>
        public static MetaProgress Normalize(MetaProgress progress)
        {
            if (progress == null)
            {
                return new MetaProgress();
            }

            if (progress.unlockedAchievements == null)
            {
                progress.unlockedAchievements = new List<string>();
            }

            progress.schemaVersion = CurrentSchemaVersion;
            return progress;
        }

        /// <summary>게임오버 결말 기록 — 횟수 누적 + 최고 도달 Day 갱신(내려가지 않는다).</summary>
        public static void ApplyGameOver(MetaProgress progress, int dayReached)
        {
            progress.totalGameOvers += 1;
            progress.bestDayReached = Mathf.Max(progress.bestDayReached, dayReached);
        }

        /// <summary>업적 해금 — 새로 해금됐을 때만 true (중복 해금은 무해한 no-op).</summary>
        public static bool Unlock(MetaProgress progress, string achievementId)
        {
            if (string.IsNullOrEmpty(achievementId) || progress.unlockedAchievements.Contains(achievementId))
            {
                return false;
            }

            progress.unlockedAchievements.Add(achievementId);
            return true;
        }

        public static bool IsUnlocked(MetaProgress progress, string achievementId)
        {
            return progress.unlockedAchievements.Contains(achievementId);
        }
    }
}
