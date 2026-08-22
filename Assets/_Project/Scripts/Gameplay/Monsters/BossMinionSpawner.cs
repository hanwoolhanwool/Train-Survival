using Game.Core.Logging;
using System.Collections.Generic;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Cycle;
using Game.Gameplay.Region;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 보스 소속 개체(부하 소환·무리 호출)의 스폰·회수 — 호스트 권위 (M7 2차 결정 ②).
    /// 프리팹·풀·변종 규약은 웨이브 스포너와 완전히 같고(프리팹·네트워크 목록 무증가),
    /// 무리 호출은 1차 스탬피드의 <b>통과 모드</b>를 그대로 재사용한다.
    /// 동시 수는 웨이브 인원과 <b>합산 cap</b>으로 눌린다 — 대역폭 방어선이 보스 밤에도 유지된다.
    /// Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class BossMinionSpawner : NetworkBehaviour, IBossMinionSink
    {
        [Tooltip("웨이브 스포너와 같은 몬스터 프리팹 — 프리팹·풀·네트워크 목록 무증가.")]
        [SerializeField] private GameObject _monsterPrefab;

        [Tooltip("변종 목록 — 보스 정의의 변종 인덱스를 여기서 조회한다.")]
        [SerializeField] private MonsterVariantCatalog _variantCatalog;

        [Tooltip("보스 소속 + 웨이브 개체의 동시 존재 합산 상한 — 보스 밤의 대역폭 방어선.")]
        [SerializeField, Min(1)] private int _combinedMaxAlive = 16;

        [Tooltip("부하 소환 배치 반경(m) — 보스 주변에 흩어 세운다.")]
        [SerializeField, Min(1f)] private float _summonRadius = 5f;

        [Tooltip("무리 호출 측면 거리(m) — 보스와 같은 측면으로 스쳐 지나간다.")]
        [SerializeField, Min(1f)] private float _herdLateralOffset = 7f;

        [Tooltip("무리 호출 유입 Z 좌표 — 전방에서 나타나 후방으로 통과한다.")]
        [SerializeField] private float _herdSpawnZ = 45f;

        private static readonly List<MonsterHealth> RemovalBuffer = new List<MonsterHealth>(8);

        private readonly List<MonsterHealth> _minions = new List<MonsterHealth>(8);

        public int ActiveMinionCount
        {
            get
            {
                PruneInactive();

                return _minions.Count;
            }
        }

        public override void OnNetworkSpawn()
        {
            if (!ServiceLocator.IsRegistered<IBossMinionSink>())
            {
                ServiceLocator.Register<IBossMinionSink>(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (ServiceLocator.TryGet(out IBossMinionSink sink) && ReferenceEquals(sink, this))
            {
                ServiceLocator.Unregister<IBossMinionSink>();
            }
        }

        public int ServerSpawnMinions(int count, Vector3 origin, int variantIndex, int ownedCap, bool passThrough)
        {
            if (!IsServer || _monsterPrefab == null)
            {
                return 0;
            }

            PruneInactive();

            int otherAlive = ServiceLocator.TryGet(out IMonsterPopulation population)
                ? population.ActiveMonsterCount
                : 0;

            int allowed = BossPhaseMath.PlanSignatureSpawnCount(
                count, _minions.Count, ownedCap, otherAlive, _combinedMaxAlive);
            if (allowed <= 0)
            {
                return 0;
            }

            float healthMultiplier = ServerResolveHealthMultiplier();
            float side = origin.x >= 0f ? 1f : -1f;
            int spawned = 0;

            for (int i = 0; i < allowed; i++)
            {
                Vector3 position = passThrough
                    ? new Vector3(side * _herdLateralOffset, 0f, _herdSpawnZ + i * 3f)
                    : ServerComputeSummonPosition(origin, i, allowed);

                if (ServerSpawnOne(position, variantIndex, healthMultiplier, passThrough))
                {
                    spawned += 1;
                }
            }

            return spawned;
        }

        public void ServerRetreatMinions()
        {
            if (!IsServer)
            {
                return;
            }

            for (int i = 0; i < _minions.Count; i++)
            {
                MonsterHealth minion = _minions[i];
                if (minion != null && minion.IsSpawned)
                {
                    // 보스가 사라지면 소속 개체도 함께 물러난다 — 사망이 아니므로 이벤트·드랍 없음.
                    minion.NetworkObject.Despawn(true);
                }
            }

            _minions.Clear();
        }

        private Vector3 ServerComputeSummonPosition(Vector3 origin, int index, int total)
        {
            // 보스 주변에 고르게 흩어 세운다 — 한 점에 겹쳐 나오면 밀림으로 튄다.
            float angle = total <= 1 ? 0f : index / (float)total * Mathf.PI * 2f;
            var offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _summonRadius;

            return new Vector3(origin.x + offset.x, 0f, origin.z + offset.z);
        }

        private bool ServerSpawnOne(Vector3 position, int variantIndex, float healthMultiplier, bool passThrough)
        {
            GameObject instance = PoolManager.Spawn(_monsterPrefab, position, Quaternion.identity);
            var health = instance.GetComponent<MonsterHealth>();
            if (health == null)
            {
                GameLog.Error(LogCategory.Monsters, "몬스터 프리팹에 MonsterHealth가 없습니다.", _monsterPrefab);
                PoolManager.Despawn(instance);
                return false;
            }

            // 변종·체력은 반드시 NGO 스폰 전에 주입한다 (웨이브 스포너와 같은 규약).
            health.ServerSetVariant(_variantCatalog == null ? null : _variantCatalog.GetVariant(variantIndex));
            health.ServerSetHealthMultiplier(healthMultiplier);

            var agent = instance.GetComponent<MonsterAgent>();
            if (agent != null)
            {
                agent.ServerSetVariant(variantIndex);
            }

            health.NetworkObject.Spawn();

            // 통과 모드는 서버 전용 시뮬레이션 상태라 스폰 직후 켠다 (1차 스탬피드와 같은 순서).
            if (passThrough && agent != null)
            {
                agent.ServerSetPassThrough(true);
            }

            _minions.Add(health);

            return true;
        }

        private float ServerResolveHealthMultiplier()
        {
            if (!ServiceLocator.TryGet(out IRegionService region) || !ServiceLocator.TryGet(out IDayCycleService cycle))
            {
                return 1f;
            }

            return region.GetDifficultyForDay(cycle.DayNumber).MonsterHealthMultiplier;
        }

        private void PruneInactive()
        {
            RemovalBuffer.Clear();
            for (int i = 0; i < _minions.Count; i++)
            {
                MonsterHealth minion = _minions[i];
                if (minion == null || !minion.IsSpawned)
                {
                    RemovalBuffer.Add(minion);
                }
            }

            for (int i = 0; i < RemovalBuffer.Count; i++)
            {
                _minions.Remove(RemovalBuffer[i]);
            }
        }
    }
}
