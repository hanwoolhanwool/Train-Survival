using Game.Core.Events;
using Game.Core.Services;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 칸 위 붙박이 건축물(온실 돔 등) 하나를 건축물 상태 모델에 바인딩한다 (기획서 §9 — 건축물 개별 파괴).
    /// 몬스터의 공격 표적(<see cref="IDamageable"/>)이자, 파괴·칸 소실 시 표현을 끄는 뷰다.
    /// 칸 오브젝트(Car_N)의 자식으로 배치해 칸의 이탈 이동을 그대로 따라간다 — <see cref="CarView"/>는
    /// 이 서브트리의 렌더러·콜라이더를 자기 표현에서 제외하므로 표시 여부는 여기서만 결정된다.
    /// <see cref="_carIndex"/>는 부모 칸과 같은 값으로 지정한다.
    /// </summary>
    public sealed class StructureView : MonoBehaviour, IDamageable
    {
        [Tooltip("이 건축물이 얹힌 칸의 편성 인덱스 — 부모 CarView의 값과 일치시킨다.")]
        [SerializeField, Min(0)] private int _carIndex;

        [Tooltip("이탈 칸이 뒤로 이만큼(m) 멀어지면 표현을 끈다 — CarView의 소실 표현과 같은 거리로 맞춘다.")]
        [SerializeField, Min(5f)] private float _ejectHideMeters = 50f;

        private Renderer[] _renderers;
        private Collider[] _colliders;

        private StructureState _lastStructure;
        private CarState _lastCar;
        private bool _carEjecting;
        private bool _registeredAsTarget;

        public int CarIndex => _carIndex;

        // ── IDamageable — 몬스터·화기가 공격하는 표적면 (데미지 확정은 호스트) ──────────

        /// <summary>건축물이 살아 있고 얹힌 칸이 편성에 살아 붙어 있을 때만 공격 대상이 된다.</summary>
        public bool IsAlive => TrainStateLogic.IsStructureAlive(_lastStructure)
            && TrainStateLogic.IsCarPresent(_lastCar);

        public void ApplyDamage(float amount, ulong instigatorClientId)
        {
            if (ServiceLocator.TryGet(out ITrainDamageSink sink))
            {
                sink.ApplyStructureDamage(_carIndex, amount);
            }
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        private void Start()
        {
            UpdateTargetRegistration();
        }

        private void OnEnable()
        {
            EventBus<TrainInitializedEvent>.Subscribe(OnTrainInitialized);
            EventBus<StructureStateChangedEvent>.Subscribe(OnStructureStateChanged);
            EventBus<CarStateChangedEvent>.Subscribe(OnCarStateChanged);
            SyncFromState();
        }

        private void OnDisable()
        {
            EventBus<TrainInitializedEvent>.Unsubscribe(OnTrainInitialized);
            EventBus<StructureStateChangedEvent>.Unsubscribe(OnStructureStateChanged);
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

        private void Update()
        {
            if (!_carEjecting)
            {
                return;
            }

            // 이탈 칸 위 건축물 — 위치는 부모(칸)가 옮겨 주므로 소실 표현만 칸과 같은 거리 기준으로 맞춘다.
            float offset = ServiceLocator.TryGet(out ITrainState train) ? train.GetEjectOffset(_carIndex) : 0f;
            SetPresentation(offset < _ejectHideMeters);
        }

        private void OnTrainInitialized(TrainInitializedEvent _)
        {
            SyncFromState();
        }

        private void OnStructureStateChanged(StructureStateChangedEvent evt)
        {
            if (evt.Index == _carIndex)
            {
                SyncFromState();
            }
        }

        private void OnCarStateChanged(CarStateChangedEvent evt)
        {
            if (evt.Index == _carIndex)
            {
                SyncFromState();
            }
        }

        private void SyncFromState()
        {
            if (!ServiceLocator.TryGet(out ITrainState train))
            {
                return;
            }

            // 증설 전 예비 슬롯(편성 밖 인덱스)은 상태가 없다 — 건축물도 함께 숨긴다.
            bool hasCar = train.TryGetCar(_carIndex, out _lastCar);
            bool hasStructure = train.TryGetStructure(_carIndex, out _lastStructure);
            if (!hasCar || !hasStructure)
            {
                _lastCar = default;
                _lastStructure = default;
            }

            Apply(train);
        }

        /// <summary>상태를 표현에 반영한다: 건축물 생존 + 칸 표시 조건(정상/이탈-소실 전)일 때만 보인다.</summary>
        private void Apply(ITrainState train)
        {
            UpdateTargetRegistration();

            bool structureAlive = TrainStateLogic.IsStructureAlive(_lastStructure);
            bool carEjectingAlive = !_lastCar.Attached && _lastCar.Health > 0f;
            _carEjecting = structureAlive && carEjectingAlive;

            bool visible;
            if (!structureAlive)
            {
                visible = false;
            }
            else if (TrainStateLogic.IsCarPresent(_lastCar))
            {
                visible = true;
            }
            else if (carEjectingAlive)
            {
                visible = train.GetEjectOffset(_carIndex) < _ejectHideMeters;
            }
            else
            {
                // 칸 파괴 → 건축물도 함께 소멸 표현.
                visible = false;
            }

            SetPresentation(visible);
        }

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

        private void SetPresentation(bool visible)
        {
            for (int i = 0; i < _renderers.Length; i++)
            {
                _renderers[i].enabled = visible;
            }

            for (int i = 0; i < _colliders.Length; i++)
            {
                _colliders[i].enabled = visible;
            }
        }
    }
}
