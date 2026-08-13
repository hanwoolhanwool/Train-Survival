namespace Game.Systems.Meta
{
    /// <summary>
    /// 메타 진행·업적의 로컬 구현 — 부팅 시 파일에서 읽고, 변경 즉시 저장한다.
    /// 갱신 규칙은 <see cref="MetaProgressOps"/>(순수), I/O는 <see cref="MetaProgressStore"/>에 위임.
    /// </summary>
    public sealed class MetaProgressService : IMetaProgressService, IAchievementService
    {
        private readonly MetaProgressStore _store;
        private readonly MetaProgress _progress;

        public MetaProgressService(MetaProgressStore store)
        {
            _store = store;
            _progress = store.Load();
        }

        public MetaProgress Current => _progress;

        public void RecordGameOver(int dayReached)
        {
            MetaProgressOps.ApplyGameOver(_progress, dayReached);
            _store.Save(_progress);
        }

        public bool IsUnlocked(string achievementId)
        {
            return MetaProgressOps.IsUnlocked(_progress, achievementId);
        }

        public void Unlock(string achievementId)
        {
            if (MetaProgressOps.Unlock(_progress, achievementId))
            {
                _store.Save(_progress);
            }
        }
    }
}
