using System.Collections.Generic;
using Game.Core.Pooling;
using Game.Core.Services;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.World
{
    /// <summary>
    /// 지상 자원 주기 스폰 — 호스트 전용 (권위 분담표: 자원 스폰 = 호스트).
    /// 누적 주행 거리 기준으로 전방 선로변에 자원을 심고, 뒤로 밀려난 자원을 회수한다.
    /// 스폰/소멸은 PoolManager + NGO 스폰(등록된 PooledNetworkPrefabHandler 경유)로 처리한다.
    /// </summary>
    public sealed class GroundResourceSpawner : NetworkBehaviour
    {
        [SerializeField] private ResourceSpawnSettings _settings;
        [SerializeField] private GameObject _resourcePrefab;

        private static readonly List<ResourceNode> RemovalBuffer = new List<ResourceNode>(8);

        private readonly List<ResourceNode> _activeNodes = new List<ResourceNode>(64);
        private float _nextSpawnDistance;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                _nextSpawnDistance = 0f;
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _settings == null || _resourcePrefab == null)
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

                GameObject instance = PoolManager.Spawn(_resourcePrefab, spawnPosition, Quaternion.identity);
                var node = instance.GetComponent<ResourceNode>();
                if (node == null)
                {
                    Debug.LogError("[GroundResourceSpawner] 자원 프리팹에 ResourceNode가 없습니다.", _resourcePrefab);
                    PoolManager.Despawn(instance);
                    return;
                }

                node.ServerSetSpawnBinding(spawnPosition, distance);
                node.NetworkObject.Spawn();
                _activeNodes.Add(node);

                _nextSpawnDistance += _settings.SpawnIntervalMeters;
            }
        }

        private void DespawnBehind(float distance)
        {
            RemovalBuffer.Clear();
            for (int i = 0; i < _activeNodes.Count; i++)
            {
                ResourceNode node = _activeNodes[i];
                if (node == null || !node.IsSpawned)
                {
                    RemovalBuffer.Add(node);
                    continue;
                }

                // 견인 중(열차 프레임 소속)에는 회수하지 않는다.
                if (!node.IsClaimed && node.GetMetersBehindSpawn(distance) > _settings.DespawnBehindMeters)
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
