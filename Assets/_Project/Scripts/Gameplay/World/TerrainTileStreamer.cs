using Game.Core.Logging;
using System.Collections.Generic;
using Game.Core.Diagnostics;
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
    // 타일에 붙어 있는 것(바다 사다리 등)을 따라가는 플레이어가 이번 프레임 위치를 읽도록,
    // 타일 배치를 플레이어 이동보다 먼저 실행한다 — 이탈 칸이 CarView(-100)로 푼 것과 같은 문제다.
    // 순서가 뒤집히면 플레이어는 한 프레임 전 타일 위치를 읽고, 그 격차가 dt 변동만큼 흔들려 떨린다.
    [DefaultExecutionOrder(-120)]
    public sealed class TerrainTileStreamer : MonoBehaviour
    {
        [SerializeField] private WorldScrollSettings _settings;

        [Tooltip("지역 데이터에 지형 프리팹이 없을 때 쓰는 기본 타일.")]
        [SerializeField] private GameObject _tilePrefab;

        [Tooltip("기차역 시퀀스 (선택) — 비우면 지형이 현행(팔레트 단독) 그대로 굴러간다.")]
        [SerializeField] private StationSequenceSettings _stationSettings;

        private static readonly List<int> RemovalBuffer = new List<int>(8);

        /// <summary>편측 승강장을 반대쪽에 세울 때의 회전 — 궤도가 좌우 대칭이라 이음매는 그대로다.</summary>
        private static readonly Quaternion MirrorRotation = Quaternion.Euler(0f, 180f, 0f);

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
            GameLog.Info(LogCategory.World, $"지형 타일 전환: {(region == null ? "기본" : region.DisplayName)} " +
                                      "— 이후 생성되는 전방 타일부터 반영됩니다.");
        }

        /// <summary>회전이 필요 없는 호출부(재판정)를 위한 편의 오버로드.</summary>
        private GameObject ResolveTilePrefab(int index)
        {
            return ResolveTilePrefab(index, out _);
        }

        /// <summary>
        /// 타일 인덱스의 지형 프리팹 — 복제된 경계 기록이 결정하고, 기록이 없는 구간은
        /// 현재 지역 프리팹(현행 동작)이다.
        ///
        /// <para><b>기차역이 팔레트보다 먼저다.</b> 역은 연속 5장이라 한 장씩 뽑는 팔레트로는
        /// 표현할 수 없어 별도 경로를 탄다. 역이 아닌 인덱스는 아래 기존 경로가 그대로 처리한다.</para>
        /// </summary>
        private GameObject ResolveTilePrefab(int index, out Quaternion rotation)
        {
            rotation = Quaternion.identity;

            GameObject station = TryPickStation(index, ref rotation);
            if (station != null)
            {
                return station;
            }

            if (ServiceLocator.TryGet(out ITerrainBoundaryService boundaries))
            {
                int regionIndex = boundaries.ResolveRegionIndex(index);
                if (regionIndex >= 0 && ServiceLocator.TryGet(out IRegionService region))
                {
                    RegionDefinition definition = region.GetRegion(regionIndex);

                    // 팔레트가 우선한다 — 있으면 타일 인덱스에서 결정론적으로 뽑는다 (미결 ① 확정).
                    GameObject fromPalette = TryPickFromPalette(definition, index);
                    if (fromPalette != null)
                    {
                        return fromPalette;
                    }

                    if (definition != null && definition.TerrainTilePrefab != null)
                    {
                        return definition.TerrainTilePrefab;
                    }

                    return _tilePrefab;
                }
            }

            // 경계 기록이 없는 구간 — 현재 지역 팔레트가 있으면 그것부터, 없으면 현행 단일 타일.
            if (ServiceLocator.TryGet(out IRegionService current))
            {
                GameObject fromPalette = TryPickFromPalette(current.CurrentRegion, index);
                if (fromPalette != null)
                {
                    return fromPalette;
                }
            }

            return _activeTilePrefab;
        }

        /// <summary>
        /// 이 인덱스가 기차역에 속하면 그 단계의 프리팹을 낸다 (아니면 null).
        /// 규칙은 <see cref="StationSequenceLogic"/>이 소유한다 — 프리웜 계획이 같은 함수를 부른다.
        /// </summary>
        private GameObject TryPickStation(int index, ref Quaternion rotation)
        {
            if (_stationSettings == null || !_stationSettings.IsEnabled)
            {
                return null;
            }

            int stage = StationSequenceLogic.StageOf(
                index, _stationSettings.BlockSize, _stationSettings.StageCount);
            if (stage == StationSequenceLogic.NoStage)
            {
                return null;
            }

            int start = index - stage;
            if (!IsStationRegionUniform(start))
            {
                return null;
            }

            GameObject prefab = _stationSettings.GetStagePrefab(stage);
            if (prefab == null)
            {
                return null;
            }

            if (StationSequenceLogic.IsMirrored(start))
            {
                rotation = MirrorRotation;
            }

            return prefab;
        }

        /// <summary>
        /// 역 5장이 <b>전부 같은 지역</b>인가 — 아니면 그 역은 통째로 생략한다.
        /// 경계를 가로지르게 두면 앞 절반은 숲 역, 뒤 절반은 사막 역이 되어 이음매가 깨진다.
        /// 지역 전환은 지역당 1회뿐이라 잃는 것은 많아야 역 하나이고, 경계 자체가 볼거리다.
        /// </summary>
        private bool IsStationRegionUniform(int startIndex)
        {
            if (!ServiceLocator.TryGet(out ITerrainBoundaryService boundaries))
            {
                // 경계 기록이 없으면 전 구간이 한 지역이다 (M4 동작).
                return true;
            }

            int expected = boundaries.ResolveRegionIndex(startIndex);
            int count = _stationSettings.StageCount;
            for (int i = 1; i < count; i++)
            {
                if (boundaries.ResolveRegionIndex(startIndex + i) != expected)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 지역 팔레트에서 이 타일 인덱스의 세그먼트를 고른다 (없으면 null).
        /// 직전 인덱스의 선택을 함께 계산해 "같은 세그먼트가 연달아" 나오는 것을 막는다 —
        /// 순수 함수라 전 피어가 같은 결과에 도달한다.
        ///
        /// <para>선택 규칙 자체는 <see cref="SegmentPickLogic.PickForTile"/>이 소유한다 —
        /// 로딩 프리웜 계획(<see cref="GameplayPreloadPlan"/>)이 <b>같은 함수</b>를 부르기 때문에
        /// 여기서 규칙을 다시 쓰면 두 벌이 조용히 어긋난다.</para>
        /// </summary>
        private GameObject TryPickFromPalette(RegionDefinition definition, int index)
        {
            TerrainSegmentPalette palette = definition == null ? null : definition.SegmentPalette;
            if (palette == null || palette.Count == 0)
            {
                return null;
            }

            float[] weights = palette.GetWeights();
            if (weights == null)
            {
                return null;
            }

            int picked = SegmentPickLogic.PickForTile(
                index, weights, palette.GetNoRepeatFlags(),
                palette.GetEntryGroups(), palette.GroupSchedule, palette.GetWeightScratch());
            return picked < 0 ? null : palette.GetPrefab(picked);
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

            using (GameProfilerMarkers.TileStreamUpdate.Auto())
            {
                StreamTiles(scroll.TraveledDistance);
            }
        }

        /// <summary>
        /// 보이는 범위를 계산해 벗어난 타일을 회수하고 빈 자리를 채운다.
        /// <see cref="Update"/>에서 분리한 이유는 프로파일러 마커로 이 구간만 감싸기 위해서다 —
        /// 타일 교체 스파이크의 범인을 프레임 번호로 특정하려면 경계가 정확해야 한다(계획 §4.2).
        /// </summary>
        private void StreamTiles(float distance)
        {
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
                    // 6.67초마다 한 번 도는 자리다 — 스파이크가 주기적이라면 범인은 거의 여기다.
                    using (GameProfilerMarkers.TileSpawn.Auto())
                    {
                        GameObject prefab = ResolveTilePrefab(index, out Quaternion rotation);
                        _activeTiles.Add(index, PoolManager.Spawn(prefab, new Vector3(0f, 0f, z), rotation));
                        _tilePrefabsByIndex[index] = prefab;
                    }
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
                GameLog.Info(LogCategory.World, $"지역 경계 재판정 — 타일 {RemovalBuffer.Count}장 교체.");
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
