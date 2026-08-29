using Game.Core.Events;
using Game.Core.Logging;
using Game.Core.Services;
using Game.Systems.Networking;
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
        [Tooltip("켜면 숫자패드 1 = 아침(낮 시작), 2 = 저녁(밤 시작), 3 = 다음 Day 아침으로 즉시 전환, " +
                 "F3 = 시간 배속 순환(×1 → ×4 → ×16), F5 = 다음 지역 첫날 아침. 릴리스에서는 끈다.")]
        [SerializeField] private bool _enableDebugPhaseKeys = true;

        /// <summary>F3이 순환하는 시간 배속 단계 (M8 2차 — 연출이 국면 <b>내내</b> 흐르는지 보려면 기다림이 필요했다).</summary>
        private static readonly float[] DebugTimeScales = { 1f, 4f, 16f };

        private readonly NetworkVariable<float> _totalSeconds = new NetworkVariable<float>();

        private DayTimelineState _state;
        private bool _hasEvaluated;

        /// <summary>호스트 전용 — 누적 속도 배율의 현재 단계. 복제하지 않는다(누적 시간 자체가 이미 복제된다).</summary>
        private int _debugTimeScaleIndex;

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
                ServerAccumulate();
            }

            HandleDebugPhaseInput();

            EvaluateAndPublish();
        }

        /// <summary>
        /// 호스트 누적 시간 가산 — 새벽 보류(M7 2차 결정 ④)가 걸려 있으면 밤 끝 경계 직전에서 멈춘다.
        /// 보류는 <b>새 복제 상태를 만들지 않는다</b>: 각 피어는 평소처럼 복제된 누적 시간에서
        /// 같은 밤을 유도하므로 후발 접속도 자동으로 같은 상태가 된다.
        /// </summary>
        private void ServerAccumulate()
        {
            bool holding = ServiceLocator.TryGet(out INightHoldGate gate) && gate.IsHoldingNight;

            // 배속은 가산량에만 곱한다 — 보류 클램프·국면 파생은 배속을 모르므로 규칙이 갈라지지 않는다.
            float delta = Time.deltaTime * DebugTimeScales[_debugTimeScaleIndex];

            float previous = _totalSeconds.Value;
            float next = NightHoldMath.ClampAccumulation(
                previous, previous + delta,
                _settings.DayDurationSeconds, _settings.NightDurationSeconds, holding);

            if (!Mathf.Approximately(next, previous))
            {
                _totalSeconds.Value = next;
            }
        }

        // ── 디버그: 숫자패드로 국면 즉시 전환 (테스트용) ──────────────────────

        private void HandleDebugPhaseInput()
        {
            // 국면 점프·시간 배속도 QA 키다 — 인게임 씬 밖에서는 받지 않는다
            // (QaDebugHotkeys와 같은 규약).
            if (!_enableDebugPhaseKeys || !GameplaySceneRoute.IsActiveSceneGameplay())
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
            // 배속은 국면 점프(1·2·3)와 같은 사이클 그룹이라 F 계열에서도 앞쪽 F3에 붙여 둔다 —
            // F8·F9·F10은 연출·플레이어 그룹이 연속으로 쓴다.
            else if (keyboard.f3Key.wasPressedThisFrame)
            {
                RequestCycleTimeScaleServerRpc();
            }
            // F5 = 지역 점프. 사이클 그룹이므로 F3 옆에 둔다 (북극 계획 결정 ⑨).
            else if (keyboard.f5Key.wasPressedThisFrame)
            {
                RequestAdvanceRegionServerRpc();
            }
        }

        /// <summary>
        /// <b>다음 지역 첫날 아침</b>으로 누적 시간을 점프시킨다 (호스트 권위, 디버그 전용 —
        /// 북극 계획 결정 ⑨).
        ///
        /// <para><b>왜 필요한가.</b> 지역을 넘기는 수단이 숫자패드 3(다음 Day) 하나뿐이라
        /// 북극(Day 17)에 닿으려면 <b>16번</b>을 눌러야 한다 — 지역이 하나 늘 때마다 검증 문서의
        /// 재현 수단이 낡는다(북극 계획 §3.4). 지역 경계는 Day 번호의 순수 함수이므로
        /// <see cref="Region.IRegionService.NextRegionFirstDay"/> 조회 하나면 목적지가 나온다.</para>
        ///
        /// <para>점프 대상은 <b>Day 3의 반복</b>과 정확히 같은 값이다 — 새 경로가 아니라 같은
        /// 누적 시간 축 위의 다른 지점이라, 지형 경계·웨이브·날씨가 종전과 같은 규약으로 따라온다.</para>
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestAdvanceRegionServerRpc()
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

            int currentDay = DayTimelineMath
                .Evaluate(_totalSeconds.Value, _settings.DayDurationSeconds, _settings.NightDurationSeconds)
                .DayNumber;

            int targetDay = ServiceLocator.TryGet(out Region.IRegionService region)
                ? region.NextRegionFirstDay(currentDay)
                : currentDay + 1;

            _totalSeconds.Value = (Mathf.Max(1, targetDay) - 1) * cycleDuration;

            GameLog.Info(LogCategory.Cycle, $"지역 점프 → Day {targetDay} 아침");
        }

        /// <summary>
        /// 시간 배속을 다음 단계로 돌린다 (호스트 권위, 디버그 전용). 국면 <b>점프</b>가 아니라
        /// <b>가속</b>이므로 낮/밤이 흐르는 과정을 그대로 볼 수 있다 — 연출(M8 2차)이 국면 내내
        /// 변하는지는 점프로는 확인할 수 없다.
        /// <para>
        /// 빨라지는 것은 <b>시간축뿐이다</b>. 이동·물리·전투 속도는 그대로이므로 <c>Time.timeScale</c>과
        /// 달리 판정이 왜곡되지 않는다. 대신 밤이 자주 오므로 웨이브가 잦아진다 —
        /// 격리 관찰이 필요하면 숫자패드 −(웨이브 토글)와 함께 쓴다.
        /// </para>
        /// </summary>
        [Rpc(SendTo.Server, RequireOwnership = false)]
        private void RequestCycleTimeScaleServerRpc()
        {
            _debugTimeScaleIndex = (_debugTimeScaleIndex + 1) % DebugTimeScales.Length;

            GameLog.Info(LogCategory.Cycle, $"시간 배속 → ×{DebugTimeScales[_debugTimeScaleIndex]:0}");
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
