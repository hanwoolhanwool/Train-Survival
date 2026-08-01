using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Cycle;
using UnityEngine;

namespace Game.Gameplay.Region
{
    /// <summary>
    /// 지역 타임라인의 파생·발행 담당 (개발 가이드 M4 — "Day/지역 진행 = 호스트 단일 타임라인").
    /// 지역은 이미 호스트 권위로 복제된 Day 번호의 순수 함수이므로 자체 네트워크 상태를 갖지 않는다 —
    /// 각 피어가 <see cref="RegionTimelineMath"/>로 같은 지역을 독립 유도하고, 전환 시점에만
    /// <see cref="RegionChangedEvent"/>를 발행한다. Game 씬에 1개 배치한다.
    /// </summary>
    public sealed class RegionController : MonoBehaviour, IRegionService
    {
        [SerializeField] private RegionTimelineSettings _settings;

        private RegionTimelineState _state;
        private bool _hasEvaluated;

        public RegionDefinition CurrentRegion =>
            _settings == null ? null : _settings.GetRegion(_state.RegionIndex);

        public RegionDefinition NextRegion =>
            _settings == null ? null : _settings.GetRegion(_state.NextRegionIndex);

        public int RegionIndex => _state.RegionIndex;

        public int CycleNumber => _state.CycleNumber;

        public int DayInRegion => _state.DayInRegion;

        public int RegionDayCount => _state.RegionDayCount;

        public bool IsFinalDayOfRegion => _state.IsFinalDayOfRegion;

        public bool IsForecastWindow => _state.IsForecastWindow;

        public RegionDifficulty GetDifficultyForDay(int dayNumber)
        {
            if (_settings == null)
            {
                return RegionDifficulty.Neutral;
            }

            RegionTimelineState state = Evaluate(dayNumber);
            if (!state.IsValid)
            {
                return RegionDifficulty.Neutral;
            }

            RegionDefinition region = _settings.GetRegion(state.RegionIndex);
            if (region == null)
            {
                return RegionDifficulty.Neutral;
            }

            // 순환 보너스: 한 바퀴 돌 때마다 난이도 배율이 가산된다 (기획서 §4.5).
            float cycleScale = 1f + state.CycleNumber * _settings.CycleDifficultyBonus;

            return new RegionDifficulty(
                region.WaveCountMultiplier * cycleScale,
                region.MonsterHealthMultiplier * cycleScale,
                state.IsFinalDayOfRegion);
        }

        private void OnEnable()
        {
            EventBus<DayPhaseChangedEvent>.Subscribe(OnDayPhaseChanged);

            if (!ServiceLocator.IsRegistered<IRegionService>())
            {
                ServiceLocator.Register<IRegionService>(this);
            }
        }

        private void OnDisable()
        {
            EventBus<DayPhaseChangedEvent>.Unsubscribe(OnDayPhaseChanged);

            if (ServiceLocator.TryGet(out IRegionService service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<IRegionService>();
            }
        }

        private void OnDayPhaseChanged(DayPhaseChangedEvent evt)
        {
            EvaluateAndPublish(evt.DayNumber);
        }

        private void EvaluateAndPublish(int dayNumber)
        {
            if (_settings == null)
            {
                return;
            }

            RegionTimelineState next = Evaluate(dayNumber);
            if (!next.IsValid)
            {
                return;
            }

            bool changed = !_hasEvaluated ||
                next.RegionIndex != _state.RegionIndex || next.CycleNumber != _state.CycleNumber;

            _state = next;
            _hasEvaluated = true;

            if (changed)
            {
                EventBus<RegionChangedEvent>.Publish(new RegionChangedEvent(
                    next.RegionIndex, next.CycleNumber, next.DayInRegion, CurrentRegion));
            }
        }

        private RegionTimelineState Evaluate(int dayNumber)
        {
            return RegionTimelineMath.Evaluate(
                dayNumber, _settings.GetDayCounts(), _settings.ForecastLeadDays, _settings.LoopAfterLastRegion);
        }
    }
}
