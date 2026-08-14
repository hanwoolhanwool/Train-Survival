using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Region;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지상 자원 주기 스폰 — 호스트 전용 (권위 분담표: 자원 스폰 = 호스트).
    /// 누적 주행 거리 기준으로 전방 선로변에 자원을 심고, 뒤로 밀려난 자원을 회수한다.
    /// 스폰/소멸은 PoolManager + NGO 스폰(등록된 PooledNetworkPrefabHandler 경유)로 처리한다.
    /// 지역이 바뀌면 이후 심는 자원의 종류·밀도가 지역 데이터를 따른다 (M4, 기획서 §4).
    /// 창고 보따리(M5 8차)도 여기서 스폰·회수한다 — 자원 노드와 같은 안착·회수 규약을 타므로
    /// 관리 목록을 <see cref="SettleableGrabbable"/>로 함께 쓴다.
    /// </summary>
    public sealed class GroundResourceSpawner : NetworkBehaviour, IResourceDropper, IStorageBundleSpawner,
        IBundleItemStore
    {
        [SerializeField] private ResourceSpawnSettings _settings;

        [Tooltip("지역 데이터에 자원 프리팹이 없을 때 쓰는 기본 자원.")]
        [SerializeField] private GameObject _resourcePrefab;

        [Tooltip("창고 보따리 프리팹 (M5 8차) — 파괴된 창고의 슬롯 전체를 담는 회수물.")]
        [SerializeField] private GameObject _bundlePrefab;

        [Tooltip("보따리의 지면·갑판 위 안착 오프셋 (m) — 시각 절반 높이만큼 띄운다.")]
        [SerializeField, Min(0f)] private float _bundleRestOffsetY = 0.35f;

        private static readonly List<SettleableGrabbable> RemovalBuffer = new List<SettleableGrabbable>(8);

        private readonly List<SettleableGrabbable> _activeNodes = new List<SettleableGrabbable>(64);
        private float _nextSpawnDistance;

        private GameObject _activeResourcePrefab;
        private float _activeIntervalMultiplier = 1f;

        // 지역의 자원 종류 후보 (종류 + 가중치) — 비어 있으면 노드 프리팹 기본 종류로 심는다.
        private Inventory.ResourceType[] _activeTypePool;
        private float[] _activeWeights;

        public override void OnNetworkSpawn()
        {
            EventBus<RegionChangedEvent>.Subscribe(OnRegionChanged);

            _activeResourcePrefab = _resourcePrefab;
            _activeIntervalMultiplier = 1f;
            if (ServiceLocator.TryGet(out IRegionService region))
            {
                ApplyRegion(region.CurrentRegion);
            }

            if (IsServer)
            {
                _nextSpawnDistance = 0f;

                if (!ServiceLocator.IsRegistered<IResourceDropper>())
                {
                    ServiceLocator.Register<IResourceDropper>(this);
                }

                if (!ServiceLocator.IsRegistered<IStorageBundleSpawner>())
                {
                    ServiceLocator.Register<IStorageBundleSpawner>(this);
                }

                if (!ServiceLocator.IsRegistered<IBundleItemStore>())
                {
                    ServiceLocator.Register<IBundleItemStore>(this);
                }

                // 새 세션 — 이전 세션의 보관물이 새지 않게 비운다 (보관은 세션 한정).
                _bundleItemContents.Clear();
            }
        }

        public override void OnNetworkDespawn()
        {
            EventBus<RegionChangedEvent>.Unsubscribe(OnRegionChanged);

            if (ServiceLocator.TryGet(out IResourceDropper dropper) && ReferenceEquals(dropper, this))
            {
                ServiceLocator.Unregister<IResourceDropper>();
            }

            if (ServiceLocator.TryGet(out IStorageBundleSpawner spawner) && ReferenceEquals(spawner, this))
            {
                ServiceLocator.Unregister<IStorageBundleSpawner>();
            }

            if (ServiceLocator.TryGet(out IBundleItemStore store) && ReferenceEquals(store, this))
            {
                ServiceLocator.Unregister<IBundleItemStore>();
            }
        }

        private void OnRegionChanged(RegionChangedEvent evt)
        {
            ApplyRegion(evt.Region);
        }

        private void ApplyRegion(RegionDefinition region)
        {
            _activeResourcePrefab = region == null || region.ResourcePrefab == null
                ? _resourcePrefab
                : region.ResourcePrefab;

            _activeIntervalMultiplier = region == null ? 1f : Mathf.Max(0.1f, region.ResourceSpawnIntervalMultiplier);

            // 종류 후보 목록 캐시 — 스폰마다 배열을 새로 만들지 않는다.
            int count = region == null ? 0 : region.ResourceSpawnCount;
            if (count <= 0)
            {
                _activeTypePool = null;
                _activeWeights = null;
                return;
            }

            _activeTypePool = new Inventory.ResourceType[count];
            _activeWeights = new float[count];
            for (int i = 0; i < count; i++)
            {
                RegionDefinition.ResourceSpawnEntry entry = region.GetResourceSpawn(i);
                _activeTypePool[i] = entry == null ? Inventory.ResourceType.None : entry.Type;
                _activeWeights[i] = entry == null ? 0f : entry.Weight;
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _settings == null || _activeResourcePrefab == null)
            {
                return;
            }

            if (!ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                return;
            }

            float distance = scroll.TraveledDistance;

            SpawnAhead(distance);
            DespawnBehind(distance);
        }

        private void SpawnAhead(float distance)
        {
            while (_nextSpawnDistance <= distance + _settings.SpawnAheadMeters)
            {
                float lateral = Random.Range(_settings.MinLateralOffset, _settings.MaxLateralOffset);
                float side = Random.value < 0.5f ? -1f : 1f;

                // 스폰 위치 z = (심는 거리 마커 − 현재 누적 거리): 누적 거리와 함께 -Z로 흘러간다.
                var spawnPosition = new Vector3(side * lateral, _settings.SpawnHeight, _nextSpawnDistance - distance);

                GameObject instance = PoolManager.Spawn(_activeResourcePrefab, spawnPosition, Quaternion.identity);
                var node = instance.GetComponent<ResourceNode>();
                if (node == null)
                {
                    Debug.LogError("[GroundResourceSpawner] 자원 프리팹에 ResourceNode가 없습니다.", _activeResourcePrefab);
                    PoolManager.Despawn(instance);
                    return;
                }

                node.ServerSetSpawnBinding(spawnPosition, distance);

                // 지역 후보에서 종류를 가중 추첨해 주입한다 — 후보가 없으면 노드 기본 종류.
                if (_activeWeights != null)
                {
                    int picked = ResourceSpawnPicker.Pick(_activeWeights, Random.value);
                    if (picked >= 0)
                    {
                        node.ServerSetResourceType(_activeTypePool[picked]);
                    }
                }

                node.NetworkObject.Spawn();
                _activeNodes.Add(node);

                // 지역별 자원 밀도 — 배율이 클수록 간격이 벌어져 희소해진다 (기획서 §4 자원 등급).
                _nextSpawnDistance += _settings.SpawnIntervalMeters * _activeIntervalMultiplier;
            }
        }

        /// <summary>
        /// 버린 자원의 낙하 스폰 (M5 3차 — 아이템 버리기). 주기 스폰과 같은 경로
        /// (PoolManager → 종류 주입 → NGO 스폰 → 회수 목록)를 타므로 낙하 노드도
        /// 컨베이어로 흘러가고 뒤로 밀리면 회수된다. 집게로 다시 주울 수 있다.
        /// </summary>
        public bool ServerSpawnDropped(Inventory.ResourceType type, int count, Vector3 dropOrigin)
        {
            if (!IsSpawned || !IsServer || _settings == null || _activeResourcePrefab == null
                || type == Inventory.ResourceType.None || count <= 0
                || !ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                return false;
            }

            float distance = scroll.TraveledDistance;
            float originLateral = Mathf.Abs(dropOrigin.x);

            // 이미 선로변 밖에서 떨어진 것(사냥한 몬스터·보스)은 그 자리 옆에 남는다 —
            // 측면을 새로 추첨하면 반대편 20 m에 떨어져 "드랍이 없다"로 보인다.
            // 열차 폭 안에서 버린 것만 기존처럼 선로변 대역으로 밀어낸다 (갑판 밑에 깔리지 않게).
            bool keepOrigin = originLateral >= _settings.MinLateralOffset;
            float side = keepOrigin
                ? Mathf.Sign(dropOrigin.x)
                : (Random.value < 0.5f ? -1f : 1f);

            for (int i = 0; i < count; i++)
            {
                float lateral = keepOrigin
                    ? Mathf.Clamp(originLateral + Random.Range(-1.5f, 1.5f),
                        _settings.MinLateralOffset, _settings.MaxLateralOffset)
                    : Random.Range(_settings.MinLateralOffset, _settings.MaxLateralOffset);

                var spawnPosition = new Vector3(
                    side * lateral, _settings.SpawnHeight, dropOrigin.z + Random.Range(-2f, 2f));

                GameObject instance = PoolManager.Spawn(_activeResourcePrefab, spawnPosition, Quaternion.identity);
                var node = instance.GetComponent<ResourceNode>();
                if (node == null)
                {
                    PoolManager.Despawn(instance);
                    return i > 0;
                }

                node.ServerSetSpawnBinding(spawnPosition, distance);
                node.ServerSetResourceType(type);
                node.NetworkObject.Spawn();
                _activeNodes.Add(node);
            }

            return true;
        }

        /// <summary>
        /// 건축물 파괴 (칸 생존) — 그 칸 갑판 위 <b>휴지 상태</b>로 보따리를 스폰한다 (M5 8차).
        /// 휴지라 후방 회수 대상이 아니고, 휴지한 칸이 소실·파괴되면 함께 회수된다 (자원 노드와 같은 규약).
        /// </summary>
        public bool ServerSpawnDeckResting(
            Inventory.HotbarSlotView[] contents, int carIndex, float deckHeight, float carCenterZ, float ejectOffset)
        {
            var position = new Vector3(0f, deckHeight + _bundleRestOffsetY, carCenterZ);
            StorageBundle bundle = InstantiateBundle(contents, position);
            if (bundle == null)
            {
                return false;
            }

            bundle.ServerSetDeckRestBinding(position, carIndex, ejectOffset, _bundleRestOffsetY);
            bundle.NetworkObject.Spawn();
            _activeNodes.Add(bundle);
            return true;
        }

        /// <summary>
        /// 칸 파괴 — 파괴 지점에서 지상 선로변으로 보따리를 <b>느린 포물선 투척</b> 스폰한다
        /// (M5 8차, 월드 프레임 소속). 버리기 낙하와 같은 측면 대역에 착지해 컨베이어로 흘러가고,
        /// 뒤로 밀리면 회수(소실)된다 — 집게로 건져 올리는 짧은 기회.
        /// </summary>
        public bool ServerSpawnOnGround(Inventory.HotbarSlotView[] contents, Vector3 throwOrigin)
        {
            if (_settings == null || !ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                return false;
            }

            float lateral = Random.Range(_settings.MinLateralOffset, _settings.MaxLateralOffset);
            float side = Random.value < 0.5f ? -1f : 1f;
            var position = new Vector3(side * lateral, _bundleRestOffsetY, throwOrigin.z);

            StorageBundle bundle = InstantiateBundle(contents, position);
            if (bundle == null)
            {
                return false;
            }

            bundle.ServerSetSpawnBinding(position, scroll.TraveledDistance);
            bundle.ServerSetThrowFlight(throwOrigin);
            bundle.NetworkObject.Spawn();
            _activeNodes.Add(bundle);
            return true;
        }

        /// <summary>
        /// 지정 위치 안착 스폰 (M5 8차 — 보따리 아이템 내려놓기): 그랩 해제 낙하와 같은
        /// 프레임 판정 — 갑판 위면 휴지, 아니면 월드 바인딩.
        /// </summary>
        public bool ServerSpawnResting(Inventory.HotbarSlotView[] contents, Vector3 position)
        {
            if (!ServiceLocator.TryGet(out IWorldScrollService scroll))
            {
                return false;
            }

            float deckHeight = 0f;
            int carIndex = -1;
            bool onDeck = ServiceLocator.TryGet(out Train.ITrainState train)
                && train.TryGetDeckSurface(position, out deckHeight, out carIndex);
            var rest = new Vector3(position.x, (onDeck ? deckHeight : 0f) + _bundleRestOffsetY, position.z);

            StorageBundle bundle = InstantiateBundle(contents, rest);
            if (bundle == null)
            {
                return false;
            }

            if (onDeck)
            {
                bundle.ServerSetDeckRestBinding(rest, carIndex, train.GetEjectOffset(carIndex), _bundleRestOffsetY);
            }
            else
            {
                bundle.ServerSetSpawnBinding(rest, scroll.TraveledDistance);
            }

            bundle.NetworkObject.Spawn();
            _activeNodes.Add(bundle);
            return true;
        }

        // ── 보따리 아이템 보관소 (M5 8차 — IBundleItemStore) ────────────────────

        // 서버 전용 — 보관 id(1~255) → 내용물. 슬롯 Count가 byte라 id도 byte다 (세션 한정).
        private readonly Dictionary<byte, Inventory.HotbarSlotView[]> _bundleItemContents =
            new Dictionary<byte, Inventory.HotbarSlotView[]>();

        private byte _nextBundleItemId = 1;

        public byte ServerStore(Inventory.HotbarSlotView[] contents)
        {
            if (!IsServer || contents == null || _bundleItemContents.Count >= 255)
            {
                return 0;
            }

            // 1~255를 순환하며 빈 id를 찾는다 — 회수된 id를 재사용한다.
            for (int step = 0; step < 255; step++)
            {
                byte id = _nextBundleItemId;
                _nextBundleItemId = (byte)(_nextBundleItemId >= 255 ? 1 : _nextBundleItemId + 1);
                if (!_bundleItemContents.ContainsKey(id))
                {
                    _bundleItemContents[id] = contents;
                    return id;
                }
            }

            return 0;
        }

        public bool ServerTryPeek(byte id, out Inventory.HotbarSlotView[] contents)
        {
            return _bundleItemContents.TryGetValue(id, out contents);
        }

        public void ServerRemove(byte id)
        {
            _bundleItemContents.Remove(id);
        }

        private StorageBundle InstantiateBundle(Inventory.HotbarSlotView[] contents, Vector3 position)
        {
            if (!IsSpawned || !IsServer || _bundlePrefab == null || contents == null)
            {
                return null;
            }

            GameObject instance = PoolManager.Spawn(_bundlePrefab, position, Quaternion.identity);
            var bundle = instance.GetComponent<StorageBundle>();
            if (bundle == null)
            {
                Debug.LogError("[GroundResourceSpawner] 보따리 프리팹에 StorageBundle이 없습니다.", _bundlePrefab);
                PoolManager.Despawn(instance);
                return null;
            }

            bundle.ServerSetContents(contents);
            return bundle;
        }

        private void DespawnBehind(float distance)
        {
            RemovalBuffer.Clear();
            for (int i = 0; i < _activeNodes.Count; i++)
            {
                SettleableGrabbable node = _activeNodes[i];
                if (node == null || !node.IsSpawned)
                {
                    RemovalBuffer.Add(node);
                    continue;
                }

                // 견인 중(열차 프레임 소속)에는 회수하지 않는다.
                if (node.IsClaimed)
                {
                    continue;
                }

                // 갑판 휴지 노드는 뒤로 밀리지 않으므로 후방 회수 대상이 아니다 — 단 휴지한 칸이
                // 소실·파괴되면 위의 물건도 함께 회수한다 (7차 2차 발견 — 재건 시 자원이 따라오지 않게).
                if (node.IsDeckResting)
                {
                    if (ServiceLocator.TryGet(out Train.ITrainState train) && !train.IsDeckAlive(node.DeckCarIndex))
                    {
                        node.NetworkObject.Despawn(true);
                        RemovalBuffer.Add(node);
                    }

                    continue;
                }

                if (node.GetMetersBehindSpawn(distance) > _settings.DespawnBehindMeters)
                {
                    node.NetworkObject.Despawn(true);
                    RemovalBuffer.Add(node);
                }
            }

            for (int i = 0; i < RemovalBuffer.Count; i++)
            {
                _activeNodes.Remove(RemovalBuffer[i]);
            }
        }
    }
}
