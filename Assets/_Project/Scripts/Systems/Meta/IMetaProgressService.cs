namespace Game.Systems.Meta
{
    /// <summary>
    /// 메타 진행 서비스 계약 (M6 3차 결정 ③) — 각 피어가 자기 로컬 파일에 기록한다
    /// (네트워크 §2.3 "메타 진행만 각자 로컬에 저장").
    /// </summary>
    public interface IMetaProgressService
    {
        /// <summary>현재 메타 진행 (읽기용 — 메인 메뉴 표시 등).</summary>
        MetaProgress Current { get; }

        /// <summary>게임오버 결말 기록 + 저장. dayReached는 호스트 권위 값(GameOverEvent 페이로드)이다.</summary>
        void RecordGameOver(int dayReached);
    }

    /// <summary>
    /// 업적 계약 (M6 3차 결정 ③) — 로컬 플래그가 원천이고, Steam 모드에서는 미러 데코레이터
    /// (<c>SteamAchievementsMirror</c>)가 이 계약을 감싼다.
    /// </summary>
    public interface IAchievementService
    {
        bool IsUnlocked(string achievementId);

        void Unlock(string achievementId);
    }
}
