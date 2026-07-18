using System;
using System.Collections;
using Game.Core.Pooling;
using Game.Systems.Networking;
using NUnit.Framework;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Game.Tests.PlayMode
{
    public class PooledNetworkPrefabHandlerTests
    {
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _prefab = new GameObject("NetworkPrefabTemplate", typeof(NetworkObject));
            _prefab.GetComponent<NetworkObject>().AutoObjectParentSync = false;
            _prefab.SetActive(false);
        }

        [TearDown]
        public void TearDown()
        {
            if (PoolManager.HasInstance)
            {
                Object.Destroy(PoolManager.Instance.gameObject);
            }

            Object.Destroy(_prefab);
        }

        [Test]
        public void 생성자는_NetworkObject가_없는_프리팹을_거부한다()
        {
            var invalidPrefab = new GameObject("InvalidPrefab");

            Assert.Throws<ArgumentException>(() => new PooledNetworkPrefabHandler(invalidPrefab));
            Assert.Throws<ArgumentNullException>(() => new PooledNetworkPrefabHandler(null));

            Object.Destroy(invalidPrefab);
        }

        [Test]
        public void 생성자는_AutoObjectParentSync가_켜진_프리팹을_거부한다()
        {
            var parentSyncPrefab = new GameObject("ParentSyncPrefab", typeof(NetworkObject));

            Assert.Throws<ArgumentException>(() => new PooledNetworkPrefabHandler(parentSyncPrefab));

            Object.Destroy(parentSyncPrefab);
        }

        [Test]
        public void Instantiate_는_풀을_경유해_NetworkObject_인스턴스를_반환한다()
        {
            var handler = new PooledNetworkPrefabHandler(_prefab);

            NetworkObject instance = handler.Instantiate(0, Vector3.one, Quaternion.identity);

            Assert.IsNotNull(instance);
            Assert.IsTrue(instance.gameObject.activeSelf);
            Assert.AreEqual(Vector3.one, instance.transform.position);

            handler.Destroy(instance);
        }

        [UnityTest]
        public IEnumerator Destroy_후_Instantiate_는_인스턴스를_재사용한다()
        {
            var handler = new PooledNetworkPrefabHandler(_prefab);

            NetworkObject first = handler.Instantiate(0, Vector3.zero, Quaternion.identity);
            handler.Destroy(first);
            Assert.IsFalse(first.gameObject.activeSelf);
            yield return null;

            NetworkObject second = handler.Instantiate(0, Vector3.one, Quaternion.identity);

            Assert.AreSame(first, second);
            Assert.IsTrue(second.gameObject.activeSelf);

            handler.Destroy(second);
        }
    }
}
