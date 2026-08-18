using Game.Core.Events;
using Game.Core.Pooling;
using Game.Core.Services;
using Game.Gameplay.Combat;
using UnityEngine;

namespace Game.Gameplay.Train
{
    /// <summary>
    /// 그리드 위 건축물 실물 하나를 그리드 항목(<see cref="StructureEntry"/>)에 바인딩한다
    /// (건축 개편 1차 — 씬 정적 배치 폐기, 계획서 §2.6). 프리팹 루트에 BoxCollider와 함께 붙는다 —
    /// NetworkObject 없음: 상태는 TrainState의 NetworkList가 가지므로 각 피어가
    /// <see cref="StructureViewSpawner"/>를 통해 PoolManager로 로컬 스폰한다.
    /// 몬스터의 공격 표적(<see cref="IDamageable"/>)이자, 칸 이탈·소실 시 표현을 끄는 뷰다.
    /// 칸 오브젝트(Car_N)의 자식으로 스폰돼 칸의 이탈 이동을 그대로 따라간다.
    /// </summary>
    public sealed class StructureView : MonoBehaviour, IDamageable, IPoolable
    {
        [Tooltip("이탈 칸이 뒤로 이만큼(m) 멀어지면 표현을 끈다 — CarView의 소실 표현과 같은 거리로 맞춘다.")]
        [SerializeField, Min(5f)] private float _ejectHideMeters = 50f;

        private Renderer[] _renderers;
        private Collider[] _colliders;

        private StructureEntry _entry;
        private CarState _car;
        private bool _bound;
        private bool _carEjecting;
        private bool _registeredAsTarget;
        private bool _visible = true;

        /// <summary>바인딩된 그리드 항목의 서버 발급 Id — 수리 망치의 부위 식별·피해 RPC 지목에 쓴다.</summary>
        public int StructureId => _entry.Id;

        // ── IDamageable — 몬스터·화기가 공격하는 표적면 (데미지 확정은 호스트) ──────────

        /// <summary>건축물이 살아 있고 얹힌 칸이 편성에 살아 붙어 있을 때만 공격 대상이 된다.</summary>
        public bool IsAlive => _bound
            && StructureGridLogic.IsAlive(_entry)
            && TrainStateLogic.IsCarPresent(_car);

        public void ApplyDamage(float amount, ulong instigatorClientId)
        {
            if (_bound && ServiceLocator.TryGet(out ITrainDamageSink sink))
            {
                sink.ApplyStructureDamage(_entry.Id, amount);
            }
        }

        private void Awake()
        {
            _renderers = GetComponentsInChildren<Renderer>(includeInactive: true);
            _colliders = GetComponentsInChildren<Collider>(includeInactive: true);
        }

        /// <summary>스폰 직후 스포너가 호출한다 — 항목을 물고 표현·표적 등록을 현재 상태로 맞춘다.</summary>
        public void Bind(StructureEntry entry)
        {
            _entry = entry;
            _bound = true;
            SyncFromState();
        }

        private void OnEnable()
        {
            EventBus<StructureEntryChangedEvent>.Subscribe(OnEntryChanged);
            EventBus<CarStateChangedEvent>.Subscribe(OnCarStateChanged);
        }

        private void OnDisable()
        {
            EventBus<StructureEntryChangedEvent>.Unsubscribe(OnEntryChanged);
            EventBus<CarStateChangedEvent>.Unsubscribe(OnCarStateChanged);
        }

        private void Update()
        {
            if (!_carEjecting)
            {
                return;
            }

            // 이탈 칸 위 건축물 — 위치는 부모(칸)가 옮겨 주므로 소실 표현만 칸과 같은 거리 기준으로 맞춘다.
            float offset = ServiceLocator.TryGet(out ITrainState train) ? train.GetEjectOffset(_entry.CarIndex) : 0f;
            SetPresentation(offset < _ejectHideMeters);
        }

        public void OnSpawned()
        {
            // 풀 재사용 — 이전 개체의 숨김 상태가 새지 않게 보이는 상태로 되돌린다. 항목은 Bind가 새로 문다.
            _visible = false;
            SetPresentation(true);
        }

        public void OnDespawned()
        {
            if (_registeredAsTarget && ServiceLocator.TryGet(out ITrainTargetRegistry registry))
            {
                registry.Unregister(transform);
            }

            _registeredAsTarget = false;
            _bound = false;
            _carEjecting = false;
        }

        private void OnEntryChanged(StructureEntryChangedEvent evt)
        {
            // 값 갱신만 여기서 반영한다 — 추가·제거·재설정은 스포너가 스폰/디스폰으로 처리한다.
            if (_bound && evt.Change == StructureListChange.Updated && evt.Entry.Id == _entry.Id)
            {
                _entry = evt.Entry;
                SyncFromState();
            }
        }

        private void OnCarStateChanged(CarStateChangedEvent evt)
        {
            if (_bound && evt.Index == _entry.CarIndex)
            {
                SyncFromState();
            }
        }

        private void SyncFromState()
        {
            if (!ServiceLocator.TryGet(out ITrainState train) || !train.TryGetCar(_entry.CarIndex, out _car))
            {
                _car = default;
            }

            UpdateTargetRegistration();

            bool carEjectingAlive = !_car.Attached && _car.Health > 0f;
            _carEjecting = carEjectingAlive;

            bool visible;
            if (TrainStateLogic.IsCarPresent(_car))
            {
                visible = true;
            }
            else if (carEjectingAlive)
            {
                float offset = ServiceLocator.TryGet(out ITrainState state) ? state.GetEjectOffset(_entry.CarIndex) : 0f;
                visible = offset < _ejectHideMeters;
            }
            else
            {
                // 칸 파괴 — 항목 제거(디스폰)가 같은 프레임에 따라오지만, 그 사이에도 잔상을 남기지 않는다.
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
            if (_visible == visible)
            {
                return;
            }

            _visible = visible;
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
