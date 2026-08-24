using Game.Gameplay.Train;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 거치 무기 장탄 보관소 검증 (M7 4차 §2.5 — 결정 ③·⑦).
    /// 장탄은 <b>사람이 아니라 무기에</b> 붙는다: 점유가 바뀌어도 남은 탄은 그대로고,
    /// 파괴·철거로 항목이 사라질 때만 소실한다. 설치 직후는 빈 탄창이다 —
    /// 낮에 채워 두고 밤에 소모하는 루프가 여기서 시작한다.
    /// </summary>
    public sealed class MountedMagazineStoreTests
    {
        [Test]
        public void 설치_직후는_빈_탄창이다()
        {
            var store = new MountedMagazineStore();

            Assert.That(store.GetRounds(5), Is.EqualTo(0));
            Assert.That(store.TryConsume(5), Is.False);
        }

        [Test]
        public void 재장전은_빈_약실만큼만_채운다()
        {
            var store = new MountedMagazineStore();

            Assert.That(store.Reload(5, 40, 100), Is.EqualTo(40));
            Assert.That(store.GetRounds(5), Is.EqualTo(40));

            // 만탄에서는 더 채우지 않는다 — 인벤 차감도 0이다.
            Assert.That(store.Reload(5, 40, 100), Is.EqualTo(0));
            Assert.That(store.GetRounds(5), Is.EqualTo(40));
        }

        [Test]
        public void 예비가_모자라면_부분_장전으로_끝난다()
        {
            var store = new MountedMagazineStore();

            Assert.That(store.Reload(5, 40, 12), Is.EqualTo(12));
            Assert.That(store.GetRounds(5), Is.EqualTo(12));
        }

        [Test]
        public void 예비가_없으면_장전하지_않는다()
        {
            var store = new MountedMagazineStore();

            Assert.That(store.Reload(5, 40, 0), Is.EqualTo(0));
            Assert.That(store.GetRounds(5), Is.EqualTo(0));
        }

        [Test]
        public void 발사는_한_발씩_차감하고_바닥에서_멈춘다()
        {
            var store = new MountedMagazineStore();
            store.Reload(5, 40, 2);

            Assert.That(store.TryConsume(5), Is.True);
            Assert.That(store.TryConsume(5), Is.True);
            Assert.That(store.TryConsume(5), Is.False);
            Assert.That(store.GetRounds(5), Is.EqualTo(0));
        }

        [Test]
        public void 무기별_장탄은_섞이지_않는다()
        {
            var store = new MountedMagazineStore();
            store.Reload(5, 40, 10);
            store.Reload(6, 25, 25);

            Assert.That(store.GetRounds(5), Is.EqualTo(10));
            Assert.That(store.GetRounds(6), Is.EqualTo(25));
        }

        [Test]
        public void 점유가_바뀌어도_남은_탄은_무기에_남는다()
        {
            // 보관소는 건축물 Id만 안다 — 누가 붙어 있었는지는 애초에 담지 않는다(§2.5 점유 교대).
            var store = new MountedMagazineStore();
            store.Reload(5, 40, 40);
            store.TryConsume(5);
            store.TryConsume(5);

            Assert.That(store.GetRounds(5), Is.EqualTo(38));
        }

        [Test]
        public void 파괴하면_남은_탄이_소실된다()
        {
            var store = new MountedMagazineStore();
            store.Reload(5, 40, 40);

            store.Clear(5);

            Assert.That(store.GetRounds(5), Is.EqualTo(0));
        }

        [Test]
        public void 세션_초기화는_전부_비운다()
        {
            var store = new MountedMagazineStore();
            store.Reload(5, 40, 40);
            store.Reload(6, 25, 25);

            store.ClearAll();

            Assert.That(store.GetRounds(5), Is.EqualTo(0));
            Assert.That(store.GetRounds(6), Is.EqualTo(0));
        }

        [Test]
        public void 무효_Id는_아무_것도_바꾸지_않는다()
        {
            var store = new MountedMagazineStore();

            Assert.That(store.Reload(0, 40, 40), Is.EqualTo(0));
            Assert.That(store.TryConsume(0), Is.False);
            Assert.That(store.GetRounds(0), Is.EqualTo(0));
        }
    }
}
