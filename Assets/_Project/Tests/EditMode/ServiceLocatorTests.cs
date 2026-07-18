using System;
using Game.Core.Services;
using NUnit.Framework;

namespace Game.Tests.EditMode
{
    public class ServiceLocatorTests
    {
        private interface ITestService
        {
        }

        private class TestService : ITestService
        {
        }

        [TearDown]
        public void TearDown()
        {
            ServiceLocator.Clear();
        }

        [Test]
        public void Register_후_Get_으로_같은_인스턴스를_얻는다()
        {
            var service = new TestService();
            ServiceLocator.Register<ITestService>(service);

            Assert.AreSame(service, ServiceLocator.Get<ITestService>());
        }

        [Test]
        public void 중복_Register_는_예외를_던진다()
        {
            ServiceLocator.Register<ITestService>(new TestService());

            Assert.Throws<InvalidOperationException>(
                () => ServiceLocator.Register<ITestService>(new TestService()));
        }

        [Test]
        public void 미등록_Get_은_예외를_던진다()
        {
            Assert.Throws<InvalidOperationException>(() => ServiceLocator.Get<ITestService>());
        }

        [Test]
        public void TryGet_미등록이면_false_를_반환한다()
        {
            bool found = ServiceLocator.TryGet(out ITestService service);

            Assert.IsFalse(found);
            Assert.IsNull(service);
        }

        [Test]
        public void Unregister_후에는_IsRegistered_가_false_다()
        {
            ServiceLocator.Register<ITestService>(new TestService());
            ServiceLocator.Unregister<ITestService>();

            Assert.IsFalse(ServiceLocator.IsRegistered<ITestService>());
        }
    }
}
