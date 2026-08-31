using System;
using Game.Core.Services;
using NUnit.Framework;
using UnityEngine;

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

        // 파괴 판정 검증용 — 서비스는 인터페이스로 등록되므로 사용처의 == null이
        // Unity의 파괴 오버로드를 타지 않는다. 등록소가 대신 걸러야 한다.
        private class TestServiceBehaviour : MonoBehaviour, ITestService
        {
        }

        private static TestServiceBehaviour CreateBehaviour()
        {
            return new GameObject(nameof(TestServiceBehaviour)).AddComponent<TestServiceBehaviour>();
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

        [Test]
        public void 파괴된_컴포넌트_서비스는_TryGet_이_false_를_반환한다()
        {
            TestServiceBehaviour service = CreateBehaviour();
            ServiceLocator.Register<ITestService>(service);
            UnityEngine.Object.DestroyImmediate(service.gameObject);

            // 해제(Unregister)를 못 받고 파괴된 등록은 없는 것으로 봐야 한다 — 넘겨주면
            // 사용처가 이미 해제된 상태(NetworkList 등)를 건드린다.
            Assert.IsFalse(ServiceLocator.TryGet(out ITestService found));
            Assert.IsNull(found);
            Assert.IsFalse(ServiceLocator.IsRegistered<ITestService>());
        }

        [Test]
        public void 파괴된_컴포넌트_서비스_자리에는_다시_Register_할_수_있다()
        {
            TestServiceBehaviour destroyed = CreateBehaviour();
            ServiceLocator.Register<ITestService>(destroyed);
            UnityEngine.Object.DestroyImmediate(destroyed.gameObject);

            var replacement = new TestService();
            Assert.DoesNotThrow(() => ServiceLocator.Register<ITestService>(replacement));
            Assert.AreSame(replacement, ServiceLocator.Get<ITestService>());
        }
    }
}
