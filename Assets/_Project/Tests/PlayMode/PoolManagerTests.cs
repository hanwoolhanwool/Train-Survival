using System.Collections;
using Game.Core.Pooling;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class PoolManagerTests
    {
        private GameObject _template;

        [SetUp]
        public void SetUp()
        {
            _template = new GameObject("PoolTemplate");
        }

        [TearDown]
        public void TearDown()
        {
            if (PoolManager.HasInstance)
            {
                Object.Destroy(PoolManager.Instance.gameObject);
            }

            Object.Destroy(_template);
        }

        [UnityTest]
        public IEnumerator Despawn_후_Spawn_은_인스턴스를_재사용한다()
        {
            GameObject first = PoolManager.Spawn(_template, Vector3.zero, Quaternion.identity);
            Assert.IsTrue(first.activeSelf);

            PoolManager.Despawn(first);
            Assert.IsFalse(first.activeSelf);
            yield return null;

            GameObject second = PoolManager.Spawn(_template, Vector3.one, Quaternion.identity);

            Assert.AreSame(first, second);
            Assert.IsTrue(second.activeSelf);
            Assert.AreEqual(Vector3.one, second.transform.position);
        }

        [UnityTest]
        public IEnumerator Prewarm_은_비활성_인스턴스를_미리_채운다()
        {
            PoolManager.Prewarm(_template, 3);
            yield return null;

            GameObject spawned = PoolManager.Spawn(_template, Vector3.zero, Quaternion.identity);

            Assert.IsTrue(spawned.activeSelf);
            Assert.AreNotSame(_template, spawned);
        }
    }
}
