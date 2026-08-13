using Game.Systems.Meta;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>메타 진행 갱신 규칙 검증 (M6 3차 결정 ③ — 런 결말 기록·업적 플래그).</summary>
    public sealed class MetaProgressOpsTests
    {
        [Test]
        public void 게임오버_기록은_횟수를_누적하고_최고_Day를_갱신한다()
        {
            var progress = new MetaProgress();

            MetaProgressOps.ApplyGameOver(progress, 4);
            MetaProgressOps.ApplyGameOver(progress, 7);

            Assert.That(progress.totalGameOvers, Is.EqualTo(2));
            Assert.That(progress.bestDayReached, Is.EqualTo(7));
        }

        [Test]
        public void 더_낮은_Day의_게임오버는_최고_기록을_내리지_않는다()
        {
            var progress = new MetaProgress { bestDayReached = 9 };

            MetaProgressOps.ApplyGameOver(progress, 2);

            Assert.That(progress.bestDayReached, Is.EqualTo(9));
            Assert.That(progress.totalGameOvers, Is.EqualTo(1));
        }

        [Test]
        public void 업적은_첫_해금만_true고_중복_해금은_무해하다()
        {
            var progress = new MetaProgress();

            Assert.That(MetaProgressOps.Unlock(progress, AchievementIds.FirstGameOver), Is.True);
            Assert.That(MetaProgressOps.Unlock(progress, AchievementIds.FirstGameOver), Is.False);
            Assert.That(progress.unlockedAchievements.Count, Is.EqualTo(1));
            Assert.That(MetaProgressOps.IsUnlocked(progress, AchievementIds.FirstGameOver), Is.True);
        }

        [Test]
        public void 빈_id는_해금되지_않는다()
        {
            var progress = new MetaProgress();

            Assert.That(MetaProgressOps.Unlock(progress, null), Is.False);
            Assert.That(MetaProgressOps.Unlock(progress, string.Empty), Is.False);
            Assert.That(progress.unlockedAchievements, Is.Empty);
        }

        [Test]
        public void 손상되거나_구버전_데이터를_수용한다()
        {
            // 파일 없음·손상 = null, 구버전 스키마 = 누락 필드(null 리스트) — 둘 다 게임을 막지 않는다.
            MetaProgress fromNull = MetaProgressOps.Normalize(null);
            Assert.That(fromNull.unlockedAchievements, Is.Not.Null);
            Assert.That(fromNull.schemaVersion, Is.EqualTo(MetaProgressOps.CurrentSchemaVersion));

            var legacy = new MetaProgress { unlockedAchievements = null, schemaVersion = 0, bestDayReached = 5 };
            MetaProgress normalized = MetaProgressOps.Normalize(legacy);
            Assert.That(normalized.unlockedAchievements, Is.Not.Null);
            Assert.That(normalized.schemaVersion, Is.EqualTo(MetaProgressOps.CurrentSchemaVersion));
            Assert.That(normalized.bestDayReached, Is.EqualTo(5));
        }
    }
}
