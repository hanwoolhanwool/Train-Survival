using System.Collections.Generic;
using Game.Core.Pooling;
using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지형 타일을 전방 생성 → 후방 회수로 스트리밍한다 (PoolManager 경유, 네트워크 문서 §4.1).
    /// 누적 주행 거리가 전 피어 공통 기준값이므로 타일 자체는 네트워크 동기화 없이 각자 로컬 구동한다.
    /// </summary>
    public sealed class TerrainTileStreamer : MonoBehaviour
    {
        [SerializeField] private WorldScrollSettings _settings;
        [SerializeField] private GameObject _tilePrefab;

        private static readonly List<int> RemovalBuffer = new List<int>(8);

        private readonly Dictionary<int, GameObject> _activeTiles = new Dictionary<int, GameObject>();

        private void Update()
        {
            if (_settings == null || _tilePrefab == null)
            {
                return;
            }

            if (!ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                return;
            }

            float distance = scroll.TraveledDistance;
            TileStreamingLogic.GetVisibleRange(
                distance, _settings.TileLength, _settings.TilesAhead, _settings.TilesBehind,
                out int first, out int last);

            RemovalBuffer.Clear();
            foreach (KeyValuePair<int, GameObject> pair in _activeTiles)
            {
                if (pair.Key < first || pair.Key > last)
                {
                    RemovalBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < RemovalBuffer.Count; i++)
            {
                PoolManager.Despawn(_activeTiles[RemovalBuffer[i]]);
                _activeTiles.Remove(RemovalBuffer[i]);
            }

            for (int index = first; index <= last; index++)
            {
                float z = TileStreamingLogic.GetTileZ(index, _settings.TileLength, distance);
                if (_activeTiles.TryGetValue(index, out GameObject tile))
                {
                    Vector3 position = tile.transform.position;
                    position.z = z;
                    tile.transform.position = position;
                }
                else
                {
                    _activeTiles.Add(index, PoolManager.Spawn(_tilePrefab, new Vector3(0f, 0f, z), Quaternion.identity));
                }
            }
        }

        private void OnDisable()
        {
            foreach (KeyValuePair<int, GameObject> pair in _activeTiles)
            {
                if (pair.Value != null)
                {
                    PoolManager.Despawn(pair.Value);
                }
            }

            _activeTiles.Clear();
        }
    }
}
