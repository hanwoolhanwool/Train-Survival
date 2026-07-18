using System.Collections.Generic;
using Game.Core.Pooling;
using Unity.Netcode;
using UnityEngine;

namespace Game.Systems.Networking
{
    /// <summary>
    /// <see cref="NetworkPoolConfig"/>의 프리팹들을 NGO PrefabHandler에 일괄 등록해
    /// 네트워크 스폰/디스폰이 <see cref="PoolManager"/>를 경유하게 연결한다.
    /// Boot 씬에서 NetworkManager와 함께 배치한다.
    /// </summary>
    /// <remarks>
    /// 핸들러 등록과 별개로, 각 프리팹은 NetworkManager의 Network Prefabs 목록에도
    /// 포함되어 있어야 스폰할 수 있다.
    /// </remarks>
    public sealed class NetworkPrefabPoolRegistrar : MonoBehaviour
    {
        [SerializeField] private NetworkPoolConfig _config;

        private readonly List<GameObject> _registeredPrefabs = new List<GameObject>();

        private NetworkManager _networkManager;

        private void Start()
        {
            _networkManager = NetworkManager.Singleton;
            if (_networkManager == null)
            {
                Debug.LogError("[NetworkPrefabPoolRegistrar] NetworkManager가 없습니다. Boot 씬 구성을 확인하세요.", this);
                return;
            }

            if (_config == null)
            {
                Debug.LogError("[NetworkPrefabPoolRegistrar] NetworkPoolConfig가 연결되지 않았습니다.", this);
                return;
            }

            RegisterAll();
        }

        private void OnDestroy()
        {
            UnregisterAll();
        }

        private void RegisterAll()
        {
            for (int i = 0; i < _config.Entries.Count; i++)
            {
                NetworkPoolConfig.Entry entry = _config.Entries[i];
                if (entry.Prefab == null)
                {
                    Debug.LogWarning($"[NetworkPrefabPoolRegistrar] 비어 있는 항목을 건너뜁니다 (index {i}).", this);
                    continue;
                }

                if (!_networkManager.PrefabHandler.AddHandler(entry.Prefab, new PooledNetworkPrefabHandler(entry.Prefab)))
                {
                    Debug.LogWarning($"[NetworkPrefabPoolRegistrar] 핸들러 등록 실패: {entry.Prefab.name}", this);
                    continue;
                }

                _registeredPrefabs.Add(entry.Prefab);

                if (entry.PrewarmCount > 0)
                {
                    PoolManager.Prewarm(entry.Prefab, entry.PrewarmCount);
                }
            }
        }

        private void UnregisterAll()
        {
            if (_networkManager == null)
            {
                return;
            }

            for (int i = 0; i < _registeredPrefabs.Count; i++)
            {
                _networkManager.PrefabHandler.RemoveHandler(_registeredPrefabs[i]);
            }

            _registeredPrefabs.Clear();
        }
    }
}
