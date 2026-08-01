using Game.Core.Events;
using Game.Core.Services;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

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

        [Header("디버그 (테스트용)")]
        [Tooltip("켜면 숫자패드 1 = 아침(낮 시작), 2 = 저녁(밤 시작), 3 = 다음 Day 아침으로 즉시 전환. 릴리스에서는 끈다.")]
        [SerializeField] private bool _enableDebugPhaseKeys = true;

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

            HandleDebugPhaseInput();

            EvaluateAndPublish();
        }

        // ── 디버그: 숫자패드로 국면 즉시 전환 (테스트용) ──────────────────────

        private void HandleDebugPhaseInput()
        {
            if (!_enableDebugPhaseKeys)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null)
            {
                return;
            }

            // 클라이언트에서 눌러도 되도록 호스트 확정(ServerRpc) 경유 — 누적 시간이 곧 전 피어의 국면이다.
            if (keyboard.numpad1Key.wasPressedThisFrame)
            {
                RequestJumpToPhaseServerRpc(DayPhase.Day);
            }
            else if (keyboard.numpad2Key.wasPressedThisFrame)
            {
                RequestJumpToPhaseServerRpc(DayPhase.Night);
            }
            else if (keyboard.numpad3Key.wasPressedThisFrame)
            {
                // 지역 전환(M4)은 Day 단위로 일어나므로 Day를 건너뛸 수단이 필요하다.
                RequestAdvanceDayServerRpc();
            }
        }

        /// <summary>다음 Day의 아침으로 누적 시간을 점프시킨다(호스트 권위, 디버그 전용).</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestAdvanceDayServerRpc()
        {
            if (_settings == null)
            {
                return;
            }

            float cycleDuration = _settings.DayDurationSeconds + _settings.NightDurationSeconds;
            if (cycleDuration <= 0f)
            {
                return;
            }

            int cycleIndex = Mathf.FloorToInt(Mathf.Max(0f, _totalSeconds.Value) / cycleDuration);
            _totalSeconds.Value = (cycleIndex + 1) * cycleDuration;
        }

        /// <summary>현재 Day를 유지한 채 해당 국면의 시작으로 누적 시간을 점프시킨다(호스트 권위).</summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestJumpToPhaseServerRpc(DayPhase phase)
        {
            if (_settings == null)
            {
                return;
            }

            float cycleDuration = _settings.DayDurationSeconds + _settings.NightDurationSeconds;
            if (cycleDuration <= 0f)
            {
                return;
            }

            int cycleIndex = Mathf.FloorToInt(Mathf.Max(0f, _totalSeconds.Value) / cycleDuration);
            float cycleStart = cycleIndex * cycleDuration;

            // 낮(아침)은 사이클 시작, 밤(저녁)은 낮 길이만큼 지난 지점 = 각 국면의 시작 경계.
            _totalSeconds.Value = phase == DayPhase.Night ? cycleStart + _settings.DayDurationSeconds : cycleStart;
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
