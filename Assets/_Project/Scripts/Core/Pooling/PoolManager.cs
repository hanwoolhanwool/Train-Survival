using System.Collections.Generic;
using Game.Core.Singletons;
using UnityEngine;

namespace Game.Core.Pooling
{
    /// <summary>
    /// 프리팹 단위 오브젝트 풀. 게임 오브젝트의 스폰/소멸은
    /// Instantiate/Destroy 대신 반드시 <see cref="Spawn(GameObject,Vector3,Quaternion,Transform)"/> /
    /// <see cref="Despawn"/>을 경유한다.
    /// </summary>
    public sealed class PoolManager : MonoSingleton<PoolManager>
    {
        private static readonly List<IPoolable> PoolableBuffer = new List<IPoolable>(16);

        private readonly Dictionary<GameObject, Stack<GameObject>> _pools =
            new Dictionary<GameObject, Stack<GameObject>>();

        private readonly Dictionary<GameObject, GameObject> _instanceToPrefab =
            new Dictionary<GameObject, GameObject>();

        private readonly HashSet<GameObject> _inactiveInstances = new HashSet<GameObject>();

        public static GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent = null)
        {
            return GetOrCreateInstance().SpawnInternal(prefab, position, rotation, parent);
        }

        public static T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parent = null)
            where T : Component
        {
            GameObject instance = Spawn(prefab.gameObject, position, rotation, parent);
            return instance.GetComponent<T>();
        }

        public static void Despawn(GameObject instance)
        {
            if (instance == null)
            {
                return;
            }

            GetOrCreateInstance().DespawnInternal(instance);
        }

        /// <summary>프리팹 인스턴스를 미리 생성해 풀에 채워 둔다. 로딩 구간에서 호출한다.</summary>
        public static void Prewarm(GameObject prefab, int count)
        {
            GetOrCreateInstance().PrewarmInternal(prefab, count);
        }

        private static PoolManager GetOrCreateInstance()
        {
            if (!HasInstance && Instance == null)
            {
                var host = new GameObject(nameof(PoolManager));
                DontDestroyOnLoad(host);
                host.AddComponent<PoolManager>();
            }

            return Instance;
        }

        private GameObject SpawnInternal(GameObject prefab, Vector3 position, Quaternion rotation, Transform parent)
        {
            GameObject instance = null;
            if (_pools.TryGetValue(prefab, out Stack<GameObject> pool))
            {
                // 씬 전환 등으로 이미 파괴된 인스턴스는 건너뛴다.
                while (pool.Count > 0 && instance == null)
                {
                    instance = pool.Pop();
                }
            }

            if (instance == null)
            {
                instance = Instantiate(prefab);
                _instanceToPrefab.Add(instance, prefab);
            }

            _inactiveInstances.Remove(instance);

            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(parent);
            instanceTransform.SetPositionAndRotation(position, rotation);
            instance.SetActive(true);

            instance.GetComponentsInChildren(true, PoolableBuffer);
            for (int i = 0; i < PoolableBuffer.Count; i++)
            {
                PoolableBuffer[i].OnSpawned();
            }

            return instance;
        }

        private void DespawnInternal(GameObject instance)
        {
            if (!_instanceToPrefab.TryGetValue(instance, out GameObject prefab))
            {
                Debug.LogWarning($"[PoolManager] 풀에서 생성되지 않은 오브젝트를 Despawn 했습니다. Destroy로 대체합니다: {instance.name}", instance);
                Destroy(instance);
                return;
            }

            if (!_inactiveInstances.Add(instance))
            {
                Debug.LogWarning($"[PoolManager] 이미 풀에 반환된 오브젝트입니다: {instance.name}", instance);
                return;
            }

            instance.GetComponentsInChildren(true, PoolableBuffer);
            for (int i = 0; i < PoolableBuffer.Count; i++)
            {
                PoolableBuffer[i].OnDespawned();
            }

            instance.SetActive(false);
            instance.transform.SetParent(transform);

            GetPool(prefab).Push(instance);
        }

        private void PrewarmInternal(GameObject prefab, int count)
        {
            Stack<GameObject> pool = GetPool(prefab);
            for (int i = 0; i < count; i++)
            {
                GameObject instance = Instantiate(prefab, transform);
                instance.SetActive(false);
                _instanceToPrefab.Add(instance, prefab);
                _inactiveInstances.Add(instance);
                pool.Push(instance);
            }
        }

        private Stack<GameObject> GetPool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out Stack<GameObject> pool))
            {
                pool = new Stack<GameObject>();
                _pools.Add(prefab, pool);
            }

            return pool;
        }
    }
}
