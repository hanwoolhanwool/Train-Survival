using Game.Core.Events;
using Game.Core.Services;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Cycle
{
    /// <summary>
    /// 낮/밤 타임라인의 호스트 권위 소유자 (개발 가이드 M2 — "Day/지역 진행 = 호스트 단일 타임라인 동기화").
    /// 호스트가 누적 시간 NetworkVariable을 갱신하고, 각 피어는 <see cref="DayTimelineMath"/>로
    /// 같은 상태를 유도한다. 국면 전환 시 <see cref="DayPhaseChangedEvent"/>를 발행한다. Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class DayCycleController : NetworkBehaviour, IDayCycleService
    {
        [SerializeField] private DayTimelineSettings _settings;

        private readonly NetworkVariable<float> _totalSeconds = new NetworkVariable<float>();

        private DayTimelineState _state;
        private bool _hasEvaluated;

        public int DayNumber => _state.DayNumber;

        public DayPhase Phase => _state.Phase;

        public float PhaseRemaining => _state.PhaseRemaining;

        public float PhaseDuration => _state.PhaseDuration;

        public override void OnNetworkSpawn()
        {
            _hasEvaluated = false;
            EvaluateAndPublish();

            if (!ServiceLocator.IsRegistered<IDayCycleService>())
            {
                ServiceLocator.Register<IDayCycleService>(this);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (ServiceLocator.TryGet(out IDayCycleService service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<IDayCycleService>();
            }
        }

        private void Update()
        {
            if (!IsSpawned || _settings == null)
            {
                return;
            }

            if (IsServer)
            {
                _totalSeconds.Value += Time.deltaTime;
            }

            EvaluateAndPublish();
        }

        private void EvaluateAndPublish()
        {
            if (_settings == null)
            {
                return;
            }

            DayTimelineState next = DayTimelineMath.Evaluate(
                _totalSeconds.Value, _settings.DayDurationSeconds, _settings.NightDurationSeconds);

            bool changed = !_hasEvaluated ||
                next.DayNumber != _state.DayNumber || next.Phase != _state.Phase;

            _state = next;
            _hasEvaluated = true;

            if (changed)
            {
                EventBus<DayPhaseChangedEvent>.Publish(new DayPhaseChangedEvent(next.DayNumber, next.Phase));
            }
        }
    }
}
