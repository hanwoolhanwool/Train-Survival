using Game.Core.Events;
using Game.Core.Services;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 승하차 통로(사다리·램프)를 현재 후미 칸(살아 붙어 있는 마지막 칸)에 맞춰 배치한다
    /// (§M3 — 증설·이탈로 후미가 움직인다). 기준점은 통로가 놓이는 방향에 따라 고른다
    /// (<see cref="BoardingAccessAnchor"/>) — 뒤로 내려가면 갑판 뒤끝, 옆에 붙으면 칸 중심이다.
    /// X·Y는 씬 배치를 그대로 둔다.
    ///
    /// <para>편성 변화 이벤트로만 갱신하는 표현 전용 컴포넌트 — 위치 계산 소스는 복제 상태라
    /// 전 피어가 일치한다.</para>
    ///
    /// <para><b>이름이 Ramp 가 아닌 이유</b>: 승차 램프는 사다리로 교체됐고(사다리 계획 §3.9)
    /// 추적 로직만 그대로 남았다. 램프 이름을 남겨 두면 다음 사람이 없는 오브젝트를 찾는다.</para>
    /// </summary>
    public sealed class BoardingAccessPositioner : MonoBehaviour
    {
        [SerializeField] private TrainLayoutSettings _layoutSettings;

        [Tooltip("후미 칸의 어디에 맞출지 — 뒤끝(뒤로 내려가는 램프) / 중심(칸 옆에 붙는 사다리).")]
        [FormerlySerializedAs("_anchor")]
        [SerializeField] private BoardingAccessAnchor _anchor = BoardingAccessAnchor.RearEdge;

        [Tooltip("기준점에서 통로 중심까지의 Z 오프셋(m) — 음수 = 열차 뒤쪽.")]
        [FormerlySerializedAs("_zOffsetFromRearEdge")]
        [SerializeField] private float _zOffset = -3.7f;

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

            float anchorZ = _layoutSettings.CarCenterZ(rearIndex);
            if (_anchor == BoardingAccessAnchor.RearEdge)
            {
                anchorZ -= _layoutSettings.DeckLength * 0.5f;
            }

            Vector3 position = transform.position;
            transform.position = new Vector3(position.x, position.y, anchorZ + _zOffset);
        }
    }
}
