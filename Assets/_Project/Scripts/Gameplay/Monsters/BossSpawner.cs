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
    /// 지역 보스의 생애주기 소유자 — 호스트 권위 (M7 2차, 기획서 §5 "지역 마지막 밤 = 대형 웨이브 + 보스").
    /// 마지막 밤에 대형 웨이브와 <b>병행</b> 스폰하고(상호 무간섭 — 웨이브 계획 총량은 그대로다),
    /// 보스가 살아 있는 동안 <see cref="INightHoldGate"/>로 새벽을 붙잡는다 (결정 ④ — 상한 없는 순수 지속).
    /// 밤 계획 총량이 고정이라 연장 시간에 비례해 유입이 늘지 않는다 — 자연 상한이다.
    /// Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class BossSpawner : NetworkBehaviour, INightHoldGate
    {
        [Tooltip("지역 타임라인 — 보스 정의를 Day 기준으로 조회한다 (처리 순서 의존 금지).")]
        [SerializeField] private RegionTimelineSettings _regionSettings;

        [Tooltip("웨이브 밸런스 — 보스 체력의 Day 성장분을 웨이브와 같은 곡선에서 가져온다.")]
        [SerializeField] private WaveSettings _waveSettings;

        [Tooltip("스폰 시 열차 중심에서의 측면 거리(m).")]
        [SerializeField, Min(5f)] private float _spawnLateralOffset = 16f;

        [Tooltip("스폰 시 Z 좌표 — 열차 전방에서 다가오게 한다.")]
        [SerializeField] private float _spawnZ = 20f;

        private BossHealth _boss;
        private BossDefinition _bossDefinition;

        /// <summary>이 개체가 새벽을 붙잡는가 — QA 소환(넘패드 /)은 밤과 무관하므로 false다.</summary>
        private bool _holdsNight;

        public bool IsHoldingNight => _holdsNight && _boss != null && _boss.IsSpawned && _boss.IsAlive;

        public override void OnNetworkSpawn()
        {
            EventBus<DayPhaseChangedEvent>.Subscribe(OnDayPhaseChanged);

            if (!ServiceLocator.IsRegistered<INightHoldGate>())
            {
                ServiceLocator.Register<INightHoldGate>(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            EventBus<DayPhaseChangedEvent>.Unsubscribe(OnDayPhaseChanged);

            if (ServiceLocator.TryGet(out INightHoldGate gate) && ReferenceEquals(gate, this))
            {
                ServiceLocator.Unregister<INightHoldGate>();
            }
        }

        private void OnDayPhaseChanged(DayPhaseChangedEvent evt)
        {
            if (!IsServer)
            {
                return;
            }

            if (evt.Phase == DayPhase.Day)
            {
                // 넘패드 3(Day 스킵)은 보류를 무시하는 강제 점프다 — 디버그 탈출구가 막히지 않게
                // 낮 전환에서 생존 보스를 회수한다 (사망이 아니므로 보상 없음).
                ServerRetreatBoss("낮 전환");
                return;
            }

            ServerTrySpawnBoss(evt.DayNumber);
        }

        private void ServerTrySpawnBoss(int dayNumber)
        {
            if (_regionSettings == null || !ServiceLocator.TryGet(out IRegionService region))
            {
                return;
            }

            // 지역 판정은 Day 기준 — 지역 전환 당일에 "현재 지역"을 읽으면 이벤트 처리 순서에
            // 따라 이전/새 지역이 갈린다 (스탬피드·날씨와 같은 규약).
            RegionTimelineState state = region.EvaluateForDay(dayNumber);
            if (!state.IsValid || !state.IsFinalDayOfRegion)
            {
                return;
            }

            RegionDefinition definition = _regionSettings.GetRegion(state.RegionIndex);
            BossDefinition boss = definition == null ? null : definition.BossDefinition;
            if (boss == null)
            {
                // 보스 미배정 지역 — 기존 대형 웨이브만으로 마지막 밤이 성립한다 (하위 호환).
                return;
            }

            // QA 소환(넘패드 /)으로 세워 둔 보스가 남아 있으면 물러나게 한다 — 그 개체는 새벽
            // 보류에 등록돼 있지 않아, 그대로 두면 마지막 밤이 보류 없이 지나가 버린다.
            if (_boss != null)
            {
                ServerRetreatBoss("마지막 밤 — QA 소환 개체 교체");
            }

            float healthMultiplier = ServerComputeHealthMultiplier(dayNumber, region);
            if (!ServerSpawnBoss(boss, healthMultiplier, true))
            {
                return;
            }

            Debug.Log($"[BossSpawner] 지역 보스 등장: Day {dayNumber} " +
                $"({definition.DisplayName} — {boss.DisplayName}), 체력 ×{healthMultiplier:F2}. " +
                "처치 전까지 새벽이 오지 않는다.");
        }

        /// <summary>
        /// QA 즉시 소환 (넘패드 <c>/</c>) — 밤·웨이브와 무관하게 현재 지역 보스를 1기 세운다.
        /// <b>새벽 보류에는 등록하지 않는다</b> — 낮에 소환해도 시간이 멈추지 않아야 격리 검증이 된다.
        /// </summary>
        public void ServerSpawnBossForQa(int dayNumber)
        {
            if (!IsServer || _regionSettings == null)
            {
                return;
            }

            if (_boss != null)
            {
                ServerRetreatBoss("QA 재소환");
            }

            if (!ServiceLocator.TryGet(out IRegionService region))
            {
                Debug.Log("[BossSpawner] QA 소환 무효: 지역 서비스 없음");
                return;
            }

            RegionTimelineState state = region.EvaluateForDay(dayNumber);
            RegionDefinition definition = state.IsValid ? _regionSettings.GetRegion(state.RegionIndex) : null;
            BossDefinition boss = definition == null ? null : definition.BossDefinition;
            if (boss == null)
            {
                Debug.Log("[BossSpawner] QA 소환 무효: 현재 지역에 배정된 보스가 없습니다.");
                return;
            }

            if (ServerSpawnBoss(boss, ServerComputeHealthMultiplier(dayNumber, region), false))
            {
                Debug.Log($"[BossSpawner] QA 보스 소환: {definition.DisplayName} — {boss.DisplayName} " +
                    "(새벽 보류 비등록 — 낮에도 검증 가능)");
            }
        }

        private bool ServerSpawnBoss(BossDefinition definition, float healthMultiplier, bool holdsNight)
        {
            if (definition.Prefab == null)
            {
                Debug.LogError($"[BossSpawner] 보스 정의 '{definition.DisplayName}'에 프리팹이 배선되지 않았습니다.", definition);
                return false;
            }

            float side = Random.value < 0.5f ? -1f : 1f;
            var position = new Vector3(side * _spawnLateralOffset, 0f, _spawnZ);

            GameObject instance = PoolManager.Spawn(definition.Prefab, position, Quaternion.identity);
            var health = instance.GetComponent<BossHealth>();
            if (health == null)
            {
                Debug.LogError("[BossSpawner] 보스 프리팹에 BossHealth가 없습니다.", definition.Prefab);
                PoolManager.Despawn(instance);
                return false;
            }

            // 체력 배율은 반드시 NGO 스폰 전에 주입한다 — OnNetworkSpawn이 확정한다 (웨이브와 같은 규약).
            health.ServerSetHealthMultiplier(healthMultiplier);
            health.NetworkObject.Spawn();

            _boss = health;
            _bossDefinition = definition;
            _holdsNight = holdsNight;

            return true;
        }

        /// <summary>
        /// 보스 체력 배율 — 웨이브와 같은 곡선(Day 성장 × 지역 배율)을 쓰되 <b>마지막 밤 배율은 뺀다</b>.
        /// 보스 자체가 그 밤의 사건이라 마지막 밤 가중을 다시 곱하면 이중 계상이 된다.
        /// </summary>
        private float ServerComputeHealthMultiplier(int dayNumber, IRegionService region)
        {
            RegionDifficulty difficulty = region.GetDifficultyForDay(dayNumber);
            if (_waveSettings == null)
            {
                return difficulty.MonsterHealthMultiplier;
            }

            return WaveMath.Plan(
                dayNumber, _waveSettings.ToCurve(),
                difficulty.WaveCountMultiplier, difficulty.MonsterHealthMultiplier,
                false, false).HealthMultiplier;
        }

        private void Update()
        {
            if (!IsSpawned || !IsServer || _boss == null)
            {
                return;
            }

            if (_boss.IsSpawned)
            {
                return;
            }

            // 처치·회수로 사라졌다 — 보류가 풀리고 소속 개체도 함께 정리된다.
            string label = _bossDefinition == null ? "보스" : _bossDefinition.DisplayName;
            bool wasHolding = _holdsNight;

            _boss = null;
            _bossDefinition = null;
            _holdsNight = false;

            if (ServiceLocator.TryGet(out IBossMinionSink sink))
            {
                sink.ServerRetreatMinions();
            }

            if (wasHolding)
            {
                Debug.Log($"[BossSpawner] {label} 처치 — 새벽 보류 해제, 시간이 다시 흐른다.");
            }
        }

        private void ServerRetreatBoss(string reason)
        {
            if (_boss == null)
            {
                return;
            }

            if (_boss.IsSpawned)
            {
                // 사망이 아니므로 보상·이벤트 없이 풀로 회수한다 (새벽 도주와 같은 규약).
                _boss.NetworkObject.Despawn(true);
                Debug.Log($"[BossSpawner] 보스 회수: {reason}");
            }

            _boss = null;
            _bossDefinition = null;
            _holdsNight = false;

            if (ServiceLocator.TryGet(out IBossMinionSink sink))
            {
                sink.ServerRetreatMinions();
            }
        }
    }
}
