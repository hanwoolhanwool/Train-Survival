using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Systems.Networking
{
    /// <summary>
    /// 풀링할 네트워크 프리팹 목록과 프리웜 수량 설정.
    /// 에셋은 <c>Assets/_Project/Data/</c>에 두고 <see cref="NetworkPrefabPoolRegistrar"/>에 연결한다.
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkPoolConfig", menuName = "Game/Network Pool Config")]
    public sealed class NetworkPoolConfig : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            [SerializeField] private GameObject _prefab;
            [SerializeField, Min(0)] private int _prewarmCount;

            public GameObject Prefab => _prefab;

            public int PrewarmCount => _prewarmCount;
        }

        [SerializeField] private List<Entry> _entries = new List<Entry>();

        public IReadOnlyList<Entry> Entries => _entries;
    }
}
