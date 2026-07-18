using Game.Core.Events;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class EventBusTests
    {
        private readonly struct TestEvent
        {
            public readonly int Value;

            public TestEvent(int value)
            {
                Value = value;
            }
        }

        [TearDown]
        public void TearDown()
        {
            EventBus<TestEvent>.Clear();
        }

        [Test]
        public void Publish_구독자가_페이로드를_수신한다()
        {
            int received = 0;
            EventBus<TestEvent>.Subscribe(e => received = e.Value);

            EventBus<TestEvent>.Publish(new TestEvent(42));

            Assert.AreEqual(42, received);
        }

        [Test]
        public void Unsubscribe_해제_후에는_수신하지_않는다()
        {
            int callCount = 0;
            void Handler(TestEvent e) => callCount++;

            EventBus<TestEvent>.Subscribe(Handler);
            EventBus<TestEvent>.Unsubscribe(Handler);

            EventBus<TestEvent>.Publish(new TestEvent(1));

            Assert.AreEqual(0, callCount);
        }

        [Test]
        public void Publish_구독자가_없어도_예외가_발생하지_않는다()
        {
            Assert.DoesNotThrow(() => EventBus<TestEvent>.Publish(new TestEvent(1)));
        }
    }
}
