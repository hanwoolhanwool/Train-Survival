using System.Collections.Generic;
using Game.Core.Events;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Cycle;
using Game.Gameplay.Region;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Monsters
{
    /// <summary>
    /// 밤 몬스터 웨이브 스폰 — 호스트 전용 (권위 분담표: 스폰·웨이브 진행 = 호스트).
    /// 밤 시작(권위 이벤트)에 Day 비례 계획을 세우고, "지속 유입" 간격으로 동시 존재 상한 안에서 스폰한다.
    /// 새벽에는 생존 몬스터를 도주 처리(회수)한다. 스폰/소멸은 PoolManager + NGO 스폰 경유. Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class MonsterWaveSpawner : NetworkBehaviour
    {
        [SerializeField] private WaveSettings _settings;
        [SerializeField] private GameObject _monsterPrefab;

        private static readonly List<MonsterHealth> RemovalBuffer = new List<MonsterHealth>(8);

        private readonly List<MonsterHealth> _activeMonsters = new List<MonsterHealth>(16);

        private WavePlan _plan;
        private bool _waveActive;
        private int _spawnedCount;
        private float _spawnTimer;

        public override void OnNetworkSpawn()
        {
            EventBus<DayPhaseChangedEvent>.Subscribe(OnDayPhaseChanged);
        }

        public override void OnNetworkDespawn()
        {
            EventBus<DayPhaseChangedEvent>.Unsubscribe(OnDayPhaseChanged);
        }

        private void OnDayPhaseChanged(DayPhaseChangedEvent evt)
        {
            if (!IsServer || _settings == null)
            {
                return;
            }

            if (evt.Phase == DayPhase.Night)
            {
                // 지역은 Day 번호의 순수 함수이므로 Day를 직접 넘겨 조회한다 —
                // RegionController와 이 스포너의 이벤트 처리 순서에 결과가 좌우되지 않게 한다.
                RegionDifficulty difficulty = ServiceLocator.TryGet(out IRegionService region)
                    ? region.GetDifficultyForDay(evt.DayNumber)
                    : RegionDifficulty.Neutral;

                _plan = WaveMath.Plan(
                    evt.DayNumber, _settings.ToCurve(),
                    difficulty.WaveCountMultiplier, difficulty.MonsterHealthMultiplier,
                    difficulty.IsFinalNightOfRegion);

                _waveActive = true;
                _spawnedCount = 0;
                _spawnTimer = 0f;

                string regionLabel = region?.CurrentRegion == null ? "지역 없음" : region.CurrentRegion.DisplayName;
                Debug.Log($"[MonsterWaveSpawner] 밤 웨이브 시작: Day {evt.DayNumber} ({regionLabel}" +
                    $"{(_plan.IsFinalNight ? ", 지역 마지막 밤" : string.Empty)}), " +
                    $"총 {_plan.TotalCount}마리, 간격 {_plan.SpawnInterval:F1}s, 동시 상한 {_plan.MaxAlive}, " +
                    $"체력 ×{_plan.HealthMultiplier:F2}");
            }
            else
            {
                _waveActive = false;
                ServerRetreatAll();
            }
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !_waveActive || _settings == null || _monsterPrefab == null)
            {
                return;
            }

            PruneInactive();

            if (_spawnedCount >= _plan.TotalCount)
            {
                return;
            }

            _spawnTimer += Time.deltaTime;
            if (_spawnTimer < _plan.SpawnInterval || _activeMonsters.Count >= _plan.MaxAlive)
            {
                return;
            }

            _spawnTimer = 0f;
            SpawnOne();
        }

        private void SpawnOne()
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            var position = new Vector3(
                side * Random.Range(_settings.MinLateralOffset, _settings.MaxLateralOffset),
                0f,
                Random.Range(_settings.SpawnZMin, _settings.SpawnZMax));

            GameObject instance = PoolManager.Spawn(_monsterPrefab, position, Quaternion.identity);
            var health = instance.GetComponent<MonsterHealth>();
            if (health == null)
            {
                Debug.LogError("[MonsterWaveSpawner] 몬스터 프리팹에 MonsterHealth가 없습니다.", _monsterPrefab);
                PoolManager.Despawn(instance);
                return;
            }

            // 체력 배율은 반드시 NGO 스폰 전에 주입한다 — OnNetworkSpawn이 최대 체력을 확정한다.
            health.ServerSetHealthMultiplier(_plan.HealthMultiplier);

            health.NetworkObject.Spawn();
            _activeMonsters.Add(health);
            _spawnedCount += 1;
        }

        private void PruneInactive()
        {
            RemovalBuffer.Clear();
            for (int i = 0; i < _activeMonsters.Count; i++)
            {
                MonsterHealth monster = _activeMonsters[i];
                if (monster == null || !monster.IsSpawned)
                {
                    RemovalBuffer.Add(monster);
                }
            }

            for (int i = 0; i < RemovalBuffer.Count; i++)
            {
                _activeMonsters.Remove(RemovalBuffer[i]);
            }
        }

        private void ServerRetreatAll()
        {
            for (int i = 0; i < _activeMonsters.Count; i++)
            {
                MonsterHealth monster = _activeMonsters[i];
                if (monster != null && monster.IsSpawned)
                {
                    // 새벽 도주 — 사망이 아니므로 이벤트 없이 회수한다.
                    monster.NetworkObject.Despawn(true);
                }
            }

            _activeMonsters.Clear();
        }
    }
}
