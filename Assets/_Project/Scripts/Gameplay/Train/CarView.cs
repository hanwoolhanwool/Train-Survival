using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 씬에 정적으로 배치된 칸 하나를 편성 상태 모델(<see cref="ITrainState"/>)에 바인딩한다.
    /// 세 역할을 겸한다: ① 상태(연결·체력) 권위 이벤트를 구독해 표현만 갱신 ② 몬스터의 공격 표적(<see cref="IDamageable"/>)
    /// ③ 이탈 칸을 호스트가 계산한 이탈 오프셋대로 배치(로컬 시뮬 아님 — 손잡이 저항 결과가 반영된다).
    /// 데미지 확정·연쇄 이탈·이탈 이동은 호스트가 담당하고 여기서는 복제된 상태를 표현만 한다.
    /// 각 칸(Car_Locomotive/Car_1/...) 오브젝트에 부착하고 <see cref="_carIndex"/>를 편성 순서(0 = 기관차)로 지정한다.
    /// </summary>
    public sealed class CarView : MonoBehaviour, IDamageable
    {
        [Tooltip("편성 순서 인덱스 — 0 = 기관차(선두), 값이 클수록 후방. TrainState의 칸 배열과 1:1 대응.")]
        [SerializeField, Min(0)] private int _carIndex;

        [Tooltip("이탈 칸이 뒤로 이만큼(m) 멀어지면 표현을 끈다(소실 표현).")]
        [SerializeField, Min(5f)] private float _ejectHideMeters = 50f;

        private Renderer[] _renderers;
        private Collider[] _colliders;
        private Vector3 _originalLocalPosition;

        private CarState _lastState;
        private bool _ejecting;
        private bool _registeredAsTarget;

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
            _originalLocalPosition = transform.localPosition;
        }

        private void Start()
        {
            // 타겟 등록소는 Awake에서 서비스 등록되므로 Start 시점엔 준비돼 있다. 현재 상태 기준으로 등록한다.
            UpdateTargetRegistration();
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
            if (_registeredAsTarget && ServiceLocator.TryGet(out ITrainTargetRegistry registry))
            {
                registry.Unregister(transform);
                _registeredAsTarget = false;
            }
        }

        /// <summary>
        /// 공격 대상 등록을 현재 상태에 맞춘다 — 이탈·파괴된 칸은 즉시 표적에서 빠진다(Feature 1:
        /// 연결 해제된 칸은 더 이상 몬스터 공격 대상이 아니다). 기관차는 파괴 불가라 애초에 표적이 아니다.
        /// </summary>
        private void UpdateTargetRegistration()
        {
            if (!ServiceLocator.TryGet(out ITrainTargetRegistry registry))
            {
                return;
            }

            bool eligible = IsAlive;
            if (eligible && !_registeredAsTarget)
            {
                registry.Register(transform, this);
                _registeredAsTarget = true;
            }
            else if (!eligible && _registeredAsTarget)
            {
                registry.Unregister(transform);
                _registeredAsTarget = false;
            }
        }

        private void Update()
        {
            if (!_ejecting)
            {
                return;
            }

            // 이탈 칸은 호스트가 계산한 오프셋대로 배치한다(손잡이 저항으로 감속/정지/전진 모두 여기에 반영된다).
            float offset = ServiceLocator.TryGet(out ITrainState train) ? train.GetEjectOffset(_carIndex) : 0f;

            // 열차는 원점 고정(미회전) → 로컬 -Z가 곧 후방. 원위치에서 오프셋만큼 뒤로 민다.
            transform.localPosition = _originalLocalPosition + Vector3.back * offset;

            SetRenderers(offset < _ejectHideMeters);
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

        /// <summary>상태를 표현에 반영한다: 정상=슬롯 표시, 파괴=즉시 소멸, 이탈=오프셋 추종(Update).</summary>
        private void ApplyState(CarState car)
        {
            _lastState = car;
            UpdateTargetRegistration();

            if (TrainStateLogic.IsCarPresent(car))
            {
                _ejecting = false;
                transform.localPosition = _originalLocalPosition;
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
            else
            {
                // 이탈(멀쩡): 호스트 오프셋을 추종해 뒤로 흘러간다(Update에서 배치).
                _ejecting = true;
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
