using Game.Core.Events;
using Game.Core.Services;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 승차 램프를 현재 후미 칸(살아 붙어 있는 마지막 칸) 뒤에 맞춰 배치한다 (§M3 — 증설·이탈로 후미가 움직인다).
    /// 편성 변화 이벤트로만 갱신하는 표현 전용 컴포넌트 — 위치 계산 소스는 복제 상태라 전 피어가 일치한다.
    /// BoardingRamp 오브젝트에 부착한다.
    /// </summary>
    public sealed class BoardingRampPositioner : MonoBehaviour
    {
        [SerializeField] private TrainLayoutSettings _layoutSettings;

        [Tooltip("현재 후미 칸 뒤끝 기준 램프 중심의 Z 오프셋(m) — 음수 = 열차 뒤쪽.")]
        [SerializeField] private float _zOffsetFromRearEdge = -3.7f;

        private void OnEnable()
        {
            EventBus<TrainInitializedEvent>.Subscribe(OnTrainInitialized);
            EventBus<CarStateChangedEvent>.Subscribe(OnCarStateChanged);
            Reposition();
        }

        private void OnDisable()
        {
            EventBus<TrainInitializedEvent>.Unsubscribe(OnTrainInitialized);
            EventBus<CarStateChangedEvent>.Unsubscribe(OnCarStateChanged);
        }

        private void OnTrainInitialized(TrainInitializedEvent _)
        {
            Reposition();
        }

        private void OnCarStateChanged(CarStateChangedEvent _)
        {
            // 증설(Add)·이탈·파괴 모두 칸 상태 변화로 흘러온다 — 후미가 바뀌었을 수 있으니 재배치한다.
            Reposition();
        }

        private void Reposition()
        {
            if (_layoutSettings == null || !ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            int rearIndex = -1;
            for (int i = 0; i < train.CarCount; i++)
            {
                if (train.TryGetCar(i, out CarState car) && TrainStateLogic.IsCarPresent(car))
                {
                    rearIndex = i;
                }
            }

            if (rearIndex < 0)
            {
                return;
            }

            float rearEdgeZ = _layoutSettings.CarCenterZ(rearIndex) - _layoutSettings.CarLength * 0.5f;
            Vector3 position = transform.position;
            transform.position = new Vector3(position.x, position.y, rearEdgeZ + _zOffsetFromRearEdge);
        }
    }
}
