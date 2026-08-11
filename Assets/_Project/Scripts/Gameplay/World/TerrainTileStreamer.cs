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
    /// 타일의 지역(프리팹)은 복제된 지역 전환 경계(M6 1차 §2.4 — <see cref="ITerrainBoundaryService"/>)로
    /// 인덱스별 결정한다 — 후발 피어도 과거 구간 타일을 당시 지역 프리팹으로 생성하고, 경계 수신
    /// 시점에는 이미 깔린 타일을 재판정해 어긋난 것을 교체한다. 경계 기록이 없으면 현행대로
    /// "현재 지역 프리팹"이다 (M4 동작 유지).
    /// </summary>
    public sealed class TerrainTileStreamer : MonoBehaviour
    {
        [SerializeField] private WorldScrollSettings _settings;

        [Tooltip("지역 데이터에 지형 프리팹이 없을 때 쓰는 기본 타일.")]
        [SerializeField] private GameObject _tilePrefab;

        private static readonly List<int> RemovalBuffer = new List<int>(8);

        private readonly Dictionary<int, GameObject> _activeTiles = new Dictionary<int, GameObject>();

        // 재판정용 — 각 타일이 어떤 프리팹으로 생성됐는지. _activeTiles와 키를 함께 관리한다.
        private readonly Dictionary<int, GameObject> _tilePrefabsByIndex = new Dictionary<int, GameObject>();

        private GameObject _activeTilePrefab;
        private bool _rejudgePending;

        private void OnEnable()
        {
            EventBus<RegionChangedEvent>.Subscribe(OnRegionChanged);
            EventBus<TerrainRegionBoundariesChangedEvent>.Subscribe(OnBoundariesChanged);

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

        private void OnBoundariesChanged(TerrainRegionBoundariesChangedEvent evt)
        {
            // 경계 목록 수신 시점의 기존 타일 재판정 (§2.4-4) — 다음 Update에서 일괄 교체한다.
            _rejudgePending = true;
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

        /// <summary>
        /// 타일 인덱스의 지형 프리팹 — 복제된 경계 기록이 결정하고, 기록이 없는 구간은
        /// 현재 지역 프리팹(현행 동작)이다.
        /// </summary>
        private GameObject ResolveTilePrefab(int index)
        {
            if (ServiceLocator.TryGet(out ITerrainBoundaryService boundaries))
            {
                int regionIndex = boundaries.ResolveRegionIndex(index);
                if (regionIndex >= 0 && ServiceLocator.TryGet(out IRegionService region))
                {
                    RegionDefinition definition = region.GetRegion(regionIndex);
                    if (definition != null && definition.TerrainTilePrefab != null)
                    {
                        return definition.TerrainTilePrefab;
                    }

                    return _tilePrefab;
                }
            }

            return _activeTilePrefab;
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
                DespawnTile(RemovalBuffer[i]);
            }

            if (_rejudgePending)
            {
                _rejudgePending = false;
                RejudgeActiveTiles();
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
                    GameObject prefab = ResolveTilePrefab(index);
                    _activeTiles.Add(index, PoolManager.Spawn(prefab, new Vector3(0f, 0f, z), Quaternion.identity));
                    _tilePrefabsByIndex[index] = prefab;
                }
            }
        }

        /// <summary>경계 기록과 어긋난 기존 타일을 회수한다 — 같은 Update의 생성 루프가 올바른
        /// 프리팹으로 즉시 다시 깐다 (despawn → 재spawn).</summary>
        private void RejudgeActiveTiles()
        {
            RemovalBuffer.Clear();
            foreach (KeyValuePair<int, GameObject> pair in _activeTiles)
            {
                if (_tilePrefabsByIndex.TryGetValue(pair.Key, out GameObject used)
                    && !ReferenceEquals(used, ResolveTilePrefab(pair.Key)))
                {
                    RemovalBuffer.Add(pair.Key);
                }
            }

            for (int i = 0; i < RemovalBuffer.Count; i++)
            {
                DespawnTile(RemovalBuffer[i]);
            }

            if (RemovalBuffer.Count > 0)
            {
                Debug.Log($"[TerrainTileStreamer] 지역 경계 재판정 — 타일 {RemovalBuffer.Count}장 교체.");
            }
        }

        private void DespawnTile(int index)
        {
            PoolManager.Despawn(_activeTiles[index]);
            _activeTiles.Remove(index);
            _tilePrefabsByIndex.Remove(index);
        }

        private void OnDisable()
        {
            EventBus<RegionChangedEvent>.Unsubscribe(OnRegionChanged);
            EventBus<TerrainRegionBoundariesChangedEvent>.Unsubscribe(OnBoundariesChanged);

            foreach (KeyValuePair<int, GameObject> pair in _activeTiles)
            {
                if (pair.Value != null)
                {
                    PoolManager.Despawn(pair.Value);
                }
            }

            _activeTiles.Clear();
            _tilePrefabsByIndex.Clear();
        }
    }
}
