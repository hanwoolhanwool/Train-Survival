using Game.Core.Logging;
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
    public sealed class MonsterWaveSpawner : NetworkBehaviour, IWaveSpawnToggle, IMonsterPopulation
    {
        [SerializeField] private WaveSettings _settings;
        [SerializeField] private GameObject _monsterPrefab;

        [Tooltip("Day에 따라 섞여 등장하는 변종 목록 (기획서 §5 — 난이도의 '패턴' 축). 비우면 프리팹 기본 설정만 쓴다.")]
        [SerializeField] private MonsterVariantCatalog _variantCatalog;

        private static readonly List<MonsterHealth> RemovalBuffer = new List<MonsterHealth>(8);

        private readonly List<MonsterHealth> _activeMonsters = new List<MonsterHealth>(16);

        private WavePlan _plan;
        private bool _waveActive;
        private int _spawnedCount;
        private float _spawnTimer;
        private int _waveDayNumber = 1;
        private bool _spawnEnabled = true;

        /// <summary>스폰 허용 여부 — QA 토글 (기본 켜짐, 릴리스 동작 불변).</summary>
        public bool SpawnEnabled => _spawnEnabled;

        /// <summary>
        /// 현재 살아 있는 웨이브 개체 수 (M7 2차) — 보스 소속 개체가 합산 cap을 지키기 위한 조회면.
        /// 회수된 개체가 남아 있지 않도록 정리한 뒤 센다.
        /// </summary>
        public int ActiveMonsterCount
        {
            get
            {
                PruneInactive();

                return _activeMonsters.Count;
            }
        }

        /// <summary>스폰 토글 — 서버 전용. 끄면 진행 중인 웨이브를 즉시 회수하고 다음 밤도 쉰다.</summary>
        public void ServerSetSpawnEnabled(bool enabled)
        {
            if (!IsServer || _spawnEnabled == enabled)
            {
                return;
            }

            _spawnEnabled = enabled;
            if (!enabled)
            {
                _waveActive = false;
                ServerRetreatAll();
            }

            GameLog.Info(LogCategory.Monsters, $"QA 스폰 토글: {(enabled ? "켜짐 — 다음 밤부터 웨이브 재개" : "꺼짐 — 웨이브 회수·중지")}");
        }

        /// <summary>
        /// QA 단건 스폰 (M5 6차) — 웨이브·밤낮과 무관하게 <b>기본 변종(일반형) 1마리</b>를 지정
        /// 위치 지상에 스폰한다. 파지·투척·즉사 존처럼 몬스터 1마리로 상황을 통제해야 하는 검증의
        /// 선결 수단. 체력 배율 1 고정이라 피해 수치 실측의 표본으로도 쓸 수 있다.
        /// 스폰된 개체는 웨이브 목록에 함께 등재된다 — 새벽 회수·스폰 토글 끄기로 같이 정리된다.
        /// </summary>
        public void ServerSpawnSingleForQa(Vector3 position)
        {
            if (!IsServer || _monsterPrefab == null)
            {
                return;
            }

            position.y = CurrentSurfaceY();
            GameObject instance = PoolManager.Spawn(_monsterPrefab, position, Quaternion.identity);
            var health = instance.GetComponent<MonsterHealth>();
            if (health == null)
            {
                GameLog.Error(LogCategory.Monsters, "몬스터 프리팹에 MonsterHealth가 없습니다.", _monsterPrefab);
                PoolManager.Despawn(instance);
                return;
            }

            // 변종 미지정(-1) = 프리팹 기본(일반형) · 체력 배율 1 — 수치가 통제된 표본.
            health.ServerSetVariant(null);
            health.ServerSetHealthMultiplier(1f);
            instance.GetComponent<MonsterAgent>()?.ServerSetVariant(-1);

            health.NetworkObject.Spawn();
            _activeMonsters.Add(health);
            GameLog.Info(LogCategory.Monsters, $"QA 단건 스폰: {position} (기본 변종 · 체력 배율 1)");
        }

        public override void OnNetworkSpawn()
        {
            EventBus<DayPhaseChangedEvent>.Subscribe(OnDayPhaseChanged);

            if (!ServiceLocator.IsRegistered<IWaveSpawnToggle>())
            {
                ServiceLocator.Register<IWaveSpawnToggle>(this);
            }

            if (!ServiceLocator.IsRegistered<IMonsterPopulation>())
            {
                ServiceLocator.Register<IMonsterPopulation>(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            EventBus<DayPhaseChangedEvent>.Unsubscribe(OnDayPhaseChanged);

            if (ServiceLocator.TryGet(out IWaveSpawnToggle toggle) && ReferenceEquals(toggle, this))
            {
                ServiceLocator.Unregister<IWaveSpawnToggle>();
            }

            if (ServiceLocator.TryGet(out IMonsterPopulation population) && ReferenceEquals(population, this))
            {
                ServiceLocator.Unregister<IMonsterPopulation>();
            }
        }

        private void OnDayPhaseChanged(DayPhaseChangedEvent evt)
        {
            if (!IsServer || _settings == null)
            {
                return;
            }

            if (evt.Phase == DayPhase.Night)
            {
                // QA 토글로 꺼져 있으면 밤 웨이브를 계획하지 않는다 (M5 4차 — 밤 노숙 검증성).
                if (!_spawnEnabled)
                {
                    GameLog.Info(LogCategory.Monsters, $"QA 스폰 꺼짐 — Day {evt.DayNumber} 밤 웨이브 생략");
                    return;
                }

                // 지역은 Day 번호의 순수 함수이므로 Day를 직접 넘겨 조회한다 —
                // RegionController와 이 스포너의 이벤트 처리 순서에 결과가 좌우되지 않게 한다.
                RegionDifficulty difficulty = ServiceLocator.TryGet(out IRegionService region)
                    ? region.GetDifficultyForDay(evt.DayNumber)
                    : RegionDifficulty.Neutral;

                _plan = WaveMath.Plan(
                    evt.DayNumber, _settings.ToCurve(),
                    difficulty.WaveCountMultiplier, difficulty.MonsterHealthMultiplier,
                    difficulty.IsFinalNightOfRegion, difficulty.IsReinforcedNightOfRegion);

                _waveActive = true;
                _spawnedCount = 0;
                _spawnTimer = 0f;
                _waveDayNumber = evt.DayNumber;

                string regionLabel = region?.CurrentRegion == null ? "지역 없음" : region.CurrentRegion.DisplayName;
                string nightLabel = _plan.IsFinalNight
                    ? ", 지역 마지막 밤"
                    : (_plan.IsReinforcedNight ? ", 지역 중간 강화 밤" : string.Empty);

                GameLog.Info(LogCategory.Monsters, $"밤 웨이브 시작: Day {evt.DayNumber} ({regionLabel}" +
                                             $"{nightLabel}), " +
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

        /// <summary>
        /// 몬스터가 서는 높이 — 지역이 물이면 물면, 아니면 지면(0).
        /// 바다는 궤도 밖이 전부 물이라 이걸 안 쓰면 몬스터가 <b>수면 위에 떠서 달린다</b>
        /// (바다 지역 구현 계획 §8.1). 지역을 못 얻으면 종전대로 0.
        /// </summary>
        private static float CurrentSurfaceY()
        {
            return ServiceLocator.TryGet(out IRegionService region) && region.CurrentRegion != null
                ? region.CurrentRegion.SurfaceY
                : 0f;
        }

        private void SpawnOne()
        {
            float side = Random.value < 0.5f ? -1f : 1f;
            var position = new Vector3(
                side * Random.Range(_settings.MinLateralOffset, _settings.MaxLateralOffset),
                CurrentSurfaceY(),
                Random.Range(_settings.SpawnZMin, _settings.SpawnZMax));

            GameObject instance = PoolManager.Spawn(_monsterPrefab, position, Quaternion.identity);
            var health = instance.GetComponent<MonsterHealth>();
            if (health == null)
            {
                GameLog.Error(LogCategory.Monsters, "몬스터 프리팹에 MonsterHealth가 없습니다.", _monsterPrefab);
                PoolManager.Despawn(instance);
                return;
            }

            // 변종·체력 배율은 반드시 NGO 스폰 전에 주입한다 — OnNetworkSpawn이 둘 다 확정한다.
            int variantIndex = PickVariantIndex();
            health.ServerSetVariant(_variantCatalog == null ? null : _variantCatalog.GetVariant(variantIndex));
            health.ServerSetHealthMultiplier(_plan.HealthMultiplier);

            var agent = instance.GetComponent<MonsterAgent>();
            if (agent != null)
            {
                agent.ServerSetVariant(variantIndex);
            }

            health.NetworkObject.Spawn();
            _activeMonsters.Add(health);
            _spawnedCount += 1;
        }

        /// <summary>이 밤의 Day 기준으로 등장 가능한 변종을 가중치 추첨한다. 카탈로그가 없으면 −1(기본 설정).</summary>
        private int PickVariantIndex()
        {
            if (_variantCatalog == null || _variantCatalog.Count == 0)
            {
                return -1;
            }

            return MonsterVariantPicker.Pick(
                _waveDayNumber, _variantCatalog.GetMinDays(), _variantCatalog.GetWeights(), Random.value);
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
