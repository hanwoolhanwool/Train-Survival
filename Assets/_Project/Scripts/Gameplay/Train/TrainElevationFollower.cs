using Game.Core.Events;
using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 열차 높이 단계를 따라 자기 y를 옮기는 표식 (열차 높이 스펙 — <c>docs/specs/world/train-elevation.md</c>).
    /// <para>
    /// 씬에서 열차와 같은 평면을 쓰는 루트(편성·손잡이·엔진 포트·제작대)와 궤도 타일 프리팹의
    /// 궤도 자식에 붙인다. 참조 배선 없이 <b>컴포넌트를 붙이는 것만으로</b> 대상이 되므로,
    /// 대상이 늘어도 컨트롤러를 고칠 일이 없다.
    /// </para>
    /// <para>
    /// 기준 위치는 <see cref="Awake"/>에서 한 번만 잡고 그 뒤로는 <b>항상 기준 + 오프셋</b>으로 쓴다 —
    /// 현재 위치에 더하는 방식이면 단계를 왕복할 때마다 값이 흘러 어긋난다.
    /// 지형 타일은 풀에서 꺼내질 때 <see cref="OnEnable"/>이 다시 불리므로, 그 시점의 현재 오프셋을
    /// 직접 물어 맞춘다 — 스트리밍으로 뒤늦게 깔리는 궤도도 같은 높이로 나온다.
    /// </para>
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrainElevationFollower : MonoBehaviour
    {
        private Vector3 _baseLocalPosition;
        private bool _captured;

        private void Awake()
        {
            CaptureBase();
        }

        private void OnEnable()
        {
            // 풀 재활성 등으로 Awake보다 늦게 켜지는 경로에서도 기준이 잡혀 있게 한다.
            CaptureBase();

            EventBus<TrainElevationChangedEvent>.Subscribe(OnElevationChanged);
            Apply(ServiceLocator.TryGet(out ITrainElevation elevation) ? elevation.Offset : 0f);
        }

        private void OnDisable()
        {
            EventBus<TrainElevationChangedEvent>.Unsubscribe(OnElevationChanged);
        }

        private void CaptureBase()
        {
            if (_captured)
            {
                return;
            }

            _baseLocalPosition = transform.localPosition;
            _captured = true;
        }

        private void OnElevationChanged(TrainElevationChangedEvent evt)
        {
            Apply(evt.Offset);
        }

        private void Apply(float offset)
        {
            // y만 갈아 끼운다 — 다른 축은 각자 주인(스트리밍·배치 로직)이 있으므로 건드리지 않는다.
            Vector3 position = transform.localPosition;
            position.y = TrainElevationLogic.ResolveElevatedY(_baseLocalPosition.y, offset);
            transform.localPosition = position;
        }
    }
}
