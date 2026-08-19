using Game.UI;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    /// <summary>
    /// 배너 큐 검증 (비주얼·UI/UX 가이드 §9.2 D계층). 배너가 "예쁘게 뜨는가"는 여기서 알 수 없다 —
    /// 이 테스트가 지키는 것은 <b>동시 표시 상한</b>, <b>급한 것이 이긴다</b>, <b>만료</b> 셋이다.
    /// </summary>
    public sealed class HudBannerQueueTests
    {
        private const float Hold = 4f;

        private HudBannerQueue _queue;
        private HudBanner[] _buffer;

        [SetUp]
        public void SetUp()
        {
            _queue = new HudBannerQueue();
            _buffer = new HudBanner[HudBannerQueue.MaxVisible];
        }

        [Test]
        public void 넣은_배너가_그대로_나온다()
        {
            _queue.Push("칸 파괴", HudBannerPriority.Critical, 0f, Hold);

            Assert.AreEqual(1, _queue.Resolve(0f, _buffer));
            Assert.AreEqual("칸 파괴", _buffer[0].Text);
        }

        [Test]
        public void 유지_시간이_지나면_사라진다()
        {
            _queue.Push("칸 파괴", HudBannerPriority.Critical, 0f, Hold);

            Assert.AreEqual(1, _queue.Resolve(Hold - 0.01f, _buffer), "만료 직전에는 아직 보여야 한다.");
            Assert.AreEqual(0, _queue.Resolve(Hold, _buffer), "만료 시각에는 사라진다.");
        }

        /// <summary>가이드 §9.2 — "동시에 2개를 넘기지 않는다".</summary>
        [Test]
        public void 동시에_두_개를_넘기지_않는다()
        {
            for (int i = 0; i < 5; i++)
            {
                _queue.Push($"사건 {i}", HudBannerPriority.Notice, 0f, Hold);
            }

            Assert.AreEqual(HudBannerQueue.MaxVisible, _queue.Resolve(0f, _buffer));
        }

        [Test]
        public void 급한_것이_먼저_자리를_차지한다()
        {
            _queue.Push("지역 진입", HudBannerPriority.Notice, 0f, Hold);
            _queue.Push("날씨 발생", HudBannerPriority.Warning, 0f, Hold);
            _queue.Push("칸 파괴", HudBannerPriority.Critical, 0f, Hold);

            Assert.AreEqual(2, _queue.Resolve(0f, _buffer));
            Assert.AreEqual("칸 파괴", _buffer[0].Text, "가장 급한 것이 맨 위여야 한다.");
            Assert.AreEqual("날씨 발생", _buffer[1].Text);
        }

        /// <summary>사건은 최근 것이 중요하다 — 같은 급함이면 새로 들어온 쪽이 위로 간다.</summary>
        [Test]
        public void 같은_급함이면_최신이_이긴다()
        {
            _queue.Push("먼저", HudBannerPriority.Critical, 0f, Hold);
            _queue.Push("나중", HudBannerPriority.Critical, 1f, Hold);

            Assert.AreEqual(2, _queue.Resolve(1f, _buffer));
            Assert.AreEqual("나중", _buffer[0].Text);
            Assert.AreEqual("먼저", _buffer[1].Text);
        }

        /// <summary>
        /// 급한 배너에 밀려 안 보이던 것도, 그 배너가 만료되면 <b>자기 차례가 온다</b>.
        /// 밀린 순간 버려지면 "칸 파괴" 3연발에 지역 진입 안내가 영영 사라진다.
        /// </summary>
        [Test]
        public void 밀렸던_배너도_자리가_나면_보인다()
        {
            _queue.Push("지역 진입", HudBannerPriority.Notice, 0f, 10f);
            _queue.Push("칸 파괴 1", HudBannerPriority.Critical, 0f, 1f);
            _queue.Push("칸 파괴 2", HudBannerPriority.Critical, 0f, 1f);

            Assert.AreEqual(2, _queue.Resolve(0f, _buffer));
            Assert.AreNotEqual("지역 진입", _buffer[0].Text);
            Assert.AreNotEqual("지역 진입", _buffer[1].Text);

            Assert.AreEqual(1, _queue.Resolve(2f, _buffer), "급한 것이 만료된 뒤에는 하나만 남는다.");
            Assert.AreEqual("지역 진입", _buffer[0].Text);
        }

        [Test]
        public void 보관_한도를_넘으면_가장_약한_것부터_버린다()
        {
            _queue.Push("약한 것", HudBannerPriority.Notice, 0f, 100f);
            for (int i = 0; i < 12; i++)
            {
                _queue.Push($"급한 것 {i}", HudBannerPriority.Critical, 0f, 100f);
            }

            Assert.That(_queue.StoredCount, Is.LessThanOrEqualTo(8), "보관량이 무한히 늘면 안 된다.");

            _queue.Resolve(0f, _buffer);
            Assert.AreNotEqual("약한 것", _buffer[0].Text);
            Assert.AreNotEqual("약한 것", _buffer[1].Text);
        }

        [Test]
        public void 빈_텍스트는_자리를_차지하지_않는다()
        {
            _queue.Push(null, HudBannerPriority.Critical, 0f, Hold);
            _queue.Push(string.Empty, HudBannerPriority.Critical, 0f, Hold);

            Assert.AreEqual(0, _queue.StoredCount);
            Assert.AreEqual(0, _queue.Resolve(0f, _buffer));
        }

        [Test]
        public void 유지_시간이_0_이하면_넣지_않는다()
        {
            _queue.Push("사건", HudBannerPriority.Critical, 0f, 0f);
            _queue.Push("사건", HudBannerPriority.Critical, 0f, -1f);

            Assert.AreEqual(0, _queue.StoredCount);
        }

        [Test]
        public void Clear는_모두_비운다()
        {
            _queue.Push("사건", HudBannerPriority.Critical, 0f, Hold);
            _queue.Clear();

            Assert.AreEqual(0, _queue.StoredCount);
            Assert.AreEqual(0, _queue.Resolve(0f, _buffer));
        }

        [Test]
        public void 버퍼가_없거나_작아도_무너지지_않는다()
        {
            _queue.Push("사건 A", HudBannerPriority.Critical, 0f, Hold);
            _queue.Push("사건 B", HudBannerPriority.Critical, 0f, Hold);

            Assert.AreEqual(0, _queue.Resolve(0f, null));
            Assert.AreEqual(1, _queue.Resolve(0f, new HudBanner[1]), "버퍼 크기를 넘겨 쓰지 않는다.");
        }
    }
}
