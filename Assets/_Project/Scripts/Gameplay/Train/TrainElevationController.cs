using Game.Core.Events;
using Game.Core.Logging;
using Game.Core.Services;
using Unity.Netcode;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차·궤도 높이를 단계로 갈아 끼우는 QA 토글의 권위 (열차 높이 스펙 — <c>docs/specs/world/train-elevation.md</c>).
    /// <para>
    /// 높이를 바꾸는 방법은 <b>오프셋 하나를 두 곳에 흘리는 것</b>뿐이다:
    /// ① 씬·프리팹 표현은 <see cref="TrainElevationFollower"/>가 자기 기준 위치에 오프셋을 더해 따라오고,
    /// ② 규칙(건설 배치·조준 평면·몬스터 착지·플레이어 스폰·체온·즉사 존)은 전부 한 값
    /// <see cref="TrainLayoutSettings.DeckHeight"/>를 읽으므로 여기에 오프셋을 얹으면 함께 따라온다.
    /// 표현과 규칙이 같은 오프셋을 쓰므로 어느 단계에서도 어긋나지 않는다.
    /// </para>
    /// <para>
    /// 단계는 호스트가 확정하고 <see cref="NetworkVariable{T}"/>로 복제되므로 후발 접속 피어도
    /// 스폰 시점의 높이를 그대로 받는다. 편성 루트(Train — 씬 NetworkObject)에 배치한다.
    /// </para>
    /// </summary>
    // Follower가 자기 기준 위치를 잡고 현재 오프셋을 물어보기 전에 서비스 등록·초기화를 끝내 둔다.
    [DefaultExecutionOrder(-200)]
    [DisallowMultipleComponent]
    public sealed class TrainElevationController : NetworkBehaviour, ITrainElevation
    {
        [Tooltip("갑판 기준선을 쥔 레이아웃 에셋 — 단계 오프셋이 여기에 얹혀 건설·조준·착지 판정에 전파된다.")]
        [SerializeField] private TrainLayoutSettings _layoutSettings;

        [Tooltip("단계별 높이 오프셋(m). 0번이 씬에 굳어 있는 기준 높이이므로 반드시 0으로 둔다. "
            + "음수가 내려간 상태다 — 기본 0 / −0.3 / −0.6.")]
        [SerializeField] private float[] _stepOffsets = { 0f, -0.3f, -0.6f };

        [Tooltip("단계가 바뀔 때 호스트·클라 콘솔에 현재 높이를 찍는다(QA 확인용).")]
        [SerializeField] private bool _logStepChange = true;

        // 호스트만 쓰고 전 피어가 읽는다 — 후발 접속도 OnNetworkSpawn에서 현재 단계를 그대로 받는다.
        private readonly NetworkVariable<int> _step = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        public int StepCount => _stepOffsets != null ? _stepOffsets.Length : 0;

        public int StepIndex => TrainElevationLogic.NormalizeStep(_step.Value, StepCount);

        public float Offset => TrainElevationLogic.ResolveOffset(_stepOffsets, _step.Value);

        private void Awake()
        {
            if (!ServiceLocator.IsRegistered<ITrainElevation>())
            {
                ServiceLocator.Register<ITrainElevation>(this);
            }

            // 레이아웃 에셋은 플레이 세션 사이에도 살아남는다(도메인 리로드를 끈 설정에서 특히).
            // 이전 세션이 내려 둔 오프셋이 남으면 갑판만 낮은 채로 시작하므로 기준 높이로 되돌린다.
            ApplyLocal(0, announce: false);
        }

        public override void OnNetworkSpawn()
        {
            _step.OnValueChanged += OnStepChanged;

            // 후발 접속 — 스폰 시점에 이미 확정돼 있는 단계를 즉시 반영한다(값 변경 콜백은 오지 않는다).
            ApplyLocal(_step.Value, announce: _step.Value != 0);
        }

        public override void OnNetworkDespawn()
        {
            _step.OnValueChanged -= OnStepChanged;
        }

        // NetworkBehaviour.OnDestroy를 가리면(new) 기반 정리가 통째로 건너뛰어져 NetworkVariable·
        // NetworkList가 해제되지 않는다 — NGO가 override와 base 호출을 명시로 요구한다.
        public override void OnDestroy()
        {
            // 씬을 떠날 때 에셋을 기준 높이로 되돌린다 — 다음 씬·다음 플레이가 남은 오프셋을 물려받지 않게 한다.
            if (_layoutSettings != null)
            {
                _layoutSettings.SetElevationOffset(0f);
            }

            if (ServiceLocator.TryGet(out ITrainElevation service) && ReferenceEquals(service, this))
            {
                ServiceLocator.Unregister<ITrainElevation>();
            }

            base.OnDestroy();
        }

        /// <summary>다음 단계로 넘긴다 — 호스트가 확정하면 복제되어 전 피어가 같은 높이로 움직인다.</summary>
        public void ServerCycleStep()
        {
            if (!IsServer)
            {
                return;
            }

            _step.Value = TrainElevationLogic.NextStep(_step.Value, StepCount);
        }

        private void OnStepChanged(int previous, int current)
        {
            ApplyLocal(current, announce: true);
        }

        /// <summary>
        /// 이 피어의 높이를 단계에 맞춘다 — <b>규칙(갑판 기준선)을 먼저</b> 갱신하고 표현에 알린다.
        /// 순서가 뒤집히면 표현이 움직인 프레임에 건설·착지 판정이 옛 높이를 본다.
        /// </summary>
        private void ApplyLocal(int step, bool announce)
        {
            float offset = TrainElevationLogic.ResolveOffset(_stepOffsets, step);

            if (_layoutSettings != null)
            {
                _layoutSettings.SetElevationOffset(offset);
            }

            EventBus<TrainElevationChangedEvent>.Publish(
                new TrainElevationChangedEvent(TrainElevationLogic.NormalizeStep(step, StepCount), offset));

            if (announce && _logStepChange && _layoutSettings != null)
            {
                GameLog.Info(LogCategory.Train, $"높이 단계 {TrainElevationLogic.NormalizeStep(step, StepCount)}/" +
                                          $"{Mathf.Max(0, StepCount - 1)} — 오프셋 {offset:F2} m, 갑판 y={_layoutSettings.DeckHeight:F3}");
            }
        }
    }
}
