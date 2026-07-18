using System.Collections;
using Game.Systems.Networking;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public class NgoNetworkSessionServiceTests
    {
        private GameObject _networkManagerHost;
        private NgoNetworkSessionService _service;

        [SetUp]
        public void SetUp()
        {
            _networkManagerHost = new GameObject("NetworkManager");
            NetworkManager networkManager = _networkManagerHost.AddComponent<NetworkManager>();
            var transport = _networkManagerHost.AddComponent<UnityTransport>();
            networkManager.NetworkConfig = new NetworkConfig { NetworkTransport = transport };
            _service = new NgoNetworkSessionService();
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            _service.Shutdown();
            yield return null;

            Object.Destroy(_networkManagerHost);
            yield return null;
        }

        [Test]
        public void 세션_시작_전에는_비활성_상태다()
        {
            Assert.IsFalse(_service.IsSessionActive);
            Assert.IsFalse(_service.IsHost);
        }

        [UnityTest]
        public IEnumerator StartHost_는_호스트_세션을_연다()
        {
            bool started = _service.StartHost();
            yield return null;

            Assert.IsTrue(started);
            Assert.IsTrue(_service.IsSessionActive);
            Assert.IsTrue(_service.IsHost);
        }

        [UnityTest]
        public IEnumerator 세션_중_StartHost_재호출은_거부된다()
        {
            _service.StartHost();
            yield return null;

            Assert.IsFalse(_service.StartHost());
        }

        [UnityTest]
        public IEnumerator Shutdown_후에는_비활성_상태로_돌아온다()
        {
            _service.StartHost();
            yield return null;

            _service.Shutdown();
            yield return null;

            Assert.IsFalse(_service.IsSessionActive);
        }
    }
}
