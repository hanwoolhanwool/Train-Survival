using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using Game.Gameplay.World;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 씬에 정적으로 배치된 칸 하나를 편성 상태 모델(<see cref="ITrainState"/>)에 바인딩한다.
    /// 세 역할을 겸한다: ① 상태(연결·체력) 권위 이벤트를 구독해 표현만 갱신 ② 몬스터의 공격 표적(<see cref="IDamageable"/>)
    /// ③ 이탈 확정 시 후방으로 흘러가는 이탈 연출. 데미지 확정·연쇄 이탈은 호스트(<see cref="ITrainDamageSink"/>)가 담당한다.
    /// 각 칸(Car_Locomotive/Car_1/...) 오브젝트에 부착하고 <see cref="_carIndex"/>를 편성 순서(0 = 기관차)로 지정한다.
    /// </summary>
    public sealed class CarView : MonoBehaviour, IDamageable
    {
        [Tooltip("편성 순서 인덱스 — 0 = 기관차(선두), 값이 클수록 후방. TrainState의 칸 배열과 1:1 대응.")]
        [SerializeField, Min(0)] private int _carIndex;

        [Tooltip("이탈한 칸이 후방으로 흘러가다 이만큼(m) 멀어지면 표현을 끈다.")]
        [SerializeField, Min(5f)] private float _ejectDespawnMeters = 50f;

        [Tooltip("이탈 시 스크롤 속도에 더해 뒤로 밀려나는 추가 속도(m/s) — 편성에서 분리되는 느낌을 준다.")]
        [SerializeField, Min(0f)] private float _ejectExtraSpeed = 2f;

        private Renderer[] _renderers;
        private Collider[] _colliders;

        private CarState _lastState;
        private bool _ejecting;
        private float _ejectTravel;

        // ── IDamageable — 몬스터가 공격하는 표적면 (데미지 확정은 호스트) ──────────

        /// <summary>파괴 가능(기관차 아님)하고 편성에 살아 붙어 있을 때만 공격 대상이 된다.</summary>
        public bool IsAlive => TrainStateLogic.IsCarPresent(_lastState) && TrainStateLogic.IsDestructible(_lastState.Type);

        public void ApplyDamage(float amount, ulong instigatorClientId)
        {
            if (ServiceLocator.TryGet(out ITrainDamageSink sink))
            {
                sink.ApplyCarDamage(_carIndex, amount);
            }
        }

        private void Awake()
        {
            // 렌더러·콜라이더가 칸 오브젝트 자신 또는 자식 어디에 있든 덮도록 자기 포함으로 수집한다.
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        private void Start()
        {
            // 타겟 등록소는 Awake에서 서비스 등록되므로 Start 시점엔 준비돼 있다.
            if (ServiceLocator.TryGet(out ITrainTargetRegistry registry))
            {
                registry.Register(transform, this);
            }
        }

        private void OnEnable()
        {
            EventBus<TrainInitializedEvent>.Subscribe(OnTrainInitialized);
            EventBus<CarStateChangedEvent>.Subscribe(OnCarStateChanged);

            // 이미 편성이 준비된 뒤에 활성화됐을 수 있으므로 현재 상태를 즉시 반영한다.
            SyncFromState();
        }

        private void OnDisable()
        {
            EventBus<TrainInitializedEvent>.Unsubscribe(OnTrainInitialized);
            EventBus<CarStateChangedEvent>.Unsubscribe(OnCarStateChanged);
        }

        private void OnDestroy()
        {
            if (ServiceLocator.TryGet(out ITrainTargetRegistry registry))
            {
                registry.Unregister(transform);
            }
        }

        private void Update()
        {
            if (!_ejecting)
            {
                return;
            }

            float scrollSpeed = ServiceLocator.TryGet(out IWorldScrollService scroll) ? scroll.ScrollSpeed : 0f;
            float speed = scrollSpeed + _ejectExtraSpeed;
            float step = speed * Time.deltaTime;

            // 열차는 원점 고정, 월드는 -Z로 스크롤 → 이탈 칸은 편성에서 뒤(-Z)로 멀어진다.
            transform.position += Vector3.back * step;
            _ejectTravel += step;

            if (_ejectTravel >= _ejectDespawnMeters)
            {
                _ejecting = false;
                SetRenderers(false);
            }
        }

        private void OnTrainInitialized(TrainInitializedEvent _)
        {
            SyncFromState();
        }

        private void OnCarStateChanged(CarStateChangedEvent evt)
        {
            if (evt.Index == _carIndex)
            {
                ApplyState(evt.State);
            }
        }

        private void SyncFromState()
        {
            if (ServiceLocator.TryGet(out ITrainState train) && train.TryGetCar(_carIndex, out CarState car))
            {
                ApplyState(car);
            }
        }

        /// <summary>상태를 표현에 반영한다: 정상=표시, 파괴=즉시 소멸, 이탈=후방으로 흘려보내는 연출 시작.</summary>
        private void ApplyState(CarState car)
        {
            _lastState = car;

            if (TrainStateLogic.IsCarPresent(car))
            {
                _ejecting = false;
                _ejectTravel = 0f;
                SetRenderers(true);
                SetColliders(true);
                return;
            }

            // 편성에서 빠진 칸 — 밟거나 공격 대상이 되지 않도록 콜라이더는 끈다.
            SetColliders(false);

            if (car.Health <= 0f)
            {
                // 파괴됨: 즉시 소멸 (잔해 연출은 후속).
                _ejecting = false;
                SetRenderers(false);
            }
            else if (!_ejecting)
            {
                // 이탈(멀쩡): 후방으로 흘러가는 연출 시작.
                _ejecting = true;
                _ejectTravel = 0f;
                SetRenderers(true);
            }
        }

        private void SetRenderers(bool enabled)
        {
            if (_renderers == null)
            {
                return;
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = enabled;
            }
        }

        private void SetColliders(bool enabled)
        {
            if (_colliders == null)
            {
                return;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].enabled = enabled;
            }
        }
    }
}
