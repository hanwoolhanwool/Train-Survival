using System;
using Game.Core.Diagnostics;
using Game.Core.Events;
using Game.Core.Logging;
using Game.Core.Services;
using Game.Gameplay.Cycle;
using Game.Gameplay.Monsters;
using Game.Utilities.Performance;
using UnityEngine;

namespace Game.Gameplay.Diagnostics
{
    /// <summary>
    /// 벤치 시나리오가 원하는 게임 상태를 실제로 만든다 — <see cref="PerfRunStartedEvent"/>의 수신측.
    ///
    /// <para><b>왜 주행기가 직접 하지 않는가.</b> 주행기는 <c>Game.Systems</c>에 있고 낮/밤·웨이브는
    /// 여기 <c>Game.Gameplay</c>에 있다. 의존이 단방향이라 주행기는 이쪽을 부를 수 없다.
    /// 주행기는 원하는 상태를 알리기만 하고, 그것을 아는 이 클래스가 적용한다
    /// (성능 프로파일링 자동화 계획 §4.7).</para>
    ///
    /// <para><b>게임 로직을 새로 만들지 않는다.</b> QA 핫키가 이미 쓰는 것과 같은 경로
    /// (<see cref="DayCycleController.ServerJumpTo"/> · <see cref="IWaveSpawnToggle"/>)로만
    /// 상태를 옮긴다 — 벤치 전용 경로를 만들면 실제 플레이와 다른 코드를 재게 된다(§7).</para>
    /// </summary>
    public sealed class PerfScenarioApplier : MonoBehaviour, IPerfSceneStats
    {
        /// <summary>사이클 서비스가 등록될 때까지 기다리는 한도(초).</summary>
        private const float ServiceWaitSeconds = 30f;

        private PerfRunStartedEvent _pending;
        private bool _hasPending;
        private float _waited;

        /// <summary>
        /// 벤치 인자가 있을 때만 스스로 생긴다 — 평범한 플레이에서는 존재하지 않는다.
        /// 씬에 배치하지 않는 이유는 주행기와 같다(씬 diff 를 만들지 않는다).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateIfRequested()
        {
            PerfRunArgs args = PerfRunArgsResolver.Resolve(Environment.GetCommandLineArgs());
            if (args.Mode != PerfRunMode.Benchmark)
            {
                return;
            }

            var host = new GameObject(nameof(PerfScenarioApplier));
            DontDestroyOnLoad(host);
            host.AddComponent<PerfScenarioApplier>();
        }

        /// <summary>측정 중 장면의 무게 — 결과 JSON 이 이 값을 싣는다.</summary>
        public int MonsterCount =>
            ServiceLocator.TryGet(out IMonsterPopulation population) ? population.ActiveMonsterCount : -1;

        private void OnEnable()
        {
            EventBus<PerfRunStartedEvent>.Subscribe(OnPerfRunStarted);

            // 주행기(Game.Systems)는 IMonsterPopulation 을 못 본다 — Core 계약으로 다리를 놓는다.
            if (!ServiceLocator.IsRegistered<IPerfSceneStats>())
            {
                ServiceLocator.Register<IPerfSceneStats>(this);
            }
        }

        private void OnDisable()
        {
            EventBus<PerfRunStartedEvent>.Unsubscribe(OnPerfRunStarted);
        }

        private void OnPerfRunStarted(PerfRunStartedEvent evt)
        {
            _pending = evt;
            _hasPending = true;
            _waited = 0f;
        }

        // 사이클 컨트롤러는 인게임 씬에서 네트워크 스폰될 때 등록된다 — 통지 시점에 아직 없을 수 있다.
        private void Update()
        {
            if (!_hasPending)
            {
                return;
            }

            if (TryApply())
            {
                _hasPending = false;
                return;
            }

            _waited += Time.unscaledDeltaTime;
            if (_waited > ServiceWaitSeconds)
            {
                _hasPending = false;
                GameLog.Error(LogCategory.Performance,
                    $"{ServiceWaitSeconds}초 안에 사이클 서비스가 등록되지 않아 상태를 강제하지 못했다 — " +
                    "이 실행의 수치는 시나리오가 의도한 상태의 것이 아니다.");
            }
        }

        private bool TryApply()
        {
            if (!ServiceLocator.TryGet(out IDayCycleService cycle) || !(cycle is DayCycleController controller))
            {
                return false;
            }

            // 웨이브를 먼저 켠다 — 밤 통지가 스포너에 닿을 때 스폰이 꺼져 있으면 그 밤은 비어 버린다.
            if (_pending.ForceWaveSpawn && ServiceLocator.TryGet(out IWaveSpawnToggle waves)
                && !waves.SpawnEnabled)
            {
                waves.ServerSetSpawnEnabled(true);
            }

            if (_pending.TimeOfDay != PerfTimeOfDay.Unchanged || _pending.DayNumber > 0)
            {
                DayPhase phase = _pending.TimeOfDay == PerfTimeOfDay.Night ? DayPhase.Night : DayPhase.Day;
                controller.ServerJumpTo(_pending.DayNumber, phase);

                GameLog.Info(LogCategory.Performance,
                    $"상태 강제 적용 — Day {(_pending.DayNumber > 0 ? _pending.DayNumber : cycle.DayNumber)} " +
                    $"{phase} · 웨이브 {(_pending.ForceWaveSpawn ? "on" : "그대로")}");
            }

            return true;
        }
    }
}
