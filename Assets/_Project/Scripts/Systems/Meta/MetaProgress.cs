using System;
using System.Collections.Generic;

namespace Game.Systems.Meta
{
    /// <summary>
    /// 메타 진행 데이터 (M6 3차 결정 ③ — 기획서 §9.1 "업적·수집품 등 메타 진행만 로컬 저장").
    /// JsonUtility 직렬화 대상이라 public 필드다. 런 내 저장은 없다(불변) — 여기 들어가는 것은
    /// 런의 <b>결말</b> 기록과 업적 플래그뿐이다. 수집품 등 확장은 schemaVersion으로 흡수한다.
    /// </summary>
    [Serializable]
    public sealed class MetaProgress
    {
        public int schemaVersion = MetaProgressOps.CurrentSchemaVersion;

        /// <summary>게임오버 시점 기준 최고 도달 Day.</summary>
        public int bestDayReached;

        /// <summary>게임오버(전멸) 횟수.</summary>
        public int totalGameOvers;

        /// <summary>해금된 업적 id 집합 — 로컬이 원천, Steam은 미러다 (결정 ③).</summary>
        public List<string> unlockedAchievements = new List<string>();
    }
}
