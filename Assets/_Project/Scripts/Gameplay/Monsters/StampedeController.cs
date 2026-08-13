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
    /// 스탬피드 이벤트의 호스트 권위 소유자 (M7 1차, 기획서 §4.3 — 대초원 정체성 이벤트).
    /// 날씨가 아니라 <b>몬스터 이벤트</b>다 — 발생 추첨만 날씨 규약(호스트 추첨·지역 데이터
    /// 확률·지역 첫날 제외·Day 기준 조회)을 따르고, 개체 스폰·회수는 웨이브 스포너와 같은
    /// PoolManager + NGO 경로를 탄다. 낮 이벤트이므로 밤 진입 시 강제 종료한다. Game 씬에 1개 배치.
    /// </summary>
    public sealed class StampedeController : NetworkBehaviour
    {
        [SerializeField] private StampedeSettings _settings;

        [Tooltip("웨이브 스포너와 같은 몬스터 프리팹 — 프리팹·풀·네트워크 목록 무증가 (변종 인덱스로 구분).")]
        [SerializeField] private GameObject _monsterPrefab;

        [Tooltip("변종 목록 — 스탬피드 전용 변종 인덱스(StampedeSettings)를 여기서 조회한다.")]
        [SerializeField] private MonsterVariantCatalog _variantCatalog;

        [Tooltip("지역 타임라인 — 발생 확률을 Day 기준으로 조회한다 (처리 순서 의존 금지).")]
        [SerializeField] private RegionTimelineSettings _regionSettings;

        private static readonly List<MonsterHealth> RemovalBuffer = new List<MonsterHealth>(8);

        private readonly List<MonsterHealth> _activeMonsters = new List<MonsterHealth>(16);

        private StampedePlan _plan;
        private bool _stampedeActive;
        private int _spawnedCount;
        private float _spawnTimer;
        private float _healthMultiplier = 1f;
        private float _side = 1f;

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
                // 낮 이벤트다 — 밤 방어전과 겹치지 않게 남은 무리를 즉시 회수한다.
                ServerEndStampede();
                return;
            }

            ServerTryStartStampede(evt.DayNumber);
        }

        private void ServerTryStartStampede(int dayNumber)
        {
            if (_regionSettings == null || !ServiceLocator.TryGet(out IRegionService region))
            {
                return;
            }

            // 지역 판정은 Day 기준 — 지역 전환 당일에 "현재 지역"을 읽으면 이벤트 처리 순서에
            // 따라 이전/새 지역이 갈린다 (날씨 규약, EvaluateForDay 전례).
            RegionTimelineState state = region.EvaluateForDay(dayNumber);
            if (!state.IsValid)
            {
                return;
            }

            RegionDefinition definition = _regionSettings.GetRegion(state.RegionIndex);
            if (definition == null
                || !StampedeMath.ShouldTrigger(definition.StampedeChancePerDay, state.DayInRegion, Random.value))
            {
                return;
            }

            _plan = StampedeMath.Plan(
                _settings.TotalCount, _settings.SpawnIntervalSeconds, _settings.MaxAlive, _settings.MaxAlive);

            // 무리는 이벤트당 한쪽 측면으로만 흐른다 — 열(列)로 읽히게 한다.
            _side = Random.value < 0.5f ? -1f : 1f;

            // 체력은 지역 난이도 배율을 따른다 — 순환(§4.5) 시 사냥 난도도 함께 오른다.
            _healthMultiplier = region.GetDifficultyForDay(dayNumber).MonsterHealthMultiplier;

            _stampedeActive = true;
            _spawnedCount = 0;
            _spawnTimer = 0f;

            Debug.Log($"[StampedeController] 스탬피드 시작: Day {dayNumber} ({definition.DisplayName}), " +
                $"총 {_plan.TotalCount}마리, 간격 {_plan.SpawnInterval:F1}s, 동시 상한 {_plan.MaxAlive}, " +
                $"측면 {(_side < 0f ? "좌" : "우")}, 체력 ×{_healthMultiplier:F2}");
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || !_stampedeActive || _settings == null || _monsterPrefab == null)
            {
                return;
            }

            PruneInactive();

            if (_spawnedCount >= _plan.TotalCount)
            {
                // 유입 완료 — 남은 개체가 전부 이탈·처치되면 이벤트 종료.
                if (_activeMonsters.Count == 0)
                {
                    _stampedeActive = false;
                    Debug.Log("[StampedeController] 스탬피드 종료 — 무리가 모두 지나갔다");
                }

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
            var position = new Vector3(
                _side * Random.Range(_settings.MinLateralOffset, _settings.MaxLateralOffset),
                0f,
                _settings.SpawnZ + Random.Range(-_settings.SpawnZJitter, _settings.SpawnZJitter));

            GameObject instance = PoolManager.Spawn(_monsterPrefab, position, Quaternion.identity);
            var health = instance.GetComponent<MonsterHealth>();
            if (health == null)
            {
                Debug.LogError("[StampedeController] 몬스터 프리팹에 MonsterHealth가 없습니다.", _monsterPrefab);
                PoolManager.Despawn(instance);
                return;
            }

            // 변종·체력·드랍·통과 모드는 반드시 NGO 스폰 전에 주입한다 (웨이브 스포너와 같은 규약).
            health.ServerSetVariant(_variantCatalog == null ? null : _variantCatalog.GetVariant(_settings.VariantIndex));
            health.ServerSetHealthMultiplier(_healthMultiplier);
            health.ServerSetDeathDrop(_settings.DropType, _settings.DropCount);

            var agent = instance.GetComponent<MonsterAgent>();
            if (agent != null)
            {
                agent.ServerSetVariant(_settings.VariantIndex);
            }

            health.NetworkObject.Spawn();

            // 통과 모드는 서버 전용 시뮬레이션 상태라 스폰 직후 켠다 (복제 불요 — 클라는 보간만).
            if (agent != null)
            {
                agent.ServerSetPassThrough(true);
            }

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

        private void ServerEndStampede()
        {
            if (!_stampedeActive && _activeMonsters.Count == 0)
            {
                return;
            }

            _stampedeActive = false;

            for (int i = 0; i < _activeMonsters.Count; i++)
            {
                MonsterHealth monster = _activeMonsters[i];
                if (monster != null && monster.IsSpawned)
                {
                    // 강제 종료 — 사망이 아니므로 이벤트 없이 풀로 회수한다 (새벽 도주와 같은 규약).
                    monster.NetworkObject.Despawn(true);
                }
            }

            _activeMonsters.Clear();
        }
    }
}
