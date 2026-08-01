using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Region;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지형 타일을 전방 생성 → 후방 회수로 스트리밍한다 (PoolManager 경유, 네트워크 문서 §4.1).
    /// 누적 주행 거리가 전 피어 공통 기준값이므로 타일 자체는 네트워크 동기화 없이 각자 로컬 구동한다.
    /// 지역이 바뀌면 이후 생성되는 타일만 새 지역 프리팹으로 바뀌고 기존 타일은 뒤로 흘러가며 회수된다 —
    /// 전방 타일이 순차 교체되므로 "지역 경계를 지나는" 전환이 자연스럽게 표현된다 (M4).
    /// </summary>
    public sealed class TerrainTileStreamer : MonoBehaviour
    {
        [SerializeField] private WorldScrollSettings _settings;

        [Tooltip("지역 데이터에 지형 프리팹이 없을 때 쓰는 기본 타일.")]
        [SerializeField] private GameObject _tilePrefab;

        private static readonly List<int> RemovalBuffer = new List<int>(8);

        private readonly Dictionary<int, GameObject> _activeTiles = new Dictionary<int, GameObject>();

        private GameObject _activeTilePrefab;

        private void OnEnable()
        {
            EventBus<RegionChangedEvent>.Subscribe(OnRegionChanged);

            // 이 컴포넌트가 지역 전환보다 늦게 켜졌어도 현재 지역 지형에서 시작하도록 한 번 맞춘다.
            _activeTilePrefab = _tilePrefab;
            if (ServiceLocator.TryGet(out IRegionService region))
            {
                ApplyRegion(region.CurrentRegion);
            }
        }

        private void OnRegionChanged(RegionChangedEvent evt)
        {
            ApplyRegion(evt.Region);
        }

        private void ApplyRegion(RegionDefinition region)
        {
            GameObject prefab = region == null || region.TerrainTilePrefab == null
                ? _tilePrefab
                : region.TerrainTilePrefab;

            if (ReferenceEquals(prefab, _activeTilePrefab))
            {
                return;
            }

            _activeTilePrefab = prefab;
            Debug.Log($"[TerrainTileStreamer] 지형 타일 전환: {(region == null ? "기본" : region.DisplayName)} " +
                "— 이후 생성되는 전방 타일부터 반영됩니다.");
        }

        private void Update()
        {
            if (_settings == null || _activeTilePrefab == null)
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
                    _activeTiles.Add(index, PoolManager.Spawn(_activeTilePrefab, new Vector3(0f, 0f, z), Quaternion.identity));
                }
            }
        }

        private void OnDisable()
        {
            EventBus<RegionChangedEvent>.Unsubscribe(OnRegionChanged);

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
